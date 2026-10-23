> Current offline Roslyn results: Validation/Roslyn/results.json and Docs/GAMECI_AND_SERVER_FINAL_REPORT.md. This is not native Unity/UPM/asmdef/Play Mode validation. The historical records below do not override current execution limits.

> Historical validation scope only. Current full project has NOT passed native Unity import/compilation. See Docs/UNITY_BUILD_EXECUTION_REPORT.md for the 2026-10-23 actual failed import and current status.

# 검증 기록 · 2026-10-22

| 항목 | 결과 | 범위 |
|---|---|---|
| 공식 Unity 에디터 아카이브 | 다운로드 및 공식 MD5 일치 | 6000.3.26f1 |
| 에디터 프로세스 시작 | 확인 | 실제 Editor 버전 로그 확인 |
| Unity 씬 생성·Play Mode | 차단 | 라이선스 초기화 IPC 및 Package Manager IPC 실패 |
| 에디터 대상 C# | 통과 | Unity 번들 C# 컴파일러와 실제 API 어셈블리 |
| 플레이어 대상 C# | 통과 | UNITY_EDITOR 코드 제외, 실제 API 어셈블리 |
| 다리 길이 계산 | 통과 | 생산 코드 40,000 포즈, 양방향 굽힘 |
| 히트스톱 시계 | 통과 | 0.05초, 겹친 요청 연장, 원래 배율 복원, 종료 복원 |
| 부위 PNG·마법진·CC0 리소스 | 파일 검사 통과 | 실제 시각 품질 평가는 미완료 |
| 셰이더 컴파일·파티클·충돌·음향·영상 | 미검증 | Unity 씬 재생 필요 |
| Windows/Android 빌드·실기 성능 | 미검증 | 해당 플랫폼 환경 필요 |

관리 코드 검사 원본 결과는 `Validation/Managed/results.json`, 에디터 시작 실패 근거는 `Validation/EngineAttempt.txt`에 있다. `Tools/verify_with_unity_assemblies.py`는 실제 설치된 Unity 어셈블리를 대상으로 컴파일과 관리 코드 검사를 재현한다.

## 수정한 문제

1. 정지 중 발 정리가 걷기 재개 후 남을 수 있어 이동 때 정리 상태를 초기화했다.
2. 보행 클립의 위상과 발 계산이 별개로 진행되어, 안정된 Walk 상태에서 동일 위상으로 맞췄다.
3. 발 고정 수치가 목표 좌표만 재확인하던 문제를 수정했다. 실제 IK 발목 좌표로 지지 중 이동량을 측정한다.
4. 기본 이동을 게임 시간에 연결해 히트스톱 때 펫도 멈춘다.
5. UI 배율을 입력 차단 영역 계산에 반영했다.
6. Unity 6.3에서 삭제된 AudioImporter.preloadAudioData 사용을 SampleSettings의 설정으로 바꾸고 재컴파일했다.
7. 발사체가 타격 직전 수명 종료되어 Trail이 잘릴 수 있어, 타격 예약 중 자동 소멸을 멈추고 타격 후 꼬리를 남긴다.
8. 씬별 빌드 메뉴를 분리해 VFX 빌드가 늘귀 단독 씬으로 바뀌지 않게 했다.

## 합격을 보류한 항목

자연스러운 펫 동작과 화려한 VFX의 최종 품질은 아직 입증하지 못했다. 실제 Unity 화면, 느린 재생, 음향 청취, 저사양 기기 성능 검사가 끝나야 합격 판단할 수 있다. 현재 결과를 그 수준의 완성 또는 검증 완료로 표현하지 않는다.

## 세 핵심 시스템 추가 검사

에디터 및 플레이어 대상 컴파일 입력에 `Assets/Scripts/Gameplay` 하위 폴더를 포함했다. `CoreLoopProbe`에서 시간 분할 불변성, 욕구 감소, 오프라인 적립 상한, 재수령, UTC 역행, 비용·쿨다운, 최신 전투 능력치, 처치 보상·방치 보너스·먹이 구매의 연결을 검사했다. 결과는 `Validation/Managed/results.json`에 기록했다.

새 기능 검증 씬의 실제 생성/재생은 아직 수행하지 못했다. UI Toolkit의 실기 터치, 물리 충돌과 애니메이션 이벤트, 저장 파일 교체의 플랫폼 동작은 Unity Play Mode 및 대상 기기 검사가 남아 있다.
