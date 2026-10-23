using System;
using System.Collections.Generic;
using Mongle.Slice;

namespace FusionTamagotchi
{
    // V2 명세의 5등급. V1 PetGrade와 숫자 캐스팅 금지: 별도의 명시적 데이터 이관 필요.
    public enum PetRarity { Common, Rare, Epic, Legendary, Divine }
    public enum EggKind { Normal, Luminous }
    public enum AssetKind { Pet, Egg, Material }
    public enum ShopProduct { HatchScroll, HatchScrollBundle, CostumeDraw, CostumeDrawTen, LuminousEgg, LuminousEggBundle }

    [Serializable]
    public struct UserCurrency
    {
        public long Jeon;
        public int Gem, MileageThread;
        public UserCurrency(long jeon, int gem = 0, int thread = 0)
        { Jeon = jeon; Gem = gem; MileageThread = thread; Validate(); }
        public void Validate()
        {
            if (Jeon < 0 || Jeon > 1000000000000L || Gem < 0 || MileageThread < 0)
                throw new InvalidOperationException("Invalid wallet.");
        }
    }
    [Serializable]
    public sealed class PetData
    {
        public const int CurrentSchema = 2;
        public int schemaVersion = CurrentSchema;
        public string petInstanceId, petName;
        public int speciesId;
        public PetRarity rarity;
        public int level = 1;
        public bool isAdult;
        public int fullness = 100, cleanliness = 100, affection = 50;
        // Eligibility는 서버 내부 필드. 아기 상태의 클라이언트 응답에 공개하지 않는다.
        public bool isHiddenPet;
        public int hiddenType = -1;
        public bool awakeningResolved;
        public int serialNumber;
        public int rerollAttempts; // 펫 전체 누적. 슬롯을 바꿔도 비용 초기화되지 않는다.
        public PetTradeData trade = PetTradeData.Create(3); // 최대 횟수는 운영 카탈로그가 덮어쓴다.
        public List<string> currentSkills = new List<string>();
        public List<int> slotRerollCounts = new List<int>();
        public int GetMaxSkillSlots()
        {
            switch(rarity)
            {
                case PetRarity.Common: case PetRarity.Rare: return 2;
                case PetRarity.Epic: case PetRarity.Legendary: return 3;
                case PetRarity.Divine: return 4;
                default: throw new InvalidOperationException("Unknown rarity.");
            }
        }
        public PetData Clone()
        {
            var p=(PetData)MemberwiseClone();p.trade=trade.Clone();
            p.currentSkills=new List<string>(currentSkills);p.slotRerollCounts=new List<int>(slotRerollCounts);return p;
        }
    }
    [Serializable]
    public sealed class EggData
    {
        public EggKind kind;
        public long incubationSeconds = 28800; // 8시간. 서버 카탈로그에서 8~12시간을 지정.
        public bool incubating;
        public long hatchAtUtc;
        public EggData Clone()=>(EggData)MemberwiseClone();
    }
    // 소유권/상장 잠금의 단일 원본. Egg/Material도 같은 경매 엔진을 거친다.
    [Serializable]
    public sealed class EconomyAsset
    {
        public string Id, OwnerId;
        public long Revision;
        public AssetKind Kind;
        public bool IsListed, Locked;
        public PetData Pet;
        public EggData Egg;
        public string MaterialId;
        public int Quantity=1;
        public EconomyAsset Clone()
        { var a=(EconomyAsset)MemberwiseClone();a.Pet=Pet?.Clone();a.Egg=Egg?.Clone();return a; }
    }
    [Serializable]
    public sealed class EconomyListing
    {
        public string Id, AssetId, SellerId;
        public long Price, AssetRevision, ExpiresUtc;
        public bool Active=true;
        public EconomyListing Clone()=>(EconomyListing)MemberwiseClone();
    }
    public sealed class LimitedQuota
    {
        public int HiddenType, Limit, Issued;
        public LimitedQuota Clone()=>(LimitedQuota)MemberwiseClone();
    }
    public sealed class EconomyReceipt
    {
        public string AccountId, RequestId, Fingerprint, ResultId;
        public long JeonSpent, FeeBurned;
        public int GemSpent, ThreadSpent;
        public string[] Rewards = Array.Empty<string>();
        public EconomyReceipt Clone()
        { var r=(EconomyReceipt)MemberwiseClone();r.Rewards=(string[])Rewards.Clone();return r; }
    }
    public sealed class AwakeningNotice
    {
        public string EventId, AssetId, OwnerId;
        public int HiddenType, Serial, Limit;
        public AwakeningNotice Clone()=>(AwakeningNotice)MemberwiseClone();
    }
    // 정수 기반 추첨. 10,000,000개 구간의 전체 합을 보존한다.
    public static class LuminousEggOdds
    {
        public const int Total=10000000;
        public static PetRarity Resolve(int roll)
        {
            if(roll<0||roll>=Total)throw new ArgumentOutOfRangeException(nameof(roll));
            if(roll<50)return PetRarity.Divine;        // 0.0005%
            if(roll<500000)return PetRarity.Legendary;// 4.9995%
            if(roll<3000000)return PetRarity.Epic;    // 25%
            return PetRarity.Rare;                   // 70%, Common 없음
        }
    }
    // Unity로 보내는 공개 모델. 각성 이전의 숨겨진 eligibility/type은 직렬화 대상에 없다.
    [Serializable]
    public sealed class PublicPetView
    {
        public string Id,Name;
        public int SpeciesId,Level,SerialNumber;
        public PetRarity Rarity;
        public bool IsAdult;
        public string[] Skills;
        public static PublicPetView From(PetData p)
        {
            return new PublicPetView{Id=p.petInstanceId,Name=p.petName,SpeciesId=p.speciesId,Level=p.level,
                SerialNumber=p.serialNumber,Rarity=p.rarity,IsAdult=p.isAdult,Skills=p.currentSkills.ToArray()};
        }
    }

}
