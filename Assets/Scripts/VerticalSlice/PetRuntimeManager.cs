using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Mongle.VisualRenewal;
namespace Mongle.Slice.Client
{
    public sealed class PetRuntimeManager:MonoBehaviour
    {
        public PetVisualManager visuals;public VisualQuality quality=VisualQuality.Standard;
        public event Action Changed;public event Action SnapshotConfirmed;public event Action<CareAction> CareConfirmed;public event Action<string> Failed;
        ClientPetSession session;CancellationTokenSource lifetime;string pendingKey;int generation;
        double receivedAt;float tick;bool storageValid=true;
        public PetState Snapshot=>session?.Snapshot?.Pet;
        public long AccountCoins=>session?.Snapshot?.AccountCoins??0;
        public int AccountFood=>session?.Snapshot?.AccountFood??0;
        public bool Busy=>session?.Busy??false;
        public bool Ready=>Snapshot!=null;
        public bool HasPending=>session?.Pending!=null;
        public async Task InitializeAsync(IPetGateway transport,string id,string account)
        {
            int epoch=++generation;lifetime?.Cancel();lifetime?.Dispose();lifetime=new CancellationTokenSource();
            if(session!=null)session.PendingChanged-=SavePending;
            session=new ClientPetSession(account,id,transport);pendingKey="FusionTamagotchi.PetPending."+account+"."+id;storageValid=true;
            session.PendingChanged+=SavePending;
            string saved=PlayerPrefs.GetString(pendingKey,"");
            if(saved!="")try{session.Restore(JsonUtility.FromJson<PendingPetCommand>(saved));}
            catch(Exception){storageValid=false;Failed?.Invoke("저장된 미확정 요청을 확인해야 합니다. 신규 요청을 차단했습니다.");}
            Changed?.Invoke();await RefreshAsync();
            if(epoch==generation&&storageValid&&HasPending)await RetryPendingAsync();
        }
        void SavePending()
        {
            var p=session.Pending;if(p==null)PlayerPrefs.DeleteKey(pendingKey);else PlayerPrefs.SetString(pendingKey,JsonUtility.ToJson(p));PlayerPrefs.Save();
        }
        public Task<bool> RefreshAsync()=>Run(s=>s.RefreshAsync(lifetime.Token));
        public Task<bool> CareAsync(CareAction action)=>Command(PetCommandKind.Care,action);
        public Task<bool> ClaimIdleAsync()=>Command(PetCommandKind.ClaimIdle,CareAction.Pet);
        public Task<bool> StartExplorationAsync()=>Command(PetCommandKind.StartExploration,CareAction.Pet);
        public Task<bool> FinishExplorationAsync()=>Command(PetCommandKind.FinishExploration,CareAction.Pet);
        public Task<bool> RetryPendingAsync()=>Run(async s=>{await s.RetryAsync(lifetime.Token);await s.RefreshAsync(lifetime.Token);});
        Task<bool> Command(PetCommandKind kind,CareAction action)
        {
            if(!storageValid)return Task.FromResult(false);
            return Run(s=>s.ExecuteAsync(kind,action,lifetime.Token),kind==PetCommandKind.Care?(CareAction?)action:null);
        }
        async Task<bool> Run(Func<ClientPetSession,Task> work,CareAction? care=null)
        {
            var s=session;int epoch=generation;if(s==null||s.Busy||lifetime==null)return false;
            try
            {
                var task=work(s);Changed?.Invoke();await task;
                if(epoch!=generation)return false;
                receivedAt=Time.realtimeSinceStartupAsDouble;var pet=Snapshot;
                if(pet!=null&&visuals&&!visuals.TryEquip(pet.SpeciesId,pet.Stage,pet.Grade,quality,out var error,pet.CostumeId))Failed?.Invoke(error);
                SnapshotConfirmed?.Invoke();if(care.HasValue)CareConfirmed?.Invoke(care.Value);return true;
            }
            catch(OperationCanceledException){return false;}
            catch(Exception e){if(epoch==generation)Failed?.Invoke(e.Message+(HasPending?" 같은 요청으로 처리 결과를 다시 확인해 주세요.":""));return false;}
            finally{if(epoch==generation)Changed?.Invoke();}
        }
        public PetState ProjectForDisplay()
        {
            var p=Snapshot;var s=session?.Snapshot;if(p==null)return null;
            PetRules.Advance(p,s.ServerUtcSeconds+(long)Math.Max(0,Time.realtimeSinceStartupAsDouble-receivedAt));return p;
        }
        void Update(){if(!Ready)return;tick+=Time.unscaledDeltaTime;if(tick>=1){tick=0;Changed?.Invoke();}}
        void OnApplicationFocus(bool focused){if(focused&&session!=null&&!Busy)_=HasPending?RetryPendingAsync():RefreshAsync();}
        public void ClearSession()
        {generation++;lifetime?.Cancel();lifetime?.Dispose();lifetime=null;if(session!=null)session.PendingChanged-=SavePending;session=null;visuals?.Clear();Changed?.Invoke();}
        void OnDestroy(){ClearSession();}
    }
}
