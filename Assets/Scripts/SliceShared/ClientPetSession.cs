using System;
using System.Threading;
using System.Threading.Tasks;
namespace Mongle.Slice
{
    public enum PetCommandKind { Care, ClaimIdle, StartExploration, FinishExploration }
    [Serializable] public sealed class PendingPetCommand
    {
        public string AccountId; public PetCommandKind Kind; public PetActionRequest Request;
        public PendingPetCommand Clone()=>new PendingPetCommand{AccountId=AccountId,Kind=Kind,Request=Request?.Clone()};
    }
    // Only a definitive server rejection clears a command; timeouts/401/5xx remain uncertain.
    public sealed class PetCommandRejectedException:Exception { public PetCommandRejectedException(string message):base(message){} }
    public sealed class ClientPetSession
    {
        readonly string accountId,petId;readonly IPetGateway gateway;
        PetSnapshot snapshot;PendingPetCommand pending;
        public PetSnapshot Snapshot=>snapshot?.Clone();
        public PendingPetCommand Pending=>pending?.Clone();
        public bool Busy{get;private set;}
        public event Action PendingChanged;
        public ClientPetSession(string account,string pet,IPetGateway transport)
        {if(!PetIds.Valid(account)||!PetIds.Valid(pet)||transport==null)throw new ArgumentException("Invalid session.");accountId=account;petId=pet;gateway=transport;}
        public void Restore(PendingPetCommand value)
        {
            if(Busy||pending!=null||value==null||value.AccountId!=accountId||value.Request==null||value.Request.PetId!=petId||
               !PetIds.Valid(value.Request.RequestId)||value.Request.ExpectedRevision<0||!Enum.IsDefined(typeof(PetCommandKind),value.Kind)||
               !Enum.IsDefined(typeof(CareAction),value.Request.Action))throw new InvalidOperationException("Invalid pending command scope.");
            pending=value.Clone();
        }
        void Accept(PetSnapshot next)
        {
            if(next?.Pet==null||next.Pet.PetId!=petId||next.Pet.OwnerAccountId!=accountId||next.ServerUtcSeconds<0||next.AccountCoins<0||next.AccountFood<0)
                throw new InvalidOperationException("Invalid server snapshot.");
            next.Pet.Validate();
            if(snapshot==null||next.Pet.Revision>=snapshot.Pet.Revision)snapshot=next.Clone();
        }
        public async Task RefreshAsync(CancellationToken token)
        {
            if(Busy)throw new InvalidOperationException("Request in progress.");Busy=true;
            try{var next=await gateway.GetAsync(petId,token);token.ThrowIfCancellationRequested();Accept(next);}finally{Busy=false;}
        }
        public Task ExecuteAsync(PetCommandKind kind,CareAction action,CancellationToken token)
        {
            if(Busy||pending!=null||snapshot==null)throw new InvalidOperationException("Refresh or retry the pending command first.");
            if(!Enum.IsDefined(typeof(PetCommandKind),kind)||!Enum.IsDefined(typeof(CareAction),action))throw new ArgumentException("Invalid command.");
            pending=new PendingPetCommand{AccountId=accountId,Kind=kind,Request=new PetActionRequest{PetId=petId,RequestId=Guid.NewGuid().ToString("N"),ExpectedRevision=snapshot.Pet.Revision,Action=action}};
            return RetryAsync(token);
        }
        public async Task RetryAsync(CancellationToken token)
        {
            if(Busy||pending==null)throw new InvalidOperationException("No retryable command.");Busy=true;
            try
            {
                // Persist before any transport call. A persistence failure must prevent transmission.
                PendingChanged?.Invoke();var c=pending.Clone();PetSnapshot next;
                switch(c.Kind)
                {
                    case PetCommandKind.Care:next=await gateway.CareAsync(c.Request,token);break;
                    case PetCommandKind.ClaimIdle:next=await gateway.ClaimIdleAsync(c.Request,token);break;
                    case PetCommandKind.StartExploration:next=await gateway.StartExplorationAsync(c.Request,token);break;
                    case PetCommandKind.FinishExploration:next=await gateway.FinishExplorationAsync(c.Request,token);break;
                    default:throw new InvalidOperationException("Invalid command.");
                }
                token.ThrowIfCancellationRequested();Accept(next);pending=null;PendingChanged?.Invoke();
            }
            catch(PetCommandRejectedException){pending=null;PendingChanged?.Invoke();throw;}
            finally{Busy=false;}
        }
    }
}
