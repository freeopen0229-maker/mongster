# 퓨전 다마고치 RPG — 서버 권한·통합 본체 수직 슬라이스 기반

이 문서가 최신 아키텍처 기준이다. 이전 VISUAL_RENEWAL_STAGE1.md의 파츠 조립 방식은 폐기했다. 새 코드의 본체는 **한 SpriteRenderer의 통합 메시/골격/Animator**이며 추가 프리팹은 코스튬·오라에만 사용한다.

현재 결과는 클라이언트/공유/서버 핵심 소스다. 실제 계정 인증, HTTP API, 운영 DB 어댑터, 결제, 새로운 통합 리그·성체 원화, Unity 씬 재생을 완성한 결과는 아니다.

## 1. 배치와 권한 경계

| 경로 | 소속과 역할 |
|---|---|
| Assets/Scripts/SliceShared/PetState.cs | 순수 C# 개체 상태·성격·개체값·등급·단계 |
| Assets/Scripts/SliceShared/PetTradeData.cs | 거래 잔여 횟수·영구 귀속 |
| Assets/Scripts/SliceShared/PetRules.cs | UTC 생활/방치·돌봄·전투 계수 |
| Assets/Scripts/SliceShared/PetGatewayContracts.cs | 서버 요청/스냅샷 계약·BM 정책 |
| Assets/Scripts/VerticalSlice/PetRuntimeManager.cs | 서버 응답 보관·UI 예상 상태·비주얼 연결 |
| Assets/Scripts/VisualRenewal/ | 통합 본체·공유 재질·코스튬·오라 표시 |
| ServerSources/WorldContracts.cs | 서버 시계·계정 지갑·판매글·원자적 저장소 계약 |
| ServerSources/ServerPetService.cs | 인증된 소유자의 돌봄·보상 확정 |
| ServerSources/PetTradeService.cs | 게시/취소/구매·이전·귀속·수수료 소각 |
| ServerSources/Testing/InMemoryWorldStore.cs | 복사 후 커밋하는 테스트 전용 저장소 |
| ServerSources/Neulgui.Server.csproj | Shared를 링크한 .NET 8 서버 도메인 라이브러리 |
| Tools/SliceProbe.cs | 권한/경제/동시 거래 관리 코드 검사 |

ServerSources는 **Assets 밖**에 둔다. Unity APK에 서버 처리나 운영 비밀을 넣지 않는다. 공용 규칙을 클라이언트에서도 실행할 수 있다는 사실은 클라이언트에 권한이 있다는 뜻이 아니다.

기존 `Mongle.Gameplay.PetRuntimeManager`는 이전 오프라인 검증 씬의 컴포넌트다. 새 온라인 슬라이스는 **`Mongle.Slice.Client.PetRuntimeManager`**를 사용한다. 두 매니저를 같은 개체에 함께 부착하지 않는다. 이전 JSON 저장/도감/알/전투 전체를 새 계정 모델로 자동 이전한 상태는 아니다.

## 2. BM와 데이터 소유권

- 모든 종/성장/개체값은 무료 획득·육성 경로에 놓는 것이 정책이다. 실제 126종 서버 드롭/퀘스트 테이블은 아직 연결하지 않았다.
- 유료 상품 종류는 `IncubationTimeScroll`, `Costume`만 허용한다.
- 스크롤은 부화 종료 시각만 앞당긴다. 개체값·성격·등급 확률·전투력에 가산/감산을 하지 않는다. 과거 시제품의 강제부화 능력치 감소는 새 정책에 적용하지 않는다.
- 코스튬은 능력치 함수에 들어가지 않는다. 계정이 소유하며 펫 거래 때 구매자에게 자동 이전하지 않는다.
- 유료 펫/확률 증가/거래 횟수 복구/귀속 해제/능력치 판매 API는 제공하지 않는다.
- 실결제 영수증 검증과 의상 인벤토리/장착 승인 API는 미연결이다. 렌더 매니저의 costumeId만으로 상품 소유권을 검증할 수 없다.

재화·먹이는 `AccountState`, 종·성장·개체값·생활 상태는 `PetState`가 소유한다. 펫 데이터 안에 구매자의 지갑을 넣지 않는다. 클라이언트가 원하는 state JSON을 서버 저장 API로 보내는 구조도 만들지 않는다.

### PetState

개체 ID/소유 계정/Revision, 종/성장단계/등급, 0~31의 체력·공격·방어·속도 개체값, 잠재력(Permille), 성격, 포만감·친밀도·피로, 마지막 갱신 UTC, 미수령 보상, 거래/잠금 상태를 기록한다.

