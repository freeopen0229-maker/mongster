# Unity 라이선스·IPC 복구 시도 결과

2026-10-23 UTC. 전체 작업 상태: 차단 지속. Unity 재설치 완료 기록과 별개로 프로젝트 복원/컴파일 완료 아님.

## 이번 요청에서 실제 실행한 사항

1. 설치된 Unity 6000.3.26f1, Hub/라이선스 파일 존재 여부 및 실행 프로세스를 검사했다. Hub는 설치되지 않았고, 검사한 기본 경로에 .ulf가 없으며 라이선스 관련 환경 변수도 없었다. 유효한 활성 라이선스는 확인되지 않았다. 단순 파일 부재만으로 계정의 라이선스 보유 여부를 판단하지 않는다.
2. 새 임시 경로의 AF_UNIX 소켓 생성/바인딩/연결 검사를 실행했다. 소켓 생성 단계에서 errno 1 `Operation not permitted`로 실패했다. AF_INET 소켓 생성은 허용됐다. 프로젝트 파일 쓰기는 성공했다. 기존 Unity 소켓 이름을 재사용하지 않은 검사이므로 오래된 소켓 파일 충돌을 원인으로 보기 어렵다.
3. `/proc/self/status`의 NoNewPrivs=1, Seccomp=2, Seccomp_filters=1을 확인했다. 호스트 보안 정책의 차단으로 판단되지만, 설치된 필터 규칙을 직접 열람한 것은 아니므로 특정 seccomp 규칙이 원인이라고 확정하지 않는다. 단순 원격 방화벽 문제만으로는 로컬 소켓 생성 거절을 설명할 수 없다. 보안 정책 우회/해제는 수행하지 않았다.
4. XDG_DATA_HOME/CONFIG_HOME/CACHE_HOME과 TMPDIR을 작업 폴더 내 쓰기 가능한 경로로 지정해 실제 Editor import를 다시 실행했다. 기존 preferences folder 생성 오류는 사라졌고 Unity 설정/캐시 디렉터리가 정상 생성됐다. HOME은 변경하지 않았다.
5. 실제 재시도는 약 30초 뒤 종료 코드 1. 라이선스 초기화 실패 및 `Upm-6` IPC 연결 타임아웃이 유지됐다. `Validation/NativeUnity/import-result.json`의 completionEvidence=false, apkExists=false다. 종료 후 프로세스 이름 검사에서 Unity/LicenseClient 잔존 프로세스는 없었다. 민감한 프로세스 명령줄은 수집하지 않았다.

## 추가한 코드

`Tools/unity_preflight.py`: Editor 존재, 프로젝트 쓰기, 새 AF_UNIX 소켓의 실제 송수신 가능 여부 검사. 라이선스는 별도로 검증해야 한다고 명시한다. 설정·방화벽·프로세스를 변경하지 않는다.
`Tools/run_unity_validation.py`: 사전 검사 실패 시 실제 Editor 실행 전 종료 코드 1을 반환하고 `<mode>-preflight-blocked.json`을 저장한다. 이전 실제 Editor 로그와 import-result.json은 덮어쓰지 않는다.
`.github/workflows/validate.yml`: native Unity job의 import 이전에 사전 검사 단계 추가. 워크플로는 로컬 파일 상태이며 원격 등록·러너 실행은 이뤄지지 않았다.

사전 검사 통과는 라이선스 활성화/패키지 복원/컴파일 통과를 의미하지 않는다.

## 단계별 최종 결과

| 단계 | 결과 |
|---|---|
| 라이선스 재인증 | 미실행: Hub·인증 세션 없음. 사용자 계정 비밀번호를 수집하지 않음 |
| 설정/임시 경로 권한 | XDG 경로 지정으로 preferences 생성 오류 해소 |
| IPC 복구 | 실패: 호스트가 AF_UNIX 소켓 생성을 거절 |
| 프로젝트 로드·UPM·C# | 실제 재시도 실패. 복원 및 Unity 컴파일 통과 기록 없음 |
| URP/씬/PlayerSettings | 자동 설정 소스 유지. 실행/자산 적용 안 됨 |
| Play Mode·연출·툴팁 | 실행 안 됨. 화면·음향·터치 검증 결과 없음 |
| Development APK | 생성 안 됨 |
| CI 등록 | 사전 검사 설정 추가. 원격 저장소/러너 미제공으로 등록·실행 안 됨 |

## 재개에 필요한 실제 환경

AF_UNIX IPC가 허용되는 Unity 호스트와 정상 활성 라이선스가 필요하다. 이 호스트 내부의 스크립트 수정만으로 현재 소켓 생성 제한을 해제할 수 있는 지원된 수단은 확인되지 않았다. Personal 활성화는 공식 Unity Hub 로그인 경로를 사용해야 한다. Pro는 보유 라이선스 유형에 맞는 공식 활성화/라이선스 서버 절차를 사용한다. 라이선스 파일을 조작하거나 인증을 생략하지 않는다.

공식 근거:
- https://docs.unity.com/en-us/engine/6000.3/manual/get-started/install-and-upgrade/licenses-and-activation/managing-your-unity-license
- https://docs.unity.com/en-us/engine/6000.0/manual/get-started/install-and-upgrade/licenses-and-activation/license-activation-methods

정상 환경에서 `python3 Tools/unity_preflight.py --editor /path/to/Editor/Unity`를 통과한 뒤 import→configure→playmode→android를 순차 실행한다. Android Build Support/SDK/NDK/JDK, 실제 그래픽 화면, 연결 기기 및 원격 저장소/러너는 각각 별도 선행 조건이다. 이전 보고서 `UNITY_BUILD_EXECUTION_REPORT.md`에 실행 명령과 설정이 있다.
