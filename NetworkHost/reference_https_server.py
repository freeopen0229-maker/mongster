"""Local HTTPS reference host invoking the actual C# economy domain.
Uses SQLite transactions to persist the entire local world snapshot and receipts.
This is not a public production host or normalized production DB adapter.
"""
import argparse,hashlib,json,os,sqlite3,ssl,subprocess,threading,time
from http.server import BaseHTTPRequestHandler,ThreadingHTTPServer
from pathlib import Path
from urllib.parse import urlsplit,parse_qs

class DomainWorker:
    def __init__(self,command,env=None):
        self.process=subprocess.Popen(command,stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=subprocess.PIPE,text=True,env=env,bufsize=1)
        self.lock=threading.Lock()
    def invoke(self,payload):
        with self.lock:
            self.process.stdin.write(json.dumps(payload,separators=(',',':'))+'\n');self.process.stdin.flush()
            line=self.process.stdout.readline()
            if not line:raise RuntimeError('C# domain worker stopped')
            return json.loads(line)
    def close(self):
        if self.process.poll() is None:
            self.process.terminate()
            try:self.process.wait(timeout=5)
            except subprocess.TimeoutExpired:self.process.kill();self.process.wait()

def initialize_reference(db,worker,tokens):
    """Explicit test fixture setup; never called by an HTTP route."""
    c=sqlite3.connect(db)
    try:
        c.execute('PRAGMA journal_mode=WAL')
        c.execute('CREATE TABLE IF NOT EXISTS LocalWorld(singleton INTEGER PRIMARY KEY CHECK(singleton=1),world_json TEXT NOT NULL)')
        c.execute('CREATE TABLE IF NOT EXISTS AuthSession(token_sha256 TEXT PRIMARY KEY,account_id TEXT NOT NULL,expires_utc INTEGER NOT NULL)')
        if c.execute('SELECT 1 FROM LocalWorld').fetchone():raise ValueError('Reference DB already initialized; refusing reset')
        result=worker.invoke({'action':'init'})
        if result['status']!=200:raise ValueError('Fixture initialization failed')
        c.execute('INSERT INTO LocalWorld VALUES(1,?)',(json.dumps(result['world']),))
        for account,token in tokens.items():
            if account not in ('local-a','local-b') or len(token)<24:raise ValueError('Explicit local fixture accounts and long random tokens required')
            c.execute('INSERT INTO AuthSession VALUES(?,?,?)',(hashlib.sha256(token.encode()).hexdigest(),account,int(time.time())+3600))
        c.commit()
    finally:c.close()

class ReferenceServer(ThreadingHTTPServer):
    daemon_threads=True
    def __init__(self,address,db,worker,cert,key):
        super().__init__(address,Handler)
        self.db=str(db);self.worker=worker
        self.delay_response_once=0 # Integration-test hook. No request header/route enables it.
        self.delay_lock=threading.Lock()
        context=ssl.SSLContext(ssl.PROTOCOL_TLS_SERVER);context.minimum_version=ssl.TLSVersion.TLSv1_2
        context.load_cert_chain(cert,key);self.socket=context.wrap_socket(self.socket,server_side=True)

class Handler(BaseHTTPRequestHandler):
    protocol_version='HTTP/1.1'
    def setup(self):super().setup();self.connection.settimeout(10)
    def log_message(self,*args):pass
    def respond(self,status,body):
        data=json.dumps(body,separators=(',',':')).encode()
        try:
            self.send_response(status);self.send_header('Content-Type','application/json; charset=utf-8')
            self.send_header('Cache-Control','no-store');self.send_header('Content-Length',str(len(data)));self.end_headers();self.wfile.write(data)
        except (BrokenPipeError,ConnectionResetError,ssl.SSLError):pass
    def do_GET(self):self.route(False)
    def do_POST(self):self.route(True)
    def route(self,post):
        path=urlsplit(self.path)
        if (not post and path.path!='/v2/client/snapshot') or (post and path.path!='/v2/client/command'):
            self.respond(404,{'error':'route_not_found'});return
        auth=self.headers.get('Authorization','')
        if not auth.startswith('Bearer ') or len(auth)>2048:self.respond(401,{'error':'unauthorized'});return
        command=None
        if post:
            try:
                n=int(self.headers.get('Content-Length','0'))
                if n<1 or n>65536 or self.headers.get_content_type()!='application/json':raise ValueError()
                command=json.loads(self.rfile.read(n))
                if not isinstance(command,dict):raise ValueError()
                defaults={'requestId':None,'petId':None,'kind':0,'tenPack':False,'expectedRevision':0,'slotIndex':0}
                if set(command)-set(defaults):raise ValueError()
                defaults.update(command);command=defaults
            except (ValueError,UnicodeError):self.respond(400,{'error':'invalid_json'});return
        c=sqlite3.connect(self.server.db,timeout=10)
        committed=False
        try:
            c.execute('BEGIN IMMEDIATE')
            session=c.execute('SELECT account_id FROM AuthSession WHERE token_sha256=? AND expires_utc>?',
                (hashlib.sha256(auth[7:].encode()).hexdigest(),int(time.time()))).fetchone()
            if not session:c.rollback();self.respond(401,{'error':'unauthorized'});return
            row=c.execute('SELECT world_json FROM LocalWorld WHERE singleton=1').fetchone()
            request={'action':'command' if post else 'read','accountId':session[0],'world':json.loads(row[0]),
                'selectedId':parse_qs(path.query).get('petId',[None])[0],'command':command}
            result=self.server.worker.invoke(request)
            if result['status']!=200:
                c.rollback();self.respond(result['status'],{'error':'request_rejected' if result['status']==409 else 'domain_failure'});return
            body=json.loads(result['bodyJson'])
            c.execute('UPDATE LocalWorld SET world_json=? WHERE singleton=1',(json.dumps(result['world'],separators=(',',':')),));c.commit();committed=True
        except Exception:
            if not committed:c.rollback()
            self.respond(500,{'error':'transaction_not_confirmed'});return
        finally:c.close()
        if post:
            with self.server.delay_lock:delay=self.server.delay_response_once;self.server.delay_response_once=0
            if delay:time.sleep(delay)
        self.respond(200,body)

def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--db',required=True);p.add_argument('--mono',required=True);p.add_argument('--worker',required=True)
    p.add_argument('--cert',required=True);p.add_argument('--key',required=True);p.add_argument('--port',type=int,default=8443)
    p.add_argument('--init-reference-token-file',help='Explicit fixture only: JSON local-a/local-b -> random token. No live economy data.')
    a=p.parse_args();worker=DomainWorker([a.mono,a.worker])
    try:
        if a.init_reference_token_file:initialize_reference(a.db,worker,json.loads(Path(a.init_reference_token_file).read_text()))
        server=ReferenceServer(('127.0.0.1',a.port),a.db,worker,a.cert,a.key)
        print('Local reference HTTPS host listening on loopback port',server.server_port,flush=True);server.serve_forever()
    finally:worker.close()
if __name__=='__main__':main()
