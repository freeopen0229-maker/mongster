using System;
using Mongle.Gameplay;
public static class CoreLoopProbe
{
    static void Check(bool ok,string text){if(!ok)throw new Exception(text);}
    static void Near(double a,double b,string text){Check(Math.Abs(a-b)<1e-6,text+": "+a+" vs "+b);}
    public static void Main()
    {
        const long now=1700000000;
        var whole=PetRules.New(now);var split=PetRules.New(now);whole.bonusUntilUtc=split.bonusUntilUtc=now+7200;
        PetRules.Advance(whole,now+14400);
        for(int i=1;i<=240;i++)PetRules.Advance(split,now+i*60);
        Near(whole.hunger,split.hunger,"Decay depends on tick partition");Near(whole.affection,split.affection,"Bond decay differs");Near(whole.pendingCoins,split.pendingCoins,"Idle reward differs by tick partition");Near(whole.pendingXp,split.pendingXp,"XP differs by tick partition");
        var cap=PetRules.New(now);PetRules.Advance(cap,now+86400);Near(cap.idleSeconds,PetRules.MaxIdleSeconds,"Offline reward cap failed");
        double bank=cap.pendingCoins;PetRules.Advance(cap,now+86400*2);Near(cap.pendingCoins,bank,"Cap bypassed on next tick");
        var reward=PetRules.Claim(cap,now+86400*2);Check(reward.coins==96,"8h base reward amount incorrect");Check(PetRules.Claim(cap,now+86400*2).coins==0,"Duplicate claim paid twice");
        PetRules.Advance(cap,now);Check(cap.lastUtc==now+86400*2,"Clock rollback moved checkpoint");Near(cap.pendingCoins,0,"Clock rollback created reward");
        var care=PetRules.New(now);care.hunger=20;var before=PetRules.Stats(care);
        Check(PetRules.Care(care,CareAction.Feed,now),"Feed failed");Check(care.food==9&&care.hunger==45,"Feed resource accounting failed");Check(!PetRules.Care(care,CareAction.Feed,now+1),"Feed cooldown failed");Check(PetRules.Stats(care).attack>before.attack,"Care did not affect real combat stats");
        Check(PetRules.Care(care,CareAction.Pet,now+1),"Pet failed");double bond=care.affection;Check(!PetRules.Care(care,CareAction.Pet,now+2)&&care.affection<=bond&&care.lastPetUtc==now+1,"Pet repeated during cooldown");
        care.food=0;Check(!PetRules.Care(care,CareAction.Feed,now+31),"No-food feeding was allowed");care.fatigue=99;Check(!PetRules.SpendAttack(care),"Exhausted attack allowed");
        var active=PetRules.New(now);PetRules.Victory(active,now);Check(active.coins==8&&active.xp==12,"Active victory reward failed");Check(PetRules.BuyFood(active)&&active.coins==3&&active.food==11,"Active reward cannot fund care");Check(!PetRules.BuyFood(active,2)&&active.coins==3,"Food purchase exceeded currency");PetRules.Advance(active,now+3600);Near(active.pendingCoins,18,"Active play did not boost idle loop");
        var invalid=PetRules.New(now);invalid.hunger=double.NaN;Check(!PetRules.IsValid(invalid),"NaN save accepted");
        Check(PetRules.IsValid(whole)&&PetRules.IsValid(split)&&PetRules.IsValid(cap)&&PetRules.IsValid(care),"Valid states rejected");
        Console.WriteLine("PASS: tick partition invariance; hunger/bond/fatigue bounds; 8h reward cap; duplicate-claim and clock-rollback protection; care costs/cooldowns; live combat modifier; active-to-idle bonus; invalid save rejection.");
    }
}
