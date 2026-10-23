# AWS mongster-game.net 로그인/API 뼈대 적용

대상: AWS 15.164.123.45, Ubuntu 사용자 ubuntu, 공개 HTTPS mongster-game.net.
이 문서는 배포 명령과 코드 뼈대다. 서버 접속/EC2 소유권/DNS/인증서 발급은 이 작업에서 실제 확인하지 않았다.
현재 운영 게임 adapter, 신규 게임 계정/카탈로그 provision, Unity native Google/Kakao SDK, 로그인 UI는 추가 연결해야 한다. default API는 인증 기능만 실행하고 미연결 게임 endpoint는 503을 반환한다. 기존 reference_https_server.py를 외부 운영 서버로 바꾸지 않는다.

## 1. EC2 접속, DNS, 방화벽

EC2 콘솔에서 해당 인스턴스의 공인 IP가 15.164.123.45인지 확인하고 가능하면 Elastic IP를 연결한다. 실제 보유한 mongster-game.net의 DNS A(@)를 이 IP로 지정한다. 사용하지 않는 AAAA 레코드가 다른 서버를 가리키지 않게 한다. apex DNS 편집 방식은 DNS 업체에 따라 다르다.
Security Group inbound: SSH/TCP22는 본인 공인IP/32, TCP80/443은 공개; 8080 허용 규칙은 만들지 않는다. IPv6를 운영할 때만 올바른 AAAA와 IPv6 규칙을 추가한다.

PC에서 실제 EC2 키 파일 경로를 사용한다. 개인 키 파일은 업로드하거나 저장소에 넣지 않는다.

```bash
ssh -i /path/to/your-ec2-key.pem ubuntu@15.164.123.45
```

SSH 접속한 서버에서(기본 SSH 포트22 기준):

```bash
sudo apt-get update
sudo apt-get install -y ufw git curl dnsutils python3-venv gnupg
sudo ufw allow OpenSSH
sudo ufw allow 80/tcp
sudo ufw allow 443/tcp
sudo ufw enable
sudo ufw status
```

UFW는 EC2 Security Group과 별도로 적용된다. SSH 규칙을 먼저 넣고 기존 SSH 창은 새 연결을 확인하기 전 닫지 않는다.
Caddy는 공식 stable 패키지를 설치한다(request_body 제한을 지원하는 현재 버전 사용).

```bash
sudo apt-get install -y debian-keyring debian-archive-keyring apt-transport-https
curl -fsSL https://dl.cloudsmith.io/public/caddy/stable/gpg.key -o /tmp/caddy-stable.key
sudo gpg --dearmor --yes -o /usr/share/keyrings/caddy-stable-archive-keyring.gpg /tmp/caddy-stable.key
curl -fsSL https://dl.cloudsmith.io/public/caddy/stable/debian.deb.txt -o /tmp/caddy-stable.list
sudo install -m 644 /tmp/caddy-stable.list /etc/apt/sources.list.d/caddy-stable.list
sudo chmod 644 /usr/share/keyrings/caddy-stable-archive-keyring.gpg
sudo apt-get update
sudo apt-get install -y caddy
```

```bash
dig +short mongster-game.net
sudo tee /etc/caddy/Caddyfile >/dev/null <<'CADDY'
mongster-game.net {
    respond "Game API deployment pending" 503
}
CADDY
sudo caddy validate --config /etc/caddy/Caddyfile
sudo systemctl enable --now caddy
sudo systemctl reload caddy
curl -i https://mongster-game.net
```

예상 결과: 신뢰된 공개 인증서로 HTTPS 연결 후503. 게임 API 성공을 뜻하지 않는다. `curl -k`로 인증서 오류를 무시하지 않는다.

```bash
sudo journalctl -u caddy -n 100 --no-pager
```

## 2. 로그인/API 뼈대 설치 및 실행

처음 설치하는 새 서버 기준. /opt/mongster가 기존 배포 폴더라면 clone으로 덮어쓰지 말고 해당 배포 방식에 맞게 갱신한다. 저장소의 실제 코드 브랜치를 사용한다.

```bash
sudo useradd --system --user-group --home-dir /var/lib/mongster --shell /usr/sbin/nologin mongster
sudo git clone --branch feat/aws-auth-scaffold https://github.com/freeopen0229-maker/mongster.git /opt/mongster
sudo python3 -m venv /opt/mongster/.venv
sudo /opt/mongster/.venv/bin/pip install -r /opt/mongster/NetworkHost/Operations/requirements.txt
sudo install -d -m 700 /etc/mongster
sudo install -m 600 /opt/mongster/Deploy/AWS/api.env.example /etc/mongster/api.env
sudo nano /etc/mongster/api.env
```

환경 파일:

```dotenv
MONGSTER_AUTH_DB=/var/lib/mongster/auth.sqlite
GOOGLE_WEB_CLIENT_ID=실제_서버용_Web_Client_ID.apps.googleusercontent.com
KAKAO_APP_ID=실제_숫자형_카카오앱ID
```

