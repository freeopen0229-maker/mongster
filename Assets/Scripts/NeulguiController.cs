using System;
using UnityEngine;

namespace Mongle
{
    [Serializable]
    public class PetLeg
    {
        public Transform upper, lower, paw;
        public float neutral, offset, bend;
        [NonSerialized] public float plant, from, to, q, height, settleTime;
        [NonSerialized] public Vector2 foot, lastAnkle;
        [NonSerialized] public bool wasPlanted;
        [NonSerialized] public int mode; // 0 standing, 1 planted, 2 swinging, 3 settling
    }

    public sealed class NeulguiController : MonoBehaviour
    {
        public Transform flip, bodyMotion, eyesClosed;
        public Animator animator;
        public PetLeg[] legs;
        public float groundY, walkSpeed = .62f;
        public bool showBones;
        public float MaxPlantDrift { get; private set; }
        public float MaxReachError { get; private set; }
        public string MotionState { get; private set; } = "Idle";
        float target, velocity, phase, turnTime, elapsed, happyUntil, nextBlink = 2.7f, blinkUntil;
        int facing = 1, settling = -1;
        bool paused, slow, patrol;
        float waitUntil;
        const float TurnDuration = .34f;

        void Start()
        {
            target = transform.position.x;
            ResetFeet();
            animator.applyRootMotion = false;
            Application.targetFrameRate = 60;
        }
        void ResetFeet()
        {
            settling = -1;
            foreach (var leg in legs)
            {
                leg.foot = new Vector2(transform.position.x + facing * leg.neutral, groundY);
                leg.plant = leg.foot.x; leg.mode = 0; leg.wasPlanted = false;
            }
        }
        public void GoTo(float x) { target = Mathf.Clamp(x, -2.3f, 2.3f); }
        public void Rest() { patrol = false; target = transform.position.x; }
        public void Pet() { Rest(); happyUntil = elapsed + 1.4f; }
        public void Turn() { patrol = false; GoTo(transform.position.x - facing * .8f); }
        public void Walk() { patrol = true; GoTo(transform.position.x >= 0 ? -1.7f : 1.7f); }

