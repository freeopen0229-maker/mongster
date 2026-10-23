# Unity 퓨전 다마고치 RPG · 세 핵심 시스템 구현 가이드

대상: Unity 6000.3.26f1, Built-in Render Pipeline, Android/Windows. 기존 늘귀/VFX 프로젝트에 기반 시스템을 추가했다. 코드에는 각 컴포넌트의 책임·이벤트 순서·시간 단위·예외 처리 주석이 있다.

**확인 범위:** 실제 Unity API를 참조한 에디터/플레이어 C# 컴파일과 순수 로직 검사를 수행했다. 이 환경은 Unity 라이선스 초기화·Package Manager의 IPC 연결에 실패하므로 씬 생성/Play Mode/셰이더/터치 실기 검사는 아직 실행하지 못했다. 컴파일 결과와 시각 품질은 별개의 판정이다.

## 1. 가장 빠른 테스트 방법

1. 압축을 풀고 Unity Hub에서 `NeulguiUnity` 폴더를 등록한다. 지정 에디터 버전으로 연다.
2. **Mongle → 12 Create Gameplay Foundation Scene**을 선택한다. `Assets/Scenes/GameplayFoundation.unity`와 Animator/클립/PanelSettings를 구성하도록 작성했다.
3. **Mongle → 13 Validate Gameplay Foundation**으로 참조와 공격 이벤트를 검사한 뒤 Play를 누른다.
4. 좌측 조이스틱 또는 WASD/방향키로 움직이고, 우측 ATTACK 버튼으로 앞쪽 표적을 공격한다. 실제 근접 공격이므로 먼저 표적 가까이 이동해야 한다.
5. Food (5c)로 보상 코인을 먹이로 바꾸고 Feed/Pet/Play로 욕구·친밀도·공격력·반응 애니메이션을 확인한다. Claim idle은 적립분만 한 번 지급한다.
6. Android는 Build Support/SDK/NDK/JDK 설치 후 **16 Build Foundation Android**, Windows는 **15 Build Foundation Windows**를 실행한다. 현재 APK/EXE는 제공하지 않는다.

검증 씬의 캐릭터는 움직임/본/입력을 검사하는 캡슐 대체 모델이다. 새 고품질 펫 모델이나 완성된 보행 작화를 제공한다는 의미는 아니다. 기존 늘귀 원화와 단독 리그는 원래 씬에 유지된다.

Unity Project Settings → Player → Active Input Handling은 **Input Manager (Old)** 또는 **Both**를 사용한다. 이 프로젝트는 별도 Input System/UGUI 패키지 없이 UI Toolkit 포인터 입력을 사용한다. `com.unity.modules.uielements`, physics, animation, audio 모듈은 manifest에 포함되어 있다.

## 2. 스크립트 구조

| 파일 | 역할 |
|---|---|
| `Assets/Scripts/Gameplay/MobilePetJoystick.cs` | 한 손가락 캡처, 다른 손가락 공격 허용, 원형 ClampMagnitude, 데드존, 취소/포커스/일시정지 시 입력 해제 |
| `PetMotor.cs` | 카메라 기준 이동, Lerp 가감속, Rigidbody 물리, Slerp 시각 회전 |
| `PetAnimatorBridge.cs` | 감쇠하는 MoveSpeed, 액션 CrossFade, 돌봄 반응, 액션 잠금과 종료 방어 |
| `PetAttackAnimationRelay.cs` | 클립 AnimationEvent의 단일 진입점 |
| `PetAttackController.cs` | 같은 이벤트의 최종 본 포즈로 Trail·파티클·히트박스 실행, 중복 명중 차단 |
| `PetState.cs` | 저장 데이터와 순수 시간/돌봄/능력치/방치·능동 보상 규칙 |
| `PetRuntimeManager.cs` | UTC 갱신, 변경 이벤트, 저장/불러오기/백업 |
| `PetGameplayHud.cs` | 조이스틱·돌봄·공격·방치 보상·능력치 화면 |
| `Assets/Scripts/HitStopState.cs`, `CombatFeedback.cs` | 겹치는 히트스톱과 원래 시간 배율 복원, DSP 음향 |
| `VfxPresentation.cs` | Perlin Noise 카메라 셰이크, 감쇠, Built-in Bloom |
| `Assets/Editor/GameplayFoundationProject.cs` | 씬/Animator/이벤트/UI 자동 구성·검사·빌드 메뉴 |

