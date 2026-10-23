# 늘귀 비주얼 리뉴얼 1단계 — 기반 코드 연결 가이드

> 이전 파츠 조립 설계의 기록이다. 최신 요구로 본체를 한 SpriteRenderer/골격으로 변경했으며 현재 배치·API는 `MASTER_VERTICAL_SLICE.md`를 따른다. 이 문서의 PartAttachment/Renderer ID 예제는 현재 코드에 적용하지 않는다.

대상: Unity 6000.3.26f1 / 호환 URP 설치 필요. 네임스페이스 `Mongle.VisualRenewal`.

이번 결과는 실행 가능한 C# 기반 소스와 URP HLSL 셰이더다. 새 늘귀 원화·성체·SpriteSkin 웨이트·마스크·Animator 클립을 제작한 결과는 아니다. 기존 프로젝트의 Built-in 파이프라인 설정도 자동 변경하지 않았다.

## 포함 파일

| 파일 | 책임 |
|---|---|
| Assets/Scripts/VisualRenewal/PetVisualDefinition.cs | 등급/성장/품질 enum, 동적 파츠·머티리얼·소켓 효과 정의 |
| PetVisualCatalog.cs | 종+성장단계+등급 조회, 중복과 누락 거부 |
| PetVisualBindings.cs | 명시된 소켓/Renderer ID와 실제 참조 |
| PetVisualManager.cs | 비활성 구성→검증→외형 교체, 공유 머티리얼, 뷰 재사용 |
| PetSecondaryMotion.cs | 이동 가속/회전 속도→관성 목표→LateUpdate 가산 회전 |
| PetSpringMath.cs | Unity 객체와 분리된 임계 감쇠 해석식 |
| Assets/Shaders/VisualRenewal/PetLocalGlow.shader | URP unlit Sprite의 국소 발광·작화 림·광택 |
| Assets/Editor/PetVisualRenewalValidation.cs | 정상 에디터에서 카탈로그/셰이더 참조 검사 |
| Tools/VisualRenewalProbe.cs | 스프링 관리 코드 검사 |

기존 문서의 의사 코드를 추가로 붙여 넣지 말고 이 파일들을 사용한다. 같은 네임스페이스의 예전 예제를 직접 복사했다면 중복 클래스부터 제거한다.

## 1. 프로젝트 설정

1. 별도 작업 복사본에서 Unity와 호환되는 URP를 Package Manager로 설치하고 패키지 버전을 잠근다.
2. URP Asset/Universal Renderer를 Graphics 및 활성 Quality 설정에 연결한다. 첫 경로는 Forward 기반 2.5D다.
3. Linear 색 공간, HDR 카메라, Post Processing, Global Volume Bloom을 설정한다.
4. 이 셰이더는 `SRPDefaultUnlit` 한 패스로 작성했다. 2D Renderer에서도 unlit 표현을 목표로 하지만 2D Light/Normal 조명 계산은 포함하지 않는다. Sprite Lit와 같은 기능이 아니다.
5. **CPU SpriteSkin**을 사용한다. 이 셰이더에는 GPU SpriteSkin 변형 variant를 넣지 않았다. GPU Skinning을 선택하고 자동 호환을 기대하지 않는다.
6. 기존 `VfxPresentation.OnRenderImage` Bloom은 URP에서 작동하지 않는다. URP Volume Bloom으로 이전하고 기존 셰이크는 분리해 유지한다.

이 소스 작업에서는 URP Asset/패키지 설치/Volume 설정을 생성하지 않았다. 기존 씬이 URP 전환 완료 상태라는 의미는 아니다.

## 2. 늘귀 리그 프리팹 만들기

루트에 `PetVisualBindings`를 추가한다. 그 아래에 실제 Animator와 제작한 파츠/본을 둔다. Collider·PetState·재화·이동은 이 리그 프리팹이 아닌 안정된 PetRoot가 소유한다.

