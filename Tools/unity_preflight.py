"""Secret-safe native runner preflight. Socket denial blocks Unity before a costly import.
License file presence is a hint only: entitlement validity requires Unity/Hub.
No process killing, firewall changes, credential output, or sandbox modification.
"""
from pathlib import Path
import argparse, datetime, json, os, shutil, socket, tempfile

def inspect_environment(root, editor):
    report = {'utc': datetime.datetime.now(datetime.timezone.utc).isoformat(),
              'editorExists': editor.is_file(), 'hubPresent': bool(shutil.which('unityhub') or shutil.which('UnityHub')),
              'licenseActivation': 'NOT VERIFIED: use official Hub/license provisioning on runner',
              'unixIpc': {}, 'hostSecurity': {}, 'projectWritable': False}
    # A fresh private socket rules out stale Unity socket names and daemon collisions.
    try:
        with tempfile.TemporaryDirectory(prefix='neulgui-ipc-') as directory:
            with socket.socket(socket.AF_UNIX, socket.SOCK_STREAM) as server:
                address = str(Path(directory)/'probe.sock')
                server.bind(address); server.listen(1)
                with socket.socket(socket.AF_UNIX, socket.SOCK_STREAM) as client:
                    client.connect(address)
                    peer, _ = server.accept()
                    with peer:
                        client.sendall(b'x'); assert peer.recv(1) == b'x'
        report['unixIpc'] = {'allowed': True}
    except (OSError, AttributeError) as error:
        report['unixIpc'] = {'allowed': False, 'errno': getattr(error, 'errno', None), 'error': str(error)}
    status = Path('/proc/self/status')
    if status.exists():
        for line in status.read_text().splitlines():
            if line.split(':')[0] in ['NoNewPrivs', 'Seccomp', 'Seccomp_filters']:
                key, value = line.split(':', 1); report['hostSecurity'][key] = value.strip()
    try:
        with tempfile.TemporaryFile(dir=root): pass
        report['projectWritable'] = True
    except OSError: pass
    report['status'] = 'PREREQUISITES_PRESENT_LICENSE_UNVERIFIED' if (
        report['editorExists'] and report['unixIpc']['allowed'] and report['projectWritable']) else 'BLOCKED'
    return report

def main():
    parser = argparse.ArgumentParser(description=__doc__); parser.add_argument('--editor', required=True)
    args = parser.parse_args(); root = Path(__file__).resolve().parents[1]
    report = inspect_environment(root, Path(args.editor).expanduser().resolve())
    out = root/'Validation/NativeUnity'; out.mkdir(parents=True, exist_ok=True)
    (out/'preflight.json').write_text(json.dumps(report, indent=2)+'\n')
    print(json.dumps(report, indent=2))
    return 1 if report['status'] == 'BLOCKED' else 0

if __name__ == '__main__': raise SystemExit(main())
