using System;
using System.Collections.Generic;
using System.Collections;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace FusionTamagotchi.Client
{
    /// <summary>
    /// 서버 확정 스냅샷을 UI에 표시한다. 버튼에서는 재화/펫 결과를 직접 변경하지 않는다.
    /// Unity 메인 스레드에서 Initialize 호출. 인증 토큰은 HTTPS gateway에 별도로 설정한다.
    /// </summary>
    public sealed class ClientGameManager:MonoBehaviour
    {
        [Header("UI 텍스트 연동")]
        public Text txtJeon,txtGem,txtThread,txtPetSerial,txtGlobalNotice,txtStatus;
        [Header("요청 중 잠글 버튼 / 연결할 Gateway")]
        public Button[] actionButtons=Array.Empty<Button>();
        public Button retryButton;
        public PetSerialTooltip serialTooltip;
        public GameObject globalNoticePanel;
        [Min(1)] public float globalNoticeSeconds=6;
        Coroutine noticeHide;
        public EconomyGatewayBehaviour gateway;
        [Header("선택한 펫")]
        public string selectedPetId;
        // Inspector에 수기 입력한 지갑을 서버 잔액으로 사용하지 않는다.
        public UserWalletDTO wallet {get;private set;}=new UserWalletDTO();
        public PetDTO SelectedPet=>session?.Pet;
        public EggDTO SelectedEgg=>session?.Egg;
        public bool isProcessing=>session!=null&&session.IsBusy;
        public event Action<string[]> EggsPurchased;
        public event Action<PetDTO> PetUpdated,PetHatched;
        public event Action<EggDTO> EggUpdated;
        ClientEconomySession session;
        CancellationTokenSource lifetime;
        string accountId;
        int generation;
        readonly HashSet<string> notices=new HashSet<string>();
        readonly Queue<string> noticeOrder=new Queue<string>();
        static readonly CultureInfo Korean=CultureInfo.GetCultureInfo("ko-KR");
        bool initialized;

        void Start(){UpdateWalletUI();UpdateControls();}
        void OnEnable()
        {
            lifetime=new CancellationTokenSource();
            if(initialized&&session!=null)_=RefreshForEnable();
        }
        void OnDestroy(){if(session!=null){session.Changed-=Render;session.PendingChanged-=SavePending;}}
        void OnDisable(){if(noticeHide!=null){StopCoroutine(noticeHide);noticeHide=null;}if(globalNoticePanel)globalNoticePanel.SetActive(false);generation++;lifetime?.Cancel();lifetime?.Dispose();lifetime=null;}
        async Task RefreshForEnable(){try{await RefreshAsync();}catch(Exception e){SetStatus(e is OperationCanceledException?"요청이 중단됐습니다. 재접속 후 확인해 주세요.":e.Message);}}
        /// <summary>로그인 완료 콜백에서 호출. 다른 계정의 이전 지갑/펫/진행 중 응답은 반영하지 않는다.</summary>
        public void Initialize(string authenticatedAccountId,IClientEconomyGateway connectedGateway)
        {
            if(!isActiveAndEnabled)throw new InvalidOperationException("Activate manager before initialization.");
            generation++;lifetime?.Cancel();lifetime?.Dispose();lifetime=new CancellationTokenSource();
            if(session!=null){session.Changed-=Render;session.PendingChanged-=SavePending;}
            accountId=authenticatedAccountId;session=new ClientEconomySession(accountId,connectedGateway);
            session.Changed+=Render;session.PendingChanged+=SavePending;initialized=true;
            wallet=new UserWalletDTO();notices.Clear();noticeOrder.Clear();
            string saved=PlayerPrefs.GetString(PendingKey,"");
            if(!string.IsNullOrEmpty(saved))
            {
                try{session.RestorePending(JsonUtility.FromJson<ClientCommandDTO>(saved));}
                catch(Exception){SetStatus("저장된 요청을 읽지 못했습니다. 새 결제 전에 서버 거래 내역을 확인해 주세요.");throw;}
            }
            Render();
        }
        public void InitializeWithConfiguredGateway(string authenticatedAccountId)
        {if(!gateway)throw new InvalidOperationException("Gateway is missing.");Initialize(authenticatedAccountId,gateway);}
        string PendingKey=>"FusionTamagotchi.Pending."+accountId;
        void SavePending()
        {
            var p=session.Pending;
            if(p==null)PlayerPrefs.DeleteKey(PendingKey);else PlayerPrefs.SetString(PendingKey,JsonUtility.ToJson(p));
            PlayerPrefs.Save(); // 계정별 요청 ID만 보존. 토큰/결제 비밀/잔액은 저장하지 않는다.
        }
        CancellationToken Token=>lifetime?.Token??new CancellationToken(true);
        public async Task RefreshAsync()
        {
            RequireSession();int g=generation;var s=session;
            await s.RefreshAsync(selectedPetId,Token);
            if(g==generation)Render();
        }
        public async Task BuyGloriousEggAsync(bool isTenPack)
        {
            RequireSession();int g=generation;var s=session;
            var r=await s.BuyEggAsync(isTenPack,Token);
            if(g!=generation)return;
            Render();SetStatus($"영롱한 알 {r.receivedAssetIds.Length}개를 받았습니다.");
            EggsPurchased?.Invoke((string[])r.receivedAssetIds.Clone());
            // 구매는 알 지급 연출. Hatch 성공 응답 전에는 펫 부화/등급 연출을 실행하지 않는다.
        }
        public async Task RerollSkillAsync(PetDTO pet,int slotIndex)
        {
            RequireSession();if(pet==null)throw new ArgumentNullException(nameof(pet));
            int g=generation;var s=session;await s.RerollAsync(pet.petId,slotIndex,Token);
            if(g!=generation)return;Render();PetUpdated?.Invoke(s.Pet);
        }
        public async Task RetryPendingAsync()
        {
            RequireSession();int g=generation;var s=session;var pending=s.Pending;
            var r=await s.RetryAsync(Token);if(g!=generation)return;
            Render();SetStatus("서버 처리 결과를 확인했습니다.");
            if(pending.kind==ClientCommandKind.BuyLuminousEgg)EggsPurchased?.Invoke((string[])r.receivedAssetIds.Clone());
            else if(pending.kind==ClientCommandKind.Hatch)PetHatched?.Invoke(s.Pet);
            else if(pending.kind==ClientCommandKind.RerollSkill)PetUpdated?.Invoke(s.Pet);
            else EggUpdated?.Invoke(s.Egg);
        }
        public async Task EggActionAsync(ClientCommandKind kind)
        {
            RequireSession();var egg=session.Egg;if(egg==null)throw new InvalidOperationException("알을 먼저 선택해 주세요.");
            int g=generation;var s=session;await s.EggActionAsync(kind,egg.assetId,egg.revision,Token);
            if(g!=generation)return;Render();
            if(kind==ClientCommandKind.Hatch){SetStatus("부화가 완료됐습니다.");PetHatched?.Invoke(s.Pet);}
            else EggUpdated?.Invoke(s.Egg);
        }
        public async void StartSelectedEgg(){await ButtonAction(()=>EggActionAsync(ClientCommandKind.StartIncubation));}
        public async void UseSelectedHatchScroll(){await ButtonAction(()=>EggActionAsync(ClientCommandKind.UseHatchScroll));}
        public async void HatchSelectedEgg(){await ButtonAction(()=>EggActionAsync(ClientCommandKind.Hatch));}
        // Unity Button.onClick에 연결하는 void 진입점. 비동기 예외는 아래 단일 창구에서 처리.
        public async void BuyGloriousEgg(bool isTenPack){await ButtonAction(()=>BuyGloriousEggAsync(isTenPack));}
        public async void ReqRerollSkill(PetDTO pet,int slotIndex,long currentRerollCost)
        {await ButtonAction(()=>RerollSkillAsync(pet,slotIndex));} // legacy 인자의 가격은 사용하지 않는다.
        public async void RerollSelectedSlot(int slotIndex){await ButtonAction(()=>RerollSkillAsync(SelectedPet,slotIndex));}
        public async void RefreshFromServer(){await ButtonAction(RefreshAsync);}
        public async void RetryPending(){await ButtonAction(RetryPendingAsync);}
        async Task ButtonAction(Func<Task> action)
        {
            int g=generation;
            try{await action();}
            catch(Exception e)
            {
                if(g!=generation)return;
                string suffix=session!=null&&session.HasPending?" 처리 결과가 미확정입니다. 같은 요청으로 다시 확인해 주세요.":"";
                SetStatus((e is OperationCanceledException?"요청이 중단됐습니다.":e.Message)+suffix);
            }
            finally{if(g==generation)UpdateControls();}
        }
        void RequireSession(){if(session==null||!isActiveAndEnabled)throw new InvalidOperationException("로그인 및 서버 연결이 필요합니다.");}
        void Render()
        {
            if(!isActiveAndEnabled)return;
            var w=session?.Wallet;if(w!=null)wallet=w;
            UpdateWalletUI();UpdateControls();var p=session?.Pet;
            if(txtPetSerial)txtPetSerial.text=p!=null&&p.isHiddenPet?HatchPresentationPolicy.Serial(p.serialNumber,p.hiddenLimit):"";
            if(serialTooltip)serialTooltip.Bind(p);
        }
        public void UpdateWalletUI()
        {
            bool ready=session!=null&&session.IsReady;
            if(txtJeon)txtJeon.text=ready?wallet.jeon.ToString("N0",Korean)+" 전":"— 전";
            if(txtGem)txtGem.text=ready?wallet.gem.ToString("N0",Korean)+" 보석":"— 보석";
            if(txtThread)txtThread.text=ready?wallet.mileageThread.ToString("N0",Korean)+" 실타래":"— 실타래";
        }
        void UpdateControls()
        {
            bool can=session!=null&&session.IsReady&&!session.IsBusy&&!session.HasPending;
            foreach(var b in actionButtons)if(b)b.interactable=can;
            if(retryButton)retryButton.interactable=session!=null&&!session.IsBusy&&session.HasPending;
        }
        void SetStatus(string text){if(txtStatus)txtStatus.text=text;Debug.Log("[Economy UI] "+text);}
        // 서버 방송 수신 계층만 호출한다. eventId로 재연결 중복 표시를 방지한다.
        public void ReceiveHiddenNotice(HiddenAwakeningNoticeDTO notice)
        {
            if(!HatchPresentationPolicy.ValidNotice(notice)||!notices.Add(notice.eventId))return;
            noticeOrder.Enqueue(notice.eventId);if(noticeOrder.Count>512)notices.Remove(noticeOrder.Dequeue());
            ShowGlobalNoticeHiddenAwakened(notice.trainerName,notice.petName,notice.rarity,notice.serialNo,notice.maxLimit);
        }
        public void ShowGlobalNoticeHiddenAwakened(string trainerName,string petName,string rarity,int serialNo,int maxLimit)
        {
            int type=rarity=="Divine"?0:1;
            if(!HatchPresentationPolicy.ValidHiddenIdentity(type,rarity,serialNo,maxLimit))return;
            string grade=rarity=="Divine"?"신수":rarity=="Legendary"?"전설":null;if(grade==null)return;
            string message=$"[전설의 각성] {trainerName} 님의 '{petName}'이(가) {grade}로 각성했습니다! No. {serialNo:D3} / {maxLimit:D3}";
            // 시리얼은 현재 총 발급량과 다르다. 오래된 이벤트로 잘못된 남은 수량을 표시하지 않는다.
            if(txtGlobalNotice){txtGlobalNotice.supportRichText=false;txtGlobalNotice.text=message;}
            if(globalNoticePanel)globalNoticePanel.SetActive(true);
            if(noticeHide!=null)StopCoroutine(noticeHide);
            if(isActiveAndEnabled)noticeHide=StartCoroutine(HideNotice());
            Debug.Log(message);
        }
        IEnumerator HideNotice(){yield return new WaitForSecondsRealtime(Mathf.Max(1,globalNoticeSeconds));if(globalNoticePanel)globalNoticePanel.SetActive(false);noticeHide=null;}
    }
}
