using System;
using System.Threading;
using System.Threading.Tasks;

namespace Mongle.Slice
{
    [Serializable]
    public sealed class PetSnapshot
    {
        public PetState Pet;
        public long AccountCoins,ServerUtcSeconds;
        public PetSnapshot Clone()=>new PetSnapshot{Pet=Pet.Clone(),AccountCoins=AccountCoins,ServerUtcSeconds=ServerUtcSeconds};
    }
    [Serializable]
    public sealed class PetActionRequest
    {
        public string PetId,RequestId;
        public long ExpectedRevision;
        public CareAction Action;
    }
    /// <summary>
    /// HTTPS 어댑터가 인증 토큰을 붙인다. 클라이언트는 소유자/개체값/보상액/시각을 보내지 않는다.
    /// 실제 HTTP 전송은 별도로 구현한다.
    /// </summary>
    public interface IPetGateway
    {
        Task<PetSnapshot> GetAsync(string petId,CancellationToken token);
        Task<PetSnapshot> CareAsync(PetActionRequest request,CancellationToken token);
        Task<PetSnapshot> ClaimIdleAsync(PetActionRequest request,CancellationToken token);
    }
    public enum PaidProductKind { IncubationTimeScroll, Costume, LuminousEgg }
    public static class MonetizationPolicy
    {
        public static bool IsAllowed(PaidProductKind kind)=>kind==PaidProductKind.IncubationTimeScroll||kind==PaidProductKind.Costume||kind==PaidProductKind.LuminousEgg;
        // V2: 영롱한 알 구매 허용. 스크롤은 부화 시간만 단축하며 잠재력/성격을 바꾸지 않는다.
        public static long ShortenIncubation(long serverNow,long currentEnd,long seconds)
        {
            if(serverNow<0||currentEnd<serverNow||seconds<=0)throw new ArgumentOutOfRangeException(nameof(seconds));
            return currentEnd-Math.Min(seconds,currentEnd-serverNow);
        }
    }
}
