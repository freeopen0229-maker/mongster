using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace FusionTamagotchi.Client
{
    /// <summary>HTTPS 전송 어댑터. 아래 API 호스트는 아직 프로젝트에 배포돼 있지 않다.</summary>
    public sealed class HttpEconomyGateway:EconomyGatewayBehaviour, Mongle.Slice.IPetGateway
    {
        [SerializeField] string baseUrl="";
        [SerializeField] int timeoutSeconds=20;
        string bearerToken;
        sealed class Pending
        {public UnityWebRequest request;public TaskCompletionSource<string> completion=new TaskCompletionSource<string>();}
        readonly List<Pending> active=new List<Pending>();
        public void ConfigureSession(string httpsBaseUrl,string accessToken)
        {
            NetworkConfig.ValidateEndpoint(httpsBaseUrl);
            Uri uri;
            if(!Uri.TryCreate(httpsBaseUrl,UriKind.Absolute,out uri)||uri.Scheme!="https"||
                !string.IsNullOrEmpty(uri.UserInfo)||!string.IsNullOrEmpty(uri.Query)||!string.IsNullOrEmpty(uri.Fragment)||string.IsNullOrWhiteSpace(accessToken))
                throw new ArgumentException("HTTPS endpoint and access token required.");
            CancelAll();baseUrl=httpsBaseUrl.TrimEnd('/');bearerToken=accessToken;
        }
        public override async Task<ClientSnapshotDTO> ReadAsync(string selectedPetId,CancellationToken token)
        {
            string path="/v2/client/snapshot";
            if(!string.IsNullOrEmpty(selectedPetId))path+="?petId="+Uri.EscapeDataString(selectedPetId);
            string json=await Send(path,null,token);return JsonUtility.FromJson<ClientSnapshotDTO>(json);
        }
        public override async Task<ClientCommandReplyDTO> ExecuteAsync(ClientCommandDTO c,CancellationToken token)
        {string json=await Send("/v2/client/command",JsonUtility.ToJson(c),token);return JsonUtility.FromJson<ClientCommandReplyDTO>(json);}
        public async Task<Mongle.Slice.PetSnapshot> GetAsync(string petId,CancellationToken token)
        {return JsonUtility.FromJson<Mongle.Slice.PetSnapshot>(await Send("/v2/pets/snapshot?petId="+Uri.EscapeDataString(petId),null,token));}
        async Task<Mongle.Slice.PetSnapshot> PetCommand(string route,Mongle.Slice.PetActionRequest request,CancellationToken token)
        {
            try{return JsonUtility.FromJson<Mongle.Slice.PetSnapshot>(await Send(route,JsonUtility.ToJson(request),token));}
            catch(EconomyRequestRejectedException e){throw new Mongle.Slice.PetCommandRejectedException(e.Message);}
        }
        public Task<Mongle.Slice.PetSnapshot> CareAsync(Mongle.Slice.PetActionRequest r,CancellationToken t)=>PetCommand("/v2/pets/care",r,t);
        public Task<Mongle.Slice.PetSnapshot> ClaimIdleAsync(Mongle.Slice.PetActionRequest r,CancellationToken t)=>PetCommand("/v2/pets/claim-idle",r,t);
        public Task<Mongle.Slice.PetSnapshot> StartExplorationAsync(Mongle.Slice.PetActionRequest r,CancellationToken t)=>PetCommand("/v2/pets/exploration/start",r,t);
        public Task<Mongle.Slice.PetSnapshot> FinishExplorationAsync(Mongle.Slice.PetActionRequest r,CancellationToken t)=>PetCommand("/v2/pets/exploration/finish",r,t);
        Task<string> Send(string path,string body,CancellationToken token)
        {
            if(!isActiveAndEnabled||string.IsNullOrEmpty(bearerToken)||string.IsNullOrEmpty(baseUrl))
                throw new InvalidOperationException("서버 주소와 로그인 세션을 먼저 연결해 주세요.");
            token.ThrowIfCancellationRequested();
            var p=new Pending{request=new UnityWebRequest(baseUrl+path,body==null?"GET":"POST")};
            p.request.downloadHandler=new DownloadHandlerBuffer();p.request.timeout=Math.Max(1,timeoutSeconds);p.request.redirectLimit=0;
            if(body!=null){p.request.uploadHandler=new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));p.request.SetRequestHeader("Content-Type","application/json");}
            p.request.SetRequestHeader("Authorization","Bearer "+bearerToken);active.Add(p);StartCoroutine(Run(p,token));return p.completion.Task;
        }
        IEnumerator Run(Pending p,CancellationToken token)
        {
            UnityWebRequestAsyncOperation op=null;Exception failure=null;
            try{op=p.request.SendWebRequest();}catch(Exception e){failure=e;}
            if(failure!=null){p.completion.TrySetException(failure);Cleanup(p);yield break;}
            try
            {
                while(!op.isDone)
                {
                    if(token.IsCancellationRequested){p.request.Abort();p.completion.TrySetCanceled();yield break;}
                    yield return null;
                }
                if(token.IsCancellationRequested){p.completion.TrySetCanceled();yield break;}
                var r=p.request;
                if(r.result==UnityWebRequest.Result.Success)p.completion.TrySetResult(r.downloadHandler.text);
                else if(r.responseCode==400||r.responseCode==403||r.responseCode==409||r.responseCode==422)
                    p.completion.TrySetException(new EconomyRequestRejectedException("서버가 요청을 거절했습니다. 상태를 새로고침해 주세요. ("+r.responseCode+")"));
                else p.completion.TrySetException(new InvalidOperationException("서버 응답을 확인하지 못했습니다. ("+r.responseCode+")"));
            }
            finally{Cleanup(p);}
        }
        void Cleanup(Pending p){active.Remove(p);p.request?.Dispose();p.request=null;}
        void CancelAll()
        {
            foreach(var p in new List<Pending>(active)){p.request?.Abort();p.completion.TrySetCanceled();Cleanup(p);}
            StopAllCoroutines();
        }
        public void ClearSession(){CancelAll();bearerToken=null;baseUrl="";}
        void OnDisable(){ClearSession();}
    }
}
