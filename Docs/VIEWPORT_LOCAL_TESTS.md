# 늘귀 뷰포트·부화 스크롤·로컬 거래 테스트

현재 작업은 실제 프로젝트 API를 사용하는 테스트 코드와 에디터 메뉴다. 새 통합 늘귀 아기/성체 원화·스키닝·클립을 제작한 결과는 아니다. 이번 수정의 Unity Editor/Player API 컴파일, Play Mode, 셰이더·뷰포트 영상 검증은 아직 수행하지 못했다. 아래의 'PASS 로그'는 순수 C#으로 실행한 거래·스크롤 결과와 구분한다.

## 1. 추가 파일

| 파일 | 기능 |
|---|---|
| Assets/Scripts/Testing/PetTestController.cs | 플레이 모드의 아기/성체/오라 미리보기·8h 알·스크롤 버튼 |
| Assets/Scripts/Testing/LocalHatchSimulation.cs | 실제 BM 시간 단축 함수를 사용하는 로컬 알 |
| Assets/Editor/PetTestControllerEditor.cs | Inspector 버튼 |
| Assets/Editor/PetViewportLabSetup.cs | 입력 리그 검사→카탈로그/재질/테스트 씬 생성 |
| Assets/Editor/PetTradeMockTests.cs | Console에 결과를 출력하는 거래 메뉴 |
| ServerSources/Testing/PetTradeMockScenario.cs | 실제 PetTradeService를 사용하는 공통 시나리오 |
| Assets/Editor/LocalMockGenerated/ | 서버 원본에서 생성한 에디터 전용 소스 사본 |
| Tools/sync_editor_mock_sources.py | 서버 원본과 에디터 사본의 동기화·일치 검사 |

## 2. 통합 본체 프리팹 계층

아기와 성체를 각각 독립된 프리팹으로 만든다. 아기를 확대해 성체로 대체하지 않는다.

| 계층 경로 예 | 컴포넌트/책임 |
|---|---|
| PetRoot | 씬의 이동·상태·PetVisualManager·PetTestController. 풀에 반환하지 않는 안정된 루트 |
| PetRoot/ViewMount | 활성 리그가 표시될 자리 |
| ViewMount/NeulguiRig | 프리팹 루트. Animator, PetVisualBindings, SortingGroup |
| NeulguiRig/Body | **한 개**의 SpriteRenderer와 SpriteSkin |
| NeulguiRig/Skeleton/RootBone | 통합 본체 골격의 루트 |
| RootBone/BodyBone/HeadBone/CostumeSocket | 머리 본을 따라 움직이는 의상 소켓 |
| RootBone/AuraSocket | 몸을 따라가는 오라 소켓 |
| RootBone/.../TailTip/TipFxSocket | 선택적인 꼬리 끝 효과 소켓 |

골격의 귀·꼬리·다리는 존재하지만 독립된 몸체 SpriteRenderer로 쪼개지 않는다. **한 메시의 웨이트가 해당 본을 따라 변형되는 구조**다. 코스튬은 추가 Renderer가 있어도 된다. 기본 리그 검사 뒤 별도 장착하기 때문이다.

### Inspector 연결

1. Body에 완성된 통합 Sprite를 지정한다.
2. Sprite Editor의 Skinning Editor에서 geometry/bones/weights를 제작한다. 자동 웨이트 초안을 관절 극단 포즈에서 보정한다.
3. SpriteSkin의 Root Bone/Bones를 실제 Skeleton Transform에 연결한다. 이 패키지 설정을 테스트 코드가 대신 생성하지 않는다.
4. 루트 Animator에 실제 Idle 등 클립이 들어 있는 Controller를 연결하고 Update Mode=Normal로 둔다.
5. PetVisualBindings.bodyRenderer는 Body, animator는 루트 Animator로 지정한다.
6. sockets에 ID `Costume`과 `Aura`를 추가하고 각각 실제 소켓 Transform을 연결한다. ID 문자열과 오브젝트 이름은 별개다.
7. 기본 리그에는 SpriteRenderer가 정확히 하나인지 확인하고 Prefab으로 저장한다.

씬 생성 메뉴는 Body에 SpriteSkin이 없으면 입력을 거부한다. 단, 컴포넌트가 있다는 것만으로 웨이트/보행의 완성도가 검증되는 것은 아니다.

소켓을 본체 Renderer의 임의 자식에 놓기보다 **따라갈 본의 자식**으로 연결한다. 머리가 숙여질 때 의상도 따라가고, 꼬리가 움직일 때 끝의 반짝임도 따라가야 한다.

