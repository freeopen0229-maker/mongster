"""Auth HTTP/ASGI and atomic adapter tests. Provider network calls are mocked, not live OAuth."""
import json
from pathlib import Path
import sqlite3
import tempfile
import unittest
from unittest.mock import Mock, patch
from concurrent.futures import ThreadPoolExecutor
from fastapi.testclient import TestClient
from .api import create_app
from .auth_store import AuthStore
from .providers import ProviderVerifier, InvalidIdentity
from .game_gateway import SqliteWorldGateway

class OperationsTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.path = Path(self.temp.name)/'auth.sqlite'
        self.now = 1000
        self.store = AuthStore(self.path,lambda:self.now)
        self.owner = self.store.provision_email('owner@example.net','very-long-test-password-123')
        self.verifier = Mock(spec=ProviderVerifier)
        self.verifier.verify.return_value = 'verified-provider-subject'
        self.client = TestClient(create_app(self.store,self.verifier))
        self.addCleanup(self.client.close)

    def login(self):
        return self.client.post('/v2/auth/email/login',json={'email':'owner@example.net','password':'very-long-test-password-123'})

    def test_password_hash_not_plaintext(self):
        with self.store.connection() as c:
            stored = c.execute('SELECT password_hash FROM EmailIdentity').fetchone()[0]
        self.assertNotIn('very-long-test-password',stored)
        self.assertIsNone(self.store.email_account('owner@example.net','wrong-password-1234'))

    def test_login_session_survives_restart_and_expires(self):
        result = self.login()
        self.assertEqual(result.status_code,200)
        token = result.json()['accessToken']
        reopened = AuthStore(self.path,lambda:self.now)
        self.assertEqual(reopened.account_for(token),self.owner)
        with reopened.connection() as c:
            self.assertNotEqual(c.execute('SELECT token_hash FROM GameSession').fetchone()[0],token)
        self.now += 3601
        self.assertIsNone(reopened.account_for(token))

    def test_logout_revokes_server_session(self):
        token = self.login().json()['accessToken']
        headers = {'Authorization':'Bearer '+token}
        self.assertEqual(self.client.post('/v2/auth/logout',headers=headers).status_code,200)
        self.assertEqual(self.client.get('/v2/client/snapshot',headers=headers).status_code,401)

    def test_missing_game_adapter_does_not_fake_success(self):
        headers = {'Authorization':'Bearer '+self.login().json()['accessToken']}
        self.assertEqual(self.client.get('/v2/client/snapshot',headers=headers).status_code,503)
        self.assertEqual(self.client.get('/v2/pets/snapshot?petId=one').status_code,401)

    def test_validation_does_not_echo_credentials(self):
        response = self.client.post('/v2/auth/social/login',json={'provider':'forged','token':'sensitive-provider-value'})
        self.assertEqual(response.status_code,400)
        self.assertNotIn('sensitive-provider-value',response.text)
        self.assertEqual(response.headers['cache-control'],'no-store')

    def test_social_subject_not_email_controls_identity(self):
        a = self.store.social_account('google','same-subject')
        self.assertEqual(a,self.store.social_account('google','same-subject'))
        self.assertNotEqual(a,self.store.social_account('kakao','same-subject'))
        self.assertNotEqual(a,self.owner)

    def test_concurrent_social_login_one_identity(self):
        with ThreadPoolExecutor(max_workers=4) as pool:
            accounts = list(pool.map(lambda _:self.store.social_account('kakao','12345'),range(4)))
        self.assertEqual(len(set(accounts)),1)

    def test_rate_limit_persists(self):
        for _ in range(10):
            self.assertTrue(self.store.allow_attempt('one-account',10))
        self.assertFalse(AuthStore(self.path,lambda:self.now).allow_attempt('one-account',10))

    def test_bad_provider_token_no_account_created(self):
        self.verifier.verify.side_effect = InvalidIdentity()
        r = self.client.post('/v2/auth/social/login',json={'provider':'google','token':'invalid'})
        self.assertEqual(r.status_code,401)
        with self.store.connection() as c:
            self.assertEqual(c.execute('SELECT COUNT(*) FROM SocialIdentity').fetchone()[0],0)

    def test_google_audience_is_explicit(self):
        with patch('NetworkHost.Operations.providers.id_token.verify_oauth2_token',return_value={'sub':'g1','iss':'accounts.google.com'}) as verify:
            self.assertEqual(ProviderVerifier('expected-web-client').verify('google','id-token'),'g1')
            self.assertEqual(verify.call_args.args[2],'expected-web-client')

    def test_kakao_checks_application_and_expiry(self):
        response = Mock(status_code=200)
        with patch('NetworkHost.Operations.providers.requests.get',return_value=response):
            response.json.return_value = {'id':123,'app_id':999,'expires_in':60}
            with self.assertRaises(InvalidIdentity):
                ProviderVerifier(kakao_app_id='100').verify('kakao','token')
            response.json.return_value = {'id':123,'app_id':100,'expires_in':0}
            with self.assertRaises(InvalidIdentity):
                ProviderVerifier(kakao_app_id='100').verify('kakao','token')
            response.json.return_value = {'id':123,'app_id':100,'expires_in':60}
            self.assertEqual(ProviderVerifier(kakao_app_id='100').verify('kakao','token'),'123')

    def test_domain_identity_comes_from_session_not_body(self):
        game = Mock()
        game.selected_pet.return_value = 'owned-pet'
        game.handle.return_value = (200,{'ok':True})
        with TestClient(create_app(self.store,self.verifier,game)) as client:
            r = client.post('/v2/auth/email/login',json={'email':'owner@example.net','password':'very-long-test-password-123'})
            self.assertEqual(r.json()['selectedPetId'],'owned-pet')
            headers = {'Authorization':'Bearer '+r.json()['accessToken']}
            command = {'PetId':'owned-pet','RequestId':'same-key','ExpectedRevision':0,'Action':1}
            self.assertEqual(client.post('/v2/pets/care',headers=headers,json=command).status_code,200)
            self.assertEqual(game.handle.call_args.args[0],self.owner)
            self.assertEqual(game.handle.call_args.args[3]['RequestId'],'same-key')
            command['AccountCoins'] = 99999
            self.assertEqual(client.post('/v2/pets/care',headers=headers,json=command).status_code,400)

    def test_domain_snapshot_and_receipts_commit_or_rollback(self):
        db = Path(self.temp.name)/'world.sqlite'
        world = {'Accounts':[{'AccountId':self.owner}],'Assets':[],'Receipts':[]}
        with sqlite3.connect(db) as c:
            c.execute('CREATE TABLE LocalWorld(singleton INTEGER PRIMARY KEY,world_json TEXT)')
            c.execute('INSERT INTO LocalWorld VALUES(1,?)',(json.dumps(world),))
        def invoke(payload):
            payload['world']['Receipts'].append({'RequestId':'once'})
            return {'status':200,'world':payload['world'],'bodyJson':'{"ok":true}'}
        gateway = SqliteWorldGateway(db,invoke)
        self.assertEqual(gateway.handle(self.owner,'command',command={'requestId':'once'})[0],200)
        gateway.invoke = lambda payload: {'status':409}
        self.assertEqual(gateway.handle(self.owner,'command',command={})[0],409)
        with sqlite3.connect(db) as c:
            persisted = json.loads(c.execute('SELECT world_json FROM LocalWorld').fetchone()[0])
        self.assertEqual(persisted['Receipts'],[{'RequestId':'once'}])

if __name__ == '__main__':
    unittest.main()
