# 늘귀 통합 세컨더리 모션 / Run 연결 가이드

작성: 2026-10-23. 대상: 기존 Unity 6000.3 URP 프로젝트.

## 이번 변경의 실제 범위

PetSecondaryMotion.cs를 확장했다. PetBlinkClock.cs는 신규 의존 파일이다.
기존 PetSpringMath.cs도 함께 필요하다. namespace는 Mongle.VisualRenewal.
PetVisualManager는 각 컴포넌트의 스프링/호흡/몸통 오프셋 중복 소유를 검사한다.
기존 BindSources, ValidateSetup, ResetMotion, Evaluate와 joints 필드는 유지했다.
기존 프리팹에서 새 필드가 자동 연결되지는 않는다.

Mono에서 눈 시간표와 기존 스프링 계산을 실행해 검증했다. Unity API 재컴파일,
Play Mode, 실제 늘귀 리그, Animator 전환, 시각적 자연스러움은 검증하지 못했다.
이 코드는 완성된 펫/영상이 아니며, 아래 리그와 클립을 연결해야 보인다.

## 1. 통합 리그 계층과 연결

부모/자식 연결 표. 본체용 SpriteRenderer와 SpriteSkin은 각각 하나다.

| 오브젝트 | 부모 | 역할 |
|---|---|---|
| PetRoot | 월드 | Rigidbody/Collider/이동. VisualManager의 motionRoot |
| FacingRoot | PetRoot | 방향. VisualManager의 turnRoot |
| RigContainer | FacingRoot | PetSecondaryMotion 부착 위치 |
| VisualOffset | RigContainer | 스크립트 전용 보빙/기울기. 클립에 키를 만들지 않는다 |
| ArtRig | VisualOffset | Animator, 통합 SpriteRenderer, SpriteSkin |
| SkeletonRoot | ArtRig | SpriteSkin의 skeleton root |
| Pelvis | SkeletonRoot | 몸통 중심 |
| Chest | Pelvis | 상체, 호흡 본 |
| UpperArm_L/R | Chest | 팔, 작은 호흡 위상차 |
| Head | Chest | 머리 |
| EarRoot_L/R | Head | 귀 부착점. 위치 보정 금지 |
| EarTip_L/R | 각 EarRoot | 스프링/트위치 회전 대상 |
| TailRoot | Pelvis | 꼬리 부착점. 위치 보정 금지 |
| TailMid | TailRoot | 꼬리 중간 회전 대상 |
| TailTip | TailMid | 꼬리 끝 회전 대상 |
| Lid_L/R | Head | 눈꺼풀 메시 가중치를 가진 전용 본 |
| Sockets | 적절한 본 | 코스튬/오라. 몸을 따라가야 할 소켓은 해당 본 자식 |

꼬리 텍스처를 별도 월드 오브젝트로 이동시키지 않는다. 꼬리 기부 정점은 Pelvis와
TailRoot에 연속적인 가중치를 주고, 그 다음 정점은 TailMid/Tip으로 부드럽게 넘긴다.
본 연결만으로 잘못된 메시 가중치가 고쳐지지는 않는다. 회전 최대값에서 찢어짐을 확인한다.

Inspector: animator=ArtRig의 Animator, visualOffset=VisualOffset.
관리자는 BindSources로 외부 이동/회전 root를 연결한다. 독립 사용 시 직접 할당한다.
몸통/팔은 breathBones, 귀 끝/꼬리 중간·끝은 joints에 등록한다. 같은 본 중복 등록 금지.
visualOffset은 컴포넌트 자기 자신이나 이동/회전 root가 될 수 없다.

## 2. Animator: 매 프레임 상태를 재시작하지 않는 Blend Tree

기존 PetAnimatorBridge.SetMovement를 MoveSpeed의 유일한 작성자로 유지한다.
PetMotor는 이미 실제 Rigidbody 수평 속도 / 최대 속도로 이 함수를 호출한다.
조이스틱은 기존 Vector2.ClampMagnitude와 가속/감속 로직을 사용한다.
따라서 벽에 막혀 실제 속도가 0이면 Run 입력을 계속 밀어도 Idle로 내려간다.