기존 Assets/Art/Neulgui의 9부위 PNG는 이전 실험용이다. 그것만으로 아기/성체 통합 리그 프리팹이 준비됐다고 간주하지 않는다. 이 테스트는 기존 캐릭터 시안의 품질과 자연스러움을 자동으로 증명하지 않는다.

## 3. 가장 빠른 씬 생성

정상 Unity 6000.3 환경에서 다음 순서로 실행한다.

1. URP 패키지/URP Asset을 설치·연결하고 PetLocalGlow 셰이더에 오류가 없는지 확인한다.
2. 위 방식의 **아기·성체 리그 프리팹**과 URP 호환 ParticleSystem 오라 프리팹을 준비한다.
3. **Mongle → Viewport Tests → Create Neulgui Lab**을 연다.
4. Baby rig, Adult rig, Aura prefab을 지정한다. 코스튬과 R/G/B/A 마스크는 선택 입력이다.
5. 소켓 ID를 리그의 Binding ID와 일치시킨다. 기본값은 Costume/Aura다.
6. **Validate Inputs and Create Test Scene**을 누른다.

입력이 유효하면 Assets/NeulguiViewportLab 또는 충돌 없는 새 폴더에 세 정의와 재질, LocalPreviewCatalog, NeulguiViewportLab.unity를 생성하도록 작성했다. 기존 씬을 덮어쓰지 않으며 수정 중인 씬은 Unity의 저장 확인을 거친다.

생성되는 정의:

| 정의 | 종 | 단계 | 등급 | 목적 |
|---|---:|---|---|---|
| BabyCommon | 0 | Baby | Common | 일반 아기 표시 |
| AdultCommon | 0 | Adult | Common | 정상 성체 전환 |
| AdultUnique_PREVIEW | 0 | Adult | Unique | 같은 성체에 고등급 오라를 붙여 보는 **로컬 미리보기** |

일반 늘귀가 자라면 자동 유니크가 된다는 게임 규칙이 아니다. 이 카탈로그는 Resources/운영 데이터와 연결하지 않는 테스트 자산이다. 히든 1% 판정을 수행하거나 저장 데이터를 바꾸지 않는다.

코스튬을 지정하면 `test-costume`이라는 테스트 ID로 아기/성체 정의에 등록한다. 실제 상품·소유권·결제와 무관하다. 코스튬이 아기와 성체의 체형에 모두 맞는지 직접 확인한다.

### 마스크/후처리

- R 발광/G 작화 림/B 광택/A 보호영역. 발광할 영역의 A는 0이다.
- 마스크 sRGB OFF, Alpha Is Transparency OFF, 원화와 같은 UV/캔버스 크기.
- 마스크가 없으면 몸체 발광이 꺼진 채 오라 부착만 확인할 수 있다.
- 카메라 HDR은 생성 코드가 켠다. **카메라 Post Processing과 Global Volume Bloom은 수동 설정**한다.
- 제안 시작값은 Bloom Threshold 1.1~1.4, Intensity 0.15~0.35이며 실제 화면에서 조정한다.
- 셰이더는 CPU SpriteSkin·Unlit 기준이다. 실제 URP 셰이더 컴파일/혼합 깊이를 검사한다.

## 4. 버튼으로 확인

Play를 누르고 LOCAL Neulgui Preview 오브젝트의 PetTestController를 선택한다. Inspector 버튼과 Game View의 간단한 버튼이 같은 메서드를 호출한다. Context Menu에도 같은 항목이 있다. 편집 모드에는 장착 버튼이 비활성이다. 정식 릴리스 빌드에서는 테스트 실행을 거부한다.

| 버튼 | 호출/기대 |
|---|---|
| Equip Baby Neulgui | 0/Baby/Common, 본체와 요청한 코스튬 확인 |
| Equip Adult Neulgui (Aura Preview) | 0/Adult/설정한 preview grade, 오라 소켓 인스턴스 필수 |
| Equip Adult Neulgui (Common) | 0/Adult/Common. 등급 상승과 분리 |
| Reset 8h Egg | 로컬 알 28,800초, 테스트 스크롤 1개 |
| Trigger Hatch Scroll | 실제 MonetizationPolicy 시간 단축 호출, 남은 시간 0·스크롤 0 |

일반 아기에는 오라가 없어도 정상이다. 고등급 미리보기는 Standard 이상 품질과 해당 등급 정의/효과 프리팹이 있어야 통과한다. Low로 바꿔 오라가 생략된 경우 고등급 오라 테스트가 실패하는 것이 정상이다.

