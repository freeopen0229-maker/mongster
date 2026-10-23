using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FusionTamagotchi.Client
{
    /// <summary>PC hover, 모바일 tap으로 확인. 비각성 아기에게 잠재 히든 정보는 표시하지 않는다.</summary>
    public sealed class PetSerialTooltip:MonoBehaviour,IPointerEnterHandler,IPointerExitHandler,IPointerClickHandler
    {
        public GameObject panel;
        public Text tooltipText;
        readonly SerialTooltipState state=new SerialTooltipState();
        public void Bind(PetDTO pet)
        {
            bool available=pet!=null&&pet.isAdult&&pet.isHiddenPet&&pet.serialNumber>0&&pet.serialNumber<=pet.hiddenLimit;
            state.Bind(available);
            if(tooltipText)
            {
                tooltipText.supportRichText=false;
                tooltipText.text=available?"전 서버 한정 각성\n"+HatchPresentationPolicy.Serial(pet.serialNumber,pet.hiddenLimit):"";
            }
            if(panel)panel.SetActive(false);
        }
        // 테스트 씬의 StandaloneInputModule 기준: mouse pointer ID는 음수, touch는 0 이상.
        public void OnPointerEnter(PointerEventData e){state.Enter(e.pointerId<0);Sync();}
        public void OnPointerExit(PointerEventData e){state.Exit(e.pointerId<0);Sync();}
        public void OnPointerClick(PointerEventData e){state.Click(e.pointerId<0);Sync();}
        void Sync(){if(panel)panel.SetActive(state.Visible);}
        void OnDisable(){state.Hide();Sync();}
    }
}
