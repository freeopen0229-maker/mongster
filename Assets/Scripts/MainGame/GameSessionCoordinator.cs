using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Mongle.Slice.Client;
namespace FusionTamagotchi.Client
{
    public sealed class GameSessionCoordinator:MonoBehaviour
    {
        public HttpEconomyGateway gateway;public ClientGameManager economy;public PetRuntimeManager pet;
        public GameLoginProvider loginProvider;
        public event Action<string> StatusChanged;
        public event Action<bool> PetVisibilityChanged;
        public bool LoggedIn{get;private set;}
        string account;int generation;CancellationTokenSource lifetime;
        void OnEnable(){if(economy){economy.PetHatched+=Hatched;economy.PetUpdated+=EconomyPetUpdated;}if(pet)pet.SnapshotConfirmed+=PetConfirmed;}
        void OnDisable(){if(economy){economy.PetHatched-=Hatched;economy.PetUpdated-=EconomyPetUpdated;}if(pet)pet.SnapshotConfirmed-=PetConfirmed;Logout();}
        public async Task LoginAsync()
        {
            try
            {
                NetworkConfig.Load();
                if(!loginProvider)throw new InvalidOperationException("로그인 서비스가 아직 연결되지 않았습니다.");
                lifetime?.Cancel();lifetime?.Dispose();lifetime=new CancellationTokenSource();
                int g=++generation;var result=await loginProvider.LoginAsync(lifetime.Token);
                if(g==generation)await CompleteLoginAsync(result);
            }
            catch(Exception e){StatusChanged?.Invoke(e.Message);}
        }
        // Called only by a trusted login-provider callback, never by address/token text fields.
        public async Task CompleteLoginAsync(GameLoginResult result)
        {
            Logout();int g=generation;
            try
            {
                result.Validate();var config=NetworkConfig.Load();
                gateway.ConfigureSession(config.apiBaseUrl,result.accessToken);account=result.accountId;
                economy.selectedPetId=result.selectedPetId;economy.Initialize(account,gateway);
                await economy.RefreshAsync();if(g!=generation)return;LoggedIn=true;
                if(economy.SelectedPet!=null)await SelectPetAsync(economy.SelectedPet.petId,g);
                else StatusChanged?.Invoke("부화할 알을 선택해 주세요.");
            }
            catch(Exception e){if(g==generation){Logout();StatusChanged?.Invoke(e.Message);}}
        }
        async void PetConfirmed()
        {int epoch=generation;try{if(LoggedIn&&!economy.isProcessing)await economy.RefreshAsync();}catch(Exception e){if(epoch==generation)StatusChanged?.Invoke(e.Message);}}
        async void EconomyPetUpdated(PetDTO value)
        {try{if(LoggedIn&&pet.Ready&&!pet.Busy)await pet.RefreshAsync();}catch(Exception e){StatusChanged?.Invoke(e.Message);}}
        async void Hatched(PetDTO value)
        {try{if(LoggedIn)await SelectPetAsync(value.petId,generation);}catch(Exception e){StatusChanged?.Invoke(e.Message);}}
        async Task SelectPetAsync(string id,int epoch)
        {
            PetVisibilityChanged?.Invoke(false);await pet.InitializeAsync(gateway,id,account);
            if(epoch!=generation)return;
            PetVisibilityChanged?.Invoke(pet.Ready);
            StatusChanged?.Invoke(pet.Ready?"펫과 함께할 준비가 됐습니다.":"펫 상태를 불러오지 못했습니다.");
        }
        public void Logout()
        {generation++;LoggedIn=false;account=null;lifetime?.Cancel();lifetime?.Dispose();lifetime=null;pet?.ClearSession();if(economy)economy.ClearSession();if(gateway)gateway.ClearSession();PetVisibilityChanged?.Invoke(false);}
    }
}
