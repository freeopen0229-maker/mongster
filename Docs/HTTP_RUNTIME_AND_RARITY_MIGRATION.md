# 실제 HTTPS 연동 검사 / 등급 변환 실행 결과

2026-10-23. 이번 지시서의 기준 문서다. 이전 문서의 'HTTP 호스트 없음',
'등급 매핑 미정'은 아래 범위에서 갱신됐다.

## 결과를 구분해서 읽기

| 항목 | 실제 상태 |
|---|---|
| 실제 네트워크 연결 | 로컬 TLS 소켓으로 C# 도메인 호출: 18개 검사 PASS |
| 인증서 | 테스트 CA를 신뢰 목록에 지정해 hostname/체인 검증을 켠 상태로 검사 |
| 지갑/영수증 지속성 | SQLite에 commit, HTTPS 호스트/C# 프로세스 재시작 후 재확인 PASS |
| Unity 클라이언트 | UnityWebRequest gateway, isProcessing, 요청 ID 재시도, 알/펫 응답 연결 소스 작성 |
| 부화 UI | 알 지급/부화 분리, 알 흔들림·균열·소멸/사운드/결과 팝업 및 Scene 생성 코드 작성 |
| Unity 실제 재생 | NOT RUN. 현재 Unity 실행 파일/에디터 환경 없음. 화면·음질·클립은 미검증 |
| 운영 DB 이관 | NOT RUN. 운영 DB 파일/서버/종류/접속 정보가 제공되지 않음 |
| 지정 등급 변환 실행 | 직접 생성한 참고 SQLite DB에서 0~5 매핑, 백업/대기 사본 생성 실행 |
| 운영 공개 서버 | 배포하지 않음. 로컬 HTTPS 테스트 서버는 검사 후 종료 |

실제 Unity 실행 영상이나 운영 DB 이관이 완료됐다는 의미가 아니다.
로컬 참고 계정/자산은 테스트 픽스처이며 사용자 게임 데이터가 아니다.

## 1. 실제 요청 경로

Unity ClientGameManager → HttpEconomyGateway → HTTPS reference host → 인증 세션 조회 →
SQLite BEGIN IMMEDIATE → 기존 C# EconomyService/ClientEconomyFacade → snapshot/영수증 저장 →
commit → JSON 응답 → 클라이언트 UI.

NetworkHost/reference_https_server.py는 Python 표준 라이브러리 기반 로컬 참고 호스트다.
경제 규칙을 Python으로 다시 작성하지 않았고 실제 C# DomainWorker를 호출한다.
DomainWorker는 매 요청마다 저장 snapshot을 복원하고 결과 snapshot을 반환한다.
SQLite에는 지갑·자산·영수증·한정량·outbox·아이템을 포함한 전체 로컬 world를 저장한다.
이는 운영용 정규화 DB/고성능 서버 아키텍처를 대신하지 않는다.

AuthSession에는 Bearer token 해시와 계정/만료 시각만 저장한다.
계정 ID를 JSON body에서 신뢰하지 않는다. fixture 초기화는 CLI의 명시적 초기화 기능뿐이고
HTTP를 통해 무료 재화를 생성하는 경로는 없다.
실제 회원가입/로그인/스토어 결제 어댑터는 여전히 없다.

HTTP 계약:

| 호출 | 동작 |
|---|---|
| GET /v2/client/snapshot?petId=... | 소유 펫 또는 알 + 지갑 조회 |
| POST /v2/client/command kind=0 | 영롱한 알 구매. tenPack=false/true |
| kind=1 | 스킬 리롤. petId/expectedRevision/slotIndex |
| kind=2 | 부화 시작. petId에는 알 자산 ID 사용 |
| kind=3 | 부화 스크롤 사용 |
| kind=4 | 준비된 알 실제 부화 |

각 POST는 requestId를 포함한다. 가격/등급/스킬 결과/현재 지갑 값은 받지 않는다.
서버 command commit 후 응답만 끊겨도 동일 requestId로 보석/아이템을 추가 소비하지 않는다.
동시 요청과 프로세스 재시작 후에도 SQLite의 영수증이 중복 처리를 막는다.

## 2. 실행한 HTTPS 검사

NetworkHost/test_https_integration.py를 실제로 실행했다.
TLS 인증서와 임시 키는 테스트 임시 디렉터리에서만 생성하고 삭제했다.
개인키/토큰은 프로젝트 ZIP에 포함하지 않았다.
검사는 Python cryptography가 필요하며 참고 호스트 자체는 표준 라이브러리만 사용한다.

```bash
python3 NetworkHost/test_https_integration.py /absolute/mono /absolute/mcs.exe
```

