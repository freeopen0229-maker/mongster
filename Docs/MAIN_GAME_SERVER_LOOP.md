# MainGame 및 서버 권한 펫 루프

## 변경된 시작점

`Assets/Scenes/MainGame.unity`를 커밋하고 EditorBuildSettings의 유일한 시작 씬으로 지정했다. BuildAutomation.Configure/BuildAndroid는 이 씬이 없거나 MainGameRoot가 없으면 BuildFailedException으로 중단한다. EconomyNetworkLabSetup.Create는 빌드에서 호출하지 않는다. 에디터 자동 검증 씬 생성도 MainGame이 존재하면 실행하지 않는다.

BuildAutomation은 URP Renderer2DData, URP Asset, Resources/MainGame/PetSprite 머티리얼을 생성하고 모든 품질 설정에 적용한다. GameplayFoundationProject의 도형/Trail 머티리얼도 기존 자산을 포함해 URP 2D Sprite-Unlit shader로 바꾼다. Built-in OnRenderImage 효과는 연결하지 않는다. Android forceInternetPermission=true는 Internet Access Require에 해당한다.

## 외부 서버와 로그인 연결에 남은 설정

`Assets/Resources/NetworkConfig.json`의 environment는 staging 또는 production이다. apiBaseUrl에는 실제 배포된 HTTPS 호스트의 루트 주소를 입력한다. 현재 빈 값은 미설정임을 명시하기 위한 것이며 실행 시 설정 오류를 표시한다. localhost/loopback, 인증정보가 들어간 URL, query/fragment, 하위 경로는 거부한다. 인증서 검증을 해제하지 않는다. API 주소는 공개 설정이며 비밀 토큰은 JSON이나 저장소에 넣지 않는다.

기존 저장소에는 로그인 API나 외부 운영 서버가 없다. 선택한 인증 서비스에 맞게 GameLoginProvider.LoginAsync를 구현한 MonoBehaviour를 만들고 MainGameRoot.loginProvider에 연결해야 한다. 서버가 발급한 GameLoginResult(accountId, accessToken, selectedPetId)를 반환한다. 서버는 계정 ID를 토큰 검증 결과로 결정해야 한다. 클라이언트가 보낸 accountId를 인증 근거로 신뢰하면 안 된다. 다른 도메인의 OAuth ID 토큰을 검증 없이 게임 API 토큰으로 사용하지 않는다.

현재 MainGame에는 서버 주소/토큰 입력 UI가 없다. NetworkConfig 로드 → 실제 로그인 결과 → HttpEconomyGateway.ConfigureSession → ClientGameManager.RefreshAsync → 선택한 펫 snapshot 순서로 초기화한다. 실제 URL/인증 제공자 없이는 로그인 또는 온라인 플레이 성공을 주장할 수 없다. reference_https_server.py는 로컬 TLS/SQLite 검증용이며 외부 운영 서버를 자동 배포하지 않는다.

## 서버 권한 구조

- MainGameRoot: 로그인, 알 구매/부화, 펫 표시, 돌봄, 방치 보상, 탐험 버튼.
- GameSessionCoordinator: 로그인/로그아웃 수명 관리, ClientGameManager.PetHatched 응답 뒤 서버 펫 조회, 경제 상태 재조회.
- Slice.Client.PetRuntimeManager: IPetGateway 요청과 확정 snapshot. 클라이언트 시간 경과는 복사본의 표시 계산에만 사용한다.
- Gameplay.PetRuntimeManager: 예전 이동/애니메이션 컴포넌트의 읽기 전용 호환 뷰. 로컬 JSON을 읽거나 돈/경험치를 저장하지 않는다. 과거 로컬 돈을 서버 계정으로 자동 이관하지 않는다.
- PetLifecycle: EconomyAsset의 소유권/Revision을 기준으로 Lifecycle 필드를 유지한다. 구형 V1 테스트 저장소만 PetState 직접 조회를 허용한다. 등급은 숫자 캐스팅 대신 명시적으로 매핑한다. 기존 economy 데이터에 없던 IV/성격 등은 보수적 기본값을 사용한다.

서버 GET `/v2/pets/snapshot?petId=...`와 POST `/v2/pets/care`, `/v2/pets/claim-idle`, `/v2/pets/exploration/start`, `/v2/pets/exploration/finish`를 reference host/domain worker에 연결했다. POST DTO는 PetId, RequestId, ExpectedRevision, Action이다. 인증된 소유권, Revision, 상태/쿨다운을 확인하고 재화는 서버가 계산한다. 돌봄과 경제 시스템은 같은 계정 지갑과 펫 asset을 사용한다.

탐험은 현재 서버 시각 기준 60초, 완료 시 5전/8XP의 staging 기본 루프다. 시작 중 펫을 잠가 거래를 차단하며 완료 요청의 서버 검증 뒤 확정 보상을 준다. 전투 맵/새 원화/126종 완성이나 출시 밸런스를 의미하지 않는다. 현재 화면은 기존 아기 원화 파츠를 표시한다. 성체·다른 종 전용 아트 및 실제 Unity 화면 품질은 별도 확인이 필요하다.

## 응답 유실 및 재시작

ClientPetSession은 최초 RequestId와 명령/Revision을 전송 전에 저장한다. 전송 예외/취소/401/5xx에서는 미확정 요청을 유지하고 신규 명령을 막는다. 앱 재시작 시 계정·펫별 pending을 복구해 같은 ID/동일 payload로 재확인한다. 서버는 account+RequestId receipt를 변경과 함께 원자적으로 저장하고, 재요청 시 Revision 검사보다 먼저 기존 결과를 반환한다. 확정 거절(400/403/409/422)은 pending을 해제한다. 같은 ID의 다른 payload는 거절한다. 저장된 pending이 손상되면 신규 펫 명령을 막는다. 운영에서도 receipt를 재시도 가능 기간보다 먼저 삭제하면 안 된다.

PlayerPrefs에는 토큰/잔액 대신 pending 명령만 둔다. 서버 authority는 PlayerPrefs 내용이나 클라이언트 보상 계산을 신뢰하지 않는다. 운영 DB 어댑터도 자산/지갑/receipt를 한 트랜잭션으로 커밋해야 한다. 기존 normalized SQLite migration 패키지는 새 Lifecycle의 운영 DB 어댑터를 대신하지 않는다.

## 검증

검증 브랜치 commit 7b8838dc5556223aa7b0628260c57f200be5f0ea의 GitHub Actions run 39968618210 managed job PASS:

- 실제 Mono C# 컴파일 및 기존 경제/클라이언트/표시 규칙 검사.
- PetLoopProbe 15개: 동일 펫 정체성, 먹이 한 번 소비, 응답 유실, 클라이언트 복구, 서버 재시작, 계정 범위, 탐험 시간/보상/잠금.
- 실제 인증서 검증을 켠 loopback HTTPS + C# worker + SQLite 24개: 부화 뒤 돌봄, 응답 유실 재전송, 토큰 소유권, 위조 필드 거절 포함.
- migration fixture 및 editor-only mock 경계 검사.

이 검사는 UnityWebRequest 실기 실행이나 Unity 렌더링 검사가 아니다. 최종 커밋의 Unity import/Android 빌드는 Actions 결과로 별도 확인해야 한다. MainGame을 열어 로그인 전 상태, 실제 provider 로그인, 부화→펫 표시, 돌봄, 탐험 60초 후 확정 결과, 앱 중단/재실행 재확인, Android 인터넷 권한을 기기에서 확인한다. 외부 서버/실제 인증 제공자가 연결되기 전에는 이 실기 온라인 검증을 완료할 수 없다.
