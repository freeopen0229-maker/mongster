"""Run only after the operator verifies the email owner's identity."""
import argparse
import getpass
from .auth_store import AuthStore

def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--db',required=True)
    p.add_argument('--email',required=True)
    args = p.parse_args()
    password = getpass.getpass('New password (12–128 characters): ')
    if password != getpass.getpass('Repeat password: '):
        raise SystemExit('Passwords differ')
    account = AuthStore(args.db).provision_email(args.email,password)
    print('Created account:',account)
    print('Game account provisioning is a separate domain-adapter step.')

if __name__ == '__main__':
    main()
