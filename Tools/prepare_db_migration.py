"""SQLite reference preflight/staging copy. Never changes the source database.
No production database adapter exists. This does NOT finish pet migration.
Usage: --source-db exported.sqlite --profile explicit-columns.json [--output-copy staged.sqlite]
"""
from pathlib import Path
import argparse,json,re,sqlite3

GRADES=['Common','Uncommon','Rare','Unique','Spirit','Divine']
TARGET={'Common','Rare','Epic','Legendary','Divine'}
def identifier(name):
    if not isinstance(name,str) or not re.fullmatch(r'[A-Za-z_][A-Za-z_0-9]*',name):
        raise ValueError('Explicit simple SQL identifier required')
    return '"'+name+'"'
def valid_id(value):
    return isinstance(value,str) and re.fullmatch(r'[A-Za-z0-9_-]{1,80}',value)
def integer(value,maximum):
    if type(value) is not int or value<0 or value>maximum:raise ValueError('Invalid currency/quota integer')
    return value

def prepare(source,profile,output=None):
    source=Path(source).resolve()
    if not source.is_file():raise ValueError('Source SQLite file missing')
    if output and (Path(output).resolve()==source or Path(output).exists()):raise ValueError('Output must be a new file, distinct from source')
    a=profile['accounts'];p=profile['pets'];mapping=profile.get('gradeMap',{})
    if any(k not in GRADES or v not in TARGET for k,v in mapping.items()):raise ValueError('Invalid explicit rarity map')
    # 누락된 보석/실타래 열을 0으로 가정하려면 프로필에서 명시해야 한다.
    fields=[identifier(a['id']),identifier(a['jeon'])]
    for key in ['gem','thread']:
        column=a.get(key)
        if column is None:
            if not a.get('initializeMissingPremiumAsZero',False):raise ValueError('Missing premium currency column requires explicit zero initialization')
            fields.append('0')
        else:fields.append(identifier(column))
    quota=profile['quotaIssued'];quota_rows=[]
    if set(quota)!=set(map(str,range(5))):raise ValueError('All five historical quota counts are required')
    for i in range(5):
        limit=10 if i==0 else 100
        quota_rows.append((i,limit,integer(quota[str(i)],limit)))
    connection=sqlite3.connect(source.as_uri()+'?mode=ro',uri=True)
    created=None
    try:
        connection.execute('BEGIN') # 동일한 읽기 snapshot에서 사본과 staging 데이터를 만든다.
        wallets=list(connection.execute('SELECT '+','.join(fields)+' FROM '+identifier(a['table'])))
        seen=set()
        for account,jeon,gem,thread in wallets:
            if not valid_id(account) or account in seen:raise ValueError('Duplicate/invalid account ID')
            seen.add(account);integer(jeon,10**12);integer(gem,2147483647);integer(thread,2147483647)
        pets=[];pet_ids=set()
        for pet_id,raw in connection.execute('SELECT '+identifier(p['id'])+','+identifier(p['payload'])+' FROM '+identifier(p['table'])):
            if not valid_id(pet_id) or pet_id in pet_ids:raise ValueError('Duplicate/invalid pet ID')
            pet_ids.add(pet_id);data=json.loads(raw)
            if not isinstance(data,dict) or data.get('PetId')!=pet_id:raise ValueError('Pet ID/payload mismatch')
            grade=data.get('Grade')
            if type(grade) is int:
                if grade<0 or grade>=len(GRADES):raise ValueError('Invalid V1 rarity ordinal')
                grade=GRADES[grade]
            if grade not in GRADES:raise ValueError('Unknown V1 rarity')
            proposed=mapping.get(grade)
            # 새 스킬/외형/케어/개체값은 임의 합성하지 않는다. 원문을 그대로 보존한다.
            status='skills_and_visual_mapping_required' if proposed else 'grade_mapping_required'
            pets.append((pet_id,raw,proposed,status))
        report={'scope':'SQLite reference preflight/staging, NOT production migration','accountCount':len(wallets),'petCount':len(pets),
            'jeonTotal':sum(w[1] for w in wallets),'gemTotal':sum(w[2] for w in wallets),'threadTotal':sum(w[3] for w in wallets),
            'gradeMappingRequired':sum(x[2] is None for x in pets),'runtimeReady':False,
            'petMigration':'Blocked until explicit V1/V2 skills, grade, visual and retained trait mappings are completed',
            'quotas':[{'hiddenType':i,'limit':limit,'issued':issued} for i,limit,issued in quota_rows]}
        if output:
            output=Path(output).resolve();output.parent.mkdir(parents=True,exist_ok=True)
            # 기존 파일은 절대로 덮어쓰지 않는다.
            with output.open('xb'):pass
            created=output
            target=sqlite3.connect(output)
            try:
                connection.backup(target)
                schema=Path(__file__).resolve().parents[1]/'ServerSources/Migrations/001_economy_v2_staging.sql'
                target.executescript('BEGIN IMMEDIATE;\n'+schema.read_text())
                target.executemany('INSERT INTO EconomyV2WalletStaging VALUES(?,?,?,?)',wallets)
                target.executemany('INSERT INTO EconomyV2HiddenQuotaStaging VALUES(?,?,?)',quota_rows)
                target.executemany('INSERT INTO EconomyV2LegacyPetStaging VALUES(?,?,?,?)',pets)
                target.commit()
            except Exception:
                target.rollback();raise
            finally:target.close()
        return report
    except Exception:
        if created:created.unlink(missing_ok=True)
        raise
    finally:connection.close()

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source-db',required=True);parser.add_argument('--profile',required=True);parser.add_argument('--output-copy')
    args=parser.parse_args()
    report=prepare(args.source_db,json.loads(Path(args.profile).read_text()),args.output_copy)
    print(json.dumps(report,ensure_ascii=False,indent=2))
if __name__=='__main__':main()
