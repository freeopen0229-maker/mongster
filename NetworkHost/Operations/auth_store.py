"""Persistent auth core. No fixture users, default passwords or automatic email linking."""
import hashlib
import hmac
import secrets
import sqlite3
import time
import uuid
from contextlib import contextmanager

ITERATIONS = 600_000

def normalize_email(email):
    email = email.strip().lower()
    if len(email) > 254 or email.count('@') != 1 or any(c.isspace() for c in email):
        raise ValueError('Invalid email')
    local, domain = email.split('@')
    if not local or '.' not in domain or domain.startswith('.') or domain.endswith('.'):
        raise ValueError('Invalid email')
    return email

def password_hash(password, salt=None):
    if not 12 <= len(password) <= 128:
        raise ValueError('Password must have 12–128 characters')
    salt = salt or secrets.token_hex(16)
    digest = hashlib.pbkdf2_hmac('sha256', password.encode(), bytes.fromhex(salt), ITERATIONS).hex()
    return salt + ':' + digest

def password_matches(password, encoded):
    salt, _ = encoded.split(':')
    return hmac.compare_digest(password_hash(password, salt), encoded)

class AuthStore:
    def __init__(self, path, clock=time.time):
        self.path, self.clock = str(path), clock
        self.dummy_hash = password_hash(secrets.token_urlsafe(24))
        with self.connection() as c:
            c.executescript('''
                PRAGMA journal_mode=WAL;
                CREATE TABLE IF NOT EXISTS GameAccount(id TEXT PRIMARY KEY);
                CREATE TABLE IF NOT EXISTS EmailIdentity(
                    email TEXT PRIMARY KEY, account_id TEXT NOT NULL REFERENCES GameAccount(id),
                    password_hash TEXT NOT NULL);
                CREATE TABLE IF NOT EXISTS SocialIdentity(
                    provider TEXT NOT NULL, subject TEXT NOT NULL,
                    account_id TEXT NOT NULL REFERENCES GameAccount(id), PRIMARY KEY(provider,subject));
                CREATE TABLE IF NOT EXISTS GameSession(
                    token_hash TEXT PRIMARY KEY, account_id TEXT NOT NULL REFERENCES GameAccount(id), expires INTEGER NOT NULL);
                CREATE TABLE IF NOT EXISTS AuthRate(key TEXT PRIMARY KEY, window INTEGER NOT NULL, count INTEGER NOT NULL);
            ''')

    @contextmanager
    def connection(self):
        c = sqlite3.connect(self.path, timeout=10)
        c.execute('PRAGMA foreign_keys=ON')
        try:
            yield c
            c.commit()
        except BaseException:
            c.rollback()
            raise
        finally:
            c.close()

    def allow_attempt(self, key, limit=20):
        # Persistent per-minute limit; enforce additional network-wide limits at the edge.
        now = int(self.clock()) // 60
        key = hashlib.sha256(key.encode()).hexdigest()
        with self.connection() as c:
            c.execute('BEGIN IMMEDIATE')
            c.execute('DELETE FROM AuthRate WHERE window < ?', (now-2,))
            row = c.execute('SELECT window,count FROM AuthRate WHERE key=?', (key,)).fetchone()
            count = row[1]+1 if row and row[0] == now else 1
            c.execute('INSERT OR REPLACE INTO AuthRate VALUES(?,?,?)', (key,now,count))
        return count <= limit

    def provision_email(self, email, password):
        """Operator-only bootstrap after email ownership verification, not a public signup endpoint."""
        email, encoded = normalize_email(email), password_hash(password)
        account = uuid.uuid4().hex
        with self.connection() as c:
            c.execute('BEGIN IMMEDIATE')
            c.execute('INSERT INTO GameAccount VALUES(?)', (account,))
            c.execute('INSERT INTO EmailIdentity VALUES(?,?,?)', (email,account,encoded))
        return account

    def email_account(self, email, password):
        email = normalize_email(email)
        with self.connection() as c:
            row = c.execute('SELECT account_id,password_hash FROM EmailIdentity WHERE email=?', (email,)).fetchone()
        valid = password_matches(password, row[1] if row else self.dummy_hash)
        return row[0] if row and valid else None

    def social_account(self, provider, subject):
        if provider not in ('google','kakao') or not subject or len(subject) > 255:
            raise ValueError('Invalid verified identity')
        with self.connection() as c:
            c.execute('BEGIN IMMEDIATE')
            row = c.execute('SELECT account_id FROM SocialIdentity WHERE provider=? AND subject=?', (provider,subject)).fetchone()
            if row:
                return row[0]
            account = uuid.uuid4().hex
            c.execute('INSERT INTO GameAccount VALUES(?)', (account,))
            c.execute('INSERT INTO SocialIdentity VALUES(?,?,?)', (provider,subject,account))
            return account

    def issue(self, account):
        token = secrets.token_urlsafe(32)
        with self.connection() as c:
            c.execute('DELETE FROM GameSession WHERE expires<=?', (int(self.clock()),))
            c.execute('INSERT INTO GameSession VALUES(?,?,?)',
                      (hashlib.sha256(token.encode()).hexdigest(), account, int(self.clock())+3600))
        return token

    def account_for(self, token):
        if not token or len(token) > 256:
            return None
        with self.connection() as c:
            row = c.execute('SELECT account_id FROM GameSession WHERE token_hash=? AND expires>?',
                            (hashlib.sha256(token.encode()).hexdigest(),int(self.clock()))).fetchone()
        return row[0] if row else None

    def revoke(self, token):
        with self.connection() as c:
            c.execute('DELETE FROM GameSession WHERE token_hash=?', (hashlib.sha256(token.encode()).hexdigest(),))
