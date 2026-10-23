using UnityEngine;

namespace Mongle
{
    public static class NeulguiMath
    {
        public const float Upper = .27f, Lower = .27f, Stride = .48f, Stance = .64f;
        public static float Ease(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }
        public static Vector2 Knee(Vector2 hip, Vector2 foot, float bend)
        {
            Vector2 delta = foot - hip;
            float d = Mathf.Clamp(delta.magnitude, .0001f, Upper + Lower - .0001f);
            Vector2 u = delta.sqrMagnitude > .000001f ? delta.normalized : Vector2.down;
            float along = (Upper * Upper - Lower * Lower + d * d) / (2f * d);
            float across = Mathf.Sqrt(Mathf.Max(0, Upper * Upper - along * along));
            return hip + u * along + new Vector2(-u.y, u.x) * across * bend;
        }
    }
}
