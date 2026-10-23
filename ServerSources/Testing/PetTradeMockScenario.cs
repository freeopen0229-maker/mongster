using System;
using Mongle.Slice;

namespace Mongle.Slice.Server.Testing
{
    /// <summary>에디터 콘솔과 관리 코드 테스트가 함께 사용하는 실제 거래 서비스 시나리오.</summary>
    public static class PetTradeMockScenario
    {
        sealed class Clock:IServerClock {public long UtcSeconds=>2000000;}
        static void Require(bool condition,string message)
        {if(!condition)throw new InvalidOperationException("MOCK ASSERTION FAILED: "+message);}

        public static void Run(Action<string> log)
        {
            if(log==null)throw new ArgumentNullException(nameof(log));
            RunOne(2,log);RunOne(1,log);
            log("[PASS MOCK] Both remaining-count and final-soulbind cases passed. No real accounts were touched.");
        }
        static void RunOne(int remaining,Action<string> log)
        {
            var clock=new Clock();var store=new InMemoryWorldStore();
            store.Seed(w=>
            {
                w.Accounts.Add("player-A",new AccountState{AccountId="player-A",Coins=0});
                w.Accounts.Add("player-B",new AccountState{AccountId="player-B",Coins=1000});
                w.Pets.Add("mock-neulgui",new PetState{PetId="mock-neulgui",OwnerAccountId="player-A",SpeciesId=0,
                    Stage=GrowthStage.Baby,Grade=PetGrade.Common,LastUtc=clock.UtcSeconds,Revision=1,
                    Trade=PetTradeData.Create(remaining)});
            });
            var service=new PetTradeService(store,clock,new TradePolicy());
            var offer=service.CreateOffer("player-A","mock-neulgui",100,1);
            log($"[MOCK LIST] A listed {offer.PetId}; remaining={remaining}; price=100.");
            var request=new BuyPetRequest{OfferId=offer.OfferId,RequestId="mock-buy-1"};
            var receipt=service.Buy("player-B",request);
            var after=store.Inspect();var pet=after.Pets["mock-neulgui"];
            Require(pet.OwnerAccountId=="player-B","buyer ownership");
            Require(pet.Trade.CurrentTradeCount==remaining-1,"one remaining trade consumed");
            Require(pet.Trade.IsSoulbound==(remaining==1),"soulbound threshold");
            Require(remaining!=1||pet.Trade.BoundAccountId=="player-B","bound to final buyer");
            Require(after.Accounts["player-B"].Coins==900&&after.Accounts["player-A"].Coins==95&&after.Burned==5,"wallet/fee settlement");
            log($"[PASS MOCK BUY] owner=B; CurrentTradeCount={remaining}->{pet.Trade.CurrentTradeCount}; IsSoulbound={pet.Trade.IsSoulbound}; feeBurned={receipt.FeeBurned}.");
            service.Buy("player-B",request);
            var retried=store.Inspect();
            Require(retried.Pets["mock-neulgui"].Trade.CurrentTradeCount==remaining-1&&retried.Burned==5&&retried.Accounts["player-B"].Coins==900,"idempotent retry");
            if(remaining==1)
            {
                bool rejected=false;
                try{service.CreateOffer("player-B","mock-neulgui",100,pet.Revision);}
                catch(InvalidOperationException){rejected=true;}
                Require(rejected,"soulbound relist rejected");log("[PASS MOCK] Soulbound relisting rejected; same request replay did not charge again.");
            }
        }
    }
}
