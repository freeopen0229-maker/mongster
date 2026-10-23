# 실행 환경 확인 및 최종 빌드 검증 계획

확인일: 2026-10-23 (한국 시간).
확인 범위: 접근 가능한 현재 작업 호스트와 NeulguiUnity 프로젝트.
사용자 PC나 접속 정보가 없는 외부 CI/클라우드 환경까지 조사한 것은 아니다.
민감한 환경 변수 값/DB 비밀번호/인증 토큰은 입력하거나 출력하지 않았다.

## 1. Unity / CI / 헤드리스 상태

| 항목 | 확인 결과 |
|---|---|
| 프로젝트 지정 버전 | Unity 6000.3.26f1 |
| 현재 호스트 Unity / Hub | 실행 파일·프로세스·알려진 설치 디렉터리 없음 |
| 라이선스 설정 | 관련 설정 이름이 현재 환경에 없음 |
| 프로젝트 CI 구성 | GitHub/GitLab/Jenkins/Azure/Buildkite 설정 파일 없음 |
| 현재 CI runner | 관련 CI indicator 없음. 외부 runner는 접근 정보 없음 |
| Headless build | 구축/실행 확인 불가. 실행할 Unity Editor 없음 |
| 도구 환경 | PATH에 dotnet/Mono/Docker 없음. 작업 호스트의 별도 portable Mono로 C# 검사 실행 |
| 실제 .unity 씬 | 없음. Editor 생성 코드만 준비됨 |
| Graphics/Player/Build Settings | 설정 asset 없음 |

따라서 현재 Headless 빌드나 APK가 성공했다고 보고할 수 없다.

## 2. DB / 네트워크 인프라

| 항목 | 확인 결과 |
|---|---|
| 실제 구현해 실행한 저장소 | 로컬 HTTPS 참고 서버의 Python sqlite3 + world/receipt 저장 |
| 포함된 DB | 명시적으로 생성한 ReferencePetMigration.sqlite 테스트 사본 |
| 운영 DB 종류 | 미설정/확인 불가. MySQL·PostgreSQL·MongoDB 중 어느 것을 사용한다고 단정할 근거 없음 |
| 운영 Connection String | 현재 환경/프로젝트에 제공된 설정 없음 |
| 운영 대상 주소·DB명·접속 권한 | 미제공 |
| 로컬 네트워크 | loopback bind 가능. 실제 TLS 요청 18개 재검사 PASS |
| 운영 DB DNS/TCP/TLS | 대상 주소가 없어 NOT RUN |
| 클라우드 방화벽/보안 그룹 | 계정·규칙·대상 정보가 없어 UNKNOWN |

운영 DB가 연결되지 않는 원인을 '방화벽이 막혔다'고 판단하지 않았다.
로컬 HTTPS 성공은 인터넷 공개 접근이나 운영 DB 연결 성공의 증거가 아니다.
이번 작업에서는 운영 DB 접근/마이그레이션/방화벽 변경을 수행하지 않았다.

## 3. 빌드 준비 점검에서 발견해 조치한 사항

기존 Packages/manifest.json에 UGUI/UnityWebRequest/URP/2D Animation이 빠져 있었다.
코드와 목표 파이프라인에 필요한 아래 의존성을 선언했다.

| 패키지 | 선언 버전 |
|---|---|
| com.unity.ugui | 2.0.0 |
| com.unity.modules.ui | 1.0.0 |
| com.unity.modules.unitywebrequest | 1.0.0 |
| com.unity.render-pipelines.universal | 17.3.0 |
| com.unity.2d.animation | 13.0.6 |

Unity 공식 6000.3/패키지 문서에서 버전과 기능을 확인했다.
manifest 선언 수정이며 UPM 설치/의존성 잠금/Unity 컴파일을 완료했다는 뜻은 아니다.
특히 URP 패키지가 있어도 RenderPipelineAsset을 생성해 Graphics/Quality에 지정해야 한다.
현재는 Editor가 없어 그 단계가 아직 실행되지 않았다.

확인한 공식 문서:
- https://docs.unity3d.com/Packages/com.unity.ugui@2.0/manual/index.html
- https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@17.3/changelog/CHANGELOG.html
- https://docs.unity3d.com/6000.3/Documentation/Manual/com.unity.2d.animation.html
- https://docs.unity3d.com/kr/current/Manual/com.unity.modules.unitywebrequest.html

## 4. UI / 데이터 규칙 최종 점검

기존 17개를 실제로 재실행했고, 입력 상태와 공지 경계값 16개를 더해 총 33개 PASS.

| 기능 | 로직 검사 결과 | 실제 Unity 검증 |
|---|---|---|
| 5등급 색상 | 서로 다른 색, 잘못된 등급 거절 | NOT RUN |
| 오라 | 일반 0, 희귀→신수 단계별 alpha 증가, 0~1 범위 | 실제 맥동/색/렌더링 NOT RUN |
| 시리얼 | No. 003 / 010, No. 042 / 100, 0/범위 초과 거절 | Text/레이아웃 NOT RUN |
| PC hover | 진입 열기 / 클릭 유지 / 이탈 닫기 | Raycast/EventSystem NOT RUN |
| 모바일 tap | touch enter 무반응 / 첫 탭 열기 / touch exit 유지 / 재탭 닫기 | 실제 디바이스 입력 NOT RUN |
| 상태 초기화 | 대상 변경·비활성 시 닫기, 비대상 펫 숨김 | OnDisable/씬 연결 NOT RUN |
| 신수 공지 | hiddenType=0, Divine, 한도 10, 시리얼 1~10 | 실제 방송 transport/UI NOT RUN |
| 전설 공지 | hiddenType=1~4, Legendary, 한도 100, 시리얼 1~100 | 실제 방송 transport/UI NOT RUN |

