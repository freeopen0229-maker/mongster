using System;

namespace Mongle.Slice
{
    public enum PetGrade { Common, Uncommon, Rare, Unique, Spirit, Divine }
    public enum GrowthStage { Baby, Adult }
    public enum PetPersonality { Affectionate, Curious, Calm, Independent }
    public enum CareAction { Feed, Pet, Play }

    [Serializable]
    public sealed class PetIndividualValues
    {
        public int Health, Attack, Defense, Speed; // 서버 생성, 각각 0~31
        public bool IsValid => Health>=0&&Health<=31&&Attack>=0&&Attack<=31&&Defense>=0&&Defense<=31&&Speed>=0&&Speed<=31;
        public PetIndividualValues Clone() => (PetIndividualValues)MemberwiseClone();
    }

    /// <summary>UnityEngine 참조가 없는 서버/클라이언트 공용 스냅샷. 재화는 AccountState가 소유한다.</summary>
    [Serializable]
    public sealed class PetState
    {
        public int SchemaVersion = 1;
        public string PetId, OwnerAccountId;
        public long Revision, LastUtc, LastCareFeedUtc, LastCarePetUtc, LastCarePlayUtc;
        public int SpeciesId, Level = 1, Experience;
        public GrowthStage Stage;
        public PetGrade Grade;
        public PetPersonality Personality;
        public PetIndividualValues Individual = new PetIndividualValues();
        public int PotentialPermille = 1000; // 960~1040 등의 개체차. 유료 구매로 변경하지 않는다.
        public double Hunger = 80, Affection = 50, Fatigue = 10;
        public double UnclaimedIdleSeconds, PendingIdleCoins, PendingIdleXp;
        public bool IsListed, IsBusy, IsLocked;
        public string ExplorationId = "";
        public long ExplorationEndUtc;
        public string CostumeId = ""; // 렌더링에만 사용. 능력치 함수는 읽지 않는다.
        public PetTradeData Trade;

        public PetState Clone()
        {
            var p=(PetState)MemberwiseClone(); p.Individual=Individual?.Clone();p.Trade=Trade?.Clone();return p;
        }
        public void Validate()
        {
            // Backward-compatible absence in existing server snapshots.
            if(ExplorationId==null&&ExplorationEndUtc==0)ExplorationId="";
            if(SchemaVersion!=1||!PetIds.Valid(PetId)||!PetIds.Valid(OwnerAccountId)||Revision<0||LastUtc<0||
               LastCareFeedUtc<0||LastCarePetUtc<0||LastCarePlayUtc<0||SpeciesId<0||SpeciesId>129||Level<1||Level>100||Experience<0||
               !Enum.IsDefined(typeof(GrowthStage),Stage)||!Enum.IsDefined(typeof(PetGrade),Grade)||
               !Enum.IsDefined(typeof(PetPersonality),Personality)||Individual==null||!Individual.IsValid||
               PotentialPermille<900||PotentialPermille>1100||!Range(Hunger,0,100)||!Range(Affection,0,100)||!Range(Fatigue,0,100)||
               !Range(UnclaimedIdleSeconds,0,PetRules.MaxIdleSeconds)||!Range(PendingIdleCoins,0,1000000)||!Range(PendingIdleXp,0,1000000)||
               ExplorationId==null||ExplorationEndUtc<0||
               (ExplorationId!=""&&(!PetIds.Valid(ExplorationId)||!IsBusy||ExplorationEndUtc==0))||
               CostumeId==null||CostumeId.Length>80||Trade==null)
                throw new InvalidOperationException("Invalid pet snapshot.");
            Trade.Validate(OwnerAccountId);
        }
        static bool Range(double n,double min,double max)=>!double.IsNaN(n)&&!double.IsInfinity(n)&&n>=min&&n<=max;
    }
    public static class PetIds
    {
        public static bool Valid(string s)
        {
            if(string.IsNullOrEmpty(s)||s.Length>80)return false;
            foreach(char c in s)if(!(c>='a'&&c<='z'||c>='A'&&c<='Z'||c>='0'&&c<='9'||c=='-'||c=='_'))return false;
            return true;
        }
    }
}
