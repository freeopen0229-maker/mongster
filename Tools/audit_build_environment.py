"""Read-only, secret-safe audit. No credential values, remote probing or DB changes."""
from pathlib import Path
import datetime,json,os,shutil,socket,subprocess
root=Path(__file__).resolve().parents[1]
executables={x:bool(shutil.which(x)) for x in ['Unity','unity','UnityHub','dotnet','mono','docker','psql','mysql','mongod','adb']}
ci_patterns=['.github/workflows','gitlab-ci','jenkinsfile','azure-pipelines','buildkite']
ci_files=[p.relative_to(root).as_posix() for p in root.rglob('*') if p.is_file() and any(k in p.as_posix().lower() for k in ci_patterns)]
ci_indicators=[k for k in ['CI','GITHUB_ACTIONS','GITLAB_CI','JENKINS_URL','TF_BUILD','BUILDKITE'] if k in os.environ]
config_names=[k for k in os.environ if any(x in k.upper() for x in ['DATABASE_URL','CONNECTION_STRING','UNITY_LICENSE','UNITY_SERIAL','PGHOST','MYSQL_HOST'])]
probe=socket.socket();loopback=False
try:probe.bind(('127.0.0.1',0));loopback=True
except OSError:pass
finally:probe.close()
unix_ipc={'allowed':False}
try:
    ipc=socket.socket(socket.AF_UNIX,socket.SOCK_STREAM);ipc.close();unix_ipc={'allowed':True}
except OSError as e:unix_ipc={'allowed':False,'errno':e.errno,'error':e.strerror}
editor_candidates=[root.parent/'unity-install/Editor/Unity']
if os.environ.get('UNITY_EDITOR_PATH'):editor_candidates.append(Path(os.environ['UNITY_EDITOR_PATH']))
manifest=json.loads((root/'Packages/manifest.json').read_text())['dependencies']
report={'checkedUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'scope':'Current workspace/host only. Does not inspect user PC or external CI/cloud firewall.',
 'unityProjectVersion':(root/'ProjectSettings/ProjectVersion.txt').read_text().strip(),'executablesPresent':executables,
 'knownUnityDirectoriesPresent':[p for p in ['/opt/unity','/opt/Unity','/Applications/Unity','/usr/local/Unity','/workspace/Unity'] if Path(p).exists()],
 'installedEditorPaths':[str(p) for p in editor_candidates if p.is_file()],
 'unixIpcSocketProbe':unix_ipc,'ciRemoteRegistration':'NOT CONFIGURED: no repository/runner supplied',
 'portableMonoPresent':(root.parent/'mono-local/usr/bin/mono').is_file(),
 'ciFiles':ci_files,'ciIndicatorNamesPresent':ci_indicators,'relevantConfigNamesPresent':config_names,
 'dbFiles':[p.relative_to(root).as_posix() for p in root.rglob('*') if p.is_file() and p.suffix in ['.sqlite','.db','.sqlite3']],
 'localSqliteReferenceHost':True,'productionDbProvider':'UNCONFIGURED/UNKNOWN','productionDbNetworkTest':'NOT RUN: no target/address supplied',
 'loopbackBindAllowed':loopback,'externalFirewallState':'UNKNOWN; no target, cloud account or firewall configuration supplied',
 'declaredPackageVersions':manifest,'unityPackageResolution':'ATTEMPTED/FAILED: installed Editor exited 1; licensing and Package Manager IPC failure before restore/compile',
 'sceneFiles':[p.relative_to(root).as_posix() for p in root.rglob('*.unity')],
 'playerSettingsPresent':(root/'ProjectSettings/ProjectSettings.asset').exists(),
 'graphicsSettingsPresent':(root/'ProjectSettings/GraphicsSettings.asset').exists(),
 'buildSettingsPresent':(root/'ProjectSettings/EditorBuildSettings.asset').exists(),
 'buildReady':False}
out=root/'Validation/EnvironmentAudit';out.mkdir(parents=True,exist_ok=True)
(out/'results.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
print(json.dumps(report,ensure_ascii=False,indent=2))