입력 상태를 SerialTooltipState로 분리했고 PetSerialTooltip이 실제로 해당 클래스를 사용한다.
검사가 UI 코드를 흉내 낸 별도 로직을 실행하는 구조는 아니다.
다만 pointer ID를 mouse/touch로 분류하는 부분은 테스트 씬의 StandaloneInputModule 기준이다.
Input System UI 모듈로 교체하면 해당 모듈의 PointerEventData에 맞게 어댑터 검증이 필요하다.

실행 기록:
- Validation/EnvironmentAudit/results.json: 비밀 값 없는 환경 점검.
- Validation/EconomyV2/results.json: 33개 presentation/입력 상태 + 기존 55개 경제 및 23개 클라이언트 검사.
- Validation/NetworkAndMigration/https_results.json: 실제 TLS/SQLite 18개 재검사.

이 값들은 C# 컴포넌트 전체를 Unity API로 컴파일하거나 실제 화면을 본 기록이 아니다.

## 5. 최종 빌드/검증 순서와 통과 기준

### 단계 1 — 실행 환경 준비

Unity 6000.3.26f1 실행 환경과 유효한 라이선스를 준비한다.
Android 대상이면 Android Build Support/SDK/NDK/JDK도 같은 설치에 준비한다.
현재 사용자 PC/외부 CI 연결 정보는 없다. 어느 쪽을 사용할지 실제 실행 대상을 정해야 한다.

통과 기준: Editor가 프로젝트를 열고 UPM 의존성 복원을 끝내며, Editor/Player C# 컴파일 오류가 0개.
URP/SpriteSkin/UGUI/UnityWebRequest 의존성 및 shader 컴파일 성공도 확인한다.

### 단계 2 — 씬/렌더링/PlayerSettings 완성

Mongle → Economy → Create HTTP Hatch Lab 실행.
URP Asset 생성 및 Graphics/Quality 연결, 실제 테스트 씬 저장/Build Settings 등록.
해상도/입력 모듈/제품 식별자/플랫폼 설정을 확정한다.

통과 기준: 실제 생성된 .unity/.anim/.controller가 있고, 핑크 shader나 누락 컴포넌트가 없음.
GUI 테스트는 화면을 렌더링하는 환경에서 수행한다. -nographics 검사는 화면/색/오디오 품질의 대체가 아니다.

### 단계 3 — Play Mode/API/디바이스 검증

유효한 HTTPS 인증서와 세션으로 서버에 연결한다.
구매→밀봉 알 지급→부화 대기/스크롤→성공 응답→균열/SFX/등급 팝업 순서를 검사한다.
희귀/영웅/전설/신수는 테스트 전용 고정 응답으로 모두 확인한다. 운영 확률을 바꾸지 않는다.
시리얼 hover/tap, 전역 띠 공지, 중복 응답/재접속/일시 정지 상태를 확인한다.

통과 기준: 실제 영상/스크린샷과 Editor log, 터치/오디오 결과가 남고 모든 연결된 UI가 작동함.
늘귀 리그/모션 품질 검증은 별도 아트/애니메이션 자산이 필요하며 아직 완료되지 않았다.

### 단계 4 — 운영 DB 연결 및 이관

DB provider·주소·DB명·TLS·접속 방식·백업/변경 권한을 실제 설정한다.
비밀은 실행 환경의 secret 설정에서 주입하고 보고서/소스/ZIP에 넣지 않는다.
DNS→TCP→TLS→인증→schema/열 타입 확인 순서로 검사한다.
백업을 보존하고 0 Common / 1·2 Rare / 3 Epic / 4 Legendary / 5 Divine 변환을 실행한다.

통과 기준: 원본 백업, 전후 행 수/등급 분포/불변 필드, 미확인 등급 대기 사본, 재실행/rollback 확인.
현재 도구는 SQLite용이다. 다른 provider를 선택하면 해당 schema/transaction에 맞게 이관해야 한다.

### 단계 5 — Development APK 및 CI

검증된 씬/PlayerSettings를 기준으로 Development APK를 생성해 실제 Android 기기에서 확인한다.
그 후 동일 버전 Editor/패키지 lock/빌드 명령/결과 로그를 CI에 고정한다.
서명 비밀/스토어 설정 및 릴리스 검토는 Development 빌드와 별도 단계다.

통과 기준: 실제 설치 가능한 APK와 기기 실행 결과. 파일이 없으면 빌드 성공으로 표시하지 않는다.

## 결론

요청한 데이터 및 입력 상태 로직은 재검사 PASS이며 manifest 누락도 수정했다.
현재 단계는 환경/의존성 복원과 실제 Unity 검증을 기다리는 상태다.
운영 DB와 외부 방화벽 상태는 미확인이며 현재 최종 빌드 준비 완료 상태는 false다.