`PotentialPermille=1000`은 100%다. 자연 생성 범위를 정할 서버 팩토리는 별도 구현 대상이며, 예제 검증 범위는 900~1100이다. 이 값을 결제로 수정하는 코드가 없다. 같은 종도 개체값/잠재력/성격에 따라 전투 수치가 달라지고 거래 후에도 유지된다.

## 3. 시간·상태·방치 보상

서버가 인증된 시간으로 `PetRules.Advance()`를 호출한다. 현재 예제 수치는 포만감 −3/h, 친밀도 −0.2/h, 피로 회복 4/h, 방치 5분당 1코인/10분당 1XP, 미수령 시간 최대 8h다. 출시 밸런스 확정값이 아니다.

히트스톱이나 게임 시간 배율은 이 서버 생활 시간에 영향을 주지 않는다. 시계가 과거로 간 경우 중복 경과를 지급하지 않는다. 온라인 틱을 여러 번 나눠 처리해도 한 번에 경과시킨 결과와 일치하도록 검사한다.

돌봄은 Feed/Pet/Play이며 서버가 쿨다운·소유권·먹이·잠금/판매 여부를 확인한다. Feed 30초, Pet 10초, Play 60초는 예제값이다. 먹이 차감과 상태 변경, 요청 영수증 저장은 같은 트랜잭션이다.

클라이언트 `ProjectForDisplay()`는 서버 UTC와 수신 후 단조 경과 시간을 이용해 UI 예상치를 계산한다. 예상 코인을 지갑에 더하거나 서버로 전체 state를 전송하지 않는다. 실제 보상은 ClaimIdle 요청의 확정 응답으로만 갱신한다.

자유로운 여러 펫의 방치 수익은 계정별 보상 상한/활동 슬롯 등 경제 정책과 함께 설계해야 한다. 이번 코드는 개체당 8시간 상한만 있으며 계정 전체의 수익 상한은 구현하지 않았다. 거래 수수료와 횟수 제한만으로 작업장·다중 계정·인플레이션이 해결되는 것은 아니다.

## 4. 거래·귀속·소각

`CurrentTradeCount`는 **남은 거래 가능 횟수**다. 누적 거래 수는 `MaxTradeCount - CurrentTradeCount`로 얻는다.

| 성공 거래 | MaxTradeCount | CurrentTradeCount | IsSoulbound |
|---|---:|---:|---|
| 최초 | 3 | 3 | false |
| 1회 | 3 | 2 | false |
| 2회 | 3 | 1 | false |
| 3회 | 3 | 0 | true, 새 구매자에 귀속 |

귀속 상태는 잔여 횟수와 일치해야 한다. 마지막 거래를 마치면 BoundAccountId는 **구매자**가 된다. 최대치/잔여치/귀속 플래그가 모순된 저장은 거부한다. 실패한 구매는 횟수를 줄이지 않는다. 유료 초기화는 없다.

### 구매 요청의 경계

클라이언트의 구매 본문에는 OfferId와 RequestId만 있다. buyerId는 인증된 서버 세션/검증된 토큰 claim에서 얻는다. 클라이언트가 보내는 가격·소유자·등급·잔액·잔여 거래 수는 받지 않는다.

서버는 한 트랜잭션에서 다음을 수행한다.

1. 동일 구매자+RequestId의 이전 영수증을 조회한다. 동일 요청 재전송은 기존 결과를 반환한다. 같은 키의 다른 판매글은 거부한다.
2. 서버 판매글의 활성/만료/판매자를 확인한다.
3. 서버 펫의 소유권·Revision·판매/잠금·잔여 거래 수를 확인한다.
4. 서버 지갑 잔액·일일 거래 한도·이전 후 대기시간을 확인한다.
5. 판매자 기간의 미수령 방치 코인은 판매자 지갑에 정산한다. 경험치는 펫의 성장 이력에 남는다.
6. 구매자 가격 차감, 판매자 수령, 수수료 소각을 처리한다.
7. 펫 소유권 이전·횟수 차감·필요 시 구매자 귀속·의상 해제를 처리한다.
8. 판매글 종료, Revision 증가, 거래 영수증을 저장하고 모두 함께 커밋한다.

예제 수수료는 판매금의 5% 올림이다. 100코인 거래이면 구매자 −100, 판매자 +95, 5코인 소각이다. 저가 분할로 수수료가 0이 되지 않게 한다. 예제 일일 거래 한도는 계정당 10회, 이전 후 대기시간은 1h이며 정책 설정에서 바꾼다.

