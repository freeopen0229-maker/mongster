using System.Threading;
namespace Mongle
{
    /// <summary>여러 펫과 발사체 시스템이 같은 적을 공격해도 ID가 충돌하지 않게 공유한다.</summary>
    public static class CombatAttackId
    {
        static int sequence;
        public static int Next(){return Interlocked.Increment(ref sequence);}
    }
}
