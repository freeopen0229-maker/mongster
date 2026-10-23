using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Mongle.VisualRenewal;

namespace Mongle.Slice.Client
{
    /// <summary>
    /// 온라인 슬라이스의 클라이언트 상태 보관자. 로컬 JSON/기기 UTC로 재화나 거래를 확정하지 않는다.
    /// 기존 Mongle.Gameplay.PetRuntimeManager는 이전 오프라인 실험용이며 함께 부착하지 않는다.
    /// </summary>
    public sealed class PetRuntimeManager:MonoBehaviour
    {
        public PetVisualManager visuals;
        public VisualQuality quality=VisualQuality.Standard;
        public event Action Changed;
        public event Action<CareAction> CareConfirmed;
        public event Action<string> Failed;
        public PetState Snapshot=>snapshot?.Pet.Clone();
        public long AccountCoins=>snapshot?.AccountCoins??0;
        public bool Busy{get;private set;}
        IPetGateway gateway;PetSnapshot snapshot;string petId;
        double receivedAt;int generation;float displayTick;
        CancellationTokenSource lifetime;

        public async Task InitializeAsync(IPetGateway transport,string id)
        {
            if(transport==null||!PetIds.Valid(id))throw new ArgumentException("Gateway/pet ID required.");
            lifetime?.Cancel();lifetime?.Dispose();lifetime=new CancellationTokenSource();
            generation++;gateway=transport;petId=id;snapshot=null;Busy=false;
            await RefreshAsync();
        }
        public Task<bool> RefreshAsync()=>RequestAsync(null,false);
        public Task<bool> CareAsync(CareAction action)=>RequestAsync(action,false);
        public Task<bool> ClaimIdleAsync()=>RequestAsync(null,true);

        async Task<bool> RequestAsync(CareAction? action,bool claim)
        {
            if(Busy||gateway==null||lifetime==null)return false;
            if((action.HasValue||claim)&&snapshot==null)return false;
            int epoch=generation;Busy=true;
            var localGateway=gateway;var token=lifetime.Token;string id=petId;
            try
            {
                PetSnapshot next;
                if(!action.HasValue&&!claim)next=await localGateway.GetAsync(id,token);
                else
                {
                    var request=new PetActionRequest{PetId=id,ExpectedRevision=snapshot.Pet.Revision,
                        RequestId=Guid.NewGuid().ToString("N"),Action=action??CareAction.Pet};
                    // HTTP 재전송은 transport가 같은 request 객체/RequestId를 유지해야 한다.
                    next=claim?await localGateway.ClaimIdleAsync(request,token):await localGateway.CareAsync(request,token);
                }
                if(token.IsCancellationRequested||epoch!=generation)return false;
                if(next==null||next.Pet==null||next.Pet.PetId!=id||next.ServerUtcSeconds<0||next.AccountCoins<0)
                    throw new InvalidOperationException("Invalid server snapshot.");
                next.Pet.Validate();
                if(snapshot!=null&&next.Pet.Revision<snapshot.Pet.Revision)return false;
                snapshot=next.Clone();receivedAt=Time.realtimeSinceStartupAsDouble;
                // Gameplay 스냅샷 수신이 렌더링 실패 때문에 취소되지 않게 분리한다.
                if(visuals&&!visuals.TryEquip(snapshot.Pet.SpeciesId,snapshot.Pet.Stage,snapshot.Pet.Grade,quality,
                                             out var visualError,snapshot.Pet.CostumeId))Failed?.Invoke(visualError);
                Changed?.Invoke();if(action.HasValue)CareConfirmed?.Invoke(action.Value);return true;
            }
            catch(OperationCanceledException){return false;}
            catch(Exception e){if(epoch==generation)Failed?.Invoke(e.Message);return false;}
            finally{if(epoch==generation)Busy=false;}
        }
        /// <summary>UI용 예상치. Advance 결과를 서버에 업로드하거나 재화로 지급하지 않는다.</summary>
        public PetState ProjectForDisplay()
        {
            if(snapshot==null)return null;
            var projected=snapshot.Pet.Clone();
            long now=snapshot.ServerUtcSeconds+(long)Math.Max(0,Time.realtimeSinceStartupAsDouble-receivedAt);
            PetRules.Advance(projected,now);return projected;
        }
        void Update()
        {
            // 매초 UI 예상치만 갱신한다. 서버 요청/보상 확정/디스크 쓰기를 하지 않는다.
            if(snapshot==null)return;displayTick+=Time.unscaledDeltaTime;
            if(displayTick>=1){displayTick=0;Changed?.Invoke();}
        }
        void OnApplicationFocus(bool focused){if(focused&&gateway!=null)_=RefreshAsync();}
        void OnDestroy(){generation++;lifetime?.Cancel();lifetime?.Dispose();}
    }
}