## 3. 조작과 애니메이션 연결

이동 경로는 `Pointer 이벤트 → MobilePetJoystick.Value → PetMotor.Update → PetMotor.FixedUpdate`다.

- UI Toolkit은 화면 아래가 +Y다. 조이스틱 입력의 Y를 이동 코드에서 반전한다.
- `Vector2.ClampMagnitude(input, 1)`로 대각선 속도를 제한한다. 데드존 안은 0, 바깥은 0~1로 다시 매핑한다.
- 한 pointerId만 소유한다. 조이스틱 밖으로 드래그해도 CapturePointer로 계속 추적하고, 다른 손가락은 공격 버튼을 누를 수 있다.
- 물리 이동은 Rigidbody의 XZ 선속도를 FixedUpdate에서 설정한다. Y 속도는 중력과 바닥 충돌에 맡긴다. Interpolate와 ContinuousDynamic을 설정했다.
- 회전은 별도 `visualRoot`에 Slerp를 적용한다. Rigidbody 캡슐의 기울기는 잠근다.

주요 보간은 다음 형태다. 계수가 고정된 `Lerp(x,y,0.1f)`와 달리 프레임 간격을 고려한다.

```csharp
float blend = 1f - Mathf.Exp(-rate * Time.fixedDeltaTime);
smoothedVelocity.x = Mathf.Lerp(smoothedVelocity.x, desired.x, blend);
smoothedVelocity.z = Mathf.Lerp(smoothedVelocity.z, desired.z, blend);

float turnBlend = 1f - Mathf.Exp(-turnSharpness * Time.deltaTime);
visualRoot.rotation = Quaternion.Slerp(
    visualRoot.rotation,
    Quaternion.LookRotation(desiredDirection, Vector3.up),
    turnBlend);
```

Animator 기본 상태는 `Locomotion`이다. Float 파라미터 `MoveSpeed`를 0~1로 전달하며 Idle/Walk/Run BlendTree의 기준은 0 / 0.4 / 1이다.

```csharp
animator.SetFloat("MoveSpeed", normalizedSpeed, 0.12f, Time.deltaTime);
```

Feed/Pet/Play/Attack은 별도 상태다. `CrossFadeInFixedTime`은 액션 진입과 복귀에 한 번씩 호출한다. 매 프레임 CrossFade를 호출해 클립이 되감기는 구조를 피했다. 액션 중에는 조이스틱 값이 유지되어도 물리 목표 속도를 0으로 감속한다.

실제 펫에 적용하려면 테스트 클립을 해당 모델의 Idle/Walk/Run/돌봄/Attack 클립으로 교체한다. 이 구조만으로 발 디딤·관절·체중 이동이 자동 완성되지는 않는다. Root Motion은 끄고 보행 클립 속도를 이동 속도와 맞춘 뒤 필요한 발 IK를 추가한다.

## 4. 공격 이벤트와 타격감

생성되는 Attack 클립에는 다음 표식이 들어 있다.

| 클립 시간 | 함수 | 책임 |
|---|---|---|
| 0.04초 | `AttackStart` | 준비 이펙트 |
| 0.22초 | `DamageWindowOpen` | 타격 구간 열기 예약 |
| 0.42초 | `DamageWindowClose` | 타격 구간 닫기 예약 |
| 0.72초 | `AttackEnd` | 판정 종료·Locomotion 복귀 |

`PetAttackAnimationRelay`는 **Animator와 같은 GameObject**에 붙여야 한다. 다른 모델의 클립을 넣어도 이 함수 이름과 수신 컴포넌트를 유지한다. 프레임 번호는 클립 FPS가 달라지면 바뀌므로 실제 접촉 포즈를 보고 표식을 옮긴다.

