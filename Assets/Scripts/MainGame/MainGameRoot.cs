using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Mongle.Slice;
using PetManager=Mongle.Slice.Client.PetRuntimeManager;
namespace FusionTamagotchi.Client
{
    /// <summary>Committed MainGame scene entry. Builds presentation only; never creates accounts, tokens or local rewards.</summary>
    public sealed class MainGameRoot:MonoBehaviour
    {
        public GameLoginProvider loginProvider;
        GameSessionCoordinator session;ClientGameManager economy;PetManager pet;
        Text status,stats,petInfo;Transform petView;Button[] commands;Button retry,connect;
        Button finish,start,hatch,incubate,buyEgg;float wander;
        void Awake()
        {
            Application.targetFrameRate=60;
            var camera=new GameObject("Main Camera").AddComponent<Camera>();camera.tag="MainCamera";camera.orthographic=true;camera.orthographicSize=4;camera.transform.position=new Vector3(0,0,-10);camera.backgroundColor=new Color(.1f,.18f,.2f);camera.clearFlags=CameraClearFlags.SolidColor;camera.gameObject.AddComponent<AudioListener>();
            // Add services inactive so all lifecycle subscriptions see assigned references on enable.
            var services=new GameObject("Server Session");services.SetActive(false);
            var gateway=services.AddComponent<HttpEconomyGateway>();economy=services.AddComponent<ClientGameManager>();economy.gateway=gateway;
            pet=services.AddComponent<PetManager>();session=services.AddComponent<GameSessionCoordinator>();
            session.gateway=gateway;session.economy=economy;session.pet=pet;session.loginProvider=loginProvider;
            var canvas=new GameObject("Game HUD",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=canvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1080,1920);scaler.matchWidthOrHeight=.5f;
            new GameObject("EventSystem",typeof(EventSystem),typeof(StandaloneInputModule));var ui=canvas.transform;
            Label(ui,"늘귀의 하루",0,810,920,90,52);
            stats=Label(ui,"로그인 후 펫 상태를 불러옵니다",0,665,960,110,28);
            petInfo=Label(ui,"나의 펫",0,480,900,90,30);
            status=Label(ui,"서버 설정과 로그인 서비스를 확인합니다",0,-760,960,150,27);
            connect=Button(ui,"로그인",0,-580,()=>_=session.LoginAsync());
            var feed=Button(ui,"먹이 주기",-250,-160,()=>_=pet.CareAsync(CareAction.Feed));
            var touch=Button(ui,"교감하기",250,-160,()=>_=pet.CareAsync(CareAction.Pet));
            var play=Button(ui,"놀아주기",-250,-285,()=>_=pet.CareAsync(CareAction.Play));
            var idle=Button(ui,"방치 보상 받기",250,-285,()=>_=pet.ClaimIdleAsync());
            start=Button(ui,"탐험 시작",-250,-410,()=>_=pet.StartExplorationAsync());
            finish=Button(ui,"탐험 결과 받기",250,-410,()=>_=pet.FinishExplorationAsync());commands=new[]{feed,touch,play,idle,start,finish};
            retry=Button(ui,"미확정 요청 다시 확인",0,-900,()=>{if(pet.HasPending)_=pet.RetryPendingAsync();else economy.RetryPending();});
            buyEgg=Button(ui,"알 구매",-250,325,()=>economy.BuyGloriousEgg(false));
            incubate=Button(ui,"부화 시작",250,325,economy.StartSelectedEgg);
            hatch=Button(ui,"부화 확인",0,200,economy.HatchSelectedEgg);
            // Show hatch control only for an authenticated server-selected egg, never an invented local egg.
            economy.PetHatched+=_=>Render();
            economy.EggsPurchased+=async ids=>{if(ids.Length==0)return;economy.selectedPetId=ids[0];try{await economy.RefreshAsync();}catch(Exception e){ShowStatus(e.Message);}};
            economy.StatusChanged+=ShowStatus;pet.Changed+=Render;pet.Failed+=ShowStatus;session.StatusChanged+=ShowStatus;session.PetVisibilityChanged+=visible=>{if(petView)petView.gameObject.SetActive(visible);};
            CreatePetView();services.SetActive(true);Render();
            try{NetworkConfig.Load();ShowStatus("로그인 후 서버에서 펫을 불러옵니다.");}
            catch(Exception e){ShowStatus(e.Message);}
        }
        void CreatePetView()
        {
            petView=new GameObject("Pet View").transform;petView.position=new Vector3(0,.3f,0);
            string[] parts={"tail","earFar","pawFar","armFar","body","head","pawNear","armNear","earNear"};
            for(int i=0;i<parts.Length;i++)
            {
                var texture=Resources.Load<Texture2D>("MainGame/"+parts[i]);if(!texture)continue;
                var r=new GameObject(parts[i]).AddComponent<SpriteRenderer>();r.transform.SetParent(petView,false);
                r.sprite=Sprite.Create(texture,new UnityEngine.Rect(0,0,texture.width,texture.height),new Vector2(.5f,.5f),100);r.sortingOrder=i;
                var material=Resources.Load<Material>("MainGame/PetSprite");if(material)r.sharedMaterial=material;
            }
            petView.gameObject.SetActive(false);
        }
        void Update()
        {
            // Display-only movement. Currency/XP/exploration completion always comes from the server.
            Render();var p=pet?pet.Snapshot:null;if(p==null||!petView)return;
            wander+=Time.deltaTime;float travel=p.ExplorationId!=""?Mathf.Sin(wander*.8f)*1.1f:0;
            petView.position=new Vector3(travel,.3f+Mathf.Sin(wander*2)*.025f,0);
        }
        void Render()
        {
            var p=pet?pet.ProjectForDisplay():null;bool ready=session&&session.LoggedIn&&p!=null;
            bool available=ready&&!pet.Busy&&!pet.HasPending&&!economy.isProcessing&&!economy.HasPending;
            foreach(var b in commands??Array.Empty<Button>())b.interactable=available&&!p.IsBusy;
            if(retry)retry.interactable=(ready&&pet.HasPending&&!pet.Busy)||(session&&session.LoggedIn&&economy.HasPending&&!economy.isProcessing);
            if(start)start.interactable=available&&!p.IsBusy;
            if(finish)finish.interactable=available&&p.ExplorationId!=""&&p.LastUtc>=p.ExplorationEndUtc;
            bool shop=session&&session.LoggedIn&&!economy.isProcessing&&!economy.HasPending&&!pet.Busy&&!pet.HasPending;
            if(buyEgg)buyEgg.interactable=shop;
            if(incubate)incubate.interactable=shop&&economy.SelectedEgg!=null&&!economy.SelectedEgg.incubating;
            if(hatch)hatch.interactable=shop&&economy.SelectedEgg!=null&&economy.SelectedEgg.incubating;
            if(p==null){if(stats)stats.text=session&&session.LoggedIn?"전 "+economy.wallet.jeon+" · 보석 "+economy.wallet.gem:"로그인 후 펫 상태를 불러옵니다";if(petInfo)petInfo.text="나의 펫";return;}
            stats.text=$"Lv.{p.Level}   포만감 {p.Hunger:F0}   친밀도 {p.Affection:F0}\n피로 {p.Fatigue:F0}   전 {pet.AccountCoins}   먹이 {pet.AccountFood}";
            petInfo.text=p.ExplorationId!=""?$"탐험 중 · 남은 시간 {Math.Max(0,p.ExplorationEndUtc-p.LastUtc)}초":$"{p.PetId} · {p.Stage} · {p.Grade}";
        }
        void ShowStatus(string value){if(status)status.text=value;}
        static RectTransform Rect(Transform parent,string name,float x,float y,float w,float h)
        {var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);r.anchoredPosition=new Vector2(x,y);r.sizeDelta=new Vector2(w,h);return r;}
        static Text Label(Transform parent,string value,float x,float y,float w,float h,int size)
        {var t=Rect(parent,value,x,y,w,h).gameObject.AddComponent<Text>();t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.text=value;t.fontSize=size;t.color=Color.white;t.alignment=TextAnchor.MiddleCenter;t.raycastTarget=false;return t;}
        static Button Button(Transform parent,string value,float x,float y,UnityEngine.Events.UnityAction action)
        {var r=Rect(parent,value,x,y,460,95);r.gameObject.AddComponent<Image>().color=new Color(.19f,.37f,.37f);var b=r.gameObject.AddComponent<Button>();b.onClick.AddListener(action);Label(r,value,0,0,440,90,29);return b;}
    }
}