18개 검사: 미인증 거절, 최신 지갑, 실제 구매, 동일 키 재전송, 키/상품 불일치 거절,
리롤/갱신, 다른 주인 펫 거절, 8시간 부화 시작, 조기 부화 거절, 스크롤 사용,
실제 부화 결과, 부화 응답 재확인, commit 후 소켓 timeout, 같은 ID 복구,
동시 동일 구매 결과 11개 ID 일치, 1회 차감, 호스트/worker 재시작, SQLite 영속성.

결과: Validation/NetworkAndMigration/https_results.json.
UnityWebRequest 자체를 실행해서 검사한 것은 아니다.

## 3. 참고 서버 실행 방법

이미 실행한 테스트는 임시 포트/임시 DB를 사용하고 종료했다.
아래는 개발자가 자기 컴퓨터에서 참고 서버를 계속 띄우는 방법이다.
외부 운영 배포 주소는 만들어져 있지 않다.

DomainWorker는 기존 Mono로 컴파일/실행을 검사했다. .NET SDK가 있는 환경용
NetworkHost/DomainWorker.csproj도 제공하지만 이번 환경에는 dotnet이 없어 SDK 빌드는 실행하지 않았다.

```bash
dotnet build NetworkHost/DomainWorker.csproj -c Release
```

--mono 인자는 runtime 실행 파일이다. 위 .NET 빌드를 사용하면 dotnet 경로와 출력 dll을 지정할 수 있다.
Mono 환경은 실제 검증한 DomainWorker.exe와 System.Runtime.Serialization/System.ServiceModel.Internals가 필요하다.

참고 DB를 새로 만들 때만 --init-reference-token-file을 지정한다.
파일 내용은 local-a/local-b 계정에 대해 각각 충분히 긴 무작위 토큰을 제공하는 JSON이다.
이 파일과 개인키는 소스 관리/공유 ZIP에 넣지 않는다. 기존 DB를 다시 초기화하려 하면 거절한다.

```bash
python3 NetworkHost/reference_https_server.py --db local-reference.sqlite --mono /path/to/runtime --worker /path/to/DomainWorker.dll --cert /path/to/cert.pem --key /path/to/key.pem --port 8443 --init-reference-token-file /path/to/private-tokens.json
```

재시작 시에는 --init-reference-token-file을 빼고 같은 DB를 사용한다.
참고 서버는 127.0.0.1에만 바인딩한다. Unity를 같은 컴퓨터에서 실행할 때 테스트할 수 있다.
Unity의 운영체제 신뢰 저장소에 맞는 유효 인증서가 필요하며 인증서 검증을 끄는 코드는 넣지 않았다.
휴대폰에서 원격으로 접속 가능한 공개 주소/인증서/운영 호스트는 별도 준비가 필요하다.

## 4. Unity 재생 장면 생성

메뉴: **Mongle → Economy → Create HTTP Hatch Lab**

Assets/Editor/EconomyNetworkLabSetup.cs는 다음을 생성하는 소스다.
이번 환경에서는 메뉴를 실행하지 못했으므로 실제 .unity/.anim/.controller 파일 생성도 아직 하지 않았다.

- 새 씬과 Canvas, 재화 바, 주소/계정/토큰/자산 입력, 구매·부화·리롤·재시도 버튼.
- 알 UI 아이콘용 Texture/Sprite. 상용 펫 원화가 아닌 테스트용 밀봉 알 그래픽.
- Closed/Crack/Reveal AnimationClip과 AnimatorController.
- 알의 흔들림/균열 표시/소멸, 부화 결과 Text/Panel, AudioSource.
- 발급받은 첫 알 자동 선택. 초기 자산은 local-neulgui.

로그인 세션/실제 HTTPS 연결 후 다음 순서로 검사한다.

1. 영롱한 알 구매 → 밀봉 알 지급 안내. 보석은 서버 응답 후 갱신.
2. 부화 시작 → 서버 기본 8시간 타이머. 이 상태에서 부화 확인하면 조기 부화 거절.
3. 자연 부화 시각을 기다리거나 부화 스크롤 사용.
4. 부화 확인 → 실제 Hatch 성공 응답 → 균열 애니메이션/짧은 chime/펫 결과 팝업.
5. 결과 펫 선택 상태에서 리롤 → 서버 스킬과 다음 비용 갱신.

