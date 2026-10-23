"""Run exact local trade + hatch code under Mono, without pretending to run Unity.
Usage: python Tools/verify_viewport_local.py /path/to/mono /path/to/mcs.exe
"""
from pathlib import Path
import hashlib,json,os,subprocess,sys
root=Path(__file__).resolve().parents[1]
mono=Path(sys.argv[1]).resolve();compiler=Path(sys.argv[2]).resolve()
out=root/'Validation/ViewportLocal';out.mkdir(parents=True,exist_ok=True)
sources=sorted((root/'Assets/Scripts/SliceShared').glob('*.cs'))+sorted((root/'Assets/Scripts/EconomyV2').glob('*.cs'))+sorted((root/'ServerSources').rglob('*.cs'))
sources += [root/'Assets/Scripts/Testing/LocalHatchSimulation.cs',root/'Tools/ViewportLocalProbe.cs']
env=os.environ.copy();env['MONO_PATH']=str(compiler.parent)
prefix=mono.parent.parent
if (prefix.parent/'etc/mono/config').exists():env['MONO_CFG_DIR']=str(prefix.parent/'etc')
env['LD_LIBRARY_PATH']=str(prefix/'lib')+os.pathsep+env.get('LD_LIBRARY_PATH','')
records=[]
def run(name,args):
    r=subprocess.run([str(x) for x in args],capture_output=True,text=True,env=env)
    records.append({'name':name,'exit_code':r.returncode,'output':r.stdout+r.stderr})
    print(name,r.returncode);print(r.stdout+r.stderr)
    return r.returncode
code=run('CompileLocalMock',[mono,compiler,'-langversion:latest','-r:System.Core','-out:'+str(out/'ViewportLocalProbe.exe'),*sources])
if not code:code=run('RunLocalMock',[mono,out/'ViewportLocalProbe.exe'])
if not code:
    # Unity Editor가 읽을 생성 사본의 전처리/컴파일도 검사한다. Unity API 검사는 아니다.
    generated=sorted((root/'Assets/Editor/LocalMockGenerated').rglob('*.cs'))
    shared=sorted((root/'Assets/Scripts/SliceShared').glob('*.cs'))+sorted((root/'Assets/Scripts/EconomyV2').glob('*.cs'))
    mirror=shared+generated+[root/'Assets/Scripts/Testing/LocalHatchSimulation.cs',root/'Tools/ViewportLocalProbe.cs']
    code=run('CompileEditorMirror',[mono,compiler,'-define:UNITY_EDITOR','-langversion:latest','-r:System.Core','-out:'+str(out/'EditorMirrorProbe.exe'),*mirror])
    if not code:code=run('RunEditorMirror',[mono,out/'EditorMirrorProbe.exe'])
    if not code:code=run('CompileNonEditorGuard',[mono,compiler,'-target:library','-langversion:latest','-out:'+str(out/'NonEditorGuard.dll'),*shared,*generated])
    if not code:code=run('CompileBoundaryProbe',[mono,compiler,'-out:'+str(out/'BoundaryProbe.exe'),root/'Tools/PlayerServerBoundaryProbe.cs'])
    if not code:code=run('RunNonEditorGuard',[mono,out/'BoundaryProbe.exe',out/'NonEditorGuard.dll'])
(out/'results.json').write_text(json.dumps({'scope':'Pure C# local trade and hatch policy. NOT Unity API compilation, Editor console execution, rendering, Play Mode or mobile tests.',
    'records':records,'source_sha256':{str(p.relative_to(root)):hashlib.sha256(p.read_bytes()).hexdigest() for p in sources}},indent=2)+'\n')
sys.exit(code)
