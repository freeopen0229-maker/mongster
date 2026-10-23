using System;
using System.Threading;
using System.Threading.Tasks;

namespace FusionTamagotchi.Client
{
    /// <summary>Unity와 분리한 UI 상태. 낙관적 차감 없음, 불확실한 실패는 동일 명령 ID로 재시도.</summary>
    public sealed class ClientEconomySession
    {
        readonly string owner;
        readonly IClientEconomyGateway gateway;
        int busy;
        UserWalletDTO wallet;
        PetDTO pet;
        EggDTO egg;
        ClientCommandDTO pending;
        public event Action Changed,PendingChanged;
        public bool IsBusy=>Volatile.Read(ref busy)!=0;
        public bool IsReady=>wallet!=null;
        public bool HasPending=>pending!=null;
        public UserWalletDTO Wallet=>wallet?.Clone();
        public PetDTO Pet=>pet?.Clone();
        public EggDTO Egg=>egg?.Clone();
        public ClientCommandDTO Pending=>pending?.Clone();
        public ClientEconomySession(string accountId,IClientEconomyGateway gateway)
        {
            if(!Mongle.Slice.PetIds.Valid(accountId))throw new ArgumentException("Invalid account.");
            owner=accountId;this.gateway=gateway??throw new ArgumentNullException(nameof(gateway));
        }
        void Enter(){if(Interlocked.CompareExchange(ref busy,1,0)!=0)throw new InvalidOperationException("Request in progress.");Changed?.Invoke();}
        void Leave(){Interlocked.Exchange(ref busy,0);Changed?.Invoke();}
        void Apply(ClientSnapshotDTO s)
        {
            if(s==null||s.accountId!=owner||s.wallet==null)throw new InvalidOperationException("Account/response mismatch.");
            s.wallet.Validate();s.pet?.Validate();s.egg?.Validate();
            if(wallet!=null&&s.wallet.revision<wallet.revision)throw new InvalidOperationException("Stale wallet snapshot; refresh/retry required.");
            // 응답 전체를 검증한 뒤 UI 상태를 한 번에 교체한다.
            wallet=s.wallet.Clone();pet=s.pet?.Clone();egg=s.egg?.Clone();Changed?.Invoke();
        }
        public async Task RefreshAsync(string selectedPetId,CancellationToken token)
        {
            Enter();try{Apply(await gateway.ReadAsync(selectedPetId,token));}finally{Leave();}
        }
        public void RestorePending(ClientCommandDTO command)
        {
            if(IsBusy||HasPending||command==null||!Mongle.Slice.PetIds.Valid(command.requestId)||
                !Enum.IsDefined(typeof(ClientCommandKind),command.kind))throw new InvalidOperationException("Invalid saved request.");
            pending=command.Clone();PendingChanged?.Invoke();
        }
        void NewCommand(ClientCommandDTO c)
        {
            if(!IsReady||HasPending)throw new InvalidOperationException("Load wallet or resolve previous request first.");
            c.requestId=Guid.NewGuid().ToString("N");pending=c;PendingChanged?.Invoke();
        }
        public async Task<ClientCommandReplyDTO> BuyEggAsync(bool tenPack,CancellationToken token)
        {
            Enter();try
            {
                if(wallet==null||wallet.gem<(tenPack?300:30))throw new InvalidOperationException("Insufficient Gem or wallet not loaded.");
                NewCommand(new ClientCommandDTO{kind=ClientCommandKind.BuyLuminousEgg,tenPack=tenPack,petId=pet?.petId});
                return await SendPending(token);
            }finally{Leave();}
        }
        public async Task<ClientCommandReplyDTO> RerollAsync(string petId,int slot,CancellationToken token)
        {
            Enter();try
            {
                if(pet==null||pet.petId!=petId||slot<0||slot>=pet.skills.Count||wallet==null||wallet.jeon<pet.nextRerollCost)
                    throw new InvalidOperationException("Invalid pet/slot or insufficient Jeon.");
                NewCommand(new ClientCommandDTO{kind=ClientCommandKind.RerollSkill,petId=petId,expectedRevision=pet.revision,slotIndex=slot});
                return await SendPending(token);
            }finally{Leave();}
        }
        public async Task<ClientCommandReplyDTO> EggActionAsync(ClientCommandKind kind,string assetId,long revision,CancellationToken token)
        {
            Enter();try
            {
                if(kind!=ClientCommandKind.StartIncubation&&kind!=ClientCommandKind.UseHatchScroll&&kind!=ClientCommandKind.Hatch)
                    throw new InvalidOperationException("Invalid egg command.");
                if(!Mongle.Slice.PetIds.Valid(assetId)||revision<0)throw new InvalidOperationException("Invalid egg.");
                NewCommand(new ClientCommandDTO{kind=kind,petId=assetId,expectedRevision=revision});
                return await SendPending(token);
            }finally{Leave();}
        }
        public async Task<ClientCommandReplyDTO> RetryAsync(CancellationToken token)
        {Enter();try{if(pending==null)throw new InvalidOperationException("No pending request.");return await SendPending(token);}finally{Leave();}}
        async Task<ClientCommandReplyDTO> SendPending(CancellationToken token)
        {
            var c=pending.Clone();
            try
            {
                var r=await gateway.ExecuteAsync(c,token);
                if(r==null||r.requestId!=c.requestId||r.receivedAssetIds==null)throw new InvalidOperationException("Command response mismatch.");
                if(c.kind==ClientCommandKind.BuyLuminousEgg)
                {
                    if(r.receivedAssetIds.Length!=(c.tenPack?11:1))throw new InvalidOperationException("Egg quantity response mismatch.");
                    var ids=new System.Collections.Generic.HashSet<string>();
                    foreach(var id in r.receivedAssetIds)if(!Mongle.Slice.PetIds.Valid(id)||!ids.Add(id))throw new InvalidOperationException("Invalid egg IDs.");
                }
                if(c.kind==ClientCommandKind.Hatch&&(r.snapshot?.pet==null||r.snapshot.pet.petId!=c.petId))
                    throw new InvalidOperationException("Hatch result missing.");
                Apply(r.snapshot);pending=null;PendingChanged?.Invoke();return r;
            }
            catch(EconomyRequestRejectedException){pending=null;PendingChanged?.Invoke();throw;}
            // 통신 실패/취소/잘못된 응답은 서버 처리 여부가 불확실하다. 명령을 지우지 않는다.
        }
    }
}
