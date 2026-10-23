using System;
namespace Mongle
{
    // Clock logic is independent of Unity objects so overlap/restoration can be tested directly.
    public sealed class HitStopState
    {
        public bool Frozen {get;private set;}
        public double Until {get;private set;}
        float original=1;
        public float Trigger(double now,double seconds,float currentScale)
        {
            if(seconds<=0)return currentScale;
            if(!Frozen){original=currentScale;Frozen=true;}
            Until=Math.Max(Until,now+seconds);return 0;
        }
        public float Evaluate(double now)
        {
            if(Frozen&&now<Until)return 0;
            return Release();
        }
        public float Release(){Frozen=false;Until=0;return original;}
    }
}