소켓 예:

- `HeadAccessory`: 머리 장식 부착.
- `TailTip`: 꼬리 끝 반짝임.
- `EarNearTip`, `EarFarTip`: 귀 끝 효과.

Renderer 예: `Body`, `Head`, `EarNear`, `EarFar`, `Tail`.

Binding의 ID와 Transform/SpriteRenderer 참조를 Inspector에서 직접 연결한다. 이름으로 위치를 추정하지 않는다. 중복 ID·다른 리그 바깥 참조·누락은 장착을 실패시킨다. root Transform 자체를 소켓으로 사용해도 되지만 재귀적으로 자기 프리팹을 부착하는 설정은 하지 않는다.

리그 프리팹 안의 모든 Animator는 **Normal** 업데이트 모드로 둔다. UnscaledTime/Fixed 계열 평가와 이 컴포넌트의 게임 시간 스프링을 혼용하지 않는다.

### 동적 파츠의 경계

`PetPartAttachment`는 소켓에 프리팹을 장착한다. 뿔·장식·눈·귀 등 분리 파츠를 구성할 수 있다. 파츠 프리팹에 `PetVisualBindings`를 추가하면 그 안의 Renderer도 재질 적용 대상이 된다. 파츠의 소켓은 다른 파츠 부착의 의존 대상으로 사용하지 않는다. 모든 Attachment 대상 소켓은 기본 리그에 있어야 한다.

부착만으로 SpriteSkin 웨이트가 공통 골격에 자동 바인딩되지는 않는다. 같은 리그의 몸통/사지 스킨은 rigPrefab 안에서 바인딩해 두거나, 자체 리그가 완결된 파츠 프리팹을 사용한다. 별도 외부 스킨 재바인더는 이번 범위에 없다.

## 3. 데이터 자산과 장착

Create → Mongle → Visual Renewal → Definition으로 다음 자산을 만든다.

| 항목 | 늘귀 아기 예 |
|---|---|
| speciesId | 0 |
| stage | Baby |
| grade | Common |
| rigPrefab | 제작한 늘귀 아기 리그 |
| attachments | 필요 파츠와 대상 소켓 |
| materials | Renderer ID별 Low/Standard/Showcase 공유 Material |
| effects | 소켓별 ParticleSystem 프리팹과 최소 품질 |

성체는 별도 Definition과 성체 rigPrefab을 연결한다. 현재 카탈로그의 히든 백령견을 연결할 때는 speciesId 126 / Adult / Spirit 정의를 별도로 만든다. 0번 늘귀를 자동으로 Spirit으로 바꾸는 코드가 아니다.

Create → Mongle → Visual Renewal → Catalog 자산의 entries에 정의를 등록한다. 기본은 매니저 Inspector에 직접 연결한다. Resources 로딩이 필요하면 `Assets/Resources/PetVisuals/NeulguiCatalog.asset`로 저장한다. 이 첫 단계는 Resources/direct reference 방식이며 Addressables·원격 다운로드·비동기 로딩을 포함하지 않는다.

안정된 PetRoot에 매니저를 추가하고 다음을 연결한다.

- `viewMount`: 외형이 표시될 자리. 로컬 position/rotation은 장착 시 0/identity로 맞춘다. 프리팹의 로컬 scale은 보존한다.
- `motionRoot`: 실제 이동하는 PetRoot.
- `turnRoot`: 진행 방향을 나타내는 안정된 Transform.
- `sortingLayerName`: 존재하는 Sorting Layer 이름.

```csharp
using Mongle.VisualRenewal;

// 아래 호출은 게임 진입 코드의 메서드 안에 둔다. manager는 Inspector 등으로 얻은 참조.
manager.Equip(0, GrowthStage.Baby, PetGrade.Common, VisualQuality.Standard);

// 오류 안내가 필요한 UI에서는 이전 외형을 유지하며 실패 사유를 얻는다.
if (!manager.TryEquip(0, GrowthStage.Adult, PetGrade.Common,
                      VisualQuality.Standard, out var error))
    UnityEngine.Debug.LogWarning(error);
```

