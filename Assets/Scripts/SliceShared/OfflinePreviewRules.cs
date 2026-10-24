using System;

namespace Mongle.OfflinePreview
{
    // Development sandbox only. Never imported into a server account or sent to a gateway.
    [Serializable] public sealed class PreviewSave
    {
        public int version=1,level=1,xp,coins=40,food=5,wins,bestStage;
        public double hunger=72,bond=40,clean=85,energy=100;
        public bool Valid()=>version==1&&level>=1&&level<=100&&xp>=0&&xp<3000&&coins>=0&&coins<=1000000&&food>=0&&food<=9999&&wins>=0&&bestStage>=0&&bestStage<=3&&Range(hunger)&&Range(bond)&&Range(clean)&&Range(energy);
        static bool Range(double n)=>!double.IsNaN(n)&&!double.IsInfinity(n)&&n>=0&&n<=100;
    }
    public static class PreviewRules
    {
        static double Clamp(double n)=>Math.Max(0,Math.Min(100,n));
        public static bool Care(PreviewSave p,int kind)
        {
            if(!p.Valid())throw new ArgumentException("Invalid preview save");
            if(kind==0){if(p.food<1||p.hunger>95)return false;p.food--;p.hunger=Clamp(p.hunger+24);p.bond=Clamp(p.bond+2);}
            else if(kind==1){p.bond=Clamp(p.bond+5);p.energy=Clamp(p.energy+3);}
            else if(kind==2){p.clean=100;p.bond=Clamp(p.bond+2);}
            else if(kind==3){if(p.energy<8)return false;p.energy-=8;p.bond=Clamp(p.bond+4);p.hunger=Clamp(p.hunger-4);Gain(p,6);}
            else if(kind==4){p.energy=Clamp(p.energy+20);p.hunger=Clamp(p.hunger-2);}
            else return false;
            return true;
        }
        public static bool BuyFood(PreviewSave p){if(p.coins<5||p.food>=9999)return false;p.coins-=5;p.food++;return true;}
        public static bool Begin(PreviewSave p,int stage){if(stage<1||stage>3||stage>p.bestStage+1||p.energy<10)return false;p.energy-=10;return true;}
        public static void Victory(PreviewSave p,int stage)
        {if(stage<1||stage>3)throw new ArgumentException("Invalid stage");p.wins++;p.bestStage=Math.Max(p.bestStage,stage);p.coins=Math.Min(1000000,p.coins+15+stage*5);p.clean=Clamp(p.clean-8);Gain(p,20+stage*10);}
        public static void Gain(PreviewSave p,int amount)
        {p.xp+=amount;while(p.level<100&&p.xp>=p.level*30){p.xp-=p.level*30;p.level++;}p.xp=Math.Min(p.xp,2999);}
        public static void Tick(PreviewSave p,double seconds)
        {double dt=Math.Max(0,Math.Min(seconds,5));p.hunger=Clamp(p.hunger-dt*.012);p.clean=Clamp(p.clean-dt*.008);p.energy=Clamp(p.energy+dt*.015);}
    }
}
