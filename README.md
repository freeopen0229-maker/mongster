> **현재 메인 게임 전환:** [MAIN_GAME_SERVER_LOOP.md](Docs/MAIN_GAME_SERVER_LOOP.md). 빌드 시작 씬은 커밋된 `Assets/Scenes/MainGame.unity`로 고정됩니다. 서버 권한 돌봄·탐험과 동일 RequestId 재확인을 연결했습니다. 외부 API 주소와 실제 로그인 제공자는 아직 등록되지 않아 온라인 플레이 연결은 미완료입니다. 아래 실행 보고서는 각 이전 버전의 기록입니다.

> **Current cloud/server delivery:** [GAMECI_AND_SERVER_FINAL_REPORT.md](Docs/GAMECI_AND_SERVER_FINAL_REPORT.md). Local Unity Editor attempts stopped. GitHub-hosted GameCI Android workflow and SQLite SQL package prepared. Current Player/Editor offline Roslyn API compilation and core scenarios passed. Remote activation/build, APK, Play Mode and production migration remain NOT RUN. Older reports below are historical.

> **Latest IPC recovery attempt:** [UNITY_IPC_RECOVERY_REPORT.md](Docs/UNITY_IPC_RECOVERY_REPORT.md). Writable XDG paths fixed the preferences-folder error. Actual import still exited 1 at licensing/UPM IPC; fresh AF_UNIX socket creation is denied. Native preflight added; no APK or Play Mode success.

> **2026-10-23 current execution status:** [UNITY_BUILD_EXECUTION_REPORT.md](Docs/UNITY_BUILD_EXECUTION_REPORT.md). Unity 6000.3.26f1 is installed and its version command succeeded. Actual project import exited 1 at licensing/UPM IPC. Native compilation, Play Mode, APK, production migration and remote CI remain uncompleted. Build/CI/test sources prepared. Older notes below describe earlier revisions only.

> Current environment/build report: [ENVIRONMENT_AND_BUILD_REPORT.md](Docs/ENVIRONMENT_AND_BUILD_REPORT.md). Manifest dependencies corrected; 33 presentation/state checks and 18 actual local HTTPS checks PASS. Native Unity/production DB/external firewall/CI build remain unavailable or unverified.

> Final execution gate: [FINALIZATION_READINESS.md](Docs/FINALIZATION_READINESS.md). Grade aura/color and serial tooltip source completed; 17 presentation-data checks passed. Unity environment and production DB connection are absent, so actual Play Mode and production migration remain NOT RUN.

> Latest execution report: [HTTP_RUNTIME_AND_RARITY_MIGRATION.md](Docs/HTTP_RUNTIME_AND_RARITY_MIGRATION.md). Real local HTTPS + C# domain + SQLite tests passed (18). Approved rarity conversion executed on an explicit reference DB with preserved backups (11 tests). Unity native playback and production DB migration NOT RUN.

> Latest client work: [CLIENT_UI_AND_MIGRATION.md](Docs/CLIENT_UI_AND_MIGRATION.md). UI scripts/HTTPS adapter + 23 pure client-domain checks; no Unity/native UI validation or deployed HTTP host. SQLite staging tools tested on fixtures; live DB migration NOT performed. B-E quotas now 100 each.

> Current economy specification: [ECONOMY_V2_SPEC.md](Docs/ECONOMY_V2_SPEC.md). This supersedes older rarity/hidden/BM descriptions. V2 server-domain tests passed; operating backend, migration and Unity economy UI are not implemented.

# 늘귀 한 마리 · Unity 네이티브 2D 제작 프로젝트

## 최신 추가: 뷰포트·로컬 테스트 도구

**Docs/VIEWPORT_LOCAL_TESTS.md**에 현재 테스트 순서가 있다. PetTestController의 Inspector/Game 버튼, 통합 아기/성체 리그를 입력받는 씬 생성 메뉴, 실제 PetTradeService를 사용하는 에디터 전용 Mock 메뉴를 추가했다. 새 C# 거래/스크롤 시나리오는 Mono에서 실행했지만 이번 추가 코드의 Unity API 컴파일·Play Mode·화면은 미검증이다. 아래의 과거 컴파일 통과 기록은 이전 버전 범위다. 새 통합 리그 프리팹/원화/클립을 이 도구가 자동 제작하지는 않는다.

## 최신: 통합 본체·서버 권한 수직 슬라이스 기반

