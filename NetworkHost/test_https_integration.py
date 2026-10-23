"""Real loopback TLS requests + actual C# domain + SQLite commit/restart tests."""
from pathlib import Path
import concurrent.futures,datetime,ipaddress,json,os,secrets,sqlite3,ssl,subprocess,sys,tempfile,threading,time,urllib.request,urllib.error
from cryptography import x509
from cryptography.hazmat.primitives import hashes,serialization
from cryptography.hazmat.primitives.asymmetric import rsa
from cryptography.x509.oid import NameOID
from reference_https_server import DomainWorker,ReferenceServer,initialize_reference
root=Path(__file__).resolve().parents[1];out=root/'Validation/NetworkAndMigration';out.mkdir(parents=True,exist_ok=True)
mono=Path(sys.argv[1]).resolve();compiler=Path(sys.argv[2]).resolve();env=os.environ.copy()
prefix=mono.parent.parent;env['MONO_PATH']=str(compiler.parent);env['LD_LIBRARY_PATH']=str(prefix/'lib')+os.pathsep+env.get('LD_LIBRARY_PATH','')
if (prefix.parent/'etc/mono/config').exists():env['MONO_CFG_DIR']=str(prefix.parent/'etc')
sources=sorted((root/'Assets/Scripts/SliceShared').glob('*.cs'))+sorted((root/'Assets/Scripts/EconomyV2').glob('*.cs'))+sorted((root/'ServerSources').rglob('*.cs'))+[root/'NetworkHost/DomainWorker.cs']
exe=out/'DomainWorker.exe'
r=subprocess.run([str(x) for x in [mono,compiler,'-r:System.Core','-r:System.Runtime.Serialization','-out:'+str(exe),*sources]],capture_output=True,text=True,env=env)
if r.returncode:raise RuntimeError(r.stdout+r.stderr)
checks=0
results=[]
def check(ok,label):
 global checks
 checks+=1
 if not ok:raise AssertionError(label)
 results.append(label)
def certificate(directory):
 key=rsa.generate_private_key(public_exponent=65537,key_size=2048)
 name=x509.Name([x509.NameAttribute(NameOID.COMMON_NAME,'Local integration fixture')]);now=datetime.datetime.now(datetime.timezone.utc)
 cert=(x509.CertificateBuilder().subject_name(name).issuer_name(name).public_key(key.public_key()).serial_number(x509.random_serial_number())
  .not_valid_before(now-datetime.timedelta(minutes=1)).not_valid_after(now+datetime.timedelta(days=1))
  .add_extension(x509.SubjectAlternativeName([x509.DNSName('localhost'),x509.IPAddress(ipaddress.ip_address('127.0.0.1'))]),False)
  .add_extension(x509.BasicConstraints(ca=True,path_length=None),True).sign(key,hashes.SHA256()))
 certpath=directory/'test-cert.pem';keypath=directory/'test-key.pem'
 certpath.write_bytes(cert.public_bytes(serialization.Encoding.PEM));keypath.write_bytes(key.private_bytes(serialization.Encoding.PEM,serialization.PrivateFormat.PKCS8,serialization.NoEncryption()))
 return certpath,keypath