Google은 Android 앱 패키지명이나 클라이언트 secret이 아니라 서버용 WEB Client ID/audience를 지정한다. Google Android OAuth 설정에는 실제 패키지명과 해당 APK 서명의 지문도 등록해야 한다. Kakao는 REST API 키가 아니라 숫자형 앱 ID로 토큰의 발급 앱을 검증한다. Android 플랫폼에 패키지명/키해시를 등록한다. 현재 패키지명은 com.fusiontamagotchi.neulgui.dev이며 release 패키지/서명을 바꾸면 콘솔도 갱신해야 한다.
네이티브 SDK로 로그인해 Google ID token/Kakao access token을 받아 서버에 전송하는 예제다. 서버가 브라우저 OAuth callback을 처리하는 방식은 이 뼈대에 포함되지 않는다. 그 방식을 선택하면 별도의 callback, state/PKCE(지원되는 흐름), 정확한 redirect URI 등록을 추가해야 한다. Client secret/Admin key는 Unity 앱에 넣지 않는다.

```bash
sudo install -m 644 /opt/mongster/Deploy/AWS/mongster-api.service /etc/systemd/system/mongster-api.service
sudo systemctl daemon-reload
sudo systemctl enable --now mongster-api
sudo systemctl status mongster-api --no-pager
curl -i http://127.0.0.1:8080/healthz
```

healthz status ok는 로그인 API 프로세스의 생존 확인이다. gameAdapterConfigured=false는 의도된 미연결 상태이며 온라인 플레이 준비 완료가 아니다.

이메일 로그인은 운영자가 이메일 소유 확인 후 초기 계정을 생성하는 방식이다. 공개 회원가입/메일 인증/비밀번호 재설정 기능은 미구현이다. 비밀번호는 터미널 숨김 프롬프트로만 입력한다.

```bash
cd /opt/mongster
sudo -u mongster /opt/mongster/.venv/bin/python -m NetworkHost.Operations.provision_email \
  --db /var/lib/mongster/auth.sqlite --email 실제확인된이메일
```

AuthStore는 PBKDF2-HMAC-SHA256 600000회+무작위 salt로 비밀번호를 저장한다. 게임 세션은 무작위 256bit 토큰, DB에는 SHA256 hash만 저장하고 1시간 후 만료한다. 로그아웃 API는 현재 토큰을 폐기한다. 자동 refresh/session 복구는 미구현이며 만료 시 재로그인한다. 로그인 횟수는 SQLite 기반 IP/이메일 제한을 적용한다. 동일 이메일의 소셜/일반 계정은 자동 병합하지 않는다. 별도 계정 연결 기능을 만들 때 두 계정의 소유권을 모두 검증해야 한다.

## 3. 게임 adapter 접합

api.create_app(store, verifier, game=...)에 game gateway를 전달한다. 기본 create_default_app은 game을 전달하지 않으므로 게임 경로가503인 것이 정상이다. 검증용 카탈로그/계정을 운영으로 올리지 않기 위한 명시적 경계다.

game_gateway.SqliteWorldGateway는 기존 C# DomainWorker invoke 호출을 연결하는 참고 adapter다:

```python
# 선택한 운영 서비스 factory 안에서 사용. worker 생성/종료/timeout과 운영 카탈로그는 별도 구현.
from NetworkHost.Operations.api import create_app
from NetworkHost.Operations.game_gateway import SqliteWorldGateway

game = SqliteWorldGateway('/var/lib/mongster/game.sqlite', reviewed_worker.invoke)
app = create_app(auth_store, provider_verifier, game=game)
```

서비스를 이 factory로 바꾸기 전 다음을 구현해야 한다:

- 인증 계정 ID와 같은 ID의 게임 AccountState 생성. 새 가입/재시도 시 이중 자산 지급 금지. 실제 신규 지급 정책을 서버가 결정.
- 운영 카탈로그/확률/종 데이터, world 초기화/백업. 기존 DomainWorker.ReferencePolicy와 Fresh의 테스트 계정은 사용하지 않는다.
- 소유 펫 목록 및 선택 상태. 예제 selected_pet은 저장된 EconomyAsset의 OwnerId/Kind를 검사해 첫 소유 펫을 반환하고 없으면 빈 문자열.
- worker invoke의 시간 제한, 프로세스 수명/실패 처리. 현재 reference DomainWorker wrapper는 그대로 상용 운영으로 승인된 프로세스 관리자가 아니다.
- 인증 계정 생성과 게임 계정 생성의 crash-safe provision/reconcile. 로그인만 성공하고 게임 상태가 없을 때는 명시적 준비중 응답.

HTTP routes는 request body의 accountId를 허용하지 않고, 세션에서 결정한 계정으로 gateway를 호출한다. 펫 POST는 PetId,RequestId,ExpectedRevision,Action만 허용한다. 거래/경제 POST DTO는 기존 DTO와 동일하다. token/client request 로그는 남기지 않는다.

