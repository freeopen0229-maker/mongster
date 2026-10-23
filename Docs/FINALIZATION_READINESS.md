# 최종 실행 검증 준비 상태

2026-10-23. 이번 요청은 Unity Play Mode 및 운영 DB의 실제 실행이다.
현재는 두 실행 환경이 없으므로 해당 작업을 완료로 보고하지 않는다.

## 확인한 환경

- Unity / UnityHub / dotnet 실행 파일 없음.
- 알려진 Unity 설치 디렉터리 없음.
- 운영 Connection String / DATABASE_URL / Unity license 설정 없음. 비밀 값은 출력하거나 수집하지 않았다.
- 작업 파일의 DB는 명시적으로 생성한 ReferencePetMigration.sqlite뿐.
- 프로젝트에 실제 생성된 .unity/.prefab가 없고 Editor 생성 코드만 있음.
- 운영 DB 종류/주소/DB명/접속 계정/권한/대상 환경은 제공되지 않음.

이전 단계의 로컬 HTTPS 검사와 SQLite fixture 변환은 유효하지만 운영 환경 실행 증거가 아니다.
Unity가 설치된 실행 환경과 실제 대상 DB가 있어야 요청한 두 검증을 수행할 수 있다.

## 이번에 발견해 보완한 누락

기존 EggHatchPresenter는 부화 결과 텍스트만 표시했다. 등급별 배경 오라/색상은 없었고,
시리얼도 표시용 Text만 있었으며 pointer/tap 툴팁이 없었다.

추가/변경:

| 파일 | 변경 |
|---|---|
| Assets/Scripts/EconomyV2/HatchPresentationPolicy.cs | 5등급 UI 스타일, 정확한 시리얼 포맷, 제한량/등급 공지 검사 |
| Assets/Scripts/HatchResultStyling.cs | 결과 배경/텍스트 색과 UI 오라 맥동 |
| Assets/Scripts/PetSerialTooltip.cs | PC hover / 모바일 tap 표시, 미각성 후보 정보 숨김 |
| Assets/Scripts/EggHatchPresenter.cs | 실제 부화 응답 시 gradeStyling.Apply 연결 |
| Assets/Scripts/ClientGameManager.cs | 시리얼 툴팁 바인딩, 유효한 각성 공지만 중복 제거 후 표시 |
| Assets/Editor/EconomyNetworkLabSetup.cs | 테스트 Scene 생성 시 오라/배경/툴팁 컴포넌트 자동 연결 |

| 등급 | 텍스트 색 | 오라 기본 alpha |
|---|---|---:|
| 일반 | 회백색 #CAD0DE | 0 |
| 희귀 | 청색 #65BAFF | 0.14 |
| 영웅 | 보라색 #BA8CFF | 0.22 |
| 전설 | 금색 #FFD57A | 0.32 |
| 신수 | 청록빛 백색 #BDFCF0 | 0.45 |

오라는 UGUI Image의 생성형 방사형 그라디언트이며 실제 URP Bloom/VFX Graph가 아니다.
고등급 상용 그래픽 품질 검증이 끝난 것으로 해석하지 않는다.
아기/성체 늘귀 리그 제작도 이번 범위에서 완료되지 않았다.

PC는 pointer hover로 표시하고, 모바일은 tap으로 열고 다시 tap하여 닫는다.
EventSystem/실제 디바이스에서의 터치/포인터 동작은 아직 검증하지 않았다.
No. 003 / 010, No. 042 / 100 형식만 계산 검증을 통과했다.

## 실행한 검사

- HatchPresentationProbe: 17개 순수 C# 스타일/시리얼/공지 규칙 검사 PASS.
- 기존 EconomyV2Probe 55개 및 ClientEconomyProbe 23개 재실행 PASS.
- 기존 SliceProbe 재실행 PASS.
- C# delimiter/프로젝트 메타데이터 검사 PASS.
- Unity 컴포넌트/API 컴파일, Play Mode, 애니메이션, 실제 오라 색, 오디오, 팝업/툴팁 입력은 NOT RUN.

실행 결과는 Validation/EconomyV2/results.json에 기록했다.

## 실제 Play Mode에서 완료해야 할 검증

1. Unity에서 Mongle → Economy → Create HTTP Hatch Lab 실행.
2. 실제 HTTPS 주소/유효 인증서/로그인 세션으로 연결.
3. 구매 → 밀봉 알 지급 → 부화 시작 → 자연 부화 또는 스크롤 → 부화 성공 응답 순서 확인.
4. 성공 응답 뒤에만 균열/사운드/결과 팝업이 실행되는지 검사.
5. 희귀/영웅/전설/신수 결과를 통제한 테스트 응답으로 각 UI 색과 오라 확인.
6. A 10 / B~E 100 한정 시리얼의 PC hover/모바일 tap 확인.
7. 실제 방송 transport에서 유효한 공지를 ReceiveHiddenNotice로 전달하고 띠 배너 확인.

실시간 outbox 방송 transport는 아직 연결하지 않았다. 함수 호출로 배너가 보이는 검사와
실제 서버 간/전 채널 방송 검사를 구분해야 한다.

## 운영 DB 이관

사용자가 지정한 매핑은 확정돼 있다: 0 Common / 1·2 Rare / 3 Epic / 4 Legendary / 5 Divine.
운영 대상 파일/접속 설정이 없어서 이번에는 운영 UPDATE나 신규 운영 백업을 실행하지 않았다.
기존 참고 DB에 변환을 다시 수행해 운영 이관처럼 보고하지도 않는다.

대상 DB의 종류와 실제 접속 설정이 제공되면 먼저 schema/열 타입/제약/행 수/권한을 확인해야 한다.
현재 Tools/migrate_pet_rarity.py는 SQLite용이다. 다른 DB에 그대로 실행할 수 없다.
PetTable_Backup_2026이 이미 있으면 덮어쓰지 않고, 기존 migration marker와 상태를 확인한다.
운영 credential은 채팅/소스/ZIP에 기록하지 않고 실행 환경의 secret 설정으로 주입해야 한다.

연결 준비에 필요한 정보: Unity를 실행할 수 있는 환경, DB 종류·주소·DB명·접속 방식과
백업/UPDATE 권한. 실제 환경이 연결되기 전까지 전체 통합 완료 상태는 false다.
