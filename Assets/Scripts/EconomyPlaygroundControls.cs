using System;
using UnityEngine;
using UnityEngine.UI;
namespace FusionTamagotchi.Client
{
    public sealed class EconomyPlaygroundControls:MonoBehaviour
    {
        public ClientGameManager manager;public HttpEconomyGateway gateway;
        public InputField endpoint,account,accessToken,assetId;
        public Text status;
        void OnEnable(){if(manager)manager.EggsPurchased+=SelectFirstEgg;}
        void OnDisable(){if(manager)manager.EggsPurchased-=SelectFirstEgg;}
        public async void Connect()
        {
            try
            {
                gateway.ConfigureSession(endpoint.text.Trim(),accessToken.text.Trim());
                manager.Initialize(account.text.Trim(),gateway);manager.selectedPetId=assetId.text.Trim();await manager.RefreshAsync();
                if(status)status.text="서버 연결 완료";
            }
            catch(Exception){if(status)status.text="연결 실패: 주소·인증서·로그인 세션을 확인해 주세요.";}
        }
        public async void SelectAsset()
        {
            try{manager.selectedPetId=assetId.text.Trim();await manager.RefreshAsync();}
            catch(Exception){if(status)status.text="선택한 자산을 조회하지 못했습니다.";}
        }
        void SelectFirstEgg(string[] ids){if(ids.Length==0)return;assetId.text=ids[0];SelectAsset();}
    }
}
