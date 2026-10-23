# 클라우드 CI·서버 중심 마무리 보고서

2026-10-23 UTC. 이번 요청에 따라 **현재 호스트에서 Unity Editor를 다시 실행하지 않았다.** 로컬 GUI/네이티브 import 재시도는 중단하고 소스·서버·클라우드 실행 구성을 정리했다. 실행 가능한 게임 완성 또는 스토어 출시 완료 보고서가 아니다.

## 완료한 작업

| 작업 | 결과 |
|---|---|
| GameCI 워크플로 | `.github/workflows/unity_build.yml` 작성. GitHub-hosted ubuntu-22.04, Unity 6000.3.26f1 Android 이미지, 공식 Unity Builder, 기존 BuildAutomation.BuildAndroid 연결 |
| 라이선스 구조 | UNITY_LICENSE/EMAIL/PASSWORD 또는 Pro SERIAL 시크릿 참조·누락 검사. 실제 GitHub 시크릿 등록·Unity 활성화는 수행하지 않음 |
| 오프라인 C# | Roslyn으로 실제 경제/DTO/서버 코드 컴파일·실행. 현재 Player C# 전체와 Editor 설정/빌드 소스를 실제 설치된 Unity API + 캐시 UGUI/URP 참조 DLL로 오프라인 컴파일 |
| 데이터 규칙 보완 | DTO·글로벌 공지·UI 레거시 진입점에서 신수 10/전설 100 한정량을 함께 검증. 각성 아기 및 잘못된 등급/후보/한정량 조합 거절 |
| DB 패키지 | SQLite 전용 원본 사본→변환 대기 보존→매핑→무결성 검사 SQL 및 서버 실행 문서. 실제 운영 DB 이관 아님 |

`UserCurrency`, `PetData`는 현재 `Assets/Scripts/EconomyV2/EconomyData.cs`에 정의되어 있다. 같은 이름의 클래스를 파일별로 다시 추가해 중복 정의하지 않았다. UI 진입점은 `Assets/Scripts/ClientGameManager.cs`, DTO는 `EconomyV2/ClientContracts.cs`다.

## 클라우드 빌드 구성과 활성화

공식 GameCI v4 참조를 GitHub API에서 확인해 `4423ee75828dff56037d1d1cfdac6b68e3a6ba00`으로 고정했다. Docker Hub의 `unityci/editor:ubuntu-6000.3.26f1-android-3` 태그가 active임을 확인했다. 이미지를 이 호스트에서 pull/실행한 것은 아니다. 기록: `Validation/CloudCI/configuration-check.json`.

워크플로는 main push의 관련 소스 변경 또는 workflow_dispatch로 실행된다. PR/fork에는 Unity 계정 시크릿을 제공하지 않는다. 기존 `validate.yml`의 push/PR 순수 관리 코드·HTTPS·DB 검사는 유지하고 새 SQL fixture 검사를 추가했다. 기존 선택적 self-hosted job은 별도 경로이며 새 클라우드 빌드는 self-hosted 러너를 요구하지 않는다.

### 원격 저장소에 적용하는 순서

1. 프로젝트 내부 내용을 저장소 루트로 올린다. 루트에 Assets, Packages, ProjectSettings, .github가 나란히 있어야 한다. ZIP의 바깥 `NeulguiUnity` 폴더 자체를 하위 디렉터리로 넣으면 workflows가 탐지되지 않으므로 주의한다. 이 구성의 projectPath는 `.`이다.
2. 저장소 Settings → Secrets and variables → Actions에 `UNITY_EMAIL`, `UNITY_PASSWORD`와 아래 활성화 수단 중 하나를 등록한다. 실제 비밀 값을 소스·문서·채팅에 남기지 않는다.
3. Personal/해당 파일 기반 라이선스는 **정상 활성화된 `.ulf`의 내용**을 `UNITY_LICENSE`에 저장한다. `.alf`는 활성화 요청이며 활성 라이선스가 아니므로 UNITY_LICENSE에 넣지 않는다. Personal은 공식 Hub 기반 활성화/파일 발급 가능 여부를 확인한다. 모든 계정·버전에서 수동 활성화가 가능한 것으로 가정하지 않는다.
4. 사용 가능한 Pro 시리얼 활성화라면 `UNITY_SERIAL`을 사용하고 UNITY_LICENSE는 비워 둔다. 파일과 시리얼이 모두 있으면 워크플로가 명시적으로 실패한다. GameCI Builder가 해당 자격 증명을 이용하는 활성화 단계를 수행한다. 이 설정 작성이 실제 인증 성공을 의미하지 않는다.
5. Actions에서 Unity cloud Android Development build를 실행한다. 기존 custom BuildAndroid가 UPM/스크립트 임포트 이후 URP/씬/PlayerSettings를 설정하고 IL2CPP ARM64 Development APK를 생성한다. 빌드 산출물과 Succeeded 기록이 없으면 job이 실패한다.
6. 성공 시 `Neulgui-Android-development` artifact에서 APK를 받는다. 테스트 폰 설치, 터치 입력·네트워크·음향·실제 펫 품질은 별도 검증한다. 헤드리스 빌드는 자연스러운 움직임/실제 화면의 품질 증거가 아니다.