현재 가이드는 **Docs/MASTER_VERTICAL_SLICE.md**다. 복잡한 파츠 조립을 제거하고 통합 본체+코스튬/오라로 변경했다. 새 공용 개체/거래 데이터는 `Assets/Scripts/SliceShared`, 온라인용 클라이언트 매니저는 `Assets/Scripts/VerticalSlice`, 서버 규칙/거래 서비스는 `ServerSources`에 있다. 서버 소스는 Unity Assets 밖에 둔다. 기존 Gameplay 폴더는 이전 오프라인 검증 씬용이며 새 `Mongle.Slice.Client.PetRuntimeManager`와 함께 부착하지 않는다. 인증·HTTP·영속 DB·결제·새 늘귀 통합 리그/실기 실행은 미완료다.

## 비주얼 리뉴얼 1단계 코드 추가

`Assets/Scripts/VisualRenewal`에 파츠/공유 머티리얼 장착 매니저, 데이터 자산, 소켓 바인딩, 이동·회전 관성 스프링을 추가했다. `Assets/Shaders/VisualRenewal/PetLocalGlow.shader`는 URP unlit Sprite용 마스크 발광·작화 림·광택이다. 상세 연결은 `Docs/VISUAL_RENEWAL_STAGE1.md`에 있다. 새 C#은 기존 코드와 함께 실제 Unity API 대상 컴파일을 통과했고 스프링 관리 코드 검사를 추가했다. URP 설치/파이프라인 전환·새 원화·리그·셰이더 실제 컴파일/화면 검증은 아직 하지 않았다. 이 추가 소스는 기존 Built-in 검증 씬의 자동 URP 변환을 의미하지 않는다.

**상태: 실제 Unity API 대상 C# 컴파일 및 관리 코드 검사 통과 / Unity 씬 재생·셰이더·실기 빌드는 환경 제약으로 미검증.**

이 프로젝트는 HTML 시연을 Unity로 표시하는 래퍼가 아니다. Unity SpriteRenderer, Transform 관절 계층, AnimatorController와 AnimationClip, C# 보행 및 두 관절 다리 계산으로 구성한다. 웹 프로젝트와 저장 데이터는 수정하지 않았다.

## 시작

1. 압축을 푼 뒤 Unity Hub에서 `NeulguiUnity` 폴더를 Add로 등록한다.
2. Unity **6000.3.26f1**로 연다. 이 버전은 제작 기준이며 최신 버전이라는 의미는 아니다.
3. 최초 스크립트 임포트 후 씬을 자동 구성하도록 작성했다. 자동 구성이 실행되지 않으면 상단 **Mongle → 01 Create Neulgui Scene**을 실행한다.
4. `Assets/Scenes/NeulguiProof.unity`를 열고 Play를 누른다.
5. **Rest / Walk / Turn / Pet / Pause / 0.5x** 버튼으로 확인한다. 바닥 클릭은 이동, 펫 클릭은 교감이다.
6. **Show joints**를 켜면 엉덩이·무릎·발의 연결을 볼 수 있다. 한국어 기본 폰트 누락을 피하려고 검증 UI는 영어로 작성했다.

공식 Unity 6000.3.26f1을 내려받아 실행했다. 에디터는 시작됐지만 라이선스 초기화 IPC와 Unity Package Manager IPC 연결이 실패해 씬 생성·재생 단계로 넘어가지 못했다. Unity에 포함된 Mono/C# 컴파일러와 실제 Unity API 어셈블리로 에디터·플레이어 소스를 각각 컴파일했고 통과했다. 이것은 Unity 에디터 전체 임포트·Play Mode 검증을 대신하지 않는다.

## 들어 있는 것

- 원래 늘귀 아기 원화에서 수동 부위 마스크로 분리한 얼굴·몸통·두 귀·두 앞발·두 뒷발·꼬리 PNG.
- 관절 아래 가려지는 연결부용 단순 색면. 원화에서 보이지 않던 신체를 완전히 복원한 아트는 아니다.
- `Assets/Scripts/NeulguiController.cs`: 이동, 감속, 방향 전환, 발 지지/공중 회수/정지 발 정리, 교감, 눈 감기, 속도 조절, 관절 오버레이.
- `Assets/Scripts/NeulguiMath.cs`: 길이를 유지하는 두 관절 다리 계산.
- `Assets/Editor/NeulguiProject.cs`: 전용 프리팹·씬·Idle/Walk/Happy/Turn 클립·Animator 상태 전이 생성 및 검사·빌드 메뉴.
- `Tools/parts.json`, `prepare_parts.py`: 기존 아틀라스에서 부위 추출을 재현하는 제작 도구. 새 이미지를 생성하지 않는다.

