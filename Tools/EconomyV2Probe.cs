using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FusionTamagotchi;
using Mongle.Slice;
using Mongle.Slice.Server;
using Mongle.Slice.Server.Testing;

public static class EconomyV2Probe
{
    sealed class Clock:IServerClock {public long UtcSeconds=>2000000;}
    sealed class RandomFake:IServerRandom
    {
        public readonly List<int> Calls=new List<int>();
        public readonly Dictionary<int,int> Values=new Dictionary<int,int>();
        public int Next(int max){Calls.Add(max);int value;return Values.TryGetValue(max,out value)?value:0;}
    }
    sealed class PurchaseFake:IGemPurchaseVerifier
    { public VerifiedGemPurchase Verify(string owner,string token)=>new VerifiedGemPurchase{AccountId=owner,PurchaseId="verified-purchase",Gems=100}; }
    sealed class RaidFake:IRaidRewardVerifier
    { public VerifiedRaidReward Verify(string owner,string token)=>new VerifiedRaidReward{AccountId=owner,RewardId="verified-raid",Jeon=100,NormalEggs=1}; }
    static int assertions;
    static void Check(bool ok,string why){assertions++;if(!ok)throw new Exception(why);}
    static void Reject(Action action,string why)
    {bool rejected=false;try{action();}catch(InvalidOperationException){rejected=true;}Check(rejected,why);}
    static EconomyPolicy Policy()
    {
        var p=new EconomyPolicy{CurrentSeason=2,NormalCommonWeight=90,NormalRareWeight=10,RaidGemPartsPerMillion=1000,AdultMinLevel=10};
        for(int i=0;i<5;i++)p.Species.Add(i,new SpeciesRule{Id=i,Name="test-species-"+i,Rarity=PetRarity.Common,HiddenType=i,
            Skills=new[]{"s1","s2","s3","s4","s5","s6"}});
        for(int i=5;i<10;i++)p.Species.Add(i,new SpeciesRule{Id=i,Name="test-species-"+i,Rarity=(PetRarity)(i-5),HiddenType=-1,
            Skills=new[]{"s1","s2","s3","s4","s5","s6"}});
        p.SeasonCostumes.Add(new WeightedCostume{Id="hat-season2",Weight=1,DuplicateThread=5});
        p.Archive.Add("hat-season1",new ArchiveCostume{Id="hat-season1",Season=1,ThreadCost=20});
        p.Archive.Add("hat-current",new ArchiveCostume{Id="hat-current",Season=2,ThreadCost=1});return p;
    }
    static InMemoryWorldStore Store()
    {
        var s=new InMemoryWorldStore();s.Seed(w=>
        {
            w.Accounts.Add("a",new AccountState{AccountId="a",Currency=new UserCurrency(100000,5000,0)});
            w.Accounts.Add("b",new AccountState{AccountId="b",Currency=new UserCurrency(100000,200,0)});
            w.Quotas.Add(0,new LimitedQuota{HiddenType=0,Limit=10,Issued=0});
            // 최신 지시: B~E 종당 100마리.
            for(int i=1;i<=4;i++)w.Quotas.Add(i,new LimitedQuota{HiddenType=i,Limit=100,Issued=0});
        });return s;
    }
    static EconomyAsset PetAsset(string id,int type=-1,int trades=3)
    {
        return new EconomyAsset{Id=id,OwnerId="a",Kind=AssetKind.Pet,Pet=new PetData{petInstanceId=id,petName="fixture",level=10,
            speciesId=type<0?5:type,rarity=PetRarity.Common,isHiddenPet=type>=0,hiddenType=type,
            fullness=50,cleanliness=50,currentSkills=new List<string>{"s1","s2"},slotRerollCounts=new List<int>{0,0},trade=PetTradeData.Create(trades)}};
    }
    static EconomyService Service(InMemoryWorldStore s,RandomFake r=null,EconomyPolicy p=null)=>new EconomyService(s,new Clock(),r??new RandomFake(),p??Policy());
    public static void Main()
    {
        var totals=new int[5];for(int i=0;i<LuminousEggOdds.Total;i++)totals[(int)LuminousEggOdds.Resolve(i)]++;
        Check(totals.SequenceEqual(new[]{0,7000000,2500000,499950,50}),"EXACT 10M weighted partitions");
        var store=Store();var svc=Service(store);
        svc.BuyShop("a","scrolls",ShopProduct.HatchScrollBundle);svc.BuyShop("a","scrolls",ShopProduct.HatchScrollBundle);
        Check(store.Inspect().Scrolls("a")==11&&store.Inspect().Accounts["a"].Currency.Gem==4900,"bundle cost and replay");
        Reject(()=>svc.BuyShop("a","scrolls",ShopProduct.HatchScroll),"payload key reuse");
        svc.BuyShop("a","single-scroll",ShopProduct.HatchScroll);
        Check(store.Inspect().Scrolls("a")==12&&store.Inspect().Accounts["a"].Currency.Gem==4889,"11 gem single");
        svc.BuyShop("a","draw10",ShopProduct.CostumeDrawTen);
        var w=store.Inspect();Check(w.HasCostume("a","hat-season2")&&w.Accounts["a"].Currency.MileageThread==45,"duplicates to Thread within same 10 draw");
        svc.BuyShop("a","draw1",ShopProduct.CostumeDraw);
        Check(store.Inspect().Accounts["a"].Currency.MileageThread==50,"15 gem draw duplicate");
        svc.BuyArchiveCostume("a","archive","hat-season1");svc.BuyArchiveCostume("a","archive","hat-season1");
        Check(store.Inspect().Accounts["a"].Currency.MileageThread==30,"archive exact once");
        Reject(()=>svc.BuyArchiveCostume("a","archive-current","hat-current"),"current season excluded");
        var eggs=svc.BuyShop("a","egg11",ShopProduct.LuminousEggBundle);
        var one=svc.BuyShop("a","egg1",ShopProduct.LuminousEgg);
        Check(eggs.Rewards.Length==11&&one.Rewards.Length==1&&store.Inspect().Accounts["a"].Currency.Gem==4409,"all shop prices");
        string egg=eggs.Rewards[0];svc.StartIncubation("a","inc",egg,0);
        Reject(()=>svc.Hatch("a","early",egg,1),"8h wait");svc.UseHatchScroll("a","hurry",egg,1);
        Check(store.Inspect().AssetsSafe(egg).Egg.hatchAtUtc==2000000&&store.Inspect().Scrolls("a")==11,"scroll sets timer zero");
        svc.UseHatchScroll("a","hurry",egg,1);
        Reject(()=>svc.UseHatchScroll("a","hurry-again",egg,2),"ready egg no extra scroll");
        svc.Hatch("a","hatch",egg,2);svc.Hatch("a","hatch",egg,2);
        var hatched=store.Inspect().EconomyAssets[egg].Pet;
        Check(hatched.rarity==PetRarity.Divine&&hatched.currentSkills.Count==4&&!hatched.isHiddenPet,"luminous jackpot independent from hidden");
        Reject(()=>svc.Hatch("a","hatch-again",egg,3),"egg cannot hatch twice");

        store=Store();var rng=new RandomFake();svc=Service(store,rng);store.Seed(x=>x.AddAsset(PetAsset("p",0)));
        svc.GrowAdult("a","adult","p",0);svc.GrowAdult("a","adult","p",0);w=store.Inspect();
        Check(w.EconomyAssets["p"].Pet.rarity==PetRarity.Divine&&w.Quotas[0].Issued==1&&w.AwakeningOutbox.Count==1,"atomic serial and outbox once");
        Check(rng.Calls.Contains(1000000000),"A denominator 1 BILLION");
        Reject(()=>svc.GrowAdult("a","adult-again","p",1),"new key cannot repeat adult roll");
        for(int i=1;i<=4;i++)
        {
            int type=i;store.Seed(x=>x.AddAsset(PetAsset("p"+type,type)));
            svc.GrowAdult("a","grow"+type,"p"+type,0);
            Check(store.Inspect().EconomyAssets["p"+type].Pet.rarity==PetRarity.Legendary,"B-E legendary path");
        }
        Check(rng.Calls.Contains(2000000),"B-E denominator 2 million");
        store=Store();rng=new RandomFake();rng.Values[1000000000]=1;svc=Service(store,rng);
        store.Seed(x=>x.AddAsset(PetAsset("fail",0)));svc.GrowAdult("a","fail-grow","fail",0);
        Check(store.Inspect().EconomyAssets["fail"].Pet.rarity==PetRarity.Common&&store.Inspect().Quotas[0].Issued==0,"non-jackpot fallback");
        store=Store();svc=Service(store);store.Seed(x=>{x.Quotas[0].Issued=9;x.AddAsset(PetAsset("p1",0));x.AddAsset(PetAsset("p2",0));});
        Parallel.Invoke(()=>svc.GrowAdult("a","g1","p1",0),()=>svc.GrowAdult("a","g2","p2",0));w=store.Inspect();
        Check(w.Quotas[0].Issued==10&&w.AwakeningOutbox.Count==1&&w.EconomyAssets.Values.Count(x=>x.Pet.rarity==PetRarity.Divine)==1,"concurrent last quota");
        Check(w.EconomyAssets.Values.All(x=>x.Pet.isAdult),"sold out normal adult fallback");
        store=Store();svc=Service(store);store.Seed(x=>{x.AddAsset(PetAsset("missing",1));x.Quotas.Remove(1);});
        Reject(()=>svc.GrowAdult("a","missing-q","missing",0),"missing B-E quota explicit refusal");
        Check(!store.Inspect().EconomyAssets["missing"].Pet.isAdult,"missing config rollback");
        store.Seed(x=>x.EconomyAssets["missing"].Pet.level=1);
        Reject(()=>svc.GrowAdult("a","too-young","missing",0),"growth level server enforced");

        store=Store();svc=Service(store);store.Seed(x=>x.AddAsset(PetAsset("r")));
        var r1=svc.Reroll("a","r1","r",0,0);var r2=svc.Reroll("a","r2","r",1,0);
        Check(r1.JeonSpent==1000&&r2.JeonSpent==1250&&store.Inspect().Burned==2250,"growing pet cumulative Jeon sink");
        svc.Reroll("a","r2","r",1,0);Check(store.Inspect().Burned==2250,"reroll replay");
        var r3=svc.Reroll("a","r3","r",2,1);
        Check(r3.JeonSpent==1500,"switching slot does not reset cost");
        Reject(()=>svc.Reroll("a","neg","r",3,-1),"negative slot");
        Reject(()=>svc.Reroll("a","over","r",3,2),"slot upper bound");
        Reject(()=>svc.Reroll("b","owner","r",3,0),"owner checked");
        svc.Care("a","feed","r",3,false);Check(store.Inspect().EconomyAssets["r"].Pet.fullness==80,"feed updates meter");

        store=Store();svc=Service(store);store.Seed(x=>x.AddAsset(PetAsset("trade",-1,1)));
        string offer=svc.List("a","list","trade",0,101).ResultId;
        svc.BuyListing("b","buy",offer);svc.BuyListing("b","buy",offer);w=store.Inspect();
        Check(w.Accounts["b"].Coins==99899&&w.Accounts["a"].Coins==100095&&w.Burned==6,"5 percent rounded up; Jeon only; replay");
        Check(w.Accounts["b"].Currency.Gem==200&&w.EconomyAssets["trade"].Pet.trade.IsSoulbound,"gem untouched and soulbound");
        Reject(()=>svc.List("b","relist","trade",2,100),"soulbound resale");
        store=Store();svc=Service(store);store.Seed(x=>{x.AddAsset(PetAsset("rollback",-1,1));x.Accounts["b"].Coins=0;});
        offer=svc.List("a","list","rollback",0,100).ResultId;
        Reject(()=>svc.BuyListing("b","buy",offer),"insufficient balance rolls back trade consumption");
        Check(store.Inspect().EconomyAssets["rollback"].Pet.trade.CurrentTradeCount==1&&store.Inspect().Accounts["b"].TradesToday==0,"full rollback");
        svc.Cancel("a","cancel",offer);Check(!store.Inspect().EconomyAssets["rollback"].IsListed,"cancel listing");
        store.Seed(x=>{x.AddAsset(new EconomyAsset{Id="material",OwnerId="a",Kind=AssetKind.Material,MaterialId="ore",Quantity=7});
            x.AddAsset(new EconomyAsset{Id="normal",OwnerId="a",Kind=AssetKind.Egg,Egg=new EggData{kind=EggKind.Normal}});x.Accounts["b"].Coins=1000;});
        offer=svc.List("a","mat-list","material",0,100).ResultId;svc.BuyListing("b","mat-buy",offer);
        Check(store.Inspect().EconomyAssets["material"].Quantity==7&&store.Inspect().EconomyAssets["material"].OwnerId=="b","material lot transfer");
        offer=svc.List("a","egg-list","normal",0,100).ResultId;svc.BuyListing("b","egg-buy",offer);
        svc.StartIncubation("b","inc-normal","normal",2);
        Reject(()=>svc.List("b","inc-list","normal",3,100),"incubating egg nontradable");

        store=Store();svc=Service(store);var verifier=new PurchaseFake();svc.CreditVerifiedPurchase("a","pay","token",verifier);
        svc.CreditVerifiedPurchase("a","pay","token",verifier);Check(store.Inspect().Accounts["a"].Currency.Gem==5100,"verified payment once");
        Reject(()=>svc.CreditVerifiedPurchase("a","pay2","token",verifier),"same purchase new key refused");
        Reject(()=>svc.CreditVerifiedPurchase("b","pay3","token",verifier),"global purchase replay refused");
        svc.SettleVerifiedRaid("a","raid","token",new RaidFake());svc.SettleVerifiedRaid("a","raid","token",new RaidFake());
        Check(store.Inspect().Accounts["a"].Currency.Gem==5101&&store.Inspect().Accounts["a"].Coins==100100,"verified raid once + rare gem");
        Reject(()=>svc.SettleVerifiedRaid("a","raid2","token",new RaidFake()),"raid new key not rerolled");
        store=Store();var empty=Policy();empty.SeasonCostumes.Clear();svc=Service(store,null,empty);
        Reject(()=>svc.BuyShop("a","no-table",ShopProduct.CostumeDraw),"unconfigured costume table rejected");
        Check(store.Inspect().Accounts["a"].Currency.Gem==5000,"failed draw rolls back charge");
        store=Store();svc=Service(store);store.Seed(x=>x.AddAsset(new EconomyAsset{Id="normal-ready",OwnerId="a",Kind=AssetKind.Egg,
            Egg=new EggData{kind=EggKind.Normal,incubating=true,hatchAtUtc=1}}));
        svc.Hatch("a","normal-hatch","normal-ready",0);
        Check(store.Inspect().EconomyAssets["normal-ready"].Pet.isHiddenPet&&store.Inspect().EconomyAssets["normal-ready"].Pet.rarity==PetRarity.Common,"normal egg hidden candidate stays visually common");
        Check(typeof(PublicPetView).GetField("isHiddenPet")==null&&typeof(PublicPetView).GetField("hiddenType")==null,"public DTO excludes hidden eligibility");
        store=Store();var unconfigured=Policy();unconfigured.NormalCommonWeight=unconfigured.NormalRareWeight=0;svc=Service(store,null,unconfigured);
        store.Seed(x=>x.AddAsset(new EconomyAsset{Id="not-configured",OwnerId="a",Kind=AssetKind.Egg,Egg=new EggData{incubating=true,hatchAtUtc=1}}));
        Reject(()=>svc.Hatch("a","not-configured","not-configured",0),"normal odds not invented");
        Check(store.Inspect().EconomyAssets["not-configured"].Kind==AssetKind.Egg,"failed hatch keeps egg");
        store=Store();var rarePolicy=Policy();rarePolicy.Species[1].Rarity=PetRarity.Rare;var failRng=new RandomFake();failRng.Values[2000000]=1;
        svc=Service(store,failRng,rarePolicy);store.Seed(x=>{var a=PetAsset("rare-fallback",1);a.Pet.rarity=PetRarity.Rare;x.AddAsset(a);});
        svc.GrowAdult("a","rare-fallback","rare-fallback",0);
        Check(store.Inspect().EconomyAssets["rare-fallback"].Pet.rarity==PetRarity.Rare,"failed hidden roll does not downgrade Rare");
        Console.WriteLine("PASS: "+assertions+" checks; all 10,000,000 egg outcomes exhaustively counted; real server service executed under Mono.");
        Console.WriteLine("PASS: shop/replay, scroll/hatch, costume/thread/archive, hidden A-E + concurrent quota, growth gate, reroll, care, market/soulbind/rollback, verified purchase and raid dedup.");
        Console.WriteLine("NOT TESTED: real DB/multiple processes/store receipts/network/auth/Unity UI/native rendering/mobile release.");
    }
    static EconomyAsset AssetsSafe(this InMemoryWorldStore.World w,string id)=>w.EconomyAssets[id];
}
