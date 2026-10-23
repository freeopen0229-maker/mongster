"""SQLite PetTable migration using the user-approved 0..5 mapping.
Default: read-only plan. --apply: atomic in-place update with immutable table backup.
No production database was supplied; repository tests use explicitly created fixtures.
"""
from pathlib import Path
import argparse,datetime,json,sqlite3

MAP={'0':'Common','1':'Rare','2':'Rare','3':'Epic','4':'Legendary','5':'Divine'}
TARGET=set(MAP.values())
def inspect(c):
    cols=[r[1] for r in c.execute('PRAGMA table_info("PetTable")')]
    if not cols or 'Rarity' not in cols:raise ValueError('SQLite PetTable with Rarity column required')
    rows=c.execute('SELECT * FROM PetTable').fetchall();index=cols.index('Rarity')
    legacy=sum(str(r[index]) in MAP for r in rows)
    pending=sum(str(r[index]) not in MAP and r[index] not in TARGET for r in rows)
    return cols,rows,index,{'rowCount':len(rows),'convertCount':legacy,'pendingCount':pending,'mapping':MAP}
def migrate(db,apply=False):
    path=Path(db).resolve()
    if not path.is_file():raise ValueError('Existing DB file required; no empty DB will be created')
    c=sqlite3.connect(path if apply else path.as_uri()+'?mode=ro',uri=not apply)
    try:
        if apply:c.execute('BEGIN IMMEDIATE')
        cols,rows,index,report=inspect(c)
        report.update({'scope':'SQLite PetTable only','applied':False})
        if not apply:return report
        journal=c.execute("SELECT 1 FROM sqlite_master WHERE type='table' AND name='PetRarityMigration_2026'").fetchone()
        if journal:
            entry=c.execute("SELECT marker FROM PetRarityMigration_2026 WHERE marker='five-grades-v1'").fetchone()
            if entry:
                if report['convertCount']:raise ValueError('New legacy rows after migration; refuse to overwrite original backup')
                report.update({'alreadyApplied':True,'backup':'PetTable_Backup_2026'});c.rollback();return report
        if c.execute("SELECT 1 FROM sqlite_master WHERE name='PetTable_Backup_2026'").fetchone():
            raise ValueError('Backup already exists without migration marker; preserve it and stop')
        c.execute('CREATE TABLE PetTable_Backup_2026 AS SELECT * FROM PetTable')
        c.execute('CREATE TABLE PetSchema_Backup_2026(original_sql TEXT NOT NULL)')
        schema=c.execute("SELECT sql FROM sqlite_master WHERE type='table' AND name='PetTable'").fetchone()[0]
        c.execute('INSERT INTO PetSchema_Backup_2026 VALUES(?)',(schema,))
        # 알 수 없는 등급을 Common으로 바꾸지 않는다. 변환 대기 사본에 보존한다.
        c.execute('''CREATE TABLE PetTable_MigrationPending_2026 AS SELECT * FROM PetTable
          WHERE Rarity IS NULL OR CAST(Rarity AS TEXT) NOT IN ('0','1','2','3','4','5','Common','Rare','Epic','Legendary','Divine')''')
        c.execute('''UPDATE PetTable SET Rarity=CASE CAST(Rarity AS TEXT)
          WHEN '0' THEN 'Common' WHEN '1' THEN 'Rare' WHEN '2' THEN 'Rare'
          WHEN '3' THEN 'Epic' WHEN '4' THEN 'Legendary' WHEN '5' THEN 'Divine' END
          WHERE CAST(Rarity AS TEXT) IN ('0','1','2','3','4','5')''')
        after=c.execute('SELECT * FROM PetTable').fetchall()
        backup=c.execute('SELECT * FROM PetTable_Backup_2026').fetchall()
        if backup!=rows or len(after)!=len(rows):raise ValueError('Backup/row count verification failed')
        # SELECT row order is unchanged by this column-only update; compare non-rarity data as a multiset too.
        def other_values(values):return sorted(repr(r[:index]+r[index+1:]) for r in values)
        if other_values(rows)!=other_values(after):raise ValueError('Non-rarity fields changed')
        expected={k:0 for k in TARGET};actual={k:0 for k in TARGET}
        for r in rows:
            value=MAP.get(str(r[index]),r[index])
            if value in expected:expected[value]+=1
        for r in after:
            if r[index] in actual:actual[r[index]]+=1
        if expected!=actual:raise ValueError('Rarity result counts mismatch')
        c.execute('CREATE TABLE IF NOT EXISTS PetRarityMigration_2026(marker TEXT PRIMARY KEY,applied_utc TEXT NOT NULL,report_json TEXT NOT NULL)')
        report.update({'applied':True,'backup':'PetTable_Backup_2026','pendingTable':'PetTable_MigrationPending_2026','resultCounts':actual})
        c.execute('INSERT INTO PetRarityMigration_2026 VALUES(?,?,?)',('five-grades-v1',datetime.datetime.now(datetime.timezone.utc).isoformat(),json.dumps(report)))
        c.commit();return report
    except Exception:c.rollback();raise
    finally:c.close()
def main():
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--db',required=True);p.add_argument('--apply',action='store_true');args=p.parse_args()
    print(json.dumps(migrate(args.db,args.apply),ensure_ascii=False,indent=2))
if __name__=='__main__':main()
