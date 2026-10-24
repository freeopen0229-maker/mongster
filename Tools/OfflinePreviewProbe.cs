using System;
using Mongle.OfflinePreview;
public static class OfflinePreviewProbe
{
    static int checks;
    static void Check(bool value,string name){if(!value)throw new Exception(name);checks++;}
    public static void Main()
    {
        var p=new PreviewSave();Check(p.Valid(),"fresh save");
        Check(PreviewRules.Care(p,0)&&p.food==4&&p.hunger==96,"feed debit");
        Check(!PreviewRules.Care(p,0)&&p.food==4,"full pet blocked");
        Check(!PreviewRules.Begin(p,2),"locked stage");
        Check(PreviewRules.Begin(p,1)&&p.energy==90,"entry consumes energy");
        PreviewRules.Victory(p,1);Check(p.bestStage==1&&p.wins==1&&p.level==2&&p.coins==60,"victory reward and level");
        Check(PreviewRules.Begin(p,2),"next stage unlocked");
        Check(PreviewRules.BuyFood(p)&&p.food==5&&p.coins==55,"food purchase");
        p.energy=0;Check(!PreviewRules.Begin(p,1),"exhausted entry blocked");
        Check(PreviewRules.Care(p,4)&&p.energy==20,"rest recovery");
        p.coins=0;Check(!PreviewRules.BuyFood(p),"no free food");
        p.hunger=double.NaN;Check(!p.Valid(),"corrupt save rejected");
        p=new PreviewSave();PreviewRules.Tick(p,100000);Check(p.Valid()&&p.hunger>71,"time capped");
        Check(!PreviewRules.Care(p,9),"unknown action");
        Console.WriteLine("PASS: "+checks+" isolated offline-preview care, combat reward, stage and save checks.");
    }
}
