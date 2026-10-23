"""Only verified provider subjects become identities. Never trust client email/account IDs."""
import requests
from google.oauth2 import id_token
from google.auth.transport.requests import Request

class InvalidIdentity(Exception):
    pass

class ProviderUnavailable(Exception):
    pass

class ProviderVerifier:
    def __init__(self, google_client_id='', kakao_app_id=''):
        self.google_client_id, self.kakao_app_id = google_client_id, str(kakao_app_id)

    def verify(self, provider, token):
        if provider == 'google':
            if not self.google_client_id:
                raise ProviderUnavailable('Google not configured')
            try:
                # Force a network timeout even when google-auth fetches signing certificates.
                transport = Request()
                def bounded_request(*args, **kwargs):
                    kwargs['timeout'] = 5
                    return transport(*args, **kwargs)
                claims = id_token.verify_oauth2_token(token, bounded_request, self.google_client_id)
                if claims.get('iss') not in ('accounts.google.com','https://accounts.google.com') or not claims.get('sub'):
                    raise InvalidIdentity()
                return str(claims['sub'])
            except (ValueError, InvalidIdentity) as e:
                raise InvalidIdentity() from e
            except Exception as e:
                raise ProviderUnavailable('Google verification unavailable') from e
        if provider == 'kakao':
            if not self.kakao_app_id:
                raise ProviderUnavailable('Kakao not configured')
            try:
                response = requests.get('https://kapi.kakao.com/v1/user/access_token_info',
                    headers={'Authorization':'Bearer '+token},timeout=5,allow_redirects=False)
                if response.status_code in (400,401,403):
                    raise InvalidIdentity()
                if response.status_code != 200:
                    raise ProviderUnavailable('Kakao verification unavailable')
                info = response.json()
                if (str(info.get('app_id')) != self.kakao_app_id or info.get('expires_in',0) <= 0
                        or not isinstance(info.get('id'),int) or info['id'] <= 0):
                    raise InvalidIdentity()
                return str(info['id'])
            except (InvalidIdentity, ProviderUnavailable):
                raise
            except Exception as e:
                raise ProviderUnavailable('Kakao verification unavailable') from e
        raise InvalidIdentity()
