> Updated execution status and confirmed rarity mapping: [HTTP_RUNTIME_AND_RARITY_MIGRATION.md](HTTP_RUNTIME_AND_RARITY_MIGRATION.md) supersedes earlier statements about absent HTTP host and unknown grade mapping.

# 클라이언트 UI 연결 및 DB 이관 준비

2026-10-23. Task 1의 ClientGameManager를 Assets/Scripts/ClientGameManager.cs에 반영했다.

## 실제 완료 범위

- 작성: 클라이언트 UI 컴포넌트, 공용 DTO/비동기 세션, HTTPS 전송 어댑터, 서버 도메인 연결 Facade.
- 실행 검증: 실제 EconomyService와 연결한 순수 C# 세션 23개 검사, 기존 경제 55개 검사,
  기존 거래/귀속 검사, Editor Mock과 비 Editor 경계 검사.
- DB 준비: SQLite 참고용 읽기 전용 사전 검사 및 사본 staging 도구. 임시 SQLite에서 15개 검사.
- 미검증: Unity API 컴파일/실제 Canvas 표시/버튼 클릭/Play Mode/실기기/실제 HTTP.
- 미완료: HTTP 호스트·인증·운영 DB adapter·실제 결제·실시간 방송 스트림·실제 운영 데이터 이관.

운영 DB 파일, 접속 정보, DB 종류, 현재 테이블 정의는 제공되지 않았다. SQLite 도구는
참고 구현이다. 현재 사용자의 운영 DB를 SQLite라고 판단한 것이 아니며 실제 DB에 접속하지 않았다.

## 제공 예제에서 바꾼 부분

| 예제 동작 | 반영한 동작 |
|---|---|
| wallet.gem -= cost | 서버 구매 성공 후 응답 지갑 반영 |
| wallet.jeon -= currentRerollCost | 클라이언트 가격을 신뢰하지 않음. 서버 가격/스킬 결과 반영 |
| 알 구매 즉시 부화 연출 | 구매 성공 시 1개/11개 알 지급 이벤트. 실제 부화는 별도 |
| 요청 완료 Debug.Log만 출력 | 실제 도메인 Facade 또는 HTTP gateway 호출 |
| 아기 DTO의 isHiddenPet/type 노출 | 잠재 후보 플래그 숨김. 각성 완료 시만 공개 |
| 남은 수량=maxLimit-serialNo | 시리얼만 표시. 오래된 방송으로 잘못된 현재 잔량 표시 방지 |
| 알 수 없는 DB 등급 자동 변환 | 명시 매핑 전 staging 원문 보존/운영 사용 차단 |

이번 지시서의 hiddenType 주석에 따라 한정량을 A=10, B~E=종당 100으로 확정했다.
HiddenQuotaPolicy.CreateFresh()는 신규 월드 전용이다. 기존 발급량을 0으로 초기화하는 데
사용하면 안 된다. 이전 문서의 B~E 미정 표기는 이 결정으로 대체한다.

## 파일 위치

| 파일 | 역할 |
|---|---|
| Assets/Scripts/ClientGameManager.cs | Text/버튼/알 구매/리롤/시리얼/방송 팝업 연결 |
| Assets/Scripts/EconomyGatewayBehaviour.cs | Inspector 연결용 gateway 추상 컴포넌트 |
| Assets/Scripts/HttpEconomyGateway.cs | UnityWebRequest HTTPS GET/POST, 토큰/취소/오류 처리 |
| Assets/Scripts/EconomyV2/ClientContracts.cs | DTO 및 gateway 계약 |
| Assets/Scripts/EconomyV2/ClientEconomySession.cs | 무차감 대기/단일 요청/동일 ID 재시도/응답 검증 |
| ServerSources/ClientEconomyFacade.cs | EconomyService 연결, 지갑/펫 공개 모델 투영, 새 quota 정책 |
| ServerSources/Migrations/001_economy_v2_staging.sql | SQLite 사본 준비 전용 참고 테이블 |
| Tools/prepare_db_migration.py | 명시 열 매핑/잔액/기발급량 검사, 선택적으로 사본 staging |
| Tools/ClientEconomyProbe.cs | 실제 도메인과 클라이언트 세션 통합 검사 |
| Tools/test_db_migration.py | 임시 SQLite 사전 검사/사본 보존 검사 |

DTO는 MonoBehaviour가 아니다. UnityEngine에 의존하지 않고 서버에서도 같은 정의를 사용한다.
ClientGameManager 클래스와 파일명은 일치하며 기존 동일 클래스/DTO를 중복 추가하지 않았다.

## Unity Inspector 연결

1. 활성 GameObject `ClientSystems`에 ClientGameManager와 HttpEconomyGateway를 부착한다.
   ClientSystems를 숨기는 팝업의 자식으로 배치하지 않는다.
2. Canvas에 재화 Text 3개를 만들고 txtJeon/txtGem/txtThread에 연결한다.
   이 코드는 UnityEngine.UI.Text(UGUI)용이다. TextMeshProUGUI는 직접 연결되지 않는다.
