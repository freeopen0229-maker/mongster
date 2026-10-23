using System;

namespace Mongle.Slice
{
    public struct PetCombatStats { public double Attack,Defense,Health,Speed,Condition; }
    public struct IdleReward { public long Coins; public int Experience; }

    /// <summary>서버가 확정하는 규칙. 클라이언트는 같은 코드를 UI 예상 표시용으로만 쓴다.</summary>
    public static class PetRules
    {
        public const double MaxIdleSeconds=8*3600;
        static double Clamp(double x,double min,double max)=>Math.Max(min,Math.Min(max,x));
        public static void Advance(PetState p,long serverUtc)
        {
            p.Validate();if(serverUtc<=p.LastUtc)return;
            // UTC 초는 양수 long. long끼리 뺄 때의 overflow를 피한다.
            double elapsed=(double)serverUtc-p.LastUtc, hours=elapsed/3600;
            p.Hunger=Clamp(p.Hunger-hours*3,0,100);
            p.Affection=Clamp(p.Affection-hours*.2,0,100);
            p.Fatigue=Clamp(p.Fatigue-hours*4,0,100);
            double rewardSeconds=Math.Min(elapsed,Math.Max(0,MaxIdleSeconds-p.UnclaimedIdleSeconds));
            p.PendingIdleCoins+=rewardSeconds/300;p.PendingIdleXp+=rewardSeconds/600;
            p.UnclaimedIdleSeconds+=rewardSeconds;p.LastUtc=serverUtc;
        }
        public static bool Care(PetState p,CareAction action,long serverUtc)
        {
            if(!Enum.IsDefined(typeof(CareAction),action))throw new ArgumentOutOfRangeException(nameof(action));
            Advance(p,serverUtc);
            if(p.IsListed||p.IsBusy||p.IsLocked)return false;
            long last=action==CareAction.Feed?p.LastCareFeedUtc:action==CareAction.Pet?p.LastCarePetUtc:p.LastCarePlayUtc;
            int cooldown=action==CareAction.Feed?30:action==CareAction.Pet?10:60;
            if(serverUtc<p.LastUtc||last>0&&serverUtc-last<cooldown)return false;
            if(action==CareAction.Feed)
            { if(p.Hunger>=99)return false;p.Hunger=Clamp(p.Hunger+25,0,100);p.Affection=Clamp(p.Affection+1,0,100);p.LastCareFeedUtc=serverUtc; }
            else if(action==CareAction.Pet)
            { p.Affection=Clamp(p.Affection+2,0,100);p.LastCarePetUtc=serverUtc; }
            else
            { if(p.Fatigue>90)return false;p.Affection=Clamp(p.Affection+4,0,100);p.Hunger=Clamp(p.Hunger-3,0,100);p.Fatigue=Clamp(p.Fatigue+6,0,100);GainXp(p,8);p.LastCarePlayUtc=serverUtc; }
            return true;
        }
        public static IdleReward ClaimIdle(PetState p,long serverUtc)
        {
            Advance(p,serverUtc);
            var r=new IdleReward{Coins=(long)Math.Floor(p.PendingIdleCoins+1e-8),Experience=(int)Math.Floor(p.PendingIdleXp+1e-8)};
            p.PendingIdleCoins=Math.Max(0,p.PendingIdleCoins-r.Coins);p.PendingIdleXp=Math.Max(0,p.PendingIdleXp-r.Experience);
            p.UnclaimedIdleSeconds=0;GainXp(p,r.Experience);return r;
        }
        public static void AwardExploration(PetState p){GainXp(p,8);p.Fatigue=Clamp(p.Fatigue+6,0,100);p.Hunger=Clamp(p.Hunger-3,0,100);}
        static void GainXp(PetState p,int amount)
        { p.Experience=checked(p.Experience+amount);while(p.Level<100&&p.Experience>=p.Level*30){p.Experience-=p.Level*30;p.Level++;} }
        public static PetCombatStats Stats(PetState p)
        {
            p.Validate();double condition=.55+p.Hunger*.0025+p.Affection*.0015+(100-p.Fatigue)*.0005;
            double potential=p.PotentialPermille/1000.0;
            return new PetCombatStats{Condition=condition,
                Attack=(12+(p.Level-1)*1.4+p.Individual.Attack*.12)*potential*(.65+.35*condition),
                Defense=(8+p.Level+p.Individual.Defense*.1)*potential*(p.Personality==PetPersonality.Calm?1.03:1),
                Health=(100+(p.Level-1)*8+p.Individual.Health*.8)*potential,
                Speed=3.5*(1+p.Individual.Speed*.001)*(.85+.15*condition)*(p.Personality==PetPersonality.Curious?1.03:1)};
        }
    }
}
