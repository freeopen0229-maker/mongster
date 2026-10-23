using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mongle.VisualRenewal
{
    /// <summary>
    /// Animator Normal 평가 후 가산 보정. 본체는 하나의 SpriteSkin을 유지한다.
    /// 스크립트는 원화, skin weight, Walk/Run/눈꺼풀 클립을 생성하지 않는다.
    /// Animator/IK 이후, SpriteSkin 변형 이전 순서를 실제 프로젝트에서 확인해야 한다.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class PetSecondaryMotion : MonoBehaviour
    {
        [Serializable]
        public sealed class Joint
        {
            public Transform bone;
            public Vector3 localAxis = Vector3.forward;
            [Min(.1f)] public float frequencyHz = 2.5f;
            [Min(0)] public float maxAngle = 18f;
            public float accelerationGain = .6f, turnGain = .02f;
            [Min(0)] public float twitchDegrees = 2f;
            public Vector2 twitchInterval = new Vector2(1.8f, 5.5f);
            [Min(.05f)] public float twitchSeconds = .32f;
            [NonSerialized] internal double angle, velocity;
            [NonSerialized] internal Quaternion applied;
            [NonSerialized] internal bool hasApplied;
            [NonSerialized] internal float wait, age, sign;
        }
        [Serializable]
        public sealed class BreathBone
        {
            public Transform bone;
            // 부모 공간의 작은 위치 변화. 몸통/팔 전용 본에만 사용. 꼬리 root/발은 제외.
            public Vector3 localAmplitude = new Vector3(0, .004f, 0);
            [Range(0, 1)] public float phaseOffset;
            [NonSerialized] internal Vector3 applied;
            [NonSerialized] internal bool hasApplied;
        }
        public Transform motionRoot, turnRoot;
        public Animator animator;
        [Tooltip("클립이 키를 쓰지 않는 전용 부모. 물리 Root/발 IK root와 분리한다.")]
        public Transform visualOffset;
        public Vector3 accelerationAxisWorld = Vector3.right, turnAxisWorld = Vector3.up;
        [Tooltip("기본 XY 스프라이트용 Z축. XZ 2.5D 리그는 축/부호를 별도 조정.")]
        public Vector3 leanAxisLocal = Vector3.forward;
        [Range(-1, 1)] public float facingSign = 1;
        public Joint[] joints = Array.Empty<Joint>();
        public BreathBone[] breathBones = Array.Empty<BreathBone>();
        [Min(.01f)] public float inputSharpness = 12f, teleportDistance = 2f, maxFrameGap = .15f;
        [Range(0, 1)] public float runStart = .5f, fullRun = .85f;
        [Min(0)] public float runBobHeight = .035f;
        [Range(0, 15)] public float runLeanDegrees = 12;
        [Min(.1f)] public float breathHz = .38f;
        [Range(0, 1)] public float movingBreathWeight = .25f;
        public Vector2 blinkInterval = new Vector2(2.5f, 6f);
        [Range(0, 1)] public float doubleBlinkChance = .15f;
        [Tooltip("0: 인스턴스별 seed. 양수: 재현 가능한 테스트용.")]
        public int randomSeed;
        public bool automaticLateUpdate = true, grounded = true, suppressLocomotion;
        public string locomotionState = "Locomotion";
        public string speedParameter = "MoveSpeed", blinkParameter = "EyeClose";

        Vector3 previousPosition, previousVelocity, previousForward;
        Vector3 bodyPositionOffset;
        Quaternion bodyRotationOffset;
        float filteredAcceleration, filteredTurn, speed, runWeight, breathPhase, bob;
        bool primed, bodyApplied, hasSpeed, hasBlink;
        int speedHash, blinkHash, locomotionHash;
        System.Random random;
        PetBlinkClock blink;

        public void BindSources(Transform movement, Transform turning)
        { RestoreOffsets(); motionRoot = movement; turnRoot = turning; ResetMotion(); }

        void Awake()
        {
            ValidateSetup();
            if (!animator) animator = GetComponentInChildren<Animator>();
            int seed = randomSeed == 0 ? GetInstanceID() : randomSeed;
            random = new System.Random(seed);
            blink = new PetBlinkClock(seed ^ 7231, blinkInterval.x, blinkInterval.y, doubleBlinkChance);
            RefreshAnimatorBindings();
        }
        /// <summary>AnimatorController를 런타임 교체했다면 다시 호출한다.</summary>
        public void RefreshAnimatorBindings()
        {
            hasSpeed = hasBlink = false;
            speedHash = Animator.StringToHash(speedParameter);
            blinkHash = Animator.StringToHash(blinkParameter);
            locomotionHash = Animator.StringToHash(locomotionState);
            if (!animator || !animator.runtimeAnimatorController) return;
            foreach (var p in animator.parameters)
            {
                if (p.type != AnimatorControllerParameterType.Float) continue;
                if (p.nameHash == speedHash) hasSpeed = true;
                if (p.nameHash == blinkHash) hasBlink = true;
            }
        }
        void OnEnable() { ResetMotion(); }
        void Update()
        {
            // 지난 프레임의 가산값을 제거한다. 애니메이션 키가 없는 본에도 누적되지 않는다.
            RestoreOffsets();
            float dt = Time.deltaTime;
            if (dt > 0 && dt <= maxFrameGap && blink != null) blink.Tick(dt);
            // 눈 레이어가 Animator 평가 때 이 값을 읽는다. 본체 전체를 찌그러뜨리지 않는다.
            if (animator && hasBlink && blink != null) animator.SetFloat(blinkHash, blink.Closure);
        }
        void LateUpdate() { if (automaticLateUpdate) Evaluate(Time.deltaTime); }
        void OnDisable() { ResetMotion(); }
        void OnApplicationPause(bool paused) { ResetMotion(); }

        public void ValidateSetup()
        {
            var seen = new HashSet<Transform>();
            if (joints == null || breathBones == null || fullRun <= runStart || blinkInterval.x <= 0 ||
                blinkInterval.y < blinkInterval.x || maxFrameGap <= 0)
                throw new InvalidOperationException("SecondaryMotion: invalid arrays/thresholds/intervals.");
            if (visualOffset && (visualOffset == transform || visualOffset == motionRoot || visualOffset == turnRoot ||
                !visualOffset.IsChildOf(transform) || leanAxisLocal.sqrMagnitude < .0001f))
                throw new InvalidOperationException("SecondaryMotion: visualOffset must be a dedicated visual child.");
            if (visualOffset) seen.Add(visualOffset);
            foreach (var j in joints)
                if (j == null || !j.bone || !seen.Add(j.bone) || j.localAxis.sqrMagnitude < .0001f ||
                    !j.bone.IsChildOf(transform) || j.frequencyHz <= 0 || j.maxAngle < 0 ||
                    j.twitchInterval.x <= 0 || j.twitchInterval.y < j.twitchInterval.x || j.twitchSeconds < .05f ||
                    float.IsNaN(j.frequencyHz) || float.IsInfinity(j.frequencyHz))
                    throw new InvalidOperationException("SecondaryMotion: invalid/duplicate joint.");
            foreach (var b in breathBones)
                if (b == null || !b.bone || !b.bone.IsChildOf(transform) || !seen.Add(b.bone))
                    throw new InvalidOperationException("SecondaryMotion: invalid/duplicate breathing bone.");
        }
        float Range(float a, float b) { return a + (float)random.NextDouble() * (b - a); }
        public void ResetMotion()
        {
            RestoreOffsets(); primed = false; previousVelocity = Vector3.zero;
            filteredAcceleration = filteredTurn = runWeight = bob = 0;
            if (joints == null) return;
            foreach (var j in joints) if (j != null)
            {
                j.angle = j.velocity = 0; j.age = j.twitchSeconds;
                j.wait = random == null ? j.twitchInterval.x : Range(j.twitchInterval.x, j.twitchInterval.y);
            }
        }
        void RestoreOffsets()
        {
            if (bodyApplied && visualOffset)
            {
                visualOffset.localPosition -= bodyPositionOffset;
                visualOffset.localRotation *= Quaternion.Inverse(bodyRotationOffset);
            }
            bodyApplied = false;
            if (joints != null) foreach (var j in joints)
                if (j != null && j.bone && j.hasApplied)
                { j.bone.localRotation *= Quaternion.Inverse(j.applied); j.hasApplied = false; }
            if (breathBones != null) foreach (var b in breathBones)
                if (b != null && b.bone && b.hasApplied)
                { b.bone.localPosition -= b.applied; b.hasApplied = false; }
        }
        /// <summary>
        /// 수동 모드: Update가 이전 보정을 제거한 뒤 Animator/IK 이후 한 번 호출한다.
        /// dt=0은 기존 자세를 유지한다. 프레임 폭증/텔레포트는 관성을 초기화한다.
        /// </summary>
        public void Evaluate(float dt)
        {
            RestoreOffsets();
            if (!motionRoot || random == null) return;
            if (dt <= 0) { ApplyOffsets(); return; }
            if (float.IsNaN(dt) || float.IsInfinity(dt) || dt > maxFrameGap) { ResetMotion(); return; }
            var turning = turnRoot ? turnRoot : motionRoot;
            Vector3 position = motionRoot.position, forward = turning.forward;
            if (!primed)
            {
                previousPosition = position; previousForward = forward; primed = true;
                ApplyOffsets(); return;
            }
            Vector3 travel = position - previousPosition;
            if (travel.sqrMagnitude > teleportDistance * teleportDistance) { ResetMotion(); return; }
            Vector3 velocity = travel / dt, acceleration = (velocity - previousVelocity) / dt;
            Vector3 axis = turnAxisWorld.sqrMagnitude > .0001f ? turnAxisWorld.normalized : Vector3.up;
            Vector3 a = Vector3.ProjectOnPlane(previousForward, axis), b = Vector3.ProjectOnPlane(forward, axis);
            float turnSpeed = a.sqrMagnitude > .0001f && b.sqrMagnitude > .0001f ? Vector3.SignedAngle(a, b, axis) / dt : 0;
            previousPosition = position; previousVelocity = velocity; previousForward = forward;
            float blend = 1 - Mathf.Exp(-inputSharpness * dt);
            filteredAcceleration = Mathf.Lerp(filteredAcceleration, Mathf.Clamp(Vector3.Dot(acceleration,
                accelerationAxisWorld.normalized), -100, 100), blend);
            filteredTurn = Mathf.Lerp(filteredTurn, Mathf.Clamp(turnSpeed, -720, 720), blend);
            speed = animator && hasSpeed ? Mathf.Clamp01(animator.GetFloat(speedHash)) : 0;
            bool locomotion = false;
            AnimatorStateInfo state = default(AnimatorStateInfo);
            if (animator && animator.runtimeAnimatorController)
            {
                state = animator.GetCurrentAnimatorStateInfo(0);
                locomotion = state.shortNameHash == locomotionHash && !animator.IsInTransition(0);
            }
            float targetRun = locomotion && grounded && !suppressLocomotion ? Mathf.InverseLerp(runStart, fullRun, speed) : 0;
            runWeight = Mathf.Lerp(runWeight, targetRun, blend);
            // 한 stride의 발 접지점을 0/.5로 맞춘다. 두 발이 번갈아 닿으므로 2회 보빙.
            float stride = state.normalizedTime - Mathf.Floor(state.normalizedTime);
            if (locomotion) bob = .5f - .5f * Mathf.Cos(stride * Mathf.PI * 4);
            breathPhase = Mathf.Repeat(breathPhase + breathHz * dt, 1);
            foreach (var j in joints)
            {
                j.wait -= dt;
                if (j.wait <= 0)
                {
                    j.age = 0; j.sign = random.NextDouble() < .5 ? -1 : 1;
                    j.wait = Range(j.twitchInterval.x, j.twitchInterval.y);
                }
                j.age += dt;
                float t = Mathf.Clamp01(j.age / j.twitchSeconds);
                float twitch = j.sign * j.twitchDegrees * Mathf.Sin(t * Mathf.PI * 2) * (1 - t);
                double target = Mathf.Clamp(-filteredAcceleration * j.accelerationGain - filteredTurn * j.turnGain + twitch,
                    -j.maxAngle, j.maxAngle);
                PetSpringMath.Step(ref j.angle, ref j.velocity, target, Math.Max(.1, j.frequencyHz), dt);
                double limited = Math.Max(-j.maxAngle, Math.Min(j.maxAngle, j.angle));
                if (limited != j.angle) j.velocity = 0;
                j.angle = limited;
            }
            ApplyOffsets();
        }
        void ApplyOffsets()
        {
            if (visualOffset)
            {
                bodyPositionOffset = Vector3.up * (bob * runBobHeight * runWeight);
                bodyRotationOffset = Quaternion.AngleAxis(-facingSign * runLeanDegrees * runWeight, leanAxisLocal.normalized);
                visualOffset.localPosition += bodyPositionOffset;
                visualOffset.localRotation *= bodyRotationOffset; bodyApplied = true;
            }
            foreach (var j in joints)
            {
                if (j == null || !j.bone) continue;
                j.applied = Quaternion.AngleAxis((float)j.angle, j.localAxis.normalized);
                j.bone.localRotation *= j.applied; j.hasApplied = true;
            }
            foreach (var b in breathBones)
            {
                if (b == null || !b.bone) continue;
                float wave = Mathf.Sin((breathPhase + b.phaseOffset) * Mathf.PI * 2);
                b.applied = b.localAmplitude * (wave * Mathf.Lerp(1, movingBreathWeight, speed));
                b.bone.localPosition += b.applied; b.hasApplied = true;
            }
        }
    }
}