동일 RequestId 재시도 처리와 Revision 검사는 기존 C# ClientEconomyFacade/ServerPetService를 사용한다. SqliteWorldGateway는 BEGIN IMMEDIATE 아래에서 world(펫/지갑/receipt 포함)를 읽고 C# 도메인을 실행한 뒤 한 번에 커밋한다. 거절/예외는 rollback한다. receipt를 별도 DB에 나중에 기록하지 않는다. DB 확장/정규화 시에도 동일 트랜잭션과 account+RequestId 유일성은 유지해야 한다. 이 SQLite whole-world adapter는 고부하/다중 서버 production 설계 완료를 의미하지 않는다.

## 4. 공개 프록시 전환

로그인 테스트를 외부에서 수행하려면 준비된 scaffold를 프록시할 수 있다. 게임 adapter가 없는 동안 게임 API503은 계속 유지된다.

```bash
sudo install -m 644 /opt/mongster/Deploy/AWS/Caddyfile.api /etc/caddy/Caddyfile
sudo caddy validate --config /etc/caddy/Caddyfile
sudo systemctl reload caddy
curl -i https://mongster-game.net/healthz
curl -i https://mongster-game.net/v2/client/snapshot
```

healthz는200, 인증 없는 snapshot은401이어야 한다. 익명401은 실제 로그인/게임 성공의 증거가 아니다. 정상 게임 토큰+provision된 계정 상태조회까지 성공해야 한다. 502는8080 프로세스/프록시 연결을 확인한다. 503은 game adapter/provider 설정/provision 누락인지 서버 로그로 확인한다.

## 5. Unity 연결

Assets/Scripts/MainGame/HttpGameLoginProvider.cs는 실제 HTTP 전송 예제이며 SocialTokenSource.cs는 네이티브 SDK 접합 인터페이스다. Unity에서 MainGame 씬을 열고:

1. 활성 GameObject에 HttpGameLoginProvider를 추가한다.
2. MainGameRoot.loginProvider 필드에 그 컴포넌트를 연결한다.
3. 실제 이메일/비밀번호 InputField를 provider.emailInput/passwordInput에 연결하고 비밀번호 입력 필드 ContentType을 Password로 지정한다.
4. 이메일/구글/카카오 버튼은 UseEmail/UseGoogle/UseKakao로 선택하고, 로그인 버튼에서 기존 session.LoginAsync를 호출한다.
5. Google/Kakao native SDK를 구현한 SocialTokenSource를 provider.socialTokenSource에 연결한다. 아직 SDK 브리지나 로그인 선택 UI는 생성하지 않았다.
6. Resources/NetworkConfig.json apiBaseUrl을 https://mongster-game.net 으로, environment를 staging으로 지정한 뒤 씬과 설정을 저장한다. DNS/HTTPS가 실제 검증되기 전에는 해당 주소로 설정했다고 연결 완료로 판단하지 않는다.

HttpGameLoginProvider.LoginAsync -> 서버 /v2/auth/... -> GameLoginResult -> 기존 GameSessionCoordinator.CompleteLoginAsync -> HttpEconomyGateway.ConfigureSession -> 경제/펫 snapshot 순서다. 외부 SDK 토큰을 게임 API Bearer로 직접 쓰지 않는다. 서버가 발급한 게임 세션을 사용한다. 비밀번호/토큰을 Inspector, PlayerPrefs, JSON, 로그에 저장하지 않는다.

소셜 증명원 구현 시 Google은 서버 WEB audience용 ID token, Kakao는 로그인한 사용자의 access token을 반환한다. JNI/SDK 로그인 취소와 CancellationToken을 함께 처리한다. 서버의 소셜 토큰 검증은 실제 외부 토큰을 얻는 UI/SDK 기능을 대신하지 않는다.

## 6. 검증 및 범위

```bash
python3 -m venv .venv
.venv/bin/pip install -r NetworkHost/Operations/requirements.txt httpx
.venv/bin/python -m unittest NetworkHost.Operations.test_operations -v
```

테스트는 실제 SQLite/ASGI HTTP 계층, 암호 해시, 세션 만료/폐기/재시작, 계정 분리/동시성, 요청 소유자 결정, 입력 정보 비노출, adapter commit/rollback을 확인한다. Google/Kakao 네트워크 검증은 mock이며 실제 OAuth/Android SDK/EC2/인증서 테스트가 아니다. 기존 C# 도메인 RequestId/보상 테스트와 별개다.

상용 공개 전 추가 범위: 공개 이메일 회원가입/소유확인/복구, 계정 삭제/소셜 연결해제 통지, 실제 소셜 SDK/UI, 게임 provision/카탈로그, 배포용 dependency lock/security review, 백업 복구/모니터링/부하 검사, release 서명 및 기기 검증. 이 코드는 요청된 로그인/API 뼈대이며 완성된 상용 서비스라는 의미는 아니다.
