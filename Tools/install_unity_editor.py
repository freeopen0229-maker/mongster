"""Install the pinned official Linux editor archive. Does not activate a Unity license.
Usage: python Tools/install_unity_editor.py --destination /absolute/tooling/unity-6000.3.26f1
"""
from pathlib import Path
import argparse,hashlib,json,subprocess,sys,urllib.request
VERSION='6000.3.26f1'
URL='https://download.unity3d.com/download_unity/de8167e7440c/LinuxEditorInstaller/Unity-6000.3.26f1.tar.xz'
SIZE=4540380840
# Recorded from the official archive downloaded during this task; not a distributor signature.
SHA256='2c2d84526c5ba3b34b2bbeb5f5a8758a113129b9abf596d2068f999c353ca547'
def main():
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--destination',required=True);a=p.parse_args()
 if sys.platform!='linux':raise SystemExit('This installer is Linux-specific')
 dest=Path(a.destination).resolve();dest.mkdir(parents=True,exist_ok=True);archive=dest/('Unity-'+VERSION+'.tar.xz')
 if not archive.exists():
  part=archive.with_suffix('.part');total=0;next_report=256*1024*1024
  with urllib.request.urlopen(URL,timeout=40) as response,part.open('wb') as output:
   while True:
    chunk=response.read(4*1024*1024)
    if not chunk:break
    output.write(chunk);total+=len(chunk)
    if total>=next_report:print('Downloaded MiB:',total//1024//1024,flush=True);next_report+=256*1024*1024
  part.rename(archive)
 sha=hashlib.sha256()
 with archive.open('rb') as f:
  while True:
   chunk=f.read(4*1024*1024)
   if not chunk:break
   sha.update(chunk)
 if archive.stat().st_size!=SIZE or sha.hexdigest()!=SHA256:raise SystemExit('Archive size/hash mismatch; extraction stopped')
 with (dest/'extract.log').open('w') as log:
  subprocess.run(['tar','--no-same-owner','--no-same-permissions','-xf',str(archive),'-C',str(dest)],stdout=log,stderr=subprocess.STDOUT,check=True)
 editor=dest/'Editor/Unity';r=subprocess.run([str(editor),'-version'],capture_output=True,text=True,timeout=30)
 report={'version':VERSION,'officialUrl':URL,'archiveSha256':SHA256,'installed':editor.is_file(),
         'versionProbeExit':r.returncode,'versionProbeOutput':r.stdout.strip(),'licenseActivation':'NOT PROVIDED'}
 (dest/'install-result.json').write_text(json.dumps(report,indent=2)+'\n');print(json.dumps(report,indent=2))
 if r.returncode or VERSION not in r.stdout:raise SystemExit('Editor version probe failed')
if __name__=='__main__':main()
