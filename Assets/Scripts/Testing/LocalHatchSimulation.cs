using System;

namespace Mongle.Slice.Testing
{
    /// <summary>테스트 전용 알. 서버 알/아이템/결제 인벤토리를 수정하지 않는다.</summary>
    public sealed class LocalHatchSimulation
    {
        public long EndUtc { get; private set; }
        public int Scrolls { get; private set; }
        public LocalHatchSimulation(long nowUtc,int hours=8,int scrolls=1)
        {
            if(nowUtc<0||hours<1||hours>24||scrolls<0)throw new ArgumentOutOfRangeException();
            EndUtc=checked(nowUtc+hours*3600L);Scrolls=scrolls;
        }
        public long Remaining(long nowUtc)
        {
            if(nowUtc<0)throw new ArgumentOutOfRangeException(nameof(nowUtc));
            return Math.Max(0,EndUtc-nowUtc);
        }
        public bool TryUseInstantScroll(long nowUtc)
        {
            long remaining=Remaining(nowUtc);
            if(remaining==0||Scrolls==0)return false;
            long nextEnd=MonetizationPolicy.ShortenIncubation(nowUtc,EndUtc,remaining);
            EndUtc=nextEnd;Scrolls--;return true;
        }
    }
}
