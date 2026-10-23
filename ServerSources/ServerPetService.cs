using System;
using Mongle.Slice;

namespace Mongle.Slice.Server
{
    /// <summary>돌봄/보상을 확정하는 서버 서비스. HTTP 인증 어댑터는 이 서비스 앞에 별도로 둔다.</summary>
    public sealed class ServerPetService
    {
        readonly IWorldStore store;readonly IServerClock clock;
        public ServerPetService(IWorldStore store,IServerClock clock)
        {this.store=store??throw new ArgumentNullException(nameof(store));this.clock=clock??throw new ArgumentNullException(nameof(clock));}

        public PetSnapshot Get(string authenticatedOwner,string petId)
        {
            return store.ExecuteSerializable(tx=>
            {
                var p=Owned(tx,authenticatedOwner,petId);
                return Snapshot(p,tx.Account(authenticatedOwner),clock.UtcSeconds);
            });
        }
        public PetSnapshot Care(string authenticatedOwner,PetActionRequest request)=>Action(authenticatedOwner,request,PetCommandKind.Care);
        public PetSnapshot ClaimIdle(string authenticatedOwner,PetActionRequest request)=>Action(authenticatedOwner,request,PetCommandKind.ClaimIdle);

        public PetSnapshot StartExploration(string owner,PetActionRequest request)=>Action(owner,request,PetCommandKind.StartExploration);
        public PetSnapshot FinishExploration(string owner,PetActionRequest request)=>Action(owner,request,PetCommandKind.FinishExploration);
        // Staging balance: one server-timed 60s outing, 5 coins and 8 XP. No client reward/result input.
        public const int ExplorationSeconds=60;
        PetSnapshot Action(string owner,PetActionRequest request,PetCommandKind kind)
        {
            if(!PetIds.Valid(owner)||request==null||!PetIds.Valid(request.PetId)||!PetIds.Valid(request.RequestId)||request.ExpectedRevision<0)
                throw new InvalidOperationException("Invalid pet command.");
            if(kind==PetCommandKind.Care&&!Enum.IsDefined(typeof(CareAction),request.Action))throw new InvalidOperationException("Invalid care action.");
            string fingerprint=request.PetId+"/"+request.ExpectedRevision+"/"+(kind==PetCommandKind.Care?"care:"+(int)request.Action:kind.ToString());
            return store.ExecuteSerializable(tx=>
            {
                var previous=tx.FindActionReceipt(owner,request.RequestId);
                if(previous!=null)
                {
                    if(previous.Fingerprint!=fingerprint)throw new InvalidOperationException("Idempotency key reused for another command.");
                    return previous.Result.Clone();
                }
                var p=Owned(tx,owner,request.PetId);var account=tx.Account(owner);long now=clock.UtcSeconds;
                if(p.Revision!=request.ExpectedRevision||now<p.LastUtc||p.IsListed||(p.IsBusy&&kind!=PetCommandKind.FinishExploration)||p.IsLocked)
                    throw new InvalidOperationException("Stale/locked pet command.");
                if(kind==PetCommandKind.ClaimIdle)
                {
                    var reward=PetRules.ClaimIdle(p,now);
                    long coins=checked(account.Coins+reward.Coins);
                    if(coins>TradePolicy.MaxWallet)throw new InvalidOperationException("Wallet capacity exceeded.");
                    account.Coins=coins;
                }
                else if(kind==PetCommandKind.StartExploration)
                {
                    if(p.ExplorationId!=""||p.Fatigue>90)throw new InvalidOperationException("Pet cannot explore.");
                    PetRules.Advance(p,now);p.ExplorationId=request.RequestId;p.ExplorationEndUtc=checked(now+ExplorationSeconds);p.IsBusy=true;
                }
                else if(kind==PetCommandKind.FinishExploration)
                {
                    if(p.ExplorationId==""||now<p.ExplorationEndUtc)throw new InvalidOperationException("Exploration is not complete.");
                    PetRules.Advance(p,now);account.Coins=checked(account.Coins+5);
                    if(account.Coins>TradePolicy.MaxWallet)throw new InvalidOperationException("Wallet capacity exceeded.");
                    PetRules.AwardExploration(p);p.ExplorationId="";p.ExplorationEndUtc=0;p.IsBusy=false;
                }
                else
                {
                    if(request.Action==CareAction.Feed&&account.Food<1)throw new InvalidOperationException("No food.");
                    if(!PetRules.Care(p,request.Action,now))throw new InvalidOperationException("Care cooldown/condition.");
                    if(request.Action==CareAction.Feed)account.Food--;
                }
                p.Revision=checked(p.Revision+1);PetLifecycle.Commit(tx,p);
                var result=Snapshot(p,account,now);
                tx.AddActionReceipt(new ActionReceipt{AccountId=owner,RequestId=request.RequestId,Fingerprint=fingerprint,Result=result.Clone()});
                return result;
            });
        }
        PetState Owned(IWorldTransaction tx,string owner,string id)
        {
            if(!PetIds.Valid(owner)||!PetIds.Valid(id))throw new InvalidOperationException("Invalid owner/pet ID.");
            var p=PetLifecycle.Resolve(tx,owner,id,clock.UtcSeconds);p.Validate();
            if(p.OwnerAccountId!=owner)throw new InvalidOperationException("Pet not owned by authenticated account.");
            return p;
        }
        static PetSnapshot Snapshot(PetState p,AccountState account,long now)
        {
            p.Validate();if(now<0||account.Coins<0||account.Food<0)throw new InvalidOperationException("Invalid server state.");
            return new PetSnapshot{Pet=p.Clone(),AccountCoins=account.Coins,AccountFood=account.Food,ServerUtcSeconds=now};
        }
    }
}
