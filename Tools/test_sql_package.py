"""Execute packaged SQLite SQL against isolated fixtures, never a configured DB."""
from pathlib import Path
import json,sqlite3,tempfile
root=Path(__file__).resolve().parents[1];sql=(root/'Database/SQLite/001_pet_rarity_v2.sql').read_text();checks=0

def check(ok):
 global checks
 checks+=1
 assert ok
with tempfile.TemporaryDirectory() as directory:
 c=sqlite3.connect(str(Path(directory)/'fixture.sqlite'))
 c.execute('CREATE TABLE PetTable(PetId TEXT PRIMARY KEY,Rarity TEXT,Name TEXT,Payload BLOB)')
 grades=['0','1','2','3','4','5','Divine','mystery',None]
 original=[(str(i),grade,'pet-'+str(i),bytes([i])) for i,grade in enumerate(grades)]
 c.executemany('INSERT INTO PetTable VALUES(?,?,?,?)',original);c.commit()
 c.executescript(sql)
 check(c.execute('SELECT * FROM PetTable_Backup_2026 ORDER BY PetId').fetchall()==original)
 mapped=['Common','Rare','Rare','Epic','Legendary','Divine','Divine','mystery',None]
 check(c.execute('SELECT Rarity FROM PetTable ORDER BY PetId').fetchall()==[(x,) for x in mapped])
 check(c.execute('SELECT COUNT(*) FROM PetTable_MigrationPending_2026').fetchone()[0]==2)
 check(c.execute('PRAGMA integrity_check').fetchone()[0]=='ok')
 check(c.execute('SELECT PetId,Name,Payload FROM PetTable ORDER BY PetId').fetchall()==[(r[0],r[2],r[3]) for r in original])
 failed=False
 try:c.executescript(sql)
 except sqlite3.Error:failed=True;c.rollback()
 check(failed)
 check(c.execute('SELECT * FROM PetTable_Backup_2026 ORDER BY PetId').fetchall()==original)
 c.close()
 # A CHECK rejecting standard names must rollback backup creation and all writes.
 c=sqlite3.connect(str(Path(directory)/'restricted.sqlite'))
 c.execute('CREATE TABLE PetTable(PetId TEXT,Rarity TEXT CHECK(Rarity="0"))');c.execute('INSERT INTO PetTable VALUES("a","0")');c.commit()
 failed=False
 try:c.executescript(sql)
 except sqlite3.Error:failed=True;c.rollback()
 check(failed)
 check(c.execute('SELECT Rarity FROM PetTable').fetchone()[0]=='0')
 check(c.execute('SELECT 1 FROM sqlite_master WHERE name="PetTable_Backup_2026"').fetchone() is None)
 c.close()
out=root/'Validation/DatabasePackage';out.mkdir(parents=True,exist_ok=True)
(out/'results.json').write_text(json.dumps({'scope':'Packaged SQL executed against SQLite fixtures only; no production DB','checks':checks,'status':'PASS'},indent=2)+'\n')
print('PASS:',checks,'SQLite SQL package checks; not production migration.')