`TryEquip`는 구성 오류를 반환하며 매니저가 이미 비활성화된 경우도 거부한다. 외형 구성은 비활성 보관 부모에서 이루어진다. 파츠·참조 검증이 성공한 다음 활성화하고 기존 뷰를 반환한다. 원본 머티리얼을 수정하지 않고 sharedMaterial만 연결한다.

풀은 Definition+품질 단위이며 키당 기본 한 뷰를 보관한다. 요청한 키만 생성한다. 126종 전체를 로드하는 전역 캐시가 아니다. 다른 프리팹에서 Destroy를 호출하거나 본 구조를 런타임에 바꾸면 풀의 불변 조건을 깨므로 재사용 뷰의 구조는 고정한다. 품질 변경 시에도 Equip을 호출한다.

`CurrentView`, `CurrentDefinition`으로 현재 뷰를 읽고 `SetSortingOrder()`로 깊이를 갱신한다. 여러 Renderer 사이의 local order는 프리팹에서 설정한다. 다른 펫과의 정렬은 루트 SortingGroup이 담당한다. SortingGroup만으로 지형 뒤 가림까지 자동 해결되지는 않는다.

### 기존 Gameplay 코드 연결 주의

기존 PetState에는 speciesId/성장단계가 없다. 임시로 명시적인 Equip 호출을 사용하고 계정 데이터 확장 후 연결한다. PetVisualManager는 PetRuntimeManager의 성장 판정을 대체하지 않는다.

기존 `PetMotor.visualRoot`, `PetAnimatorBridge`, 공격 소켓은 뷰 교체 뒤 오래된 Transform을 가리키지 않도록 게임 조립 코드에서 다시 연결해야 한다. 이 작업은 자동화하지 않았다. 특히 PetAnimatorBridge의 돌봄 이벤트 구독은 OnEnable 시점의 data 참조에 의존하므로 실제 리그 통합 시 활성화 전 의존성 주입 또는 전용 재바인딩 메서드를 구현한다.

고정 사선 2D 원화를 Y축으로 계속 돌리면 납작해진다. turnRoot는 방향 입력용이며 실제 방향별 외형 교체와 회전 연기는 별도 제작한다. 기존 모터의 3D 캡슐 회전을 그대로 일러스트 회전으로 쓰지 않는다.

## 4. PetSecondaryMotion 연결

늘귀 리그 루트에 컴포넌트를 두고 귀/꼬리 또는 전용 offset 노드만 joints에 등록한다.

| 설정 | 귀 시작값 | 꼬리 시작값 |
|---|---:|---:|
| frequencyHz | 3 | 2 |
| maxAngle | 12도 | 18도 |
| accelerationGain | 0.4 | 0.7 |
| turnGain | 0.015 | 0.025 |
| localAxis | 로컬 Z | 로컬 Z |

모델 축과 물리 단위에 맞춰 조정한다. accelerationAxisWorld는 가속도를 투영하는 월드 축이며 고정 사선 카메라의 오른쪽 방향 등을 사용할 수 있다. turnAxisWorld 기본은 월드 Y다. turnRoot.forward를 이 축 평면에 투영해 SignedAngle/dt로 회전 속도를 구한다. 회전축과 forward가 나란하면 회전 입력은 0이다.

### 평가 순서

1. Update에서 이전 프레임의 가산 quaternion을 제거.
2. Animator Normal이 새 기본 포즈 평가.
3. LateUpdate(-50)에서 이동 가속/회전 속도를 읽고 필터링.
4. 임계 감쇠 해석식으로 관성 각도 갱신.
5. 본 로컬 회전에 가산 quaternion 적용.
6. SpriteSkin 변형 및 최종 소켓/공격 샘플.

