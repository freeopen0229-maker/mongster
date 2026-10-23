// GENERATED from ServerSources/ServerPetService.cs. Do not edit.
#if UNITY_EDITOR
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
        public PetSnapshot Care(string authenticatedOwner,PetActionRequest request)=>Action(authenticatedOwner,request,false);
        public PetSnapshot ClaimIdle(string authenticatedOwner,PetActionRequest request)=>Action(authenticatedOwner,request,true);

        PetSnapshot Action(string owner,PetActionRequest request,bool claim)
        {
            if(!PetIds.Valid(owner)||request==null||!PetIds.Valid(request.PetId)||!PetIds.Valid(request.RequestId)||request.ExpectedRevision<0)
                throw new InvalidOperationException("Invalid pet command.");
            string fingerprint=request.PetId+"/"+request.ExpectedRevision+"/"+(claim?"claim":"care:"+(int)request.Action);
            return store.ExecuteSerializable(tx=>
            {
                var previous=tx.FindActionReceipt(owner,request.RequestId);
                if(previous!=null)
                {
                    if(previous.Fingerprint!=fingerprint)throw new InvalidOperationException("Idempotency key reused for another command.");
                    return previous.Result.Clone();
                }
                var p=Owned(tx,owner,request.PetId);var account=tx.Account(owner);long now=clock.UtcSeconds;
                if(p.Revision!=request.ExpectedRevision||now<p.LastUtc||p.IsListed||p.IsBusy||p.IsLocked)
                    throw new InvalidOperationException("Stale/locked pet command.");
                if(claim)
                {
                    var reward=PetRules.ClaimIdle(p,now);
                    long coins=checked(account.Coins+reward.Coins);
                    if(coins>TradePolicy.MaxWallet)throw new InvalidOperationException("Wallet capacity exceeded.");
                    account.Coins=coins;
                }
                else
                {
                    if(request.Action==CareAction.Feed&&account.Food<1)throw new InvalidOperationException("No food.");
                    if(!PetRules.Care(p,request.Action,now))throw new InvalidOperationException("Care cooldown/condition.");
                    if(request.Action==CareAction.Feed)account.Food--;
                }
                p.Revision=checked(p.Revision+1);
                var result=Snapshot(p,account,now);
                tx.AddActionReceipt(new ActionReceipt{AccountId=owner,RequestId=request.RequestId,Fingerprint=fingerprint,Result=result.Clone()});
                return result;
            });
        }
        static PetState Owned(IWorldTransaction tx,string owner,string id)
        {
            if(!PetIds.Valid(owner)||!PetIds.Valid(id))throw new InvalidOperationException("Invalid owner/pet ID.");
            var p=tx.Pet(id);p.Validate();
            if(p.OwnerAccountId!=owner)throw new InvalidOperationException("Pet not owned by authenticated account.");
            return p;
        }
        static PetSnapshot Snapshot(PetState p,AccountState account,long now)
        {
            p.Validate();if(now<0||account.Coins<0||account.Food<0)throw new InvalidOperationException("Invalid server state.");
            return new PetSnapshot{Pet=p.Clone(),AccountCoins=account.Coins,ServerUtcSeconds=now};
        }
    }
}

#endif
