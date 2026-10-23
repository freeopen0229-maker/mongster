using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FusionTamagotchi;
using FusionTamagotchi.Client;
using Mongle.Slice;
using Mongle.Slice.Server;
using Mongle.Slice.Server.Testing;

class ClientEconomyProbe
{
    sealed class Clock:IServerClock{public long UtcSeconds=>2000000;}
    sealed class Rng:IServerRandom{public int Next(int max)=>0;}
    sealed class Transport:IClientEconomyGateway
    {
        readonly IClientEconomyGateway inner;
        public bool TimeoutAfterCommit,RejectOnce,Hold,WrongOwner,Stale;
        public readonly List<string> RequestIds=new List<string>();
        public TaskCompletionSource<bool> Gate=new TaskCompletionSource<bool>();
        public Transport(IClientEconomyGateway inner){this.inner=inner;}
        public Task<ClientSnapshotDTO> ReadAsync(string pet,CancellationToken t)=>inner.ReadAsync(pet,t);
        public async Task<ClientCommandReplyDTO> ExecuteAsync(ClientCommandDTO c,CancellationToken t)
        {
            RequestIds.Add(c.requestId);if(Hold)await Gate.Task;
            if(RejectOnce){RejectOnce=false;throw new EconomyRequestRejectedException("rollback confirmed");}
            var r=await inner.ExecuteAsync(c,t);
            if(TimeoutAfterCommit){TimeoutAfterCommit=false;throw new TimeoutException("committed, response lost");}
            if(WrongOwner){WrongOwner=false;r.snapshot.accountId="other";}
            if(Stale){Stale=false;r.snapshot.wallet.revision=0;}
            return r;
        }
    }
    static int checks;
    static void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
    static void Fail(Action a,string why){bool failed=false;try{a();}catch(Exception){failed=true;}Check(failed,why);}
    static void Run(Task t){t.GetAwaiter().GetResult();}
    static void Main()
    {
        var store=new InMemoryWorldStore();var policy=new EconomyPolicy{AdultMinLevel=10};
        policy.Species.Add(0,new SpeciesRule{Id=0,Name="Neulgui",Skills=new[]{"s1","s2","s3","s4","s5"}});
        store.Seed(w=>
        {
            w.Accounts.Add("a",new AccountState{AccountId="a",Currency=new UserCurrency(100000,1000,0)});
            foreach(var q in HiddenQuotaPolicy.CreateFresh())w.Quotas.Add(q.HiddenType,q);
            w.AddAsset(new EconomyAsset{Id="pet",OwnerId="a",Kind=AssetKind.Pet,Pet=new PetData{petInstanceId="pet",petName="Neulgui",speciesId=0,
                isHiddenPet=true,hiddenType=0,currentSkills=new List<string>{"s1","s2"},slotRerollCounts=new List<int>{0,0}}});
        });
        var server=new EconomyService(store,new Clock(),new Rng(),policy);
        var facade=new ClientEconomyFacade("a",store,server,policy);var network=new Transport(facade);
        var client=new ClientEconomySession("a",network);
        Run(client.RefreshAsync("pet",CancellationToken.None));
        Check(client.Wallet.gem==1000&&client.Pet.hiddenType==-1&&!client.Pet.isHiddenPet,"loaded wallet + hidden redaction");
        Check(store.Inspect().Quotas[1].Limit==100&&store.Inspect().Quotas[0].Limit==10,"latest quota limits");
        network.TimeoutAfterCommit=true;
        Fail(()=>Run(client.BuyEggAsync(false,CancellationToken.None)),"transport error surfaced");
        Check(client.Wallet.gem==1000&&store.Inspect().Accounts["a"].Currency.Gem==970&&client.HasPending&&!client.IsBusy,"no optimistic deduction after lost response");
        string pending=client.Pending.requestId;
        Fail(()=>Run(client.BuyEggAsync(false,CancellationToken.None)),"pending blocks fresh purchase");
        Run(client.RetryAsync(CancellationToken.None));
        Check(client.Wallet.gem==970&&store.Inspect().EconomyAssets.Count==2&&!client.HasPending,"retry does not grant/spend twice");
        Check(network.RequestIds[0]==pending&&network.RequestIds[1]==pending,"stable retry request ID");
        network.Hold=true;var buy=client.BuyEggAsync(true,CancellationToken.None);
        Check(client.IsBusy&&client.Wallet.gem==970,"pending request leaves displayed balance unchanged");
        Fail(()=>Run(client.BuyEggAsync(false,CancellationToken.None)),"double click blocked");
        network.Gate.SetResult(true);Run(buy);network.Hold=false;
        Check(client.Wallet.gem==670&&store.Inspect().EconomyAssets.Count==13,"300 gems grants eleven unhatched eggs");
        Check(store.Inspect().EconomyAssets["pet"].Kind==AssetKind.Pet&&store.Inspect().EconomyAssets.Count==13,"purchase does not hatch pets");
        Run(client.RefreshAsync("pet",CancellationToken.None));
        Run(client.RerollAsync("pet",0,CancellationToken.None));
        Check(client.Wallet.jeon==99000&&client.Pet.skills[0]=="s3"&&client.Pet.nextRerollCost==1250,"authoritative reroll result + next price");
        network.RejectOnce=true;long before=client.Wallet.jeon;
        Fail(()=>Run(client.RerollAsync("pet",0,CancellationToken.None)),"definitive rejection surfaced");
        Check(client.Wallet.jeon==before&&!client.HasPending,"rejected request no deduction and clears pending");
        network.TimeoutAfterCommit=true;Fail(()=>Run(client.BuyEggAsync(false,CancellationToken.None)),"lost reply for restart");
        var saved=client.Pending;var restarted=new ClientEconomySession("a",network);restarted.RestorePending(saved);
        Run(restarted.RefreshAsync("pet",CancellationToken.None));Run(restarted.RetryAsync(CancellationToken.None));
        Check(restarted.Wallet.gem==640&&!restarted.HasPending,"saved request survives session restart");
        network.WrongOwner=true;Fail(()=>Run(restarted.BuyEggAsync(false,CancellationToken.None)),"wrong account response rejected");
        Check(restarted.Wallet.gem==640&&restarted.HasPending,"wrong account response never overwrites UI wallet");
        Run(restarted.RetryAsync(CancellationToken.None));Check(restarted.Wallet.gem==610,"wrong response retried with same grant");
        network.Stale=true;Fail(()=>Run(restarted.BuyEggAsync(false,CancellationToken.None)),"stale snapshot refused");
        Check(restarted.Wallet.gem==610&&restarted.HasPending,"stale response retains pending");
        Run(restarted.RetryAsync(CancellationToken.None));Check(restarted.Wallet.gem==580,"stale response recovered");
        var copy=restarted.Wallet;copy.gem=999999;Check(restarted.Wallet.gem==580,"UI snapshot copy does not change session");
        Console.WriteLine("PASS: "+checks+" client/server integration checks: real domain facade, no optimistic spend, redaction, pending retry/restart, duplicate click, eleven eggs, reroll, rejection, wrong account/stale response.");
        Console.WriteLine("NOT TESTED: Unity UI compilation/rendering, HTTP host/network deployment, store receipts, production DB.");
    }
}
