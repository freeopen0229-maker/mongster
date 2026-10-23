using System;

namespace Mongle.Slice
{
    /// <summary>
    /// CurrentTradeCount는 '누적 거래 수'가 아닌 '남은 거래 가능 횟수'. 3→2→1→0.
    /// 직렬화 필드는 UI 표시용으로도 전달하지만 서버는 클라이언트의 이 값을 신뢰하지 않는다.
    /// </summary>
    [Serializable]
    public sealed class PetTradeData
    {
        public int MaxTradeCount, CurrentTradeCount;
        public bool IsSoulbound;
        public string BoundAccountId = "";
        public long LastTransferredUtc;

        public static PetTradeData Create(int maximum)
        {
            if(maximum<1||maximum>10)throw new ArgumentOutOfRangeException(nameof(maximum));
            return new PetTradeData{MaxTradeCount=maximum,CurrentTradeCount=maximum};
        }
        public PetTradeData Clone()=>(PetTradeData)MemberwiseClone();
        public void Validate(string currentOwner)
        {
            if(MaxTradeCount<1||MaxTradeCount>10||CurrentTradeCount<0||CurrentTradeCount>MaxTradeCount||LastTransferredUtc<0||
               BoundAccountId==null||IsSoulbound!=(CurrentTradeCount==0)||
               (IsSoulbound?BoundAccountId!=currentOwner:BoundAccountId!=""))
                throw new InvalidOperationException("Invalid trade/soulbound state.");
        }
        public bool CanTrade(string owner)
        { Validate(owner);return !IsSoulbound&&CurrentTradeCount>0; }

        /// <summary>서버 트랜잭션 안에서만 호출한다. 이 메서드만으로 인증/잔액/소유권 검증이 되지는 않는다.</summary>
        public void ConsumeTransfer(string currentOwner,string nextOwner,long serverUtc)
        {
            if(!PetIds.Valid(nextOwner)||currentOwner==nextOwner||serverUtc<LastTransferredUtc||!CanTrade(currentOwner))
                throw new InvalidOperationException("Pet cannot be transferred.");
            CurrentTradeCount--;LastTransferredUtc=serverUtc;
            if(CurrentTradeCount==0){IsSoulbound=true;BoundAccountId=nextOwner;}
        }
    }
}
