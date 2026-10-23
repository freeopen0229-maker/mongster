from pathlib import Path
import hashlib,json,sqlite3,tempfile
from prepare_db_migration import prepare
checks=0
def check(ok):
    global checks
    checks+=1;assert ok
def rejected(call):
    try:call()
    except (ValueError,sqlite3.Error):return True
    return False
with tempfile.TemporaryDirectory() as tmp:
    source=Path(tmp)/'source.sqlite';dest=Path(tmp)/'staging.sqlite'
    c=sqlite3.connect(source);c.executescript('CREATE TABLE Accounts(AccountId TEXT, Coins INTEGER, Gem INTEGER, Thread INTEGER); CREATE TABLE Pets(PetId TEXT, SnapshotJson TEXT);')
    c.execute('INSERT INTO Accounts VALUES(?,?,?,?)',('a',123456,100,5))
    raw=json.dumps({'PetId':'pet-a','Grade':2,'Individual':{'Attack':31},'Personality':1})
    c.execute('INSERT INTO Pets VALUES(?,?)',('pet-a',raw));c.commit();c.close()
    digest=hashlib.sha256(source.read_bytes()).hexdigest()
    profile={'accounts':{'table':'Accounts','id':'AccountId','jeon':'Coins','gem':'Gem','thread':'Thread'},
             'pets':{'table':'Pets','id':'PetId','payload':'SnapshotJson'},'quotaIssued':{'0':3,'1':20,'2':0,'3':0,'4':0}}
    report=prepare(source,profile)
    check(report['jeonTotal']==123456 and report['gradeMappingRequired']==1 and not report['runtimeReady'])
    check(not dest.exists())
    prepare(source,profile,dest)
    check(hashlib.sha256(source.read_bytes()).hexdigest()==digest)
    c=sqlite3.connect(dest)
    check(c.execute('SELECT * FROM EconomyV2WalletStaging').fetchone()==('a',123456,100,5))
    check(c.execute('SELECT quota_limit,issued FROM EconomyV2HiddenQuotaStaging WHERE hidden_type=0').fetchone()==(10,3))
    check(c.execute('SELECT quota_limit,issued FROM EconomyV2HiddenQuotaStaging WHERE hidden_type=1').fetchone()==(100,20))
    check(c.execute('SELECT source_json,proposed_rarity FROM EconomyV2LegacyPetStaging').fetchone()==(raw,None))
    check(c.execute('SELECT ready_for_runtime FROM EconomyV2MigrationState').fetchone()[0]==0);c.close()
    check(rejected(lambda:prepare(source,profile,dest)))
    check(rejected(lambda:prepare(source,profile,source)))
    bad=json.loads(json.dumps(profile));bad['quotaIssued']['0']=11
    check(rejected(lambda:prepare(source,bad)))
    bad=json.loads(json.dumps(profile));bad['accounts']['gem']=None
    check(rejected(lambda:prepare(source,bad)))
    mapped=json.loads(json.dumps(profile));mapped['gradeMap']={'Rare':'Epic'}
    check(prepare(source,mapped)['gradeMappingRequired']==0) # 예시 변환일 뿐 운영 등급 매핑 확정 아님.
    # 음수 잔액은 사본을 만들기 전에 차단한다.
    c=sqlite3.connect(source);c.execute('UPDATE Accounts SET Coins=-1');c.commit();c.close()
    check(rejected(lambda:prepare(source,profile,Path(tmp)/'bad.sqlite')))
    check(not (Path(tmp)/'bad.sqlite').exists())
root=Path(__file__).resolve().parents[1];out=root/'Validation/ClientIntegration';out.mkdir(parents=True,exist_ok=True)
result={'scope':'Temporary SQLite fixtures only. No production/user DB accessed. Pet migration remains blocked.','checks':checks,'result':'PASS'}
(out/'migration_results.json').write_text(json.dumps(result,indent=2)+'\n')
print('PASS:',checks,'migration preflight/staging checks. No live DB migration performed.')
