"""Run a real licensed Unity editor. A failed launch/import is never reported as compilation PASS.
Usage: --editor /absolute/Unity [--mode import|configure|android] [--timeout 900]
"""
from pathlib import Path
import argparse,datetime,json,os,subprocess,time,xml.etree.ElementTree as ET
from unity_preflight import inspect_environment

def main():
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--editor',required=True);p.add_argument('--mode',choices=['import','configure','playmode','android'],default='import');p.add_argument('--timeout',type=int,default=900);a=p.parse_args()
 root=Path(__file__).resolve().parents[1];editor=Path(a.editor).expanduser().resolve();out=root/'Validation/NativeUnity';out.mkdir(parents=True,exist_ok=True)
 if not editor.is_file() or not os.access(editor,os.X_OK):raise SystemExit('Executable Unity editor not found')
 preflight=inspect_environment(root,editor)
 (out/'preflight.json').write_text(json.dumps(preflight,indent=2)+'\n')
 if preflight['status']=='BLOCKED':
  # Preserve earlier real import logs: this is a prerequisite failure, not an Editor run.
  (out/(a.mode+'-preflight-blocked.json')).write_text(json.dumps(preflight,indent=2)+'\n')
  print(json.dumps(preflight,indent=2));raise SystemExit(1)
 log=out/(a.mode+'-editor.log');console=out/(a.mode+'-console.log')
 if a.mode=='playmode' and os.name!='nt' and not (os.environ.get('DISPLAY') or os.environ.get('WAYLAND_DISPLAY')):
  raise SystemExit('Play Mode frame capture requires a display/GPU runner; configure DISPLAY or WAYLAND_DISPLAY')
 command=[str(editor),'-batchmode','-projectPath',str(root),'-logFile',str(log)]
 if a.mode=='playmode':command+=['-runTests','-testPlatform','PlayMode','-testResults',str(out/'playmode.xml')]
 else:command+=['-nographics','-quit']
 if a.mode=='configure':command+=['-executeMethod','FusionTamagotchi.EditorTools.BuildAutomation.Configure']
 if a.mode=='android':command+=['-buildTarget','Android','-executeMethod','FusionTamagotchi.EditorTools.BuildAutomation.BuildAndroid']
 started=time.time();code=None;timed_out=False
 # Non-secret command line only; licensing/credentials must be configured on the runner outside this script.
 with console.open('w') as f:
  try:code=subprocess.run(command,stdout=f,stderr=subprocess.STDOUT,timeout=a.timeout).returncode
  except subprocess.TimeoutExpired:timed_out=True
 text=log.read_text(errors='replace') if log.exists() else ''
 indicators=[line for line in text.splitlines() if any(k in line.lower() for k in ['error cs','licensing','license','ipc','package manager','failed to'])]
 artifact=root/'Builds/Android/Neulgui-development.apk'
 result={'utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'mode':a.mode,'editorPath':str(editor),'exitCode':code,'timedOut':timed_out,
  'status':'PASS' if code==0 and not timed_out else 'FAILED/BLOCKED','editorLog':log.relative_to(root).as_posix(),
  'note':'No native Play Mode/rendering/audio/mobile assertion is made. Exit zero import only establishes that this invocation ended successfully.',
  'failureIndicators':indicators[-30:],'apkExists':artifact.is_file()}
 evidence=False
 if a.mode=='import':
  evidence=('Asset Pipeline Refresh' in text or 'Initial Asset Pipeline Refresh' in text) and ('ReloadAssembly' in text or 'Reloading assemblies' in text)
 elif a.mode=='configure':
  marker=out/'configured.json';evidence=marker.exists() and marker.stat().st_mtime>=started and 'DEVELOPMENT CONFIGURATION COMPLETE' in text
 elif a.mode=='android':evidence=artifact.is_file() and artifact.stat().st_mtime>=started
 elif a.mode=='playmode':
  xml=out/'playmode.xml'
  if xml.is_file() and xml.stat().st_mtime>=started:
   try:
    run=ET.parse(xml).getroot();cases=run.findall('.//test-case')
    evidence=run.attrib.get('result')=='Passed' and len(cases)>=2 and all(c.attrib.get('result')=='Passed' for c in cases)
   except ET.ParseError:pass
 result['completionEvidence']=evidence
 if not evidence:result['status']='FAILED/BLOCKED'
 if 'error CS' in text:result['status']='FAILED/BLOCKED' 
 (out/(a.mode+'-result.json')).write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(result,indent=2))
 raise SystemExit(0 if result['status']=='PASS' else 1)
if __name__=='__main__':main()
