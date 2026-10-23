using UnityEngine;
using UnityEngine.UI;

namespace FusionTamagotchi.Client
{
    /// <summary>부화 성공 팝업의 색상과 UI 오라. 본체/전투 스탯은 변경하지 않는다.</summary>
    public sealed class HatchResultStyling:MonoBehaviour
    {
        public Image background,aura;
        public Text resultText;
        HatchGradeStyle style;
        bool configured;
        public void Apply(PetDTO pet)
        {
            if(pet==null)return;
            style=HatchPresentationPolicy.Style(pet.rarity);configured=true;
            Color color=new Color32(style.R,style.G,style.B,255);
            if(resultText){resultText.supportRichText=false;resultText.color=color;}
            if(background)background.color=new Color(color.r*.12f,color.g*.12f,color.b*.12f,.96f);
            if(aura){aura.raycastTarget=false;aura.rectTransform.localRotation=Quaternion.identity;UpdateAura();}
        }
        void Update()
        {
            if(!configured||!aura)return;
            aura.rectTransform.Rotate(0,0,style.RotationDegreesPerSecond*Time.unscaledDeltaTime);
            UpdateAura();
        }
        void UpdateAura()
        {
            if(!aura)return;
            float wave=.85f+.15f*Mathf.Sin(Time.unscaledTime*2.4f);
            aura.color=new Color(style.R/255f,style.G/255f,style.B/255f,style.AuraAlpha*wave);
        }
    }
}