CreateOffer는 판매자와 Revision을 검증한 뒤 서버가 판매글 ID를 만들고 펫을 판매 중으로 잠근다. CancelOffer는 게시 소유자만 수행한다. 판매 중에는 돌봄/보상 행동을 막는다. 만료 판매글을 자동 정리하는 서버 작업은 아직 없다. 판매자는 만료 뒤에도 취소할 수 있다.

### 운영 DB 어댑터 계약

IWorldStore.ExecuteSerializable의 단일 트랜잭션 안에 pet/offer/account/receipt/burn 원장을 포함한다. 실제 DB에서는 관련 행 잠금 또는 Revision 조건 갱신과 직렬화 충돌 재시도를 구현한다. 계정 잠금 순서는 ID 등으로 고정해 교착을 줄인다.

필수 스키마 제약 예:

- pets(PetId PK), CurrentTradeCount >= 0 AND <= MaxTradeCount.
- wallets(AccountId PK), Coins >= 0.
- trade_receipts UNIQUE(BuyerId, RequestId).
- action_receipts UNIQUE(AccountId, RequestId).
- 펫당 활성 판매글 하나를 보장하는 제약.
- fee_burn 원장과 거래 영수증의 고유 연결.

재시도 시 중복 영수증을 새 거래로 실행하지 않는다. 테스트용 메모리 lock은 다중 서버 안전성을 제공하지 않는다. 현재 소스에는 운영 PostgreSQL/EF 어댑터가 없다.

## 5. 통합 본체 비주얼

PetVisualDefinition의 `rigPrefab`은 하나의 본체 SpriteRenderer와 Animator를 가진다. 본체를 귀·몸통·꼬리 프리팹으로 조립하는 배열을 제거했다. 하나의 메시를 통합 골격으로 스키닝해 움직이며 코스튬/오라만 소켓에 붙인다.

PetVisualBindings의 `bodyRenderer`, `animator`, 소켓 참조를 Inspector에서 연결한다. 기본 리그 내부 SpriteRenderer는 정확히 하나여야 한다. 코스튬은 이 검사 이후 별도 부착한다. 눈 표정은 본체 텍스처/클립·Sprite 스왑 등 통합 방식으로 제작한다.

등급·성장 enum은 이제 `Mongle.Slice`에 있다. 아래 두 namespace를 함께 사용한다.

```csharp
using Mongle.Slice;
using Mongle.VisualRenewal;

// 아래는 초기화 코드의 메서드 안에서 호출한다.
visuals.Equip(0, GrowthStage.Baby, PetGrade.Common, VisualQuality.Standard);
// 실제 서버가 승인한 CostumeId가 있을 때만 전달한다.
visuals.Equip(0, GrowthStage.Adult, PetGrade.Common, VisualQuality.Standard, "ribbon");
```

정의 조회 키는 speciesId+stage+grade다. 본체 공유 재질, CostumeId에 대응하는 의상 프리팹, 등급별 오라 프리팹을 장착한다. 실패 시 기존 외형을 유지한다. 거래 후 CostumeId를 비우므로 구매자는 본인 계정의 의상을 다시 선택한다.

한 리그를 통합했다고 자동으로 자연스러운 보행이 생기지는 않는다. 실제 스킨/웨이트/발 IK/클립과 방향별 원화가 필요하다. 기존 9부위 늘귀 원화는 이전 검증용으로 남아 있으며 새 통합 리그를 제작한 결과가 아니다.

URP HLSL은 기존 `PetLocalGlow.shader`를 유지한다. R 발광/G 작화 림/B 광택/A 보호영역이며 CPU SpriteSkin·Unlit 기준이다. 새 통합 텍스처의 UV에 맞는 단일 마스크를 제작한다. URP 패키지/Asset/Volume은 정상 Unity 환경에서 설정해야 한다. 이번 작업은 파이프라인을 자동 전환하지 않았다.

고등급의 차이는 오라/발광/장식 효과로 표현하되 얼굴과 선화를 가리지 않는다. 저품질 기기에서는 본체를 동일하게 유지하고 입자/후처리만 줄인다. 공유 머티리얼은 개체마다 복제하지 않는다. 코스튬도 통합 본체의 전체 메시를 분해하는 작업으로 연결하지 않는다.

## 6. Unity 초기화 흐름