정상 계정/라이선스, GitHub 저장소 권한, Actions 사용 가능량, 러너 디스크 및 패키지 서버 연결이 선행 조건이다. 이 요청에는 저장소 주소·연결·시크릿이 제공되지 않아 원격 파일 등록, 활성화, Actions dispatch, APK 생성은 실행하지 못했다. 비밀 값은 워크플로에서 출력하지 않으며 Unity 라이선스·비밀번호 파일을 artifact로 업로드하지 않는다.

공식 참고:
- https://game.ci/docs/github/builder/
- https://game.ci/docs/github/activation/
- https://docs.unity.com/en-us/engine/6000.3/manual/get-started/install-and-upgrade/licenses-and-activation/managing-your-unity-license

## 검증 범위

`Tools/verify_roslyn_core.py`는 Unity 설치본의 Mono에서 Roslyn csc.exe를 실행한다. Editor 실행·소켓 통신·라이선스 활성화를 하지 않는다. 실제 도메인과 클라이언트 소스를 컴파일하고 동일 C# 검사를 실행한다. Unity API를 쓰는 UI/에디터 도구는 순수 도메인 DLL만으로 컴파일할 수 없어 설치된 엔진 및 템플릿의 UGUI/URP API DLL을 참조한다. Editor 검사는 편집기 조건부 소스를 함께 컴파일하며, 실제 asmdef별 분리 및 Play Mode 테스트 어셈블리를 확인한 것은 아니다. 이는 UPM이 이 프로젝트 의존성을 복원했다는 증거가 아니며 asmdef/플레이어 빌드·셰이더·씬 임포트는 검증하지 않는다.

이번 검사: 경제 55개 + 알 확률 1천만 구간 전체 분할, 클라이언트 23개, 표시/DTO 39개, Slice 권한·거래·케어 시나리오 통과. DB 기존 이관 11개·사전 검사 15개와 새 SQL 10개 fixture 검사 통과. DTO 변경 후 실제 로컬 HTTPS/SQLite 18개 검사도 다시 통과했다. `Validation/Roslyn/results.json`과 각 DB 검사 기록을 참조한다. 예전 33개 표시 검사는 이번 39개로 확장했다.

## DB 배포 범위

`Database/README.md` 및 SQLite SQL 2개가 실행 패키지다. 0→Common, 1/2→Rare, 3→Epic, 4→Legendary, 5→Divine. 알 수 없는 값은 원본/대기 사본에 남긴다. 전체 DB 백업을 먼저 확보한 뒤 트랜잭션 적용·정합성 검사를 수행한다. 운영 제공자가 미정이므로 이 SQL을 PostgreSQL/MySQL용으로 주장하지 않는다. 이번 실행 대상은 임시 fixture뿐이다.

## 남은 실제 실행

원격 저장소 등록/라이선스 시크릿 설정 → GameCI 실제 실행 → APK 다운로드 및 폰 검증, 운영 DB 제공자 확정 및 스테이징/운영 이관, 네이티브 Play Mode·통합 늘귀 리그/아트 품질 확인이 남았다. 이번 범위의 소스 구성·규칙 보완·서버 실행 패키징은 제공하지만, 이 단계들이 이미 성공한 것으로 보고하지 않는다.
