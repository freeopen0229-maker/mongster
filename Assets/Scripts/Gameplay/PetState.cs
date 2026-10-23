using System;
namespace Mongle.Gameplay
{
    /// <summary>저장 가능한 순수 데이터. Unity 오브젝트와 분리하여 시간/보상 로직을 직접 검사한다.</summary>
    [Serializable]
    public sealed class PetState
    {
        public int version=1,level=1,xp=0,coins=0,food=10;
        public double hunger=80,affection=50,fatigue=10;
        public double idleSeconds,pendingCoins,pendingXp;
        public long lastUtc,lastFeedUtc,lastPetUtc,lastPlayUtc,bonusUntilUtc;
    }
    public enum CareAction { Feed,Pet,Play }
    public struct PetCombatStats
    {
        public float attack,maxHealth,moveSpeed,condition;
    }
    public struct IdleReward { public int coins,xp; }

    /// <summary>시간 단위는 초, 욕구는 0~100. 이 예제의 숫자는 출시 밸런스가 아닌 조정 가능한 기본값이다.</summary>
    public static class PetRules
    {
        public const double MaxIdleSeconds=8*3600;
        static double Clamp(double v,double min,double max){return Math.Max(min,Math.Min(max,v));}
        public static PetState New(long now){return new PetState{lastUtc=now};}
        public static bool IsValid(PetState s)
        {
            return s!=null&&s.version==1&&s.level>=1&&s.level<=100&&s.xp>=0&&s.coins>=0&&s.food>=0&&s.lastUtc>=0
                &&Finite(s.hunger)&&Finite(s.affection)&&Finite(s.fatigue)&&Finite(s.idleSeconds)&&Finite(s.pendingCoins)&&Finite(s.pendingXp)
                &&s.hunger>=0&&s.hunger<=100&&s.affection>=0&&s.affection<=100&&s.fatigue>=0&&s.fatigue<=100
                &&s.idleSeconds>=0&&s.idleSeconds<=MaxIdleSeconds&&s.pendingCoins>=0&&s.pendingCoins<1000000&&s.pendingXp>=0&&s.pendingXp<1000000;
        }
        static bool Finite(double x){return !double.IsNaN(x)&&!double.IsInfinity(x);}
        /// <summary>
        /// UTC 경과를 한 번만 소비한다. 같은 시각/시계 역행은 보상과 감소를 발생시키지 않는다.
        /// 일시정지와 히트스톱의 게임 시간 배율은 생활 시간에 영향을 주지 않는다.
        /// 시계 조작을 막는 서버 검증은 별도지만, 타임스탬프를 되돌려 중복 보상하지 않는다.
        /// </summary>
        public static void Advance(PetState s,long now)
        {
            if(now<=s.lastUtc)return;
            double seconds=now-s.lastUtc;
            // 극단적인 시계 변경에도 능력치 연산이 넘치지 않도록 감소 계산만 30일로 제한한다.
            double hours=Math.Min(seconds,30*86400)/3600;
            s.hunger=Clamp(s.hunger-hours*3,0,100);
            s.affection=Clamp(s.affection-hours*.2,0,100);
            s.fatigue=Clamp(s.fatigue-hours*4,0,100);
            double eligible=Math.Min(seconds,Math.Max(0,MaxIdleSeconds-s.idleSeconds));
            // 돌봄/전투로 얻은 보너스 기간과 실제 적립 구간의 교집합만 가산한다.
            double bonus=Math.Max(0,Math.Min((double)s.bonusUntilUtc,s.lastUtc+eligible)-s.lastUtc);
            bonus=Math.Min(eligible,bonus);
            s.pendingCoins+=eligible/300+bonus/600; // 기본 5분/1코인, 보너스 중 50% 증가
            s.pendingXp+=eligible/600+bonus/1200;
            s.idleSeconds+=eligible;s.lastUtc=now;
        }
        public static PetCombatStats Stats(PetState s)
        {
            double c=.55+s.hunger*.0025+s.affection*.0015+(100-s.fatigue)*.0005;
            return new PetCombatStats{condition=(float)c,attack=(float)((12+(s.level-1)*1.4)*(.65+c*.35)),maxHealth=(float)((100+(s.level-1)*8)*(.9+c*.1)),moveSpeed=(float)(3.5*(.85+c*.15))};
        }
        public static bool Care(PetState s,CareAction action,long now)
        {
            Advance(s,now);
            long last=action==CareAction.Feed?s.lastFeedUtc:action==CareAction.Pet?s.lastPetUtc:s.lastPlayUtc;
            int cooldown=action==CareAction.Feed?30:action==CareAction.Pet?10:60;
            if(last>0&&now-last<cooldown)return false;
            if(action==CareAction.Feed)
            {
                if(s.food<=0||s.hunger>=99)return false;
                s.food--;s.hunger=Clamp(s.hunger+25,0,100);s.affection=Clamp(s.affection+1,0,100);s.lastFeedUtc=now;
            }
            else if(action==CareAction.Pet){s.affection=Clamp(s.affection+2,0,100);s.lastPetUtc=now;}
            else
            {
                if(s.fatigue>90)return false;
                s.affection=Clamp(s.affection+4,0,100);s.fatigue=Clamp(s.fatigue+6,0,100);s.hunger=Clamp(s.hunger-3,0,100);s.lastPlayUtc=now;GainXp(s,8);
            }
            s.bonusUntilUtc=Math.Max(s.bonusUntilUtc,now+2*3600);return true;
        }
        /// <summary>활동/방치 코인을 먹이로 환원해 돌봄→전투→보상→돌봄의 고리를 만든다.</summary>
        public static bool BuyFood(PetState s,int count=1)
        {
            if(count<1||count>99||s.coins<count*5)return false;
            s.coins-=count*5;s.food+=count;return true;
        }
        public static bool SpendAttack(PetState s)
        {
            if(s.fatigue>=98)return false;
            s.fatigue=Clamp(s.fatigue+.6,0,100);s.hunger=Clamp(s.hunger-.15,0,100);return true;
        }
        /// <summary>실제 적 처치가 확정된 이벤트에서만 호출한다. 버튼 클릭 보상이 아니다.</summary>
        public static void Victory(PetState s,long now)
        {
            Advance(s,now);s.coins+=8;GainXp(s,12);s.affection=Clamp(s.affection+.4,0,100);
            s.bonusUntilUtc=Math.Max(s.bonusUntilUtc,now+2*3600);
        }
        public static IdleReward Claim(PetState s,long now)
        {
            Advance(s,now);var r=new IdleReward{coins=(int)Math.Floor(s.pendingCoins+1e-8),xp=(int)Math.Floor(s.pendingXp+1e-8)};
            s.pendingCoins=Math.Max(0,s.pendingCoins-r.coins);s.pendingXp=Math.Max(0,s.pendingXp-r.xp);s.idleSeconds=0;s.coins+=r.coins;GainXp(s,r.xp);return r;
        }
        static void GainXp(PetState s,int amount)
        {
            s.xp+=amount;
            while(s.level<100&&s.xp>=s.level*30){s.xp-=s.level*30;s.level++;}
        }
    }
}
