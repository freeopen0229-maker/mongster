using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using FusionTamagotchi;

namespace Mongle.Slice.Server
{
    public interface IServerRandom { int Next(int exclusiveMaximum); }
    // 모듈로 편향을 버리는 rejection sampling. 게임 클라이언트에는 배포하지 않는다.
    public sealed class CryptoServerRandom:IServerRandom,IDisposable
    {
        readonly RandomNumberGenerator rng=RandomNumberGenerator.Create();
        public int Next(int maximum)
        {
            if(maximum<=0)throw new ArgumentOutOfRangeException(nameof(maximum));
            ulong range=1UL<<32,limit=range-range%(uint)maximum;var bytes=new byte[4];uint value;
            lock(rng){do{rng.GetBytes(bytes);value=BitConverter.ToUInt32(bytes,0);}while(value>=limit);}
            return (int)(value%(uint)maximum);
        }
        public void Dispose(){rng.Dispose();}
    }
    // DB 어댑터는 기존 월드와 같은 트랜잭션에 지갑/자산/수량/영수증/outbox를 포함해야 한다.
    public interface IEconomyTransaction
    {
        EconomyAsset Asset(string id);
        void AddAsset(EconomyAsset asset);
        void RemoveAsset(string id);
        EconomyListing EconomyOffer(string id);
        void AddEconomyOffer(EconomyListing offer);
        LimitedQuota Quota(int hiddenType);
        EconomyReceipt EconomyReceipt(string account,string request);
        void AddEconomyReceipt(EconomyReceipt receipt);
        bool HasCostume(string owner,string costume);
        void AddCostume(string owner,string costume);
        int Scrolls(string owner);
        void SetScrolls(string owner,int count);
        void AddAwakeningNotice(AwakeningNotice notice);
        string ExternalGrantOwner(string purchaseId);
        void RecordExternalGrant(string purchaseId,string account);
    }
    public sealed class WeightedCostume
    {
        public string Id;public int Weight,DuplicateThread;
    }
    public sealed class SpeciesRule
    {
        public int Id,HiddenType=-1;
        public string Name;
        public PetRarity Rarity;
        public string[] Skills=Array.Empty<string>();
    }
    public sealed class ArchiveCostume
    { public string Id;public int Season,ThreadCost; }
    public sealed class EconomyPolicy
    {
        public int CurrentSeason=1,MaxPetTransfers=3,AdultMinLevel=0; // 성체 성장 레벨 미정. 설정 전 각성 비활성.
        public int MarketFeeBasisPoints=500; // 500~1000. 올림 소각.
        public int NormalCommonWeight=0,NormalRareWeight=0; // 미정: 설정 전 일반 알 추첨 불가.
        public int RaidGemPartsPerMillion=0; // 미정: 1000~5000 (0.1~0.5%) 중 선택.
        public long RerollBaseJeon=1000,RerollStepJeon=250,RerollMaximumJeon=1000000000;
        public long FoodJeon=500,WashJeon=300; // 명세의 예시 기본값, 운영 전 검토.
        public readonly Dictionary<int,SpeciesRule> Species=new Dictionary<int,SpeciesRule>();
        public readonly List<WeightedCostume> SeasonCostumes=new List<WeightedCostume>();
        public readonly Dictionary<string,ArchiveCostume> Archive=new Dictionary<string,ArchiveCostume>();
        public void Validate()
        {
            if(AdultMinLevel<0||CurrentSeason<1||MaxPetTransfers<1||MaxPetTransfers>10||MarketFeeBasisPoints<500||MarketFeeBasisPoints>1000||
               RerollBaseJeon<1||RerollStepJeon<1||RerollMaximumJeon<RerollBaseJeon||FoodJeon<1||WashJeon<1||
               NormalCommonWeight<0||NormalRareWeight<0||NormalCommonWeight>1000000||NormalRareWeight>1000000||
               (RaidGemPartsPerMillion!=0&&(RaidGemPartsPerMillion<1000||RaidGemPartsPerMillion>5000)))
                throw new InvalidOperationException("Invalid economy policy.");
        }
        public long RerollCost(int count)
        {
            if(count<0)throw new InvalidOperationException("Invalid reroll count.");
            return Math.Min(RerollMaximumJeon,checked(RerollBaseJeon+checked((long)count*RerollStepJeon)));
        }
        // 운영 서비스는 생성 후 policy를 수정하지 않는다. 스냅샷 변경은 새 인스턴스로 배포한다.
        public EconomyPolicy Copy()
        {
            var p=new EconomyPolicy{CurrentSeason=CurrentSeason,MaxPetTransfers=MaxPetTransfers,AdultMinLevel=AdultMinLevel,
                MarketFeeBasisPoints=MarketFeeBasisPoints,NormalCommonWeight=NormalCommonWeight,NormalRareWeight=NormalRareWeight,
                RaidGemPartsPerMillion=RaidGemPartsPerMillion,RerollBaseJeon=RerollBaseJeon,RerollStepJeon=RerollStepJeon,
                RerollMaximumJeon=RerollMaximumJeon,FoodJeon=FoodJeon,WashJeon=WashJeon};
            foreach(var x in Species)p.Species.Add(x.Key,new SpeciesRule{Id=x.Value.Id,Name=x.Value.Name,Rarity=x.Value.Rarity,
                HiddenType=x.Value.HiddenType,Skills=(string[])x.Value.Skills.Clone()});
            foreach(var x in SeasonCostumes)p.SeasonCostumes.Add(new WeightedCostume{Id=x.Id,Weight=x.Weight,DuplicateThread=x.DuplicateThread});
            foreach(var x in Archive)p.Archive.Add(x.Key,new ArchiveCostume{Id=x.Value.Id,Season=x.Value.Season,ThreadCost=x.Value.ThreadCost});
            return p;
        }
    }
    // 구매 검증자는 앱스토어 영수증 원문을 검증하고 서버가 신뢰할 수 있는 거래번호/보석 수를 반환한다.
    // 현재 실제 Apple/Google 검증 어댑터는 없다. 클라이언트가 아래 결과를 직접 보내면 안 된다.
    public sealed class VerifiedGemPurchase { public string PurchaseId,AccountId;public int Gems; }
    public sealed class VerifiedRaidReward
    { public string RewardId,AccountId;public long Jeon;public int NormalEggs; }
    public interface IRaidRewardVerifier { VerifiedRaidReward Verify(string authenticatedOwner,string completionToken); }
    public interface IGemPurchaseVerifier { VerifiedGemPurchase Verify(string authenticatedOwner,string receiptToken); }
}
