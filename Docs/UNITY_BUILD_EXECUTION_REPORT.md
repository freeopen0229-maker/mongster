# Unity 환경 복원·최종 빌드 실행 보고서

검사일: 2026-10-23 UTC. 대상: 현재 작업 호스트와 이 프로젝트. 사용자 PC나 외부 CI 서버를 검사한 결과가 아니다.

## 단계별 결과

| 요청 | 실제 수행 결과 | 완료 여부 |
|---|---|---|
| Unity 설치 | 공식 Linux Editor 6000.3.26f1 다운로드·압축 해제. `Unity -version` 종료 코드 0, 출력 6000.3.26f1 | 설치/버전 실행 완료 |
| 프로젝트 로드·패키지 복원·컴파일 | 실제 batch import 실행, 약 30초 후 종료 코드 1. 라이선스 및 Package Manager IPC 실패 | 실패/차단. 현재 전체 소스의 Unity 컴파일 통과를 주장하지 않음 |
| URP·PlayerSettings·씬 | BuildAutomation.cs에 2D Renderer/URP 자산 생성, 렌더 파이프라인 지정, Android 패키지 ID·세로 해상도·IL2CPP ARM64·개발 서명, 테스트 씬 생성 경로 작성 | 소스 준비. 설정 자산 생성/적용 미실행 |
| Play Mode·UI·사운드 | 네이티브 Play Mode 테스트 2개 및 프레임 캡처 경로 추가 | 실행 못함. 실제 화면·오디오·장치 입력 미검증 |
| 운영 DB 백업·등급 변환 | 설정 존재 여부만 검사. 운영 DB 종류·주소·접속 설정 없음 | 미실행. 실제 운영 데이터 변경 없음 |
| Android Development APK | BuildAutomation.BuildAndroid와 검증 래퍼 준비 | APK 없음. Android 모듈/SDK/NDK 추가 설치 및 빌드 미실행 |
| 실기기 | adb 및 연결 장치 환경 없음 | 미실행 |
| GitHub Actions | .github/workflows/validate.yml 작성, YAML 구조 검사 | 원격 저장소·러너 등록/실행 미완료 |

## 설치 및 실패 증거

설치 실행 파일: `/workspace/scratch/73110fac84d1/unity-install/Editor/Unity`.
공식 다운로드: https://download.unity3d.com/download_unity/de8167e7440c/LinuxEditorInstaller/Unity-6000.3.26f1.tar.xz
설치 파일 4,540,380,840바이트. 다운로드 바이트에서 계산한 SHA-256:
`2c2d84526c5ba3b34b2bbeb5f5a8758a113129b9abf596d2068f999c353ca547`.
이 해시는 서명 검증이나 별도 공식 체크섬 대조를 의미하지 않는다. 엔진 설치본은 프로젝트 ZIP에 포함하지 않는다.

실제 실행 명령:

```bash
python3 Tools/run_unity_validation.py \
  --editor /workspace/scratch/73110fac84d1/unity-install/Editor/Unity \
  --mode import --timeout 110
```

`Validation/NativeUnity/import-editor.log`와 `import-result.json`에 실패를 보존했다.
핵심 메시지는 `Licensing initialization failed`, `Could not connect to IPC stream "Upm-15" after 30.0 seconds`, `Failed to start the Unity Package Manager local server process`다.
독립 검사에서도 AF_UNIX 소켓 생성 자체가 errno 1 `Operation not permitted`로 거절됐다.
라이선스도 활성화되지 않았다. 정상 라이선스만 추가한다고 이 호스트의 IPC 제한까지 해결되는 것은 아니다.

에디터 번들 안의 UGUI 2.0.0, URP 17.3.0, Test Framework 1.6.0 파일은 확인했다. 이는 프로젝트의 패키지 복원 완료를 의미하지 않는다. 2D Animation 13.0.6은 외부 복원이 필요하며 lockfile은 생성되지 않았다.
현재 .unity 씬, GraphicsSettings/PlayerSettings/EditorBuildSettings 자산은 생성되지 않았다.

## 이번에 작성한 실행 경로

