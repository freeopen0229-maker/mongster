import json,sqlite3,tempfile
from pathlib import Path
from migrate_pet_rarity import migrate
checks=0
def check(ok):
    global checks
    checks+=1;assert ok
def rejects(f):
    try:f()
    except (ValueError,sqlite3.Error):return True
    return False
with tempfile.TemporaryDirectory() as temp:
    path=Path(temp)/'reference.sqlite';c=sqlite3.connect(path)
    c.execute('CREATE TABLE PetTable(PetId TEXT PRIMARY KEY,Rarity INTEGER,Payload TEXT)')
    for i in range(6):c.execute('INSERT INTO PetTable VALUES(?,?,?)',('p'+str(i),i,'preserve-'+str(i)))
    c.executemany('INSERT INTO PetTable VALUES(?,?,?)',[('unknown',7,'iv=31'),('missing',None,'keep'),('new','Divine','serial=3')]);c.commit();c.close()
    plan=migrate(path);check(plan['convertCount']==6 and plan['pendingCount']==2 and not plan['applied'])
    report=migrate(path,True);check(report['applied'] and report['rowCount']==9)
    c=sqlite3.connect(path);check(c.execute('SELECT Rarity FROM PetTable ORDER BY PetId').fetchall()==[(None,),('Divine',),('Common',),('Rare',),('Rare',),('Epic',),('Legendary',),('Divine',),(7,)])
    check(c.execute("SELECT Rarity FROM PetTable_Backup_2026 WHERE PetId='p2'").fetchone()[0]==2)
    check(c.execute('SELECT COUNT(*) FROM PetTable_MigrationPending_2026').fetchone()[0]==2)
    check(c.execute("SELECT Payload FROM PetTable WHERE PetId='unknown'").fetchone()[0]=='iv=31');c.close()
    check(migrate(path,True)['alreadyApplied'])
    # 문자열을 허용하지 않는 제약이면 transaction 전체 rollback. 스키마를 임의 변경하지 않는다.
    constrained=Path(temp)/'constrained.sqlite';c=sqlite3.connect(constrained)
    c.execute("CREATE TABLE PetTable(PetId TEXT PRIMARY KEY,Rarity INTEGER CHECK(typeof(Rarity)='integer'))")
    c.execute('INSERT INTO PetTable VALUES(?,?)',('a',0));c.commit();c.close()
    check(rejects(lambda:migrate(constrained,True)))
    c=sqlite3.connect(constrained);check(c.execute('SELECT Rarity FROM PetTable').fetchone()[0]==0)
    check(c.execute("SELECT COUNT(*) FROM sqlite_master WHERE name='PetTable_Backup_2026'").fetchone()[0]==0);c.close()
    check(rejects(lambda:migrate(Path(temp)/'absent.sqlite',True)))
root=Path(__file__).resolve().parents[1];out=root/'Validation/NetworkAndMigration';out.mkdir(parents=True,exist_ok=True)
(out/'rarity_migration_results.json').write_text(json.dumps({'result':'PASS','checks':checks,'scope':'Created SQLite fixtures only; no production DB supplied or changed','appliedMapping':{'0':'Common','1':'Rare','2':'Rare','3':'Epic','4':'Legendary','5':'Divine'}},indent=2)+'\n')
print('PASS:',checks,'PetTable backup/update/pending/idempotency/rollback checks. Test fixtures only.')