3. txtPetSerial, txtStatus, txtGlobalNotice를 연결한다. 전역 알림용 Panel을 만들어
   globalNoticePanel에 연결하고 초기에는 숨긴다. 게임 매니저 자체를 이 필드에 지정하지 않는다.
4. 구매·리롤 버튼을 actionButtons에 등록한다. 별도 '다시 확인' 버튼을 retryButton에 연결한다.
   재화 Text 및 안내 배너의 Raycast Target을 끄면 조작을 가리지 않는다.
5. Button.onClick 연결:

| 버튼 | 함수/고정 인자 |
|---|---|
| 영롱한 알 1개 | BuyGloriousEgg(false) |
| 영롱한 알 10+1 | BuyGloriousEgg(true) |
| 스킬 슬롯 1 | RerollSelectedSlot(0) |
| 스킬 슬롯 2 | RerollSelectedSlot(1) |
| 추가 슬롯 | RerollSelectedSlot(2/3), 해당 등급일 때만 표시 |
| 서버 상태 갱신 | RefreshFromServer() |
| 미확정 요청 재확인 | RetryPending() |

6. 실제 로그인 성공 콜백에서 gateway 세션과 계정을 연결하고 최초 상태를 조회한다.
   아래 URL은 변수이며 배포된 엔드포인트가 프로젝트에 준비돼 있다는 의미가 아니다.

```csharp
// 로그인 완료 콜백 안에서 실행. 변수들은 실제 인증 계층에서 전달한다.
httpGateway.ConfigureSession(apiHttpsBaseUrl, accessToken);
gameManager.Initialize(authenticatedAccountId, httpGateway);
gameManager.selectedPetId = ownedPetId;
await gameManager.RefreshAsync();
```

인증 토큰은 Inspector/PlayerPrefs/로그에 기록하지 않는다. gateway가 비활성화되면 토큰을
지우므로 다시 활성화한 후 로그인 계층에서 재설정해야 한다.
지갑 로드 전에는 '— 전/보석/실타래'로 표시하고 구매 버튼을 막는다.
실제 Unity Scene/Canvas 프리팹을 생성하거나 렌더링해서 검사하지는 못했다.

## 요청/응답 계약

HTTPS 호스트가 아래 라우트를 구현해야 HttpEconomyGateway로 통신할 수 있다.
이번 변경에는 라우트 자체를 배포한 서버가 없다.

| 요청 | 입력 | 응답 |
|---|---|---|
| GET /v2/client/snapshot | 선택 petId query, Bearer 인증 | ClientSnapshotDTO |
| POST /v2/client/command | ClientCommandDTO JSON, Bearer 인증 | ClientCommandReplyDTO |

ClientCommandKind의 JSON 값은 현재 C# 정수 enum이다: BuyLuminousEgg=0, RerollSkill=1.
서버 JSON serializer는 DTO 필드를 lower camel case 그대로 포함해야 한다.
System.Text.Json 사용 시 public field 직렬화를 명시적으로 활성화하거나 DTO 프로퍼티로
동일 계약을 구성해야 한다. 소유자/가격/스킬 결과/보석 지급량을 요청 body에서 받지 않는다.
호스트는 인증된 계정으로 ClientEconomyFacade를 만들고 해당 DTO에 전달한다.

HTTP 400/403/409/422는 서버가 변경 없음/rollback을 확정했을 때만 사용한다.
명령 commit 이후 snapshot 응답이 실패한 경우에는 위 상태로 응답하면 안 된다.
5xx/timeout/취소/잘못된 응답은 처리 여부 미확정으로 유지하며 같은 요청 ID로 확인한다.
401도 세션 복구 후 같은 ID로 재시도할 수 있도록 미확정으로 취급한다.
실제 송수신, 인증, host-side serialization 검증은 아직 수행하지 않았다.

순수 C# 테스트의 ClientEconomyFacade는 서버 클래스로, Unity 플레이어에 넣지 않는다.
HTTP 호스트가 배포되기 전에는 테스트 러너에서만 실제 서비스 연결을 검증했다.

## UI 상태와 중복 소비 방지

- 요청 시작 즉시 actionButtons 잠금. 빠른 중복 클릭도 세션의 단일 요청 검사로 차단.
- 서버 응답 전 지갑을 차감하지 않는다. 실패 시 '되돌려 주는' 낙관적 잔액도 만들지 않는다.
- 응답 유실 후 새 구매를 허용하지 않고 동일 ID의 RetryPending으로 결과를 확인한다.
- pending 명령만 계정별 PlayerPrefs에 저장해 앱 재시작 후 복구한다. 소유권/보상은 서버가 검증한다.
- 클라이언트의 로컬 저장소를 변조해도 서버 잔액을 직접 변경할 수 없다.
- 지갑 revision이 뒤로 가거나 계정/요청 ID/알 개수가 다르면 UI 반영을 거절한다.
- 새 계정 Initialize 시 이전 구독을 해제한다. 이전 요청 완료로 새 계정 UI를 덮어쓰지 않는다.
- AccountState.Coins는 같은 Jeon 지갑이며 setter에서 WalletRevision을 증가시킨다.
  V2 Gem/Thread 변경도 같은 서버 command 안에서 revision을 증가시킨다.
  운영 DB adapter도 모든 잔액 변경에서 이 revision 계약을 유지해야 한다.