프리팹·씬·Animator와 클립은 최초 에디터 실행 시 생성한다. 이미 생성된 `.unity`/`.anim` 파일을 포함한 검증 완료 프로젝트라는 의미는 아니다.

## 실행 검증

**Mongle → 02 Validate Scene And Rig**는 스프라이트 누락, 관절 참조, 다리 길이 계산, 클립 곡선을 검사한다. 수학 검사 통과가 자연스러운 움직임의 합격을 뜻하지 않는다.

Unity 에디터 경로를 알고 있는 환경에서는 다음 명령으로 검사할 수 있다.

```text
Unity -batchmode -projectPath <NeulguiUnity 절대경로> -executeMethod NeulguiProject.Validate -quit -logFile validation.log
```

씬을 새로 구성하려면 `NeulguiProject.Create`, Windows 빌드는 `NeulguiProject.BuildWindows`, Android 빌드는 `NeulguiProject.BuildAndroid`를 사용한다. 해당 플랫폼의 Build Support가 설치되어 있어야 한다. APK나 EXE는 이 압축 파일에 포함되어 있지 않다.

## 시각 검수 기준과 남은 작업

- 정상 속도에서 최소 10걸음을 관찰한다. 발 지지 중 미끄러짐, 다리 과신장, 몸통/귀 절단면 노출이 없어야 한다.
- 느린 재생과 일시정지로 착지, 방향 반전, 멈추고 발을 정리하는 과정을 확인한다.
- 발 지지 좌표 변화와 관절 도달 오차를 함께 본다. 도달 오차가 생기면 실제 발은 길이 제한 때문에 목표 지면점에서 벗어날 수 있다.
- 현재 회전은 옆모습 좌우 전환이다. 뒤/정면 회전 원화는 없다.
- 귀는 분리된 부위의 관절 회전이다. 귀 내부의 다중 뼈 스킨 변형은 후속 작업이다.
- 눈 감김은 고정 위치 덧그림이다. 눈꺼풀 경계는 Unity 화면에서 수동 보정이 필요하다.
- 원본 해상도는 약 301×268px다. 크게 확대하면 선명도에 한계가 있다.
- 실제 Unity API 대상 C# 컴파일은 통과했다. 씬 자동 구성, 애니메이션 합성, 렌더 순서, 입력, 셰이더, Windows/Android 빌드는 **확인이 남아 있다**.
- 기존 126종 게임의 성장·알·도감·전투·거래를 Unity로 이전한 프로젝트는 아니다. 한 마리의 동작 제작을 위한 첫 단위다.

## 기준 문서

- Unity 6000.3.26f1: https://unity.com/releases/editor/whats-new/6000.3.26f1
- AnimatorController: https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Animations.AnimatorController.html
- AnimationClip.SetCurve: https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AnimationClip.SetCurve.html

기존 늘귀 원화: 몽글펫 `dist/assets/species/atlas-00.png`의 첫 번째 아기. 게임의 창작 아트에서 파생한 부위다. 추가 이미지 생성은 하지 않았다.

## RPG VFX 전투 검증 씬

**Mongle → 06 Create RPG VFX Scene**으로 별도 전투 검증 씬을 구성하도록 작성했다. 씬 파일은 에디터에서 생성된다.

