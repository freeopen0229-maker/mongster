"""Integration seam for the existing C# domain. Does not seed test accounts or give currency."""
import json
import sqlite3

class DomainUnavailable(Exception):
    pass

class SqliteWorldGateway:
    def __init__(self, path, invoke):
        self.path, self.invoke = str(path), invoke

    def selected_pet(self, account):
        c = sqlite3.connect(self.path, timeout=10)
        try:
            row = c.execute('SELECT world_json FROM LocalWorld WHERE singleton=1').fetchone()
            if not row:
                raise DomainUnavailable('World not provisioned')
            world = json.loads(row[0])
            if not any(a['AccountId'] == account for a in world['Accounts']):
                raise DomainUnavailable('Game account not provisioned')
            return next((a['Id'] for a in world['Assets']
                         if a['OwnerId'] == account and a['Kind'] == 0), '')
        finally:
            c.close()

    def handle(self, account, action, selected_id=None, command=None):
        # DB must be provisioned by the chosen game-account/catalog adapter first.
        c = sqlite3.connect(self.path, timeout=10)
        try:
            c.execute('BEGIN IMMEDIATE')
            row = c.execute('SELECT world_json FROM LocalWorld WHERE singleton=1').fetchone()
            if not row:
                raise DomainUnavailable('World not provisioned')
            world = json.loads(row[0])
            if not any(a['AccountId'] == account for a in world['Accounts']):
                raise DomainUnavailable('Game account not provisioned')
            payload = dict(action=action,accountId=account,selectedId=selected_id,world=world,
                           command=command if action=='command' else None,
                           petCommand=command if action.startswith('pet-') else None)
            result = self.invoke(payload)
            if result['status'] != 200:
                c.rollback()
                return result['status'], {'error':'command_rejected'}
            # Existing C# services add receipts before this single commit. Replay is inside the domain.
            c.execute('UPDATE LocalWorld SET world_json=? WHERE singleton=1', (json.dumps(result['world']),))
            c.commit()
            return 200, json.loads(result['bodyJson'])
        except BaseException:
            c.rollback()
            raise
        finally:
            c.close()