이벤트가 발생하면 표식만 기록하고, 본 Transform이 적용된 **같은 프레임의 LateUpdate**에서 Trail 시작·입자·첫 히트박스 검사를 함께 처리한다. 이전 프레임 무기 위치를 읽는 문제를 줄이는 구조다. 별도 Invoke/WaitForSeconds로 판정과 이펙트 시간을 각각 복제하지 않는다.

판정은 대상 Layer 8에 대해 `OverlapSphereNonAlloc`과 이전 무기 위치에서 현재 위치까지의 `SphereCastNonAlloc`을 사용한다. 동일 공격 ID와 대상 HashSet으로 콜라이더가 여러 개거나 여러 프레임 겹쳐도 한 번만 피해를 준다. 적 체력이 실제로 0이 되는 한 번의 명중에서만 능동 보상을 지급한다.

```text
AnimationEvent
  → relay
  → Pending marker
  → LateUpdate의 최종 무기 위치
  → Trail + Particle + Hitbox
  → 명중 확인
  → Damage + Flash + Knockback + Hit stop + Shake + Sound
```

프레임 기반 엔진의 디스플레이·물리·오디오 장치까지 절대 0초 오차를 보장할 수는 없다. 이 구현은 같은 이벤트 원천으로 논리적 시점이 따로 흐르지 않게 한다. 실제 기기에서는 30/60fps와 느린 프레임에서 판정 누락·최종 영상·소리를 확인해야 한다.

히트스톱은 `HitStopState`가 0.05초의 **비배율 시간**을 추적하고 `CombatFeedback`이 Time.timeScale을 0으로 설정한다. 중첩된 명중은 정지 종료를 연장하며 원래 값이 0.5였다면 0.5로 돌려놓는다. 비활성화/파괴 시에도 복원한다. 다른 일시정지 관리자를 추가할 때는 글로벌 시간 배율 소유권을 통합해야 한다.

카메라는 Perlin Noise 진동을 trauma²로 조절하고 SmoothDamp와 감쇠를 적용한다. 비배율 시간을 사용해 히트스톱 중에도 충격 반응이 보인다. 실행 순서를 500으로 두어 공격 LateUpdate 이후 셰이크를 계산한다.

충격음은 미리 메모리에 올린 소리를 다음 DSP 버퍼에 예약한다. +10ms 파열음, +20ms 잔향은 동일 DSP 시계의 오프셋이다. 물리적 스피커 출력 지연이 10ms 이하라는 보증은 아니다.

## 5. 육성·능력치·방치 보상

시간 로직은 Unity 객체와 분리된 `PetRules`에 있어 앱을 닫은 시간과 온라인 시간을 같은 규칙으로 처리한다.

| 항목 | 예제 기본값 |
|---|---|
| 포만감 감소 | 시간당 3 |
| 친밀도 감소 | 시간당 0.2 |
| 피로 회복 | 시간당 4 |
| 먹이 구매 | 5코인 → 음식 1개 |
| 먹이 | 포만감 +25, 친밀도 +1, 음식 1개 소비, 30초 쿨다운 |
| 쓰다듬기 | 친밀도 +2, 10초 쿨다운 |
| 놀기 | 친밀도 +4, 피로 +6, 포만감 -3, XP +8, 60초 쿨다운 |
| 공격 | 피로 +0.6, 포만감 -0.15, 피로 98 이상이면 불가 |
| 적 처치 | 8코인, 12 XP, 친밀도 +0.4 |
| 방치 보상 | 5분/1코인, 10분/1 XP, 미수령 적립은 최대 8시간 |
| 돌봄/처치 보너스 | 이후 2시간 방치 보상 50% 증가, 기간은 무한 가산하지 않음 |