- 화염구: 준비 마법진 → 3D 발사체·빛·Trail → 구 충돌 검사 → 폭발·불꽃·연기·충격파.
- 검기: 회전하는 검기 텍스처·Trail → 발사체 충돌 → 타격 파편·파동.
- 번개: 상공에서 내려오는 다중 절선과 가지 → 번쩍임 → 범위 피격.
- 마법진: AI 생성 시질 텍스처를 바닥에 배치·회전 → 상승 입자·기둥 → 광역 충격.
- 공통: 스킬별 ScriptableObject 설정, 쿨다운, 중복 피격 차단, 적의 붉은 피격 발광·넉백·체력·복귀.
- 0.05초 히트스톱: 겹친 요청을 연장하고 원래 시간 배율로 복원, 비활성화·종료 때 복원. 펫 이동은 게임 시간에 연결해 함께 멈춘다.
- 카메라: 감쇠하는 부드러운 셰이크, HDR 밝기 추출과 블러를 합성하는 Built-in 렌더 파이프라인용 Bloom. URP 전용 프로젝트가 아니다.
- 사운드: Kenney CC0 타격음 3층을 DSP 예약 재생. 충격음 0ms, 파열음 +10ms, 잔향 +20ms. 실제 화면/소리 동기 오차가 10ms 이내라는 보증은 아니다. 프레임 속도와 오디오 장치의 지연은 별도 측정이 필요하다.
- 파티클 풀과 활성 레이어 제한, 일시적 시각 객체의 수명 종료를 적용했다. 모바일 성능·배터리는 측정하지 않았다.
- 1~4 키 또는 버튼으로 스킬 사용. 바닥 클릭으로 조준 위치 변경. Hit stop/Bloom/Camera shake 개별 비교 가능.

**07 Validate RPG VFX Resources**는 셰이더 컴파일 오류·텍스처·오디오·스킬 참조를 확인한다. **09 Run RPG VFX Playback Validation**은 4개 스킬을 실행하고 실제 충돌/타격 수, DSP 시계, 영상 프레임과 오류를 `Validation/VFXPlayback`에 기록하도록 작성했다. 이 자동 재생 검사는 아직 실행되지 않았다. 수치 통과 후에도 캡처 영상과 실제 소리를 검수해야 한다.

VFX 씬 빌드는 **10 Build VFX Windows / 11 Build VFX Android APK**를 사용한다. 03/04는 늘귀 단독 검증 씬 빌드다. APK·EXE는 포함하지 않았다.

## 리소스 출처

- Kenney Particle Pack: https://kenney.nl/assets/particle-pack (CC0, 공식 Unity 샘플 패키지 포함).
- Kenney Impact Sounds: https://kenney.nl/assets/impact-sounds (CC0). 각 원본 라이선스를 `Assets/VFX/`에 동봉했다.
- 내장 이미지 생성 도구로 제작한 마법진: `Assets/Resources/VFX/arcane_sigil_ai.png`. 생성 프롬프트는 `Assets/VFX/AI-TEXTURE-PROMPT.md`. 콘셉트 그림이 아닌 실제 셰이더 입력 텍스처다.
- 08 메뉴로 공식 Kenney Unity 샘플을 추가 임포트할 수 있다. 현재 스킬 코드에는 선별 텍스처와 직접 구성한 ParticleSystem을 사용한다.

이 프로젝트에는 파티클·텍스처·셰이더·타격 연출의 구현 소스가 들어 있다. 상용 RPG나 AAA 게임과 동등한 품질이라는 검증은 하지 못했다. 펫 자체는 기존 2D 분리 원화를 사용하며 3D 캐릭터 모델로 바뀐 것은 아니다. VFX 발사체·입자·번개·마법진은 Unity 3D 공간에서 움직인다.

## 세 핵심 시스템 기반 코드 업데이트

`Docs/CORE_GUIDE.md`에 상세 주석이 포함된 새 스크립트 구조, Animator/AnimationEvent 연결법, 모바일 조이스틱, 생활 상태/능력치 연동, 저장, 방치/능동 보상, 테스트와 튜닝 가이드를 작성했다.

**Mongle → 12 Create Gameplay Foundation Scene → 13 Validate Gameplay Foundation → Play** 순서로 기능 검증 씬을 구성한다. UI Toolkit 조이스틱과 돌봄·먹이 구매·공격·방치 보상 버튼을 포함한다. Food(5c) → Feed/Pet/Play → 컨디션 상승 → 이동/공격 → 처치 보상 → 방치 보너스 → 먹이 구매의 루프다.

검증 씬은 캡슐 모델을 사용한다. 완성된 펫 모델/보행 클립을 제공한다는 의미는 아니다. 전체 C#은 실제 Unity API 참조로 에디터/플레이어 대상으로 컴파일했고, 순수 로직은 `CoreLoopProbe`에서도 검사한다. 씬 생성/재생은 기존 IPC 제약으로 확인하지 못했다.

## 2026-10-23: Run / secondary motion source update

See [SECONDARY_MOTION_RUN.md](Docs/SECONDARY_MOTION_RUN.md). Includes `PetBlinkClock.cs`; existing spring math is still required. Pure math/timing checks passed; Unity compilation, rig setup and visual Play Mode are NOT verified for this revision.
