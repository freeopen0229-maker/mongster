using System;
using System.Threading;
using System.Threading.Tasks;
using Mongle.Slice;
using Mongle.Slice.Server;
using Mongle.Slice.Server.Testing;

public static class SliceProbe
{
    sealed class Clock:IServerClock { public long Now=2000000;public long UtcSeconds=>Now; }
    static void Require(bool ok,string reason){if(!ok)throw new Exception(reason);}
    static void Reject(Action action,string reason)
    {bool rejected=false;try{action();}catch(InvalidOperationException){rejected=true;}Require(rejected,reason);}
    static PetState NewPet(long now,string owner="seller")=>new PetState{PetId="pet-1",OwnerAccountId=owner,Revision=7,
        LastUtc=now,SpeciesId=0,Grade=PetGrade.Common,Trade=PetTradeData.Create(1),PotentialPermille=1010,
        Individual=new PetIndividualValues{Health=20,Attack=10,Defense=15,Speed=25}};
    static InMemoryWorldStore Store(Clock clock,bool listed=true)
    {
        var store=new InMemoryWorldStore();store.Seed(w=>
        {
            var p=NewPet(clock.Now);p.IsListed=listed;w.Pets.Add(p.PetId,p);
            w.Accounts.Add("seller",new AccountState{AccountId="seller",Coins=100});
            w.Accounts.Add("buyer",new AccountState{AccountId="buyer",Coins=1000});
            w.Accounts.Add("buyer2",new AccountState{AccountId="buyer2",Coins=1000});
            w.Offers.Add("offer-1",new TradeOffer{OfferId="offer-1",PetId=p.PetId,SellerId="seller",Price=100,
                PetRevision=p.Revision,ExpiresUtc=clock.Now+86400,Active=listed});
        });return store;
    }
    static PetActionRequest CareRequest(string key,long revision)=>new PetActionRequest{PetId="pet-1",RequestId=key,ExpectedRevision=revision,Action=CareAction.Feed};
    public static void Main()
    {
        var clock=new Clock();var store=Store(clock);var trade=new PetTradeService(store,clock,new TradePolicy());
        var request=new BuyPetRequest{OfferId="offer-1",RequestId="buy-1"};
        var receipt=trade.Buy("buyer",request);trade.Buy("buyer",request);
        var world=store.Inspect();var pet=world.Pets["pet-1"];
        Require(world.Accounts["buyer"].Coins==900&&world.Accounts["seller"].Coins==195&&world.Burned==5,"one-time transfer/fee burn");
        Require(pet.OwnerAccountId=="buyer"&&pet.Trade.CurrentTradeCount==0&&pet.Trade.IsSoulbound&&pet.Trade.BoundAccountId=="buyer","final trade soulbind");
        Reject(()=>trade.Buy("buyer",new BuyPetRequest{OfferId="other",RequestId="buy-1"}),"key payload mismatch");
        Reject(()=>trade.CreateOffer("buyer","pet-1",100,pet.Revision),"soulbound relisting");

        store=Store(clock);store.Seed(w=>w.Accounts["buyer"].Coins=1);trade=new PetTradeService(store,clock,new TradePolicy());
        Reject(()=>trade.Buy("buyer",request),"insufficient balance");world=store.Inspect();
        Require(world.Pets["pet-1"].OwnerAccountId=="seller"&&world.Pets["pet-1"].Trade.CurrentTradeCount==1&&world.Burned==0&&world.Accounts["buyer"].TradeUtcDay==-1,"rollback all prior mutations");

        store=Store(clock);store.Seed(w=>w.Accounts["seller"].Coins=TradePolicy.MaxWallet);
        var capacityClock=new Clock{Now=clock.Now+3600};trade=new PetTradeService(store,capacityClock,new TradePolicy());
        Reject(()=>trade.Buy("buyer",request),"wallet capacity after idle settlement");world=store.Inspect();
        Require(world.Pets["pet-1"].LastUtc==clock.Now&&world.Pets["pet-1"].PendingIdleCoins==0&&world.Accounts["buyer"].Coins==1000,"rollback after idle computation");
        store=Store(clock);store.Seed(w=>{w.Accounts["buyer"].TradeUtcDay=clock.Now/86400;w.Accounts["buyer"].TradesToday=10;});
        trade=new PetTradeService(store,clock,new TradePolicy());Reject(()=>trade.Buy("buyer",request),"daily account limit");
        store=Store(clock);store.Seed(w=>{w.Pets["pet-1"].Trade.MaxTradeCount=2;w.Pets["pet-1"].Trade.LastTransferredUtc=clock.Now-60;});
        trade=new PetTradeService(store,clock,new TradePolicy());Reject(()=>trade.Buy("buyer",request),"ownership cooldown");

        store=Store(clock);trade=new PetTradeService(store,clock,new TradePolicy());int success=0;
        Parallel.Invoke(
            ()=>{try{trade.Buy("buyer",request);Interlocked.Increment(ref success);}catch(InvalidOperationException){}},
            ()=>{try{trade.Buy("buyer2",new BuyPetRequest{OfferId="offer-1",RequestId="buy-2"});Interlocked.Increment(ref success);}catch(InvalidOperationException){}});
        Require(success==1&&store.Inspect().Burned==5,"two buyers one listing");

        store=Store(clock);clock.Now+=3600;trade=new PetTradeService(store,clock,new TradePolicy());trade.Buy("buyer",request);world=store.Inspect();
        Require(world.Accounts["seller"].Coins==207&&world.Pets["pet-1"].PendingIdleCoins<1,"idle coins settle to seller");

        store=Store(clock,false);var petService=new ServerPetService(store,clock);
        var care=CareRequest("care-1",7);petService.Care("seller",care);petService.Care("seller",care);
        Require(store.Inspect().Accounts["seller"].Food==9,"care receipt prevents duplicate food spend");
        Reject(()=>petService.Care("buyer",CareRequest("forged-owner",8)),"authenticated ownership");
        Reject(()=>petService.Care("seller",CareRequest("stale",7)),"revision conflict");
        Reject(()=>petService.Care("seller",CareRequest("cooldown",8)),"care cooldown rollback");
        clock.Now+=3600;var claim=new PetActionRequest{PetId="pet-1",RequestId="claim-1",ExpectedRevision=8};
        petService.ClaimIdle("seller",claim);petService.ClaimIdle("seller",claim);
        Require(store.Inspect().Accounts["seller"].Coins==112,"idle receipt once");
        claim=new PetActionRequest{PetId="pet-1",RequestId="claim-2",ExpectedRevision=9};petService.ClaimIdle("seller",claim);
        Require(store.Inspect().Accounts["seller"].Coins==112,"idle drain once with another key");

        var p1=NewPet(clock.Now);var p2=p1.Clone();PetRules.Advance(p1,clock.Now+86400);
        for(int i=1;i<=24;i++)PetRules.Advance(p2,clock.Now+i*3600);
        Require(Math.Abs(p1.PendingIdleCoins-p2.PendingIdleCoins)<1e-7&&p1.UnclaimedIdleSeconds==28800&&p1.PendingIdleCoins==96,"partition/cap");
        long prior=p1.LastUtc;PetRules.Advance(p1,prior-100);Require(p1.LastUtc==prior,"clock rollback");
        p2.Individual.Attack=32;Reject(()=>p2.Validate(),"invalid IV");
        var data=PetTradeData.Create(2);data.ConsumeTransfer("a","b",100);data.ConsumeTransfer("b","c",200);
        Require(data.IsSoulbound&&data.CurrentTradeCount==0&&data.BoundAccountId=="c","two transfers exhaust limit");
        data.IsSoulbound=false;Reject(()=>data.Validate("c"),"forged soulbound flag");
        var before=PetRules.Stats(p1);long end=MonetizationPolicy.ShortenIncubation(100,1000,10000);var after=PetRules.Stats(p1);
        Require(end==100&&before.Attack==after.Attack&&!MonetizationPolicy.IsAllowed((PaidProductKind)99),"paid policy time only");

        store=Store(clock,false);trade=new PetTradeService(store,clock,new TradePolicy());var offer=trade.CreateOffer("seller","pet-1",100,7);
        Reject(()=>trade.CancelOffer("buyer",offer.OfferId),"cancel authorization");trade.CancelOffer("seller",offer.OfferId);
        Require(!store.Inspect().Pets["pet-1"].IsListed,"owner cancellation");
        Console.WriteLine("PASS: UTC/cap/partition; IV validation; care ownership/revision/cooldown/idempotency; soulbind exhaustion; atomic rollback; two concurrent buyers; seller idle settlement; fee burn; listing cancellation; paid policy + time-only scroll effect. In-memory transaction tests, not a deployed backend.");
    }
}
