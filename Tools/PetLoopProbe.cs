using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FusionTamagotchi;
using Mongle.Slice;
using Mongle.Slice.Server;
using Mongle.Slice.Server.Testing;
public static class PetLoopProbe
{
    sealed class Clock:IServerClock { public long Now=2000000;public long UtcSeconds=>Now; }
    sealed class Transport:IPetGateway
    {
        public ServerPetService Server;public bool Drop;public string LastId;
        Task<PetSnapshot> Call(Func<PetSnapshot> work,PetActionRequest r=null)
        {
            try
            {
                LastId=r?.RequestId;PetSnapshot value;
                try{value=work();}catch(InvalidOperationException e){throw new PetCommandRejectedException(e.Message);}
                if(r!=null&&Drop){Drop=false;throw new IOException("Response lost after commit");}return Task.FromResult(value);
            }
            catch(Exception e){return Task.FromException<PetSnapshot>(e);}
        }
        public Task<PetSnapshot> GetAsync(string id,CancellationToken t)=>Call(()=>Server.Get("local-a",id));
        public Task<PetSnapshot> CareAsync(PetActionRequest r,CancellationToken t)=>Call(()=>Server.Care("local-a",r),r);
        public Task<PetSnapshot> ClaimIdleAsync(PetActionRequest r,CancellationToken t)=>Call(()=>Server.ClaimIdle("local-a",r),r);
        public Task<PetSnapshot> StartExplorationAsync(PetActionRequest r,CancellationToken t)=>Call(()=>Server.StartExploration("local-a",r),r);
        public Task<PetSnapshot> FinishExplorationAsync(PetActionRequest r,CancellationToken t)=>Call(()=>Server.FinishExploration("local-a",r),r);
    }
    static int checks;
    static void Check(bool ok,string name){if(!ok)throw new Exception(name);checks++;}
    static async Task Reject(Func<Task> work){bool failed=false;try{await work();}catch(Exception){failed=true;}Check(failed,"expected rejection");}
    static async Task Run()
    {
        var clock=new Clock();var store=new InMemoryWorldStore();store.Seed(w=>
        {
            w.Accounts.Add("local-a",new AccountState{AccountId="local-a",Currency=new UserCurrency(100,0),Food=10});
            w.AddAsset(new EconomyAsset{Id="pet-one",OwnerId="local-a",Kind=AssetKind.Pet,Pet=new PetData{petInstanceId="pet-one",petName="Neulgui",fullness=70,currentSkills=new System.Collections.Generic.List<string>{"s1","s2"}}});
        });
        var service=new ServerPetService(store,clock);var gateway=new Transport{Server=service};var session=new ClientPetSession("local-a","pet-one",gateway);
        var token=CancellationToken.None;await session.RefreshAsync(token);Check(session.Snapshot.Pet.PetId=="pet-one","economy->care same identity");
        gateway.Drop=true;await Reject(()=>session.ExecuteAsync(PetCommandKind.Care,CareAction.Feed,token));
        string key=session.Pending.Request.RequestId;Check(store.Inspect().Accounts["local-a"].Food==9,"server committed one feed");
        Check(store.Inspect().EconomyAssets["pet-one"].Pet.fullness==95,"economy projection synchronized");
        await Reject(()=>session.ExecuteAsync(PetCommandKind.Care,CareAction.Pet,token));
        var restored=new ClientPetSession("local-a","pet-one",gateway);restored.Restore(session.Pending);await restored.RefreshAsync(token);await restored.RetryAsync(token);
        Check(gateway.LastId==key&&restored.Pending==null&&store.Inspect().Accounts["local-a"].Food==9,"restart and retry retained ID; no duplicate food debit");
        var other=new ClientPetSession("local-b","pet-one",gateway);await Reject(()=>{other.Restore(session.Pending);return Task.CompletedTask;});
        await restored.ExecuteAsync(PetCommandKind.StartExploration,CareAction.Pet,token);
        Check(restored.Snapshot.Pet.IsBusy&&store.Inspect().EconomyAssets["pet-one"].Locked,"exploration locks canonical asset");
        await Reject(()=>restored.ExecuteAsync(PetCommandKind.FinishExploration,CareAction.Pet,token));
        Check(restored.Pending==null&&store.Inspect().Accounts["local-a"].Coins==100,"early finish rejected atomically");
        clock.Now+=60;gateway.Drop=true;await Reject(()=>restored.ExecuteAsync(PetCommandKind.FinishExploration,CareAction.Pet,token));
        var finish=restored.Pending;long coins=store.Inspect().Accounts["local-a"].Coins;
        var disk=store.Inspect().Export();var reopened=new InMemoryWorldStore();reopened.Restore(disk);gateway.Server=new ServerPetService(reopened,clock);
        var restart=new ClientPetSession("local-a","pet-one",gateway);restart.Restore(finish);await restart.RefreshAsync(token);await restart.RetryAsync(token);
        Check(reopened.Inspect().Accounts["local-a"].Coins==coins&&coins==105,"receipt survives server restart; reward once");
        Check(!restart.Snapshot.Pet.IsBusy&&!reopened.Inspect().EconomyAssets["pet-one"].Locked,"exploration unlocks pet");
        Check(restart.Snapshot.Pet.Experience==8,"server XP applied once");
        await Reject(()=>{gateway.Server.Care("local-b",new PetActionRequest{PetId="pet-one",RequestId="unauthorized",ExpectedRevision=0});return Task.CompletedTask;});
        Console.WriteLine("PASS: "+checks+" canonical-pet, uncertain-response/restart, account-scope and timed-exploration assertions.");
    }
    public static void Main(){Run().GetAwaiter().GetResult();}
}