with tempfile.TemporaryDirectory() as directory:
 d=Path(directory);db=d/'reference.sqlite';cert,key=certificate(d);tokens={'local-a':secrets.token_urlsafe(32),'local-b':secrets.token_urlsafe(32)}
 context=ssl.create_default_context(cafile=str(cert)) # Certificate and host validation ON, no bypass.
 worker=DomainWorker([str(mono),str(exe)],env);initialize_reference(db,worker,tokens)
 server=ReferenceServer(('127.0.0.1',0),db,worker,str(cert),str(key));thread=threading.Thread(target=server.serve_forever,daemon=True);thread.start()
 def request(path='/v2/client/snapshot',body=None,account='local-a',timeout=5):
  data=None if body is None else json.dumps(body).encode();headers={'Content-Type':'application/json'}
  if account:headers['Authorization']='Bearer '+tokens[account]
  req=urllib.request.Request('https://127.0.0.1:'+str(server.server_port)+path,data=data,headers=headers)
  try:
   with urllib.request.urlopen(req,context=context,timeout=timeout) as response:return response.status,json.load(response)
  except urllib.error.HTTPError as e:return e.code,json.load(e)
 def command(kind,key,**fields):return {'kind':kind,'requestId':key,**fields}
 try:
  check(request(account=None)[0]==401,'unauthenticated denied')
  status,view=request('/v2/client/snapshot?petId=local-neulgui');check(status==200 and view['wallet']['gem']==1000,'real TLS wallet read')
  buy=command(0,'buy-one',tenPack=False)
  status,reply=request('/v2/client/command',buy);egg=reply['receivedAssetIds'][0]
  check(status==200 and reply['snapshot']['wallet']['gem']==970 and len(reply['receivedAssetIds'])==1,'buy one real domain commit')
  status,replay=request('/v2/client/command',buy);check(status==200 and replay['receivedAssetIds']==[egg] and replay['snapshot']['wallet']['gem']==970,'same ID replay no double grant')
  check(request('/v2/client/command',command(0,'buy-one',tenPack=True))[0]==409,'same ID different payload denied')
  reroll=command(1,'roll',petId='local-neulgui',expectedRevision=0,slotIndex=0)
  status,result=request('/v2/client/command',reroll)
  check(status==200 and result['snapshot']['wallet']['jeon']==99000 and result['snapshot']['pet']['skills'][0] not in ['s1','s2'],'real reroll and wallet refresh')
  check(request('/v2/client/command',command(1,'forged',petId='local-neulgui',expectedRevision=1,slotIndex=0),account='local-b')[0]==409,'token ownership enforced')
  status,result=request('/v2/client/command',command(2,'incubate',petId=egg,expectedRevision=0))
  check(status==200 and result['snapshot']['egg']['incubating'],'start eight hour incubation')
  check(request('/v2/client/command',command(4,'too-early',petId=egg,expectedRevision=1))[0]==409,'purchase does not bypass incubation')
  check(request('/v2/client/command',command(3,'scroll',petId=egg,expectedRevision=1))[0]==200,'server consumes hatch scroll')
  status,result=request('/v2/client/command',command(4,'hatch',petId=egg,expectedRevision=2))
  check(status==200 and result['snapshot']['pet']['petId']==egg and result['snapshot']['pet']['rarity']!='Common','real hatch returns pet after ready')
  check(request('/v2/client/command',command(4,'hatch',petId=egg,expectedRevision=2))[0]==200,'hatch response replay')
  status,pet=request('/v2/pets/snapshot?petId='+egg)
  check(status==200 and pet['Pet']['PetId']==egg,'hatch and care share canonical pet identity')
  care={'PetId':egg,'RequestId':'pet-care-lost','ExpectedRevision':pet['Pet']['Revision'],'Action':1}
  affection=pet['Pet']['Affection']
  server.delay_response_once=.35;timedout=False
  try:request('/v2/pets/care',care,timeout=.08)
  except (TimeoutError,OSError):timedout=True
  check(timedout,'pet response lost after SQLite commit');time.sleep(.4)
  status,confirmed=request('/v2/pets/care',care)
  check(status==200 and confirmed['Pet']['Affection']==min(100,affection+2),'same pet RequestId applies care once')
  check(request('/v2/pets/care',care)[1]==confirmed,'pet receipt replay returns identical snapshot')
  check(request('/v2/pets/snapshot?petId='+egg,account='local-b')[0]==409,'pet snapshot enforces token ownership')
  forged=dict(care,RequestId='pet-forged',AccountCoins=999999)
  check(request('/v2/pets/care',forged)[0]==400,'client cannot supply authoritative reward fields')
  server.delay_response_once=.35;lost=command(0,'lost-after-commit',tenPack=False)
  timedout=False
  try:request('/v2/client/command',lost,timeout=.08)
  except (TimeoutError,OSError):timedout=True
  check(timedout,'real socket timeout after commit');time.sleep(.4)
  status,result=request('/v2/client/command',lost)
  check(status==200 and result['snapshot']['wallet']['gem']==940,'lost response retry spends once')
  simultaneous=command(0,'concurrent-same-key',tenPack=True)
  with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
   rs=list(pool.map(lambda _:request('/v2/client/command',simultaneous),range(2)))
  check(all(x[0]==200 for x in rs) and rs[0][1]['receivedAssetIds']==rs[1][1]['receivedAssetIds'],'concurrent identical requests same eleven IDs')
  check(request()[1]['wallet']['gem']==640,'concurrent purchase single charge')
  server.shutdown();server.server_close();thread.join();worker.close()
  worker=DomainWorker([str(mono),str(exe)],env)
  server=ReferenceServer(('127.0.0.1',0),db,worker,str(cert),str(key));thread=threading.Thread(target=server.serve_forever,daemon=True);thread.start()
  status,result=request('/v2/client/command',simultaneous)
  check(status==200 and result['snapshot']['wallet']['gem']==640 and len(result['receivedAssetIds'])==11,'host and C# worker restart retains wallet and receipt')
  c=sqlite3.connect(db);world=json.loads(c.execute('SELECT world_json FROM LocalWorld').fetchone()[0]);c.close()
  check(len(world['Assets'])==14 and len(world['Receipts'])==7,'SQLite retained all assets and command receipts')
 finally:
  server.shutdown();server.server_close();thread.join();worker.close()
record={'scope':'Actual loopback HTTPS with certificate validation, C# domain worker and SQLite durability. NOT UnityWebRequest execution, public deployment or production DB.',
 'checks':checks,'result':'PASS','assertions':results,'compilerOutput':r.stdout+r.stderr}
(out/'https_results.json').write_text(json.dumps(record,indent=2)+'\n')
print('PASS:',checks,'real HTTPS/domain/SQLite checks; cert validation, timeout/retry, concurrent purchase and process restart.')
