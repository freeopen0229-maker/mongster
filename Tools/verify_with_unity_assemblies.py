"""Compile against real Unity assemblies; execute production managed math/clock tests.
Does not replace Unity Play Mode, shader compilation or visual review.
Usage: python Tools/verify_with_unity_assemblies.py /absolute/path/to/Unity/Editor
"""
from pathlib import Path
import subprocess,sys,json,os
root=Path(__file__).resolve().parents[1];editor=Path(sys.argv[1]);data=editor/'Data';out=root/'Validation/Managed';out.mkdir(parents=True,exist_ok=True)
mono=data/'MonoBleedingEdge/bin/mono';csc=data/'MonoBleedingEdge/lib/mono/4.5/csc.exe';facade=data/'MonoBleedingEdge/lib/mono/4.5/Facades/netstandard.dll'
refs=list((data/'Managed').glob('*.dll'))+list((data/'Managed/UnityEngine').glob('*.dll'))
records=[]
def compile(name,sources,defines=None,player=False,exe=False,pure=False):
 args=['-nologo','-target:'+('exe' if exe else 'library'),'-out:'+str(out/(name+('.exe' if exe else '.dll')))]
 if defines:args+=['-define:'+defines]
 args+=['-r:'+str(p) for p in refs if not pure and not(player and ('UnityEditor' in p.name or p.name=='UnityEditor.dll'))]
 args+=['-r:'+str(facade)]+[str(s) for s in sources]
 rsp=out/(name+'.rsp');rsp.write_text('\n'.join(args));r=subprocess.run([str(mono),str(csc),'@'+str(rsp)],text=True,capture_output=True)
 records.append({'name':name,'exit_code':r.returncode,'output':r.stdout+r.stderr});print(name,r.returncode,r.stdout+r.stderr)
 if r.returncode:raise RuntimeError('C# compilation failed: '+name)
compile('EditorSource',list((root/'Assets').rglob('*.cs')),'UNITY_EDITOR,UNITY_6000_0_OR_NEWER')
compile('PlayerSource',list((root/'Assets/Scripts').rglob('*.cs')),player=True)
compile('MathProbe',[root/'Assets/Scripts/NeulguiMath.cs',root/'Assets/Scripts/HitStopState.cs',root/'Tools/MathProbe.cs'],exe=True)
env=os.environ.copy();env['MONO_PATH']=str(data/'Managed/UnityEngine')
r=subprocess.run([str(mono),str(out/'MathProbe.exe')],text=True,capture_output=True,env=env);records.append({'name':'ManagedMathAndHitStop','exit_code':r.returncode,'output':r.stdout+r.stderr});print(r.stdout+r.stderr)
(out/'results.json').write_text(json.dumps({'scope':'Actual Unity assembly C# compilation and managed tests. NOT Play Mode.','records':records},indent=2))
if r.returncode:raise RuntimeError('Managed tests failed')

compile('CoreLoopProbe',[root/'Assets/Scripts/Gameplay/PetState.cs',root/'Tools/CoreLoopProbe.cs'],exe=True)
r=subprocess.run([str(mono),str(out/'CoreLoopProbe.exe')],text=True,capture_output=True,env=env)
records.append({'name':'CoreLoopRules','exit_code':r.returncode,'output':r.stdout+r.stderr});print(r.stdout+r.stderr)
(out/'results.json').write_text(json.dumps({'scope':'Actual Unity assembly C# compilation and managed tests. NOT Play Mode.','records':records},indent=2))
if r.returncode:raise RuntimeError('Core loop tests failed')

compile('VisualRenewalProbe',[root/'Assets/Scripts/VisualRenewal/PetSpringMath.cs',root/'Tools/VisualRenewalProbe.cs'],exe=True)
r=subprocess.run([str(mono),str(out/'VisualRenewalProbe.exe')],text=True,capture_output=True,env=env)
records.append({'name':'VisualRenewalSpring','exit_code':r.returncode,'output':r.stdout+r.stderr});print(r.stdout+r.stderr)
(out/'results.json').write_text(json.dumps({'scope':'Actual Unity assembly C# compilation and managed tests. NOT Play Mode or shader compilation.','records':records},indent=2))
if r.returncode:raise RuntimeError('Visual renewal spring tests failed')

shared=list((root/'Assets/Scripts/SliceShared').glob('*.cs'))+list((root/'Assets/Scripts/EconomyV2').glob('*.cs'))
compile('SliceServer',shared+list((root/'ServerSources').glob('*.cs')),pure=True)
compile('SliceProbe',shared+list((root/'ServerSources').rglob('*.cs'))+[root/'Tools/SliceProbe.cs'],exe=True,pure=True)
r=subprocess.run([str(mono),str(out/'SliceProbe.exe')],text=True,capture_output=True)
records.append({'name':'SliceAuthorityAndEconomy','exit_code':r.returncode,'output':r.stdout+r.stderr});print(r.stdout+r.stderr)
(out/'results.json').write_text(json.dumps({'scope':'Unity API C# compilation; pure C# server compilation; managed rules/in-memory atomicity tests. NOT Play Mode, deployed server or shader compilation.','records':records},indent=2))
if r.returncode:raise RuntimeError('Slice authority/economy tests failed')

compile('ViewportLocalProbe',shared+list((root/'ServerSources').rglob('*.cs'))+[root/'Assets/Scripts/Testing/LocalHatchSimulation.cs',root/'Tools/ViewportLocalProbe.cs'],exe=True,pure=True)
r=subprocess.run([str(mono),str(out/'ViewportLocalProbe.exe')],text=True,capture_output=True)
records.append({'name':'ViewportLocalMockAndHatch','exit_code':r.returncode,'output':r.stdout+r.stderr});print(r.stdout+r.stderr)
(out/'results.json').write_text(json.dumps({'scope':'Unity API C# compilation and pure managed scenarios. NOT native viewport rendering or shader/Play Mode validation.','records':records},indent=2))
if r.returncode:raise RuntimeError('Viewport local mock/hatch tests failed')