        void Update()
        {
            float dt = paused ? 0 : Mathf.Min(Time.deltaTime, .04f) * (slow ? .5f : 1f);
            animator.speed = paused ? 0 : slow ? .5f : 1f;
            if (dt <= 0) return;
            elapsed += dt;
            float uiScale = Mathf.Clamp(Screen.width / 900f, .7f, 1.7f);
            if (Camera.main && Input.GetMouseButtonDown(0) && Input.mousePosition.y < Screen.height - 130 * uiScale && Input.mousePosition.y > 85 * uiScale)
            {
                var point = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                if (Mathf.Abs(point.x - transform.position.x) < .85f && point.y > 0 && point.y < 2.8f) Pet();
                else { patrol = false; GoTo(point.x); }
            }
            float dx = target - transform.position.x;
            int wanted = dx < 0 ? -1 : 1;
            if (Mathf.Abs(dx) > .07f && wanted != facing && turnTime <= 0) turnTime = TurnDuration;
            if (turnTime > 0)
            {
                turnTime = Mathf.Max(0, turnTime - dt);
                velocity *= Mathf.Exp(-18 * dt);
                if (turnTime < TurnDuration * .5f && facing != wanted)
                {
                    facing = wanted; flip.localScale = new Vector3(facing, 1, 1);
                    ResetFeet(); phase = 0;
                }
            }
            else
            {
                float wantedSpeed = Mathf.Abs(dx) < .02f ? 0 : Mathf.Sign(dx) * Mathf.Min(walkSpeed, Mathf.Abs(dx) * 3);
                velocity = Mathf.Lerp(velocity, wantedSpeed, 1 - Mathf.Exp(-9 * dt));
            }
            float oldX = transform.position.x;
            float travel = velocity * dt;
            if (Mathf.Abs(dx) < .03f && Mathf.Abs(travel) > Mathf.Abs(dx)) { transform.position = new Vector3(target, 0, 0); velocity = 0; }
            else transform.position += new Vector3(travel, 0, 0);
            phase += Mathf.Abs(transform.position.x - oldX) / NeulguiMath.Stride;
            bool moving = Mathf.Abs(velocity) > .05f && turnTime <= 0;
            for (int i = 0; i < legs.Length; i++) UpdateFoot(legs[i], i, moving, dt);
            if (patrol && Mathf.Abs(target - transform.position.x) < .03f && Mathf.Abs(velocity) < .03f)
            {
                if (waitUntil == 0) waitUntil = elapsed + 1.2f;
                if (elapsed >= waitUntil) { GoTo(facing > 0 ? -1.7f : 1.7f); waitUntil = 0; }
            }
            if (elapsed >= nextBlink) { blinkUntil = elapsed + .13f; nextBlink = elapsed + 3.2f + Mathf.Sin(elapsed) * .5f; }
            eyesClosed.gameObject.SetActive(elapsed < blinkUntil);
            MotionState = turnTime > 0 ? "Turn" : elapsed < happyUntil ? "Happy" : moving ? "Walk" : "Idle";
            animator.SetFloat("GaitRate", Mathf.Max(.05f, Mathf.Abs(velocity) / walkSpeed));
            animator.SetBool("Walking", moving);
            animator.SetBool("Happy", elapsed < happyUntil);
            animator.SetBool("Turning", turnTime > 0);
        }
        void UpdateFoot(PetLeg leg, int index, bool moving, float dt)
        {
            float q = Mathf.Repeat(phase + leg.offset, 1);
            float neutral = transform.position.x + facing * leg.neutral;
            if (moving)
            {
                settling = -1;
                if (q < NeulguiMath.Stance)
                {
                    if (leg.mode != 1) { leg.plant = leg.mode == 2 ? leg.to : leg.foot.x; leg.mode = 1; }
                    leg.foot = new Vector2(leg.plant, groundY);
                }
                else
                {
                    float u = (q - NeulguiMath.Stance) / (1 - NeulguiMath.Stance);
                    if (leg.mode != 2)
                    {
                        leg.mode = 2; leg.from = leg.foot.x;
                        leg.to = neutral + facing * (NeulguiMath.Stride * (1 - q) + NeulguiMath.Stride * .24f);
                    }
                    leg.foot = new Vector2(Mathf.Lerp(leg.from, leg.to, NeulguiMath.Ease(u)), groundY + Mathf.Sin(Mathf.PI * u) * .11f);
                }
            }
            else
            {
                if (leg.mode == 1 || leg.mode == 2) { leg.mode = 0; leg.height = leg.foot.y; }
                if (leg.mode == 3)
                {
                    leg.settleTime = Mathf.Min(1, leg.settleTime + dt / .24f);
                    leg.foot = new Vector2(Mathf.Lerp(leg.from, leg.to, NeulguiMath.Ease(leg.settleTime)), groundY + Mathf.Sin(Mathf.PI * leg.settleTime) * .06f);
                    if (leg.settleTime >= 1) { leg.mode = 0; settling = -1; }
                }
                else if (settling < 0 && Mathf.Abs(leg.foot.x - neutral) > .02f)
                {
                    settling = index; leg.mode = 3; leg.from = leg.foot.x; leg.to = neutral; leg.settleTime = 0;
                }
                else leg.foot = new Vector2(leg.foot.x, Mathf.Lerp(leg.foot.y, groundY, 1 - Mathf.Exp(-16 * dt)));
            }
            leg.q = q;
        }
        void LateUpdate()
        {
            if (legs == null) return;
            if (!paused && MotionState == "Walk" && animator.GetCurrentAnimatorStateInfo(0).IsName("Walk") && !animator.IsInTransition(0))
            {
                animator.Play("Walk", 0, Mathf.Repeat(phase, 1));
                animator.Update(0);
            }
            for (int i = 0; i < legs.Length; i++)
            {
                var leg = legs[i];
                Vector2 hip = bodyMotion.TransformPoint(new Vector3(i == 0 ? -.24f : .42f, .40f, 0));
                Vector2 knee = NeulguiMath.Knee(hip, leg.foot, leg.bend * facing);
                // Hold the geometric limb lengths; report targets beyond anatomical reach.
                Vector2 delta = leg.foot - hip;
                float max = NeulguiMath.Upper + NeulguiMath.Lower - .0001f;
                Vector2 ankle = hip + Vector2.ClampMagnitude(delta, max);
                MaxReachError = Mathf.Max(MaxReachError, Mathf.Max(0, delta.magnitude - max));
                // Measure the rendered ankle after IK, not the requested ground target.
                if (leg.mode == 1 && leg.wasPlanted)
                    MaxPlantDrift = Mathf.Max(MaxPlantDrift, Vector2.Distance(leg.lastAnkle, ankle));
                leg.lastAnkle = ankle; leg.wasPlanted = leg.mode == 1;
                PlaceSegment(leg.upper, hip, knee, NeulguiMath.Upper);
                PlaceSegment(leg.lower, knee, ankle, NeulguiMath.Lower);
                leg.paw.position = new Vector3(ankle.x, ankle.y, 0);
                leg.paw.rotation = Quaternion.identity;
                leg.paw.localScale = new Vector3(facing, 1, 1);
            }
        }
        static void PlaceSegment(Transform segment, Vector2 a, Vector2 b, float length)
        {
            segment.position = a;
            segment.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg + 90);
            segment.localScale = new Vector3(.22f, length + .05f, 1);
        }
        void OnGUI()
        {
            float ui = Mathf.Clamp(Screen.width / 900f, .7f, 1.7f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(ui, ui, 1));
            float width = Screen.width / ui, height = Screen.height / ui;
            if (showBones && Camera.main)
            {
                foreach (var leg in legs)
                {
                    OverlayLine(leg.upper.position, leg.lower.position, ui);
                    OverlayLine(leg.lower.position, leg.paw.position, ui);
                }
            }
            GUI.Box(new Rect(18, 18, width - 36, 105), "NEULGUI / UNITY NATIVE 2D");
            GUI.Label(new Rect(36, 48, width - 72, 28), "One pet. Original painted parts. Animator + planted feet + two-bone legs.");
            GUI.Label(new Rect(36, 78, width - 72, 28), "State: " + MotionState + "  |  Unity " + Application.unityVersion);
            float bw = Mathf.Min(120, (width - 60) / 6);
            string[] labels = { "Rest", "Walk", "Turn", "Pet", paused ? "Resume" : "Pause", slow ? "1x" : "0.5x" };
            for (int i = 0; i < labels.Length; i++) if (GUI.Button(new Rect(22 + i * (bw + 3), height - 74, bw, 40), labels[i]))
            {
                switch (i) { case 0: Rest(); break; case 1: Walk(); break; case 2: Turn(); break; case 3: Pet(); break; case 4: paused = !paused; break; case 5: slow = !slow; break; }
            }
            showBones = GUI.Toggle(new Rect(24, height - 28, 140, 24), showBones, "Show joints");
            GUI.Label(new Rect(170, height - 28, width - 180, 24), "Plant drift: " + MaxPlantDrift.ToString("F4") + "  Reach error: " + MaxReachError.ToString("F4"));
        }
        static void OverlayLine(Vector3 worldA, Vector3 worldB, float ui)
        {
            Vector3 a = Camera.main.WorldToScreenPoint(worldA), b = Camera.main.WorldToScreenPoint(worldB);
            Vector2 p = new Vector2(a.x / ui, (Screen.height - a.y) / ui), q = new Vector2(b.x / ui, (Screen.height - b.y) / ui);
            Matrix4x4 matrix = GUI.matrix; Color color = GUI.color;
            GUI.color = Color.cyan;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(q.y - p.y, q.x - p.x) * Mathf.Rad2Deg, p);
            GUI.DrawTexture(new Rect(p.x, p.y - 1, Vector2.Distance(p,q), 2), Texture2D.whiteTexture);
            GUI.matrix = matrix; GUI.color = color;
        }
        void OnDrawGizmos()
        {
            if (!showBones || legs == null) return;
            Gizmos.color = Color.cyan;
            foreach (var leg in legs) if (leg.upper && leg.lower && leg.paw)
            {
                Gizmos.DrawLine(leg.upper.position, leg.lower.position);
                Gizmos.DrawLine(leg.lower.position, leg.paw.position);
                Gizmos.DrawSphere(leg.paw.position, .025f);
            }
        }
    }
}