1. PetRoot에는 새 `Mongle.Slice.Client.PetRuntimeManager`를 하나만 추가한다.
2. 같은 루트의 PetVisualManager에 카탈로그·viewMount·motionRoot·turnRoot를 연결한다.
3. 아기/성체의 통합 리그 Definition과 품질별 bodyMaterials, 의상/오라 소켓을 작성한다.
4. IPetGateway를 구현하는 HTTPS 클라이언트를 만든다. 인증 토큰·JSON·오류 매핑·취소·재전송 처리를 담당한다. 이 어댑터는 아직 제공하지 않았다.
5. 로그인과 보유 펫 선택 후 InitializeAsync(gateway, petId)를 호출한다.
6. 버튼은 await CareAsync()/ClaimIdleAsync()를 사용한다. 서버 확정 후 Changed/CareConfirmed 이벤트로 UI·반응 애니메이션을 갱신한다.

```csharp
// transport는 프로젝트에서 구현한 IPetGateway 참조다.
await runtime.InitializeAsync(transport, selectedPetId);
runtime.Changed += RefreshHud;
runtime.CareConfirmed += PlayCareReaction;
await runtime.CareAsync(CareAction.Pet);
await runtime.ClaimIdleAsync();
```

같은 HTTP 요청을 재전송할 때는 RequestId를 유지한다. RuntimeManager가 생성한 요청을 어댑터 내부 재시도에서 새 ID로 바꾸지 않는다. 응답 유실 뒤 앱 재시작까지 재시도를 복원하려면 미확정 명령 outbox의 영속 저장이 추가로 필요하다. 현재는 프로세스 내 요청 ID 유지 계약까지만 제공한다.

기존 검증 씬의 PetMotor/PetAnimatorBridge는 이전 Gameplay 데이터 타입을 참조한다. 새 RuntimeManager의 Stats/이벤트를 읽는 어댑터 또는 모터 수정은 별도 통합 작업이다. 새 서버 스냅샷 모델을 넣었다고 기존 캡슐 씬이 온라인 육성 게임으로 바뀌지는 않는다.

## 7. 서버 배치·검증

ServerSources/Neulgui.Server.csproj는 .NET 8 도메인 라이브러리다. Shared C#을 링크하며 UnityEngine을 참조하지 않는다. HTTP 호스트가 아니므로 이것만 실행해 서버가 열리지는 않는다. 인증된 라우트와 영속 DB 어댑터를 작성해 ServerPetService/PetTradeService를 호출한다.

관리 코드 검사 명령:

`python3 Tools/verify_with_unity_assemblies.py <Unity Editor 폴더>`

이번 검사 범위:

- 실제 Unity API 참조 Editor/Player C# 컴파일.
- Unity 어셈블리 없이 공용 규칙/서버 도메인 라이브러리 컴파일.
- UTC 시간 분할·8시간 상한·과거 시각 중복 방지·개체값 범위.
- 돌봄 소유권·Revision·쿨다운·중복 요청.
- 마지막 거래의 구매자 귀속·귀속 재판매 거부.
- 잔액 부족 시 지갑·횟수·소각·일일 카운트의 원자적 롤백.
- 두 구매자 동시 요청에서 판매 한 번만 성공.
- 반복 구매의 중복 차감/소각 방지, 판매자 방치 코인 정산.
- 게시 취소 권한과 유료 시간 단축 정책.

이것은 단일 프로세스 메모리 저장소를 사용한 규칙/원자성 검사다. 운영 DB의 격리·동시성, 실제 인증·네트워크 재시도, Unity Play Mode·셰이더·모바일 성능을 검증한 결과가 아니다. 기존 라이선스/Package Manager IPC 문제는 이번 작업에서 해결하지 않았다.

## 다음 통합 범위

실제 HTTPS/인증·DB 어댑터, 무료 획득·알/성장 판정과 서버 난수, 의상 인벤토리/영수증 검증, 계정 수익 한도·이상 거래 탐지, 통합 늘귀 리그, 새 모터/전투 연결, 실제 기기 플레이가 남아 있다. 이 기반을 안전한 거래가 운영 중인 출시 게임으로 표현하지 않는다.

## 공식 DB 구현 참고

- PostgreSQL 행 잠금/교착: https://www.postgresql.org/docs/17/explicit-locking.html
- EF Core 트랜잭션: https://learn.microsoft.com/en-us/ef/core/saving/transactions
- EF Core 동시성 토큰: https://learn.microsoft.com/en-us/ef/core/modeling/concurrency