Unity 패키지별 SpriteSkin/IK 실행 순서를 실제 Profiler와 느린 재생으로 확인한다. 기본 실행 순서가 프로젝트에 맞지 않으면 Script Execution Order를 조정하거나 automaticLateUpdate를 끄고 `Evaluate(Time.deltaTime)`를 적절한 평가 관리자에서 프레임당 한 번 호출한다. 매니저가 모든 패키지의 실행 순서를 자동으로 보장하지는 않는다.

동일 본은 이 컴포넌트 하나만 소유한다. 매니저는 컴포넌트 간 중복 소유도 거부한다. 기존 NeulguiController/다른 LateUpdate가 같은 귀·꼬리 본을 다시 쓰지 않도록 한다. 발 IK와 얼굴 본은 등록하지 않는다.

정지 시간에는 상태를 적분하지 않고 유지한 회전을 재적용한다. 따라서 히트스톱 때 귀가 기본 포즈로 튀는 것을 피한다. 텔레포트·앱 일시정지·긴 프레임·풀 재사용은 관성을 초기화한다. 임계 감쇠이므로 불필요한 진동을 줄이는 방식이며 고의로 과장된 반복 출렁임을 만드는 underdamped 모델은 아니다.

## 5. 마스크·셰이더 설정

Shader: `Mongle/VisualRenewal/PetLocalGlow`.

| 채널 | 의미 | 예 |
|---|---|---|
| R | Emission 허용 | 꼬리 끝·문양 |
| G | 작화한 림 | 뿔/날개 가장자리 |
| B | 이동하는 광택 허용 | 결정·장식 |
| A | 효과 보호 | 얼굴·선화. 1이면 R/G/B 효과 차단 |

**마스크의 A는 투명도가 아니다.** 발광 영역은 예를 들어 RGBA=(1,0,0,0)이다. 모든 픽셀 A=1로 저장하면 보호 때문에 아무 부위도 빛나지 않는다. 검정 바탕에 빨강만 그린 뒤 알파를 그대로 1로 두는 실수를 피한다.

Import 설정:

- 마스크: Texture Type Default, sRGB OFF, Alpha Source=Input Texture Alpha, Alpha Is Transparency OFF.
- 첫 검증은 압축/밉맵을 끄고 원화와 같은 필터/Wrap Clamp로 시작한다.
- 원화: Sprite (2D and UI), sRGB ON, 원화 알파 사용.
- 원화/Mask는 같은 캔버스 크기·UV 배치. 이번 셰이더는 독립 원화/마스크를 사용한다.
- Sprite Atlas 패킹은 비활성으로 검사한다. Shader 태그도 CanUseSpriteAtlas=False다. 다른 Atlas에서 재패킹한 UV를 이 마스크에 그대로 사용할 수 없다. Atlas 도입 시 secondary texture 또는 일치하는 패킹으로 확장해야 한다.
- RGB 분리 알파(ETC1 external alpha) 경로는 포함하지 않았다. 대상 기기에서 일반 RGBA 텍스처 경로를 사용한다.

핵심 HLSL:

```hlsl
half allowed = 1.0h - mask.a;
half pulse = lerp(_PulseMin, 1.0h,
    0.5h + 0.5h * sin(_Time.y * 6.2831853 * _PulseHz + _PulsePhase));
half3 glow = mask.r * allowed * _GlowColor.rgb * _GlowStrength * pulse;
half3 rim = mask.g * allowed * _RimColor.rgb * _RimStrength;
return half4(baseColor.rgb + glow + rim + sheen, baseColor.a);
```

전체 셰이더에는 B 광택 계산·SpriteRenderer color/flip·UV·straight-alpha 합성이 있다. 평면의 잘못된 Fresnel 대신 G 마스크로 림을 표현한다. 원화에 있는 선화는 유지하지만 새로운 외곽선을 자동 생성하는 셰이더는 아니다. 보호할 선화의 A 값을 올려야 한다.