| 항목 | 타입/설정 | 값 |
|---|---|---|
| MoveSpeed | Float | 0~1, 기존 SetFloat dampTime=0.12초 |
| EyeClose | Float | 0 열린 눈, 1 감은 눈. 스크립트가 시간표를 작성 |
| Base Layer 기본 State | Locomotion | 1D Blend Tree, 파라미터 MoveSpeed |
| Idle | Blend Tree threshold | 0 |
| Walk | Blend Tree threshold | 0.45 |
| Run | Blend Tree threshold | 1 |
| Eyes Layer | Override, weight=1 | 눈꺼풀 본만 포함하는 Transform AvatarMask |
| Eyes Layer 기본 State | BlinkPose | 1D Blend Tree, 파라미터 EyeClose |
| EyesOpen / EyesClosed | threshold | 0 / 1 |

눈 클립 2개는 눈꺼풀 본의 열린/닫힌 포즈를 각각 일정하게 유지하는 클립이다.
Eyes Mask에 몸통/Renderer/skeleton 전체를 포함하지 않는다. 본체 전체 이미지 교체나
전체 스케일 축소로 눈을 감기지 않는다. 눈꺼풀 메시/본이 없는 원화는 이 float만으로
깜빡이지 않는다. Base Layer 및 액션 클립에서 눈꺼풀 트랙을 제외한다.

Animator Update Mode=Normal, Apply Root Motion=off. 보이는 동안 Always Animate 권장.
Idle/Walk/Run은 같은 skeleton/path를 사용하고 Loop Time을 켠다.
Locomotion 내부 전환은 Blend Tree가 하므로 Idle/Walk/Run 간 별도 transition은 필요 없다.
기존 Attack/Feed/Pet/Play 진입은 Bridge의 CrossFadeInFixedTime 유지.
액션 종료 AnimationEvent에서 CompleteAction 호출. CrossFade/Play를 매 프레임 호출하지 않는다.
Controller 교체 시 secondary.RefreshAnimatorBindings()를 호출한다.
Missing Float는 스크립트가 쓰지 않으므로 경고를 반복하지 않지만, 효과도 나타나지 않는다.

## 3. Run 보빙/기울기와 주기 동기화

기본 runStart=0.5, fullRun=0.85. 실제 MoveSpeed가 그 사이면 Run 가산값을 부드럽게 증가시킨다.
최대 높이=0.035 Unity unit, 최대 기울기=12도. 펫 높이를 약 1 unit로 정규화한 시작값이다.
그림 크기와 무관하게 수치를 고정하지 말고, 펫 키의 약 2~4% 높이에서 조절한다.

Locomotion normalizedTime의 소수부를 stride로 사용한다.
한 주기에서 좌/우 발 접지점을 0/0.5로 맞추면 다음 식은 접지에서 0, 공중에서 최대가 된다.

```csharp
float stride = state.normalizedTime - Mathf.Floor(state.normalizedTime);
float height01 = .5f - .5f * Mathf.Cos(stride * Mathf.PI * 4);
// 전용 visualOffset만 올린다. Rigidbody/Collider를 흔들지 않는다.
```

Walk/Run 클립은 같은 발 접지 위상으로 제작한다. 보빙이 이미 들어간 클립은
runBobHeight=0으로 두거나 클립의 root 상하 이동을 제거해 이중 보빙을 피한다.
이 절차 없이 코딩 보빙만 추가하면 발 미끄러짐/바닥 관통이 남는다.
Run의 발/척추/체중 이동은 실제 클립이 담당한다. 이 스크립트가 발 동작을 합성하지 않는다.
기본 runLeanDegrees=12는 XY 옆모습용이며 오른쪽 facingSign=1, 왼쪽=-1이다.

```csharp
// 기존 이동 컨트롤러에서 설정하는 연결 예시. 실제 참조/접지 판정에 맞춰 사용.
secondary.facingSign = lookingRight ? 1f : -1f;
secondary.grounded = isGrounded;
secondary.suppressLocomotion = animationBridge.ActionBusy;
```

3D XZ 평면 이동의 기존 PetMotor는 FacingRoot를 Y축 회전시킨다. XY 옆모습의
Z축 기울기를 그대로 적용하지 말고 leanAxisLocal을 리그의 전후 기울기 축으로 바꾸고
facingSign은 리그의 정면 기준으로 고정한다. Y회전으로 단면 Sprite가 옆면에서
사라지는 문제는 이 스크립트의 해결 범위가 아니다. 카메라 방향별 리그/방향 원화 또는
billboard 구성을 별도 선택해야 한다. 현재 Viewport는 이 결정을 검증하지 못했다.

