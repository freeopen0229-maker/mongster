using System;

namespace Mongle.VisualRenewal
{
    /// <summary>Transform과 분리한 임계 감쇠 해석식. 각도는 도, 시간은 초.</summary>
    public static class PetSpringMath
    {
        public static void Step(ref double angle, ref double velocity, double target,
                                double frequencyHz, double dt)
        {
            if (dt <= 0) return;
            if (!Finite(dt) || !Finite(target) || !Finite(frequencyHz) || frequencyHz <= 0 ||
                !Finite(angle) || !Finite(velocity)) throw new ArgumentOutOfRangeException("spring input");
            double omega = 2 * Math.PI * frequencyHz;
            double error = angle - target, c = velocity + omega * error, decay = Math.Exp(-omega * dt);
            angle = target + (error + c * dt) * decay;
            velocity = (velocity - omega * c * dt) * decay;
        }
        public static double WrapDegrees(double delta)
        {
            delta %= 360;
            if (delta > 180) delta -= 360;
            if (delta < -180) delta += 360;
            return delta;
        }
        static bool Finite(double x) => !double.IsNaN(x) && !double.IsInfinity(x);
    }
}
