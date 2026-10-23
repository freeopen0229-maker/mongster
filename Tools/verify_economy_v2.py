"""Compile/run actual server domain sources on Mono. No Unity/native/DB claims.
Usage: python Tools/verify_economy_v2.py /absolute/mono /absolute/mcs.exe
"""
from pathlib import Path
import hashlib,json,os,subprocess,sys
root=Path(__file__).resolve().parents[1]
mono=Path(sys.argv[1]).resolve();compiler=Path(sys.argv[2]).resolve()
out=root/'Validation/EconomyV2';out.mkdir(parents=True,exist_ok=True)
env=os.environ.copy();env['MONO_PATH']=str(compiler.parent)
prefix=mono.parent.parent
if (prefix.parent/'etc/mono/config').exists():env['MONO_CFG_DIR']=str(prefix.parent/'etc')
env['LD_LIBRARY_PATH']=str(prefix/'lib')+os.pathsep+env.get('LD_LIBRARY_PATH','')
sources=sorted((root/'Assets/Scripts/SliceShared').glob('*.cs'))+sorted((root/'Assets/Scripts/EconomyV2').glob('*.cs'))+sorted((root/'ServerSources').rglob('*.cs'))
records=[]
for name in ['EconomyV2Probe','SliceProbe','ClientEconomyProbe','HatchPresentationProbe','PetLoopProbe']:
    exe=out/(name+'.exe')
    args=[mono,compiler,'-r:System.Core','-out:'+str(exe),*sources,root/'Tools'/(name+'.cs')]
    for phase,command in [('Compile',args),('Run',[mono,exe])]:
        r=subprocess.run([str(x) for x in command],text=True,capture_output=True,env=env)
        record={'name':name+phase,'exit_code':r.returncode,'output':r.stdout+r.stderr};records.append(record)
        print(record['name'],r.returncode);print(record['output'])
        if r.returncode:break
    if r.returncode:break
(out/'results.json').write_text(json.dumps({'scope':'Actual pure C# domain compilation and single-process in-memory scenarios. NOT Unity compilation/Play Mode, deployed DB/auth/HTTP or store verification.',
    'records':records,'source_sha256':{str(p.relative_to(root)):hashlib.sha256(p.read_bytes()).hexdigest() for p in sources}},indent=2)+'\n')
sys.exit(r.returncode)