컨디션은 포만감·친밀도·피로를 합쳐 계산한다. `data.Stats.attack`, `moveSpeed`, `maxHealth`로 전달하며 공격은 시작 때 최신 공격력을 읽고 이동은 매 프레임 최신 속도를 읽는다. maxHealth는 전투 체력 컴포넌트를 확장할 때 사용할 계산 값이다. 예제에는 플레이어 피격 AI/체력 루프까지 구현하지 않았다.

생활 시간은 UTC, 이동/애니메이션은 게임 시간이다. 히트스톱이 생활 시간이나 방치 보상을 멈추지 않는다. `lastUtc`까지의 경과를 한 번 소비하고 앞으로만 갱신한다. 보상 수령은 적립분을 지우므로 동일 시각에 반복 수령해도 다시 지급되지 않는다. 시간 분할에 따라 보상량이 달라지지 않는지도 검사한다.

저장은 `Application.persistentDataPath/mongle-pet-core-v1.json`이다. 기존 웹 저장과 분리되어 있다. 임시 파일을 쓴 뒤 Replace/Move하고 이전 파일을 `.bak`으로 둔다. 읽기에 실패하면 백업을 검사하고 실패 원인을 경고한다. UTC와 로컬 JSON만으로 기기 시간 조작이나 저장 편집을 방지할 수 없으므로 실제 온라인 경제의 기준 시각/보상은 서버 권한으로 확장해야 한다.

현재 매니저는 **한 마리 개체**를 소유한다. 126종 계정 도감/알/서버 거래 전체를 이전한 시스템이라는 의미는 아니다. 여러 개체로 확장할 때는 PetState에 고유 ID를 추가하고 계정 저장 컨테이너가 개체 목록과 재화를 소유하게 한다.

## 6. 검사와 실제 기기 확인

`Tools/verify_with_unity_assemblies.py <Unity Editor 폴더>`는 공식 Unity 어셈블리로 에디터/플레이어용 코드를 따로 컴파일하고 관리 코드를 실행한다.

- 다리 수학 40,000 포즈, 히트스톱 시간·중첩·복원.
- 시간 경과를 한 번에/여러 번 처리한 결과 일치.
- 방치 보상 8시간 상한, 재수령, UTC 역행 시 중복 방지.
- 음식 비용, 액션 쿨다운, 돌봄 후 실제 전투 수치 상승.
- 처치 보상과 방치 보너스 연결, 잘못된 저장 값 거부.

Play Mode에서는 다음을 직접 확인해야 한다.

1. 조이스틱을 작게/크게/대각선으로 움직여 Idle/Walk/Run과 회전·충돌을 확인한다.
2. 이동 중 다른 손가락으로 공격하고, 화면 밖 드래그/알림창/앱 전환 후 입력이 남지 않는지 확인한다.
3. 같은 표적에 한 공격이 여러 번 들어가지 않는지, 명중 순간 Trail·발광·셰이크가 맞는지 확인한다.
4. 돌봄 버튼의 애니메이션과 수치 변화, 쿨다운 안내를 확인한다.
5. 앱 종료 후 재접속·보상 수령·재수령을 확인한다. 로컬 시계 테스트는 테스트 기기에서 수행한다.
6. 실제 펫 클립, 셰이더, 모바일 FPS/터치 지연/사운드 출력은 실기 프로파일링으로 판정한다.

이 자료는 요청한 세 핵심 시스템의 실행 가능한 기반 소스다. 모바일 스토어 출시 품질을 확인한 완제품이라는 의미는 아니다.

## 공식 API 참고

- Animator.SetFloat 감쇠 인자: https://docs.unity.com/ko-kr/engine/6000.3/script-reference/unityengine/animator/setfloat
- UI Toolkit 포인터 캡처: https://docs.unity3d.com/kr/current/Manual/UIE-capture-the-pointer.html
- Rigidbody: https://docs.unity.com/en-us/engine/6000.0/script-reference/unityengine/rigidbody

전체 구현 코드는 프로젝트의 `Assets/Scripts/Gameplay`와 공용 VFX/피드백 스크립트에 포함되어 있다. 가이드의 짧은 예제만 복사하기보다 전체 파일을 함께 가져와 참조를 연결한다.