ReqRerollSkill(PetDTO,int,long)은 제공된 호출부 호환용이다. 세 번째 가격 인자는 사용하지 않는다.
서버에서 받은 PetDTO.nextRerollCost는 안내/잔액 사전 검사에 사용하고 최종 가격은 서버가 계산한다.
실제 스킬 아이콘/애니메이션은 PetUpdated 이벤트에서 구현해야 한다.
알 구매 연출은 EggsPurchased 이벤트를 구독한다. 이벤트는 미부화 알 ID만 전달한다.
연출 타임라인/가챠 화면 자체를 이번에 제작한 것은 아니다.

## 전역 알림

서버 outbox → 방송 transport → ReceiveHiddenNotice(HiddenAwakeningNoticeDTO)로 연결한다.
팝업 Text/Panel을 켜고 기본 6초 뒤 unscaled time으로 숨긴다. 이벤트 ID 중복은
최근 512개 범위에서 차단한다. 재접속 시 512개보다 오래된 이벤트의 완전한 중복 제거는
별도 cursor/서버 방송 보존 정책이 필요하다.

서버 trainerName/petName을 Rich Text로 해석하지 않는다. 예시의 ShowGlobalNotice 함수는
표시 함수이며 펫 획득/발급 카운터/서버 방송 자체를 발생시키지 않는다.
실시간 네트워크 구독과 실제 outbox 소비자는 여전히 미구현이다.

## DB 이관 준비: 실제 운영 DB를 수정하지 않는 참고 흐름

현재는 운영 DB 종류/스키마/연결 정보가 없다. V1→V2 6등급→5등급 대응,
스킬 풀, 외형 프로필, 개체값/성격/케어 상태 보존 기준도 확정되지 않았다.
따라서 완료된 DB 마이그레이션이라고 보고하지 않는다.

Tools/prepare_db_migration.py는 명시한 SQLite export/reference DB에 한해 다음을 수행한다.

1. mode=ro로 원본을 읽고 계정/펫 열 이름을 명시한 profile만 사용한다.
2. 재화 음수/한도/중복 ID 및 A~E 기존 발급량을 검사한다.
3. 기본 실행은 건수/총액/누락 매핑 보고서만 출력한다.
4. --output-copy를 지정하면 새 사본에 staging 테이블을 만든다. 기존 파일 덮어쓰기 금지.
5. 지갑은 전→jeon, 명시된 gem/thread 열로 옮긴다. 없는 유료 재화를 조용히 0으로 만들지 않는다.
6. 기존 기발급량을 보존하고 한도만 A10/B~E100으로 검사한다. 초과하면 중단한다.
7. PetState 원문 JSON을 그대로 보존한다. 명시 gradeMap이 없으면 숫자 등급을 자동 변환하지 않는다.
8. 매핑이 있어도 스킬/외형/특성 보존이 미완성이므로 ready_for_runtime=0으로 남긴다.

이 도구는 운영 서비스가 사용할 완성 V2 펫 레코드를 만들지 않는다. 파괴적 UPDATE/DROP,
실제 DB 연결, 운영 전환을 수행하지 않는다. 현재 서비스에 SQLite DB adapter도 없다.

샘플 프로필 Docs/db_migration_profile.example.json은 열 이름 예시다. quotaIssued=null은
실제 기발급량을 입력하기 전 실행을 막기 위한 값이며 운영 발급량을 추측한 것이 아니다.

```bash
# 사전 검사만. 실제 export에 맞춰 profile의 열 이름과 기발급량을 먼저 입력한다.
python3 Tools/prepare_db_migration.py --source-db exported.sqlite --profile reviewed-profile.json
# 원본 보존 + 신규 사본 staging. 운영 적용은 하지 않는다.
python3 Tools/prepare_db_migration.py --source-db exported.sqlite --profile reviewed-profile.json --output-copy staging.sqlite
```

## 검증 결과

- 실제 C# 도메인+클라이언트 세션 통합 23개 검사 PASS.
- 기존 경제 55개 검사 및 기존 SliceProbe PASS.
- 원본 보존/잔액/한정량/매핑/재실행/음수 입력 등 SQLite 임시 데이터 15개 검사 PASS.
- Editor Mock 생성 소스 일치/비 Editor 서버 타입 제외/기존 스크롤 거래 검사 PASS.
- Unity API 컴파일, 실제 Canvas/HTTP/운영 DB는 NOT RUN.

코드의 네트워크 API 기준 문서:
https://docs.unity3d.com/cn/6000.0/ScriptReference/Networking.UnityWebRequest.SendWebRequest.html
https://docs.unity3d.com/cn/6000.0/ScriptReference/Networking.UnityWebRequest.html
