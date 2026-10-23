using UnityEngine;
using UnityEngine.UIElements;
namespace Mongle.Gameplay
{
    /// <summary>추가 UGUI 패키지 없이 UI Toolkit으로 조이스틱과 돌봄/공격 버튼을 구성한다.</summary>
    [RequireComponent(typeof(UIDocument),typeof(MobilePetJoystick))]
    public sealed class PetGameplayHud:MonoBehaviour
    {
        public PetRuntimeManager data;
        public PetAttackController attack;
        public MobilePetJoystick joystick;
        Label stats,message;
        VisualElement layout;
        void OnEnable()
        {
            if(!joystick)joystick=GetComponent<MobilePetJoystick>();
            var root=GetComponent<UIDocument>().rootVisualElement;root.Clear();root.pickingMode=PickingMode.Ignore;
            layout=new VisualElement{pickingMode=PickingMode.Ignore};layout.style.flexGrow=1;root.Add(layout);
            var top=new VisualElement();top.style.position=Position.Absolute;top.style.left=24;top.style.right=24;top.style.top=22;top.style.paddingLeft=16;top.style.paddingRight=16;top.style.paddingTop=12;top.style.paddingBottom=12;top.style.backgroundColor=new Color(.06f,.10f,.13f,.88f);layout.Add(top);
            stats=new Label();stats.style.color=Color.white;stats.style.fontSize=18;top.Add(stats);
            message=new Label("Server-confirmed care and exploration rewards.");message.style.color=new Color(.7f,.84f,.78f);message.style.marginTop=6;top.Add(message);
            var care=new VisualElement();care.style.flexDirection=FlexDirection.Row;care.style.flexWrap=Wrap.Wrap;care.style.marginTop=10;top.Add(care);
            Add(care,"Food (5c)",()=>{message.text=data.BuyFood()?"Food +1":"Server food-shop command required.";Refresh();});
            Add(care,"Feed",()=>Care(CareAction.Feed));Add(care,"Pet",()=>Care(CareAction.Pet));Add(care,"Play",()=>Care(CareAction.Play));
            Add(care,"Claim idle",()=>{data.ClaimIdle();message.text="Check server-confirmed balance after the request.";Refresh();});
            var pad=new VisualElement{name="joystick-pad"};pad.style.position=Position.Absolute;pad.style.left=32;pad.style.bottom=28;pad.style.width=160;pad.style.height=160;
            pad.style.backgroundColor=new Color(.15f,.25f,.27f,.75f);Round(pad,80);layout.Add(pad);
            var handle=new VisualElement{name="joystick-knob",pickingMode=PickingMode.Ignore};handle.style.position=Position.Absolute;handle.style.left=52;handle.style.top=52;handle.style.width=56;handle.style.height=56;handle.style.backgroundColor=new Color(.68f,.91f,.82f);Round(handle,28);pad.Add(handle);joystick.Bind(pad,handle);
            var attackButton=new Button(()=>{if(!attack.RequestAttack())message.text="Attack unavailable: action, cooldown or fatigue.";else message.text="Attack queued; animation events control the hit window.";}){text="ATTACK"};
            attackButton.style.position=Position.Absolute;attackButton.style.right=36;attackButton.style.bottom=52;attackButton.style.width=140;attackButton.style.height=85;attackButton.style.fontSize=20;layout.Add(attackButton);
            if(data)data.Changed+=Refresh;Refresh();
        }
        void Add(VisualElement row,string text,System.Action callback){var b=new Button(callback){text=text};b.style.minWidth=110;b.style.height=38;b.style.marginRight=8;row.Add(b);}
        static void Round(VisualElement el,float r){el.style.borderTopLeftRadius=r;el.style.borderTopRightRadius=r;el.style.borderBottomLeftRadius=r;el.style.borderBottomRightRadius=r;}
        void Care(CareAction action){message.text=data.TryCare(action)?action+" requested; awaiting server confirmation":"Unavailable: cooldown, food, fatigue or action in progress.";Refresh();}
        void Refresh()
        {
            if(!data||data.State==null||stats==null)return;var s=data.State;var c=data.Stats;
            stats.text="Lv."+s.level+"  Hunger "+s.hunger.ToString("F0")+"  Bond "+s.affection.ToString("F0")+"  Fatigue "+s.fatigue.ToString("F0")+"  ATK "+c.attack.ToString("F1")+"  Coins "+s.coins+"  Food "+s.food;
        }
        void OnDisable(){if(data)data.Changed-=Refresh;if(joystick)joystick.ResetInput();}
    }
}
