// GENERATED from ServerSources/WorldContracts.cs. Do not edit.
#if UNITY_EDITOR
using System;
using Mongle.Slice;

namespace Mongle.Slice.Server
{
    public interface IServerClock { long UtcSeconds{get;} }
    public sealed class SystemServerClock:IServerClock
    { public long UtcSeconds=>DateTimeOffset.UtcNow.ToUnixTimeSeconds(); }

    [Serializable] public sealed class AccountState
    {
        public string AccountId;
        public FusionTamagotchi.UserCurrency Currency;
        public long WalletRevision;
        // V1 서비스 호환 이름. 별도 잔액이 아니며 동일한 Jeon을 읽고 쓴다.
        public long Coins {get=>Currency.Jeon;set{long next=checked(WalletRevision+1);Currency.Jeon=value;WalletRevision=next;}}
        public int Food=10;
        public long TradeUtcDay=-1;public int TradesToday;
        public AccountState Clone()=>(AccountState)MemberwiseClone();
    }
    [Serializable] public sealed class TradeOffer
    {
        public string OfferId,PetId,SellerId;
        public long Price,PetRevision,ExpiresUtc;
        public bool Active=true;
        public TradeOffer Clone()=>(TradeOffer)MemberwiseClone();
    }
    public sealed class BuyPetRequest { public string OfferId,RequestId; }
    public sealed class TradeReceipt
    {
        public string BuyerId,SellerId,OfferId,PetId,RequestId;
        public long Price,FeeBurned,PetRevision,ServerUtc;
        public TradeReceipt Clone()=>(TradeReceipt)MemberwiseClone();
    }
    public sealed class ActionReceipt
    {
        public string AccountId,RequestId,Fingerprint;public PetSnapshot Result;
        public ActionReceipt Clone()=>new ActionReceipt{AccountId=AccountId,RequestId=RequestId,Fingerprint=Fingerprint,Result=Result.Clone()};
    }
    public interface IWorldTransaction:IEconomyTransaction
    {
        PetState Pet(string id);
        AccountState Account(string id);
        TradeOffer Offer(string id);
        void AddOffer(TradeOffer offer);
        TradeReceipt FindTradeReceipt(string buyer,string key);
        void AddTradeReceipt(TradeReceipt receipt);
        ActionReceipt FindActionReceipt(string owner,string key);
        void AddActionReceipt(ActionReceipt receipt);
        void Burn(long coins);
    }
    /// <summary>
    /// DB adapter는 콜백 전체를 원자적 commit/rollback해야 한다. pet/offer/wallet 잠금과
    /// receipt unique 제약을 같은 transaction에 포함한다. Unity 클라이언트에 넣지 않는다.
    /// </summary>
    public interface IWorldStore
    { T ExecuteSerializable<T>(Func<IWorldTransaction,T> work); }

    public sealed class TradePolicy
    {
        public int FeeBasisPoints=500; // 예제 5%. 판매자 수령액에서 차감 후 소각.
        public int MaxTradesPerAccountPerUtcDay=10;
        public long OwnershipCooldownSeconds=3600;
        public const long MaxPrice=1000000000,MaxWallet=1000000000000;
        public void Validate()
        {
            if(FeeBasisPoints<1||FeeBasisPoints>=10000||MaxTradesPerAccountPerUtcDay<1||OwnershipCooldownSeconds<0)
                throw new InvalidOperationException("Invalid trade policy.");
        }
        public long Fee(long price)
        {
            Validate();if(price<1||price>MaxPrice)throw new InvalidOperationException("Invalid listing price.");
            return checked((price*FeeBasisPoints+9999)/10000); // 올림. 저가 분할로 수수료 0 만들기 방지.
        }
    }
}

#endif
