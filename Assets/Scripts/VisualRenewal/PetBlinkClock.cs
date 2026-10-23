using System;

namespace Mongle.VisualRenewal
{
    /// <summary>Scaled dt로 진행하는 눈꺼풀 시간표. Unity와 분리해 확률/프레임 독립성을 검사한다.</summary>
    public sealed class PetBlinkClock
    {
        readonly Random random;
        readonly double minWait, maxWait, chance;
        double elapsed, duration;
        int phase; // 0 wait, 1 close, 2 hold, 3 open, 4 double-blink gap
        bool repeat;
        public float Closure { get; private set; }
        public int BlinkCount { get; private set; }
        public int BurstCount { get; private set; }
        public int DoubleCount { get; private set; }
        public PetBlinkClock(int seed, double minWait = 2.5, double maxWait = 6, double chance = .15)
        {
            if (minWait <= 0 || maxWait < minWait || chance < 0 || chance > 1)
                throw new ArgumentOutOfRangeException("blink settings");
            random = new Random(seed); this.minWait = minWait; this.maxWait = maxWait; this.chance = chance;
            Wait();
        }
        double Range(double a, double b) { return a + random.NextDouble() * (b - a); }
        void Wait() { phase = 0; elapsed = 0; duration = Range(minWait, maxWait); Closure = 0; }
        void Next()
        {
            elapsed = 0;
            switch (phase)
            {
                case 0:
                    repeat = random.NextDouble() < chance; BurstCount++;
                    if (repeat) DoubleCount++;
                    phase = 1; duration = .045; BlinkCount++; break;
                case 1: phase = 2; duration = .035; break;
                case 2: phase = 3; duration = .08; break;
                case 3:
                    if (repeat) { repeat = false; phase = 4; duration = .09; }
                    else Wait();
                    break;
                case 4: phase = 1; duration = .045; BlinkCount++; break;
            }
        }
        public void Tick(double dt)
        {
            if (double.IsNaN(dt) || double.IsInfinity(dt) || dt < 0)
                throw new ArgumentOutOfRangeException("dt");
            // 이벤트 경계를 넘은 나머지 시간을 보존한다. 프레임레이트가 달라도 같은 seed는 같은 시간표.
            while (dt > 0)
            {
                double advance = Math.Min(dt, duration - elapsed);
                elapsed += advance; dt -= advance;
                if (elapsed >= duration - 1e-10) Next();
            }
            double t = elapsed / duration;
            float smooth = (float)(t * t * (3 - 2 * t));
            Closure = phase == 1 ? smooth : phase == 2 ? 1 : phase == 3 ? 1 - smooth : 0;
        }
    }
}