## 4. 눈 / 스프링 / 트위치 / 호흡

- 눈: 깜빡임 묶음이 끝난 뒤 2.5~6초 대기. 묶음마다 15% 확률로 두 번 깜빡임.
  닫기 0.045초, 유지 0.035초, 열기 0.08초, 연속 깜빡임 간격 0.09초.
  scaled dt 사용, timeScale=0에서는 눈/관성/호흡이 정지한다.
- randomSeed=0은 개체별 난수, 양수는 재현용. 동시에 생성한 여러 펫에 같은 양수 seed를
  주면 동시에 깜빡이므로 운영에서는 개체 ID 기반 seed를 주거나 기본값을 유지한다.
- 스프링: 기존 임계 감쇠 해석식으로 가속/회전 및 작은 Twitch 목표 각도를 추종한다.
  임계 감쇠 자체는 출렁이는 진동을 만들지 않는다. Twitch의 짧은 양방향 펄스가 움직임을 만든다.
- 귀 frequencyHz=2.5, maxAngle=12~18, twitchDegrees=1~2부터 시작.
  꼬리 중간은 2.0Hz/8~12도/1도, 꼬리 끝은 2.5Hz/12~18도/2도부터 조절.
  본마다 twitchInterval을 조금 다르게 주면 군무처럼 보이는 현상이 줄어든다.
- 호흡: Chest localAmplitude=(0,0.004,0), 팔=(0,0.002,0), phaseOffset=0.08~0.15.
  breathHz=0.38, 이동 중 호흡 비중=0.25. root/발/꼬리 부착점 위치는 보정하지 않는다.

## 5. 실행 순서와 재생 검증

1. Update에서 이전 가산값 제거 → 눈 Float 설정.
2. Animator Normal 평가 → 사용하는 2D IK가 기본 포즈를 결정.
3. PetSecondaryMotion.Evaluate → SpriteSkin이 수정된 본으로 메시 변형.
4. 렌더링.

DefaultExecutionOrder(-50)은 Animator 자체와 SpriteSkin 패키지의 내부 순서를 보장하지 않는다.
Profiler/해당 패키지 실행 순서로 확인한다. 2D IK를 쓰면 해당 Solver보다 뒤,
SpriteSkin 변형보다 앞에 배치해야 한다. 수동 파이프라인은 automaticLateUpdate=false로
놓고 Animator/IK 이후 Evaluate(Time.deltaTime)를 프레임마다 정확히 한 번 호출한다.
다른 스크립트는 이 본들의 LateUpdate 포즈를 다시 덮어쓰지 않아야 한다.

실제 장면 승인 체크:

| 상황 | 확인 조건 |
|---|---|
| 10초 Idle | 눈 간격이 불규칙, 작은 호흡, 귀/꼬리 부착점 유지 |
| 0→0.45→1→0 입력 | Idle/Walk/Run 전환, 정지 시 Run 보빙과 기울기 소멸 |
| 발 접지 | 보빙 최저점과 발 접촉 일치, 지속 발 미끄러짐 없음 |
| 좌/우 방향 전환 | 기울기가 진행 방향에 맞음, 꼬리 root 이탈 없음 |
| Attack/Feed/점프 | suppressLocomotion/grounded 연결, Run 오프셋이 완화됨 |
| Hit Stop 0.05초 | 눈/호흡/본이 정지하며 기본 포즈로 튀지 않음 |
| 비활성/풀 반환/다시 장착 | 이전 오프셋 잔류 없음 |
| 30/60/120fps | 털 떨림/발 미끄러짐/눈 시각 품질 비교 |
| 2분 실행 | 회전/위치 누적 드리프트 없음 |

위 표는 아직 실행하지 않은 Unity 수동 검증 항목이다. 통과했다고 해석하지 않는다.

## 6. 실행한 계산 테스트

Tools/SecondaryMotionProbe.cs는 Unity와 독립된 실제 PetBlinkClock/PetSpringMath를 실행한다.
30/60/120Hz로 1시간의 눈 시간표/최종 눈 감김값을 비교하고, 0초 pause,
프레임 경계 잔여 시간, 두 번 깜빡임 경계, 스프링의 30/120Hz 동등성을 검사했다.
10시간 8,080묶음 중 1,185묶음이 두 번 깜빡임(14.67%)이었다.
이 결과는 확률/시간표/수학 검사이며, 메시/렌더링/상용 게임 같은 자연스러움의 증거가 아니다.