구매 성공만으로 알을 깨면 8~12시간 부화 시스템을 우회하므로 그렇게 연결하지 않았다.
스크롤은 준비 시각을 현재로 바꾸고 실제 Hatch 명령이 결과를 확정한다.
EggHatchPresenter는 같은 펫 부화 이벤트를 이 컴포넌트 생명주기 안에서 중복 재생하지 않는다.
진짜 늘귀 아기/성체의 완성된 리그/클립은 여전히 없다. 이 테스트는 알 UI 연출용이다.
사운드는 기본 합성 chime이며 실제 아트/음원 품질 검증 또는 상용 VFX 완성이라고 보지 않는다.

기존 프로젝트의 Active Input Handling에 StandaloneInputModule이 지원돼야 한다.
Input System only 구성이라면 씬의 입력 모듈을 해당 프로젝트의 UI 입력 모듈로 교체해야 한다.
UGUI 및 Editor API 컴파일/Canvas 레이아웃/실제 재생/휴대폰 버튼 조작은 검증하지 못했다.

## 5. 승인된 6→5등급 매핑

| 이전 Rarity | 새 문자열 |
|---:|---|
| 0 | Common |
| 1 | Rare |
| 2 | Rare |
| 3 | Epic |
| 4 | Legendary |
| 5 | Divine |

Tools/migrate_pet_rarity.py는 SQLite PetTable/Rarity용이다.
원본 DB 종류가 밝혀지지 않았으므로 PostgreSQL/MySQL용 쿼리를 실행했다고 주장하지 않는다.
숫자 열에 문자열을 저장할 수 없는 제약이 있으면 전체 transaction을 rollback하고 멈춘다.
강제로 열 제약이나 테이블을 삭제하지 않는다.

기본은 읽기 전용 계획이다.

```bash
python3 Tools/migrate_pet_rarity.py --db /path/to/actual.sqlite
```

--apply를 지정하면 해당 파일에 다음을 같은 transaction으로 수행한다.

```bash
python3 Tools/migrate_pet_rarity.py --db /path/to/actual.sqlite --apply
```

- BEGIN IMMEDIATE로 읽기/변환 사이의 동시 변경을 막는다.
- PetTable_Backup_2026에 전체 행/열 값을 보존한다. 기존 사본 덮어쓰기 금지.
- 원본 CREATE TABLE 구문을 PetSchema_Backup_2026에 저장한다.
- 알 수 없는 등급은 PetTable_MigrationPending_2026에 보존한다.
- 0~5만 지정한 문자열로 변환한다. 알 수 없는 값을 Common으로 만들지 않는다.
- 비등급 필드/행 수/결과 분포/백업 일치를 검증한 뒤 commit한다.
- 완료 marker가 있으면 재실행 시 이미 적용됨으로 반환한다.
- 완료 후 새 legacy 값이 유입됐으면 기존 사본을 덮어쓰지 않고 중단한다.

사용자 SQL의 WHERE Rarity BETWEEN 0 AND 5 바깥 값은 원래도 대상이 아니다.
이번 도구도 알 수 없는 데이터를 강등/삭제하지 않는다.
이 변환은 PetTable.Rarity 열의 변환이다. 별도 JSON payload의 Grade, 신규 스킬,
비주얼 리그/아이템 매핑까지 자동 이관하는 도구는 아니다.

## 6. 실제로 실행해 남긴 참고 DB

운영 DB가 없으므로 직접 만든 가상 데이터로 실행했다.
Validation/NetworkAndMigration/ReferencePetMigration.sqlite에는 다음이 남아 있다.

- 원본 PetTable: 가상 데이터 7행 중 0~5 여섯 행 변환 완료.
- PetTable_Backup_2026: 변환 전 여섯 숫자 등급과 모든 payload 보존.
- PetTable_MigrationPending_2026: 등급 99인 가상 데이터 한 행 보존.
- 원본 CREATE TABLE 및 migration marker.

실행 보고서: reference_migration_applied.json.
테스트: Tools/test_pet_rarity_migration.py의 백업/변환/대기/중복 실행/제약 실패 rollback
11개 검사 PASS. 다른 임시 DB staging 15개 검사도 그대로 PASS.
운영 데이터는 한 건도 읽거나 변환하지 않았다.

## 7. 남은 실제 작업

- Unity 에디터 환경에서 생성 메뉴 실행 및 API/애니메이션/UI 재생 검증.
- 운영 서버 배포·실제 회원 인증·세션 발급·스토어 검증·공개 TLS 주소 연결.
- 운영 DB의 실제 종류/테이블/접속 정보 확인 후 그 DB에 맞는 이관 실행.
- V1 JSON/스킬/개체값/성격/비주얼의 완전한 V2 이관과 운영용 DB adapter.

현재 코드/실행 증거로 확인되지 않은 항목을 완료로 표시하지 않았다.