### 머티리얼 예제 시작값

| 등급 | GlowStrength | RimStrength | 사용 위치 |
|---|---:|---:|---|
| 일반/희귀 | 0 | 0 | 원화의 기본색 유지 |
| 레어 | 0~0.2 | 0~0.1 | 광택 중심 |
| 유니크 | 1.2 | 0.25 | 소수 파츠 |
| 영물 | 1.8 | 0.35 | 문양/꼬리 끝 |
| 신수 | 2.4 | 0.45 | 상징 파츠 |

값은 HDR 색·노출·배경에 따라 조정한다. PulseHz 0.25~0.4, PulseMin 0.75~0.9부터 시작한다. 공용 머티리얼을 공유하므로 같은 머티리얼의 맥동 위상도 공유한다. 개체별 위상까지 필요하면 별도 vertex 데이터 또는 제한된 위상 프리셋을 설계한다. 매 프레임 Material 인스턴스를 만들지 않는다.

Bloom은 Threshold 약 1.1~1.4 / Intensity 0.15~0.35에서 시작하고 카메라 HDR/Volume을 확인한다. 셰이더가 밝은 RGB를 출력하는 것과 주변을 실제로 비추는 광원은 다르다. 이 셰이더는 주변 지형을 조명하지 않는다.

### 파티클

효과 정의는 ParticleSystem 프리팹을 소켓에 붙인다. 최소 품질보다 낮으면 장착하지 않는다. Local/World만 지원하고 Custom 시뮬레이션은 거부한다. 파티클 머티리얼은 별도 URP 호환 FX 셰이더를 사용한다.

World 시뮬레이션은 이동 경로에 입자를 남길 수 있지만 뷰 교체/비활성화 때는 모두 정리한다. 진화 뒤에도 남아야 하는 긴 잔상·스킬은 별도 월드 VFX 풀로 분리해야 한다. 루트 SortingGroup 내부의 입자는 그룹 안에서 정렬되므로 서로 다른 펫/지형과의 깊이 표현도 확인한다.

## 6. 검사와 한계

이번 환경에서 실제 확인한 것:

- 새 코드를 포함한 전체 Editor/Player C#을 실제 Unity API 어셈블리로 컴파일.
- 기존 IK/히트스톱/생활 규칙 회귀 검사.
- 생산 PetSpringMath로 30/60/120Hz 시간 분할 일치, 정지, 긴 dt, 100,000회 가변 입력, 각도 래핑, 비정상 입력 거부.

확인하지 못한 것:

- Unity Play Mode의 Instantiate/풀 재사용/Animator 합성.
- URP HLSL 실제 컴파일과 SpriteSkin 평가 시점.
- 실제 늘귀 마스크·원화·부착된 효과의 외형.
- 모바일 성능·터치·화면 품질.

기존 환경의 라이선스 및 Package Manager IPC 차단은 해결하지 않았다. 이번에는 동일한 네이티브 실행을 반복해 검증 완료로 표시하지 않는다.

정상 에디터에서 Catalog를 선택하고 **Mongle → Visual Renewal → Validate Selected Catalog**를 실행한다. Play Mode에서는 아기→성체→품질 단계 반복, 잘못된 소켓 장착 실패 시 기존 외형 유지, 이동→정지→회전, 히트스톱, 앱 복귀를 검사한다. 최종 자연스러움은 정상/느린 재생 영상으로 판정한다.

## 공식 참고

- SpriteSkin CPU/GPU 요구: https://docs.unity3d.com/Packages/com.unity.2d.animation@13.0/manual/SpriteSkin.html
- URP 패스 태그: https://docs.unity3d.com/6000.3/Documentation/Manual/urp/urp-shaders/urp-shaderlab-pass-tags.html
- SortingGroup: https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Rendering.SortingGroup.html