스크롤을 다시 눌러도 추가 차감하지 않는다. 이미 자연부화 시간이 지난 알에도 스크롤을 소비하지 않는다. 로컬 시간은 UTC 시작값과 비배율 단조 경과 시간을 사용한다. 서버 인벤토리/결제 아이템/개체값을 바꾸지 않는다.

표시 테스트의 PASS는 요청한 뷰·Sprite 참조와 소켓 인스턴스가 있다는 뜻이다. 입자가 실제 카메라에서 예쁘게 보이는지, Idle이 자연스러운지, 파티클이 충분히 방출되는지는 별도 시각 검수다. Console의 animation clips=0이면 클립 연결부터 고친다.

카메라 밖에 보이면 Game Camera의 위치/Orthographic Size를 리그 크기에 맞춰 조정한다. Rig scale을 무작정 크게 키워 원화 디테일 문제를 숨기지 않는다.

## 5. 로컬 Mock 거래 메뉴

**Mongle → Viewport Tests → Run Local Mock Trade**를 누른다. Play Mode 없이도 실행하도록 작성했다.

이 메뉴는 테스트 전용 메모리 저장소에 A/B를 만들고 기존 실제 PetTradeService의 CreateOffer/Buy를 호출한다. 정상 케이스와 마지막 거래를 별개의 저장소에서 실행한다.

기대 로그:

```text
[MOCK LIST] A listed mock-neulgui; remaining=2; price=100.
[PASS MOCK BUY] owner=B; CurrentTradeCount=2->1; IsSoulbound=False; feeBurned=5.
[MOCK LIST] A listed mock-neulgui; remaining=1; price=100.
[PASS MOCK BUY] owner=B; CurrentTradeCount=1->0; IsSoulbound=True; feeBurned=5.
[PASS MOCK] Soulbound relisting rejected; same request replay did not charge again.
```

또한 B의 잔액 1000→900, A의 잔액 0→95, 소각 5를 검증한다. 같은 RequestId 재전송 시 잔액/거래 횟수/소각이 다시 변하지 않는지도 확인한다. 기대와 다르면 예외를 던져 Console에 실패를 표시한다.

### 서버 소스를 에디터에서 참조하는 방법

ServerSources는 Assets 밖이므로 Unity가 직접 컴파일하지 않는다. 이 프로젝트에는 원본을 복사한 Assets/Editor/LocalMockGenerated가 포함되어 있다. 각 파일은 `#if UNITY_EDITOR`로 감쌌다. 원본 서버 DLL을 런타임 Assets에 넣지 않는다.

서버 코드가 바뀌면 프로젝트 루트에서 다음을 실행한다.

```text
python3 Tools/sync_editor_mock_sources.py
python3 Tools/sync_editor_mock_sources.py --check
```

생성 사본을 직접 고치지 않는다. 다른 이름의 서버 DLL이나 서버 소스도 Editor에 중복 추가하면 동일 타입 충돌이 생긴다. 둘 중 한 방식만 사용한다.

## 6. 이번에 실제 실행한 검사와 남은 확인

순수 C# 검사 명령:

`python3 Tools/verify_viewport_local.py <mono 경로> <mcs.exe 경로>`

이번 환경에서는 다음을 실제 컴파일·실행했다.

- 서버 원본을 사용한 A→B 등록·구매, 2→1 잔여 횟수, 1→0 귀속, 수수료, 재판매 거부, 중복 요청.
- 실제 LocalHatchSimulation과 MonetizationPolicy를 사용한 8h→0, 1회 소비, 반복/이미 준비된 알의 소비 차단.
- UNITY_EDITOR가 켜진 생성 서버 사본에서 동일 시나리오.
- UNITY_EDITOR 없이 생성 서버 사본을 컴파일한 어셈블리에 서버 타입이 없는지 검사. 이는 실제 APK 빌드 검증은 아니다.

기록은 Validation/ViewportLocal/results.json에 있다. 기존 Validation/Managed/results.json은 이전 버전의 Unity API 컴파일/검사 기록이며 새 뷰포트 코드까지 검증한 것으로 해석하지 않는다.

이번에 수행하지 못한 것: 새 PetTestController/CustomEditor/씬 생성기의 실제 Unity API 컴파일, 에디터 메뉴 실행, 아기·성체 소환 영상, SpriteSkin 타이밍, URP 셰이더, 모바일 기기. 실제 Unity가 있는 환경에서는 Tools/verify_with_unity_assemblies.py로 전체 컴파일 검사를 한 뒤 위 메뉴를 실행한다.

자연스러운 한 마리의 완성도는 실제 통합 리그를 정상/느린 재생으로 확인해야 한다. 현재 파일과 로그를 그 시각적 증명으로 사용하지 않는다.
