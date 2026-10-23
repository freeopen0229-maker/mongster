using System;
using FusionTamagotchi.Client;
class HatchPresentationProbe
{
    static int count;
    static void Check(bool ok,string why){count++;if(!ok)throw new Exception(why);}
    static void Main()
    {
        var names=new[]{"Common","Rare","Epic","Legendary","Divine"};float last=-1;
        var colors=new System.Collections.Generic.HashSet<string>();
        foreach(var name in names)
        {
            var p=HatchPresentationPolicy.Style(name);
            Check(p.AuraAlpha>last&&p.AuraAlpha<=1,"grade aura strength");last=p.AuraAlpha;
            Check(colors.Add(p.R+"/"+p.G+"/"+p.B),"unique grade color");
        }
        Check(HatchPresentationPolicy.Serial(3,10)=="No. 003 / 010","divine serial");
        Check(HatchPresentationPolicy.Serial(42,100)=="No. 042 / 100","legendary serial");
        var notice=new HiddenAwakeningNoticeDTO{eventId="event",hiddenType=0,rarity="Divine",serialNo=3,maxLimit=10};
        Check(HatchPresentationPolicy.ValidNotice(notice),"divine notice");notice.serialNo=11;
        Check(!HatchPresentationPolicy.ValidNotice(notice),"serial range");notice.hiddenType=2;notice.rarity="Legendary";notice.maxLimit=100;
        Check(HatchPresentationPolicy.ValidNotice(notice),"legend notice");notice.rarity="Rare";
        Check(!HatchPresentationPolicy.ValidNotice(notice),"invalid awakened rarity");
        bool rejected=false;try{HatchPresentationPolicy.Serial(0,10);}catch(ArgumentOutOfRangeException){rejected=true;}Check(rejected,"zero serial");
        var tooltip=new SerialTooltipState();tooltip.Bind(false);tooltip.Enter(true);tooltip.Click(false);
        Check(!tooltip.Visible,"unavailable tooltip remains hidden");
        tooltip.Bind(true);tooltip.Enter(true);Check(tooltip.Visible,"mouse hover opens");
        tooltip.Click(true);Check(tooltip.Visible,"mouse click does not close hovered tooltip");
        tooltip.Exit(true);Check(!tooltip.Visible,"mouse exit closes");
        tooltip.Enter(false);Check(!tooltip.Visible,"touch enter does not pre-open");
        tooltip.Click(false);Check(tooltip.Visible,"first mobile tap opens");
        tooltip.Exit(false);Check(tooltip.Visible,"touch exit preserves tap-open state");
        tooltip.Click(false);Check(!tooltip.Visible,"second mobile tap closes");
        tooltip.Click(false);tooltip.Hide();Check(!tooltip.Visible,"disable closes");
        tooltip.Click(false);tooltip.Bind(false);Check(!tooltip.Visible&&!tooltip.Available,"pet selection change clears tooltip");
        notice.hiddenType=0;notice.rarity="Divine";notice.serialNo=3;notice.maxLimit=100;
        Check(!HatchPresentationPolicy.ValidNotice(notice),"divine limit must be ten");
        notice.hiddenType=1;notice.rarity="Legendary";notice.maxLimit=10;
        Check(!HatchPresentationPolicy.ValidNotice(notice),"legend limit must be hundred");
        notice.maxLimit=100;notice.hiddenType=5;Check(!HatchPresentationPolicy.ValidNotice(notice),"unknown hidden type");
        notice.hiddenType=4;notice.serialNo=0;Check(!HatchPresentationPolicy.ValidNotice(notice),"notice zero serial");
        notice.serialNo=100;Check(HatchPresentationPolicy.ValidNotice(notice),"last legendary serial valid");
        notice.serialNo=101;Check(!HatchPresentationPolicy.ValidNotice(notice),"over hundred rejected");
        var pet=new PetDTO{petId="pet-test",rarity="Divine",revision=0,isAdult=true,isHiddenPet=true,hiddenType=0,
            serialNumber=3,hiddenLimit=10,fullness=100,cleanliness=100,nextRerollCost=1000,
            skills=new System.Collections.Generic.List<string>{"s1","s2","s3","s4"}};
        pet.Validate();Check(true,"valid hidden DTO");
        pet.hiddenLimit=100;rejected=false;try{pet.Validate();}catch(InvalidOperationException){rejected=true;}Check(rejected,"Divine DTO rejects limit 100");
        pet.hiddenLimit=10;pet.isAdult=false;rejected=false;try{pet.Validate();}catch(InvalidOperationException){rejected=true;}Check(rejected,"baby cannot claim awakened identity");
        pet.isAdult=true;pet.hiddenType=2;pet.rarity="Legendary";pet.hiddenLimit=100;pet.skills.RemoveAt(3);pet.Validate();Check(true,"valid legendary DTO");
        pet.hiddenLimit=10;rejected=false;try{pet.Validate();}catch(InvalidOperationException){rejected=true;}Check(rejected,"Legendary DTO rejects limit 10");
        Check(!HatchPresentationPolicy.ValidHiddenIdentity(0,"Legendary",3,100),"grade and hidden type agree");
        Console.WriteLine("PASS: "+count+" presentation data checks. NOT Unity rendering, popup input, tooltip hover/touch, audio, Animator or Play Mode.");
    }
}
