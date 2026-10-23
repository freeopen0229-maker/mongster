"""Offline Roslyn compilation; never starts Unity Editor or activates a license.
Uses installed Unity compiler/API DLLs as references, not a restored project's assemblies.
Usage: python3 Tools/verify_roslyn_core.py /path/to/Editor
"""
from pathlib import Path
import hashlib,json,os,subprocess,sys
root=Path(__file__).resolve().parents[1];data=Path(sys.argv[1]).resolve()/'Data'
mono=data/'MonoBleedingEdge/bin/mono';csc=data/'MonoBleedingEdge/lib/mono/4.5/csc.exe'
out=root/'Validation/Roslyn';out.mkdir(parents=True,exist_ok=True)
records=[]
shared=sorted((root/'Assets/Scripts/SliceShared').glob('*.cs'))+sorted((root/'Assets/Scripts/EconomyV2').glob('*.cs'))
domain=shared+sorted((root/'ServerSources').rglob('*.cs'))
def compile(name,sources,refs=(),defines='',exe=False):
    target=out/(name+('.exe' if exe else '.dll'))
    framework=data/'MonoBleedingEdge/lib/mono/4.5'
    args=['-nologo','-nostdlib+','-target:'+('exe' if exe else 'library'),'-out:'+str(target)]
    args+=['-r:'+str(framework/(name+'.dll')) for name in ['mscorlib','System','System.Core','System.Runtime.Serialization']]
    args+=['-r:'+str(x) for x in refs]
    if defines:args+=['-define:'+defines]
    # Response file quoted paths, no shell interpolation.
    rsp=out/(name+'.rsp');rsp.write_text('\n'.join('"'+x+'"' for x in args+[str(x) for x in sources]))
    result=subprocess.run([str(mono),str(csc),'-noconfig','@'+str(rsp)],capture_output=True,text=True)
    records.append({'name':name+'Compile','exitCode':result.returncode,'output':result.stdout+result.stderr})
    print(name,result.returncode,result.stdout+result.stderr)
    return result.returncode,target
for name in ['EconomyV2Probe','SliceProbe','ClientEconomyProbe','HatchPresentationProbe','PetLoopProbe']:
    code,exe=compile(name,domain+[root/'Tools'/(name+'.cs')],exe=True)
    if code:break
    result=subprocess.run([str(mono),str(exe)],capture_output=True,text=True)
    records.append({'name':name+'Run','exitCode':result.returncode,'output':result.stdout+result.stderr});print(result.stdout+result.stderr)
    if result.returncode:break
else:
    # UI glue is not pure C#. Compile against actual engine/UI reference assemblies.
    template=data/'Resources/PackageManager/ProjectTemplates/libcache/com.unity.template.2d-cross-platform-2d-6.1.6/ScriptAssemblies'
    refs=[p for p in (data/'Managed').glob('*.dll') if p.name not in ('UnityEngine.dll','UnityEditor.dll')]+list((data/'Managed/UnityEngine').glob('*.dll'))
    refs+=[template/'UnityEngine.UI.dll',data/'MonoBleedingEdge/lib/mono/4.5/Facades/netstandard.dll']
    code,_=compile('PlayerApiSource',sorted((root/'Assets/Scripts').rglob('*.cs')),refs)
    if not code:
        # Only URP package APIs needed by build tooling; template folders can
        # contain unrelated test/stub assemblies and must not be bulk referenced.
        editor_refs=sorted(set(refs+list(template.glob('Unity.RenderPipelines.*.dll'))))
        compile('EditorApiSource',sorted((root/'Assets/Scripts').rglob('*.cs'))+sorted((root/'Assets/Editor').rglob('*.cs')),
                editor_refs,'UNITY_EDITOR,UNITY_6000_0_OR_NEWER')
(out/'results.json').write_text(json.dumps({'scope':'Roslyn offline compilation and managed core execution. Player source uses real installed engine + template UGUI API DLLs. NOT Unity import/UPM/asmdef/shader/build/Play Mode validation.',
 'compiler':str(csc),'records':records,'source_sha256':{p.relative_to(root).as_posix():hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(set(domain+list((root/'Assets/Scripts').rglob('*.cs'))+list((root/'Assets/Editor').rglob('*.cs'))))}},indent=2)+'\n')
raise SystemExit(1 if any(r['exitCode'] for r in records) else 0)