- `Tools/install_unity_editor.py`: 공식 버전 고정 다운로드, 크기/해시 확인, Linux 압축 해제, 버전 실행 확인. 라이선스 우회나 자동 계정 로그인 없음.
- `Tools/run_unity_validation.py`: 실제 Editor import/configure/playmode/android 호출, 로그·종료코드·신규 산출물 검사. 실패를 성공으로 처리하지 않는다.
- `Assets/Editor/BuildAutomation.cs`: `Mongle/Build/Configure Development Project`, `Mongle/Build/Build Development APK` 메뉴와 CI executeMethod.
- `Assets/Tests/PlayMode/HatchUiPlayModeTests.cs`: 등급 5종 UI 컬러/오라 상태와 실제 프레임 캡처, 마우스/터치 PointerEventData를 통한 툴팁 이벤트 검사. 하드웨어 터치·레이캐스트·서버 구매부터 사운드까지의 전체 흐름 검사와는 범위가 다르다.
- `.github/workflows/validate.yml`: push/PR의 Mono·HTTPS·DB fixture 검사, 수동 실행의 네이티브 Unity 검사와 선택적 Development APK. 원격 실행 기록은 없다.

## 실행한 검사와 범위

이번 변경에서 순수 C# EconomyV2 55개 검사 및 1천만 구간 알 확률 분할, ClientEconomy 23개, HatchPresentation 33개, Slice/ViewportLocal 거래·부화·서버 경계 검사를 재실행해 통과했다. Python 구문, JSON/asmdef, YAML 구조 및 소스 구분자 검사도 통과했다.
기존 로컬 HTTPS 18개 및 마이그레이션 fixture 검사 기록은 유지했다. 이번 변경에서 해당 검사를 다시 실행한 것으로 표기하지 않는다.
Mono 검사는 새 BuildAutomation/PlayMode 테스트를 Unity API로 컴파일했다는 증거가 아니다.
기존 VALIDATION.md의 Unity 어셈블리 컴파일 기록은 과거 소스 범위이며 현재 전체 프로젝트의 통과 기록으로 사용하지 않는다.

## 정상 환경에서 이어 실행하는 순서

1. AF_UNIX IPC와 패키지 서버 접근이 허용되고 정식 라이선스가 활성화된 Linux Unity 6000.3.26f1 러너를 준비한다. 화면 검사는 DISPLAY/그래픽 지원이 필요하다.
2. 저장소 루트에 이 프로젝트 내용을 배치한다. GitHub Actions self-hosted 라벨은 `self-hosted, linux, x64, unity-6000.3.26f1`, 저장소 변수 `UNITY_NATIVE_RUNNER_ENABLED=true`, `UNITY_EDITOR_PATH`는 실행 파일 경로로 설정한다. 자격 증명은 워크플로 파일에 넣지 않는다.
3. 로컬에서는 아래 모드를 순차 실행한다. configure가 성공한 뒤 생성 씬을 열어 알 구매→인벤토리→준비된 알 부화→응답→깨짐/소리/팝업을 직접 확인한다. HTTPS 서버 주소와 테스트 인증 세션은 별도 구성한다.

```bash
python3 Tools/run_unity_validation.py --editor /path/to/Editor/Unity --mode import
python3 Tools/run_unity_validation.py --editor /path/to/Editor/Unity --mode configure
python3 Tools/run_unity_validation.py --editor /path/to/Editor/Unity --mode playmode --timeout 1200
python3 Tools/run_unity_validation.py --editor /path/to/Editor/Unity --mode android --timeout 2400
```

Android Build Support, SDK/NDK/OpenJDK 설치가 마지막 단계의 선행 조건이다. APK 경로는 `Builds/Android/Neulgui-development.apk`. 생성 후 실기기 설치·터치·네트워크·소리 검사 결과를 별도로 남긴다.
운영 DB는 제공자/스키마를 확인한 뒤 해당 DB용 트랜잭션·백업 방식으로 적용해야 한다. 현재 SQLite 도구를 MySQL/PostgreSQL 운영 DB 도구처럼 사용하면 안 된다. 비밀번호를 문서나 채팅에 쓰지 않고 배포 환경의 비밀 설정으로 주입한다.

## 최종 판정

전체 완료 아님. Unity 설치 및 재현 가능한 로컬 실행 시도까지 완료했고, 실제 import는 호스트 IPC/라이선스 단계에서 막혔다. 정상 네이티브 러너, 운영 DB 대상, 원격 저장소·CI 러너, Android 빌드 모듈·실기기가 남은 실행 조건이다. 완성된 통합 늘귀 아기/성체 리그·클립과 상용 수준 비주얼 품질 역시 별도 제작·화면 검증이 필요하다.
