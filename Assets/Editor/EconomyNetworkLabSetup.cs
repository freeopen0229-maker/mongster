#if UNITY_EDITOR
using System;
using System.IO;
using FusionTamagotchi.Client;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FusionTamagotchi.EditorTools
{
    /// <summary>연동 테스트 Scene/UGUI/알 Animator 생성. 실제 Unity에서 아직 실행하지 않은 소스.</summary>
    public static class EconomyNetworkLabSetup
    {
        static Font font;
        static RectTransform Rect(string name,Transform parent,float x,float y,float w,float h)
        {
            var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();
            r.SetParent(parent,false);r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);r.anchoredPosition=new Vector2(x,y);r.sizeDelta=new Vector2(w,h);return r;
        }
        static Text Label(string name,Transform parent,string value,float x,float y,float w=900,float h=60,int size=30)
        {
            var t=Rect(name,parent,x,y,w,h).gameObject.AddComponent<Text>();t.font=font;t.text=value;t.fontSize=size;
            t.alignment=TextAnchor.MiddleCenter;t.color=new Color(.9f,.92f,1);t.raycastTarget=false;t.supportRichText=false;return t;
        }
        static Button Button(string name,Transform parent,string value,float x,float y,float width=430)
        {
            var r=Rect(name,parent,x,y,width,85);var image=r.gameObject.AddComponent<Image>();image.color=new Color(.24f,.25f,.42f);
            var b=r.gameObject.AddComponent<Button>();b.targetGraphic=image;Label("Label",r,value,0,0,width-20,80,27);return b;
        }
        static InputField Input(string name,Transform parent,string value,float y,bool password=false)
        {
            var r=Rect(name,parent,0,y,940,72);var image=r.gameObject.AddComponent<Image>();image.color=new Color(.14f,.16f,.23f);
            var t=Label("Text",r,"",0,0,900,66,25);t.alignment=TextAnchor.MiddleLeft;
            var input=r.gameObject.AddComponent<InputField>();input.targetGraphic=image;input.textComponent=t;
            if(password)input.contentType=InputField.ContentType.Password;input.text=value;return input;
        }
        static void Curve(AnimationClip clip,string path,Type type,string property,params Keyframe[] keys)
        {AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(path,type,property),new AnimationCurve(keys));}
        static AnimationClip Clip(string root,string name,float[] x,float[] y,float[] z,float[] alpha)
        {
            var c=new AnimationClip{name=name,frameRate=60};
            string[] properties={"m_LocalScale.x","m_LocalScale.y","localEulerAnglesRaw.z"};var values=new[]{x,y,z};
            for(int i=0;i<3;i++)
            {
                var keys=new Keyframe[values[i].Length];for(int k=0;k<keys.Length;k++)keys[k]=new Keyframe(k*.18f,values[i][k]);
                Curve(c,"Shell",typeof(RectTransform),properties[i],keys);
            }
            var a=new Keyframe[alpha.Length];for(int k=0;k<a.Length;k++)a[k]=new Keyframe(k*.18f,alpha[k]);
            Curve(c,"Shell",typeof(CanvasGroup),"m_Alpha",a);
            float first=name=="Reveal"?1:0,last=name=="Crack"?1:0;
            Curve(c,"Shell/CrackMarks",typeof(CanvasGroup),"m_Alpha",new Keyframe(0,first),new Keyframe(.3f,last));
            AssetDatabase.CreateAsset(c,root+"/"+name+".anim");return c;
        }
        static Sprite AuraSprite(string root)
        {
            var t=new Texture2D(128,128,TextureFormat.RGBA32,false){name="UI aura"};var pixels=new Color[128*128];
            for(int y=0;y<128;y++)for(int x=0;x<128;x++)
            {
                float dx=(x-63.5f)/63.5f,dy=(y-63.5f)/63.5f,r=Mathf.Sqrt(dx*dx+dy*dy);
                float a=Mathf.Pow(Mathf.Clamp01(1-r),1.6f);pixels[y*128+x]=new Color(1,1,1,a);
            }
            t.SetPixels(pixels);t.Apply();AssetDatabase.CreateAsset(t,root+"/AuraTexture.asset");
            var sprite=Sprite.Create(t,new UnityEngine.Rect(0,0,128,128),new Vector2(.5f,.5f),100);
            AssetDatabase.CreateAsset(sprite,root+"/AuraSprite.asset");return sprite;
        }
        static Sprite EggSprite(string root)
        {
            var texture=new Texture2D(128,160,TextureFormat.RGBA32,false){name="Test egg icon"};
            var pixels=new Color[128*160];
            for(int y=0;y<160;y++)for(int x=0;x<128;x++)
            {
                float ny=(y-76)/69f,nx=(x-64)/(44*(1-.16f*ny)),d=nx*nx+ny*ny;
                float alpha=Mathf.Clamp01((1-d)*30);float shade=Mathf.Clamp01(.9f-.12f*nx+.06f*ny);
                pixels[y*128+x]=new Color(shade*.9f,shade*.93f,shade,alpha);
            }
            texture.SetPixels(pixels);texture.Apply();AssetDatabase.CreateAsset(texture,root+"/EggTexture.asset");
            var sprite=Sprite.Create(texture,new UnityEngine.Rect(0,0,128,160),new Vector2(.5f,.5f),100);
            sprite.name="Test sealed egg";AssetDatabase.CreateAsset(sprite,root+"/EggSprite.asset");return sprite;
        }
        [MenuItem("Mongle/Economy/Create HTTP Hatch Lab")]
        public static void Create()
        {
            if(!Application.isBatchMode&&!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            string root=AssetDatabase.GenerateUniqueAssetPath("Assets/EconomyNetworkLab");AssetDatabase.CreateFolder("Assets",Path.GetFileName(root));
            font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var systems=new GameObject("ClientSystems");var manager=systems.AddComponent<ClientGameManager>();
            var gateway=systems.AddComponent<HttpEconomyGateway>();manager.gateway=gateway;
            var controls=systems.AddComponent<EconomyPlaygroundControls>();controls.manager=manager;controls.gateway=gateway;
            new GameObject("EventSystem",typeof(EventSystem),typeof(StandaloneInputModule));
            var canvas=new GameObject("EconomyCanvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=canvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1080,1920);scaler.matchWidthOrHeight=.5f;
            var parent=canvas.transform;
            var bg=Rect("Background",parent,0,0,1080,1920).gameObject.AddComponent<Image>();bg.color=new Color(.06f,.07f,.12f);bg.raycastTarget=false;
            Label("Title",parent,"알 상점 · 서버 연동 테스트",0,850,940,80,40);
            manager.txtJeon=Label("Jeon",parent,"— 전",-340,760,330,70,27);
            manager.txtGem=Label("Gem",parent,"— 보석",0,760,330,70,27);manager.txtThread=Label("Thread",parent,"— 실타래",340,760,330,70,27);
            controls.endpoint=Input("Endpoint",parent,"https://localhost:8443",650);
            controls.account=Input("Account",parent,"local-a",550);
            controls.accessToken=Input("Token",parent,"",450,true);controls.assetId=Input("Asset",parent,"local-neulgui",350);
            var connect=Button("Connect",parent,"서버 연결",-240,240);UnityEventTools.AddPersistentListener(connect.onClick,controls.Connect);
            var select=Button("Select",parent,"자산 조회",240,240);UnityEventTools.AddPersistentListener(select.onClick,controls.SelectAsset);
            var icon=Rect("EggAnimation",parent,0,80,130,160);var animator=icon.gameObject.AddComponent<Animator>();
            var shell=Rect("Shell",icon,0,0,130,160);shell.gameObject.AddComponent<Image>().sprite=EggSprite(root);shell.gameObject.AddComponent<CanvasGroup>();
            var marks=Rect("CrackMarks",shell,0,0,80,50);marks.gameObject.AddComponent<CanvasGroup>().alpha=0;
            for(int i=0;i<3;i++)
            {
                var mark=Rect("Crack"+i,marks,-22+i*22,(i%2==0?0:6),28,4);
                mark.localRotation=Quaternion.Euler(0,0,i%2==0?28:-28);
                mark.gameObject.AddComponent<Image>().color=new Color(.15f,.18f,.29f);
            }
            var controller=AnimatorController.CreateAnimatorControllerAtPath(root+"/Egg.controller");var machine=controller.layers[0].stateMachine;
            var closed=machine.AddState("Closed");closed.motion=Clip(root,"Closed",new[]{1f,1f},new[]{1f,1f},new[]{0f,0f},new[]{1f,1f});machine.defaultState=closed;
            machine.AddState("Crack").motion=Clip(root,"Crack",new[]{1f,1.07f,.95f,1.03f,1f},new[]{1f,.96f,1.04f,.98f,1f},new[]{0f,-8f,7f,-4f,0f},new[]{1f,1f,1f,1f,1f});
            machine.AddState("Reveal").motion=Clip(root,"Reveal",new[]{1f,1.2f,0f},new[]{1f,1.2f,0f},new[]{0f,0f,0f},new[]{1f,.8f,0f});animator.runtimeAnimatorController=controller;
            var one=Button("BuyOne",parent,"영롱한 알 · 30 보석",-240,-100);UnityEventTools.AddBoolPersistentListener(one.onClick,manager.BuyGloriousEgg,false);
            var many=Button("BuyEleven",parent,"10+1 · 300 보석",240,-100);UnityEventTools.AddBoolPersistentListener(many.onClick,manager.BuyGloriousEgg,true);
            var start=Button("Start",parent,"부화 시작",-240,-220);UnityEventTools.AddPersistentListener(start.onClick,manager.StartSelectedEgg);
            var scroll=Button("Scroll",parent,"스크롤 사용",240,-220);UnityEventTools.AddPersistentListener(scroll.onClick,manager.UseSelectedHatchScroll);
            var hatch=Button("Hatch",parent,"부화 확인",-240,-340);UnityEventTools.AddPersistentListener(hatch.onClick,manager.HatchSelectedEgg);
            var reroll=Button("Reroll",parent,"선택 펫 · 스킬 1 리롤",240,-340);UnityEventTools.AddIntPersistentListener(reroll.onClick,manager.RerollSelectedSlot,0);
            var retry=Button("Retry",parent,"미확정 요청 다시 확인",0,-460,940);UnityEventTools.AddPersistentListener(retry.onClick,manager.RetryPending);
            manager.actionButtons=new[]{one,many,start,scroll,hatch,reroll};manager.retryButton=retry;
            var presenter=systems.AddComponent<EggHatchPresenter>();presenter.manager=manager;presenter.eggAnimator=animator;
            presenter.audioSource=systems.AddComponent<AudioSource>();presenter.audioSource.playOnAwake=false;systems.AddComponent<AudioListener>();
            presenter.purchaseText=Label("PurchaseStatus",parent,"구매 → 알 지급 → 부화 대기",0,-570,940,80,27);
            presenter.resultPanel=Rect("ResultPanel",parent,0,-690,940,140).gameObject;
            var background=presenter.resultPanel.AddComponent<Image>();background.raycastTarget=false;
            var aura=Rect("GradeAura",presenter.resultPanel.transform,0,0,950,270).gameObject.AddComponent<Image>();aura.sprite=AuraSprite(root);aura.raycastTarget=false;
            presenter.resultText=Label("ResultText",presenter.resultPanel.transform,"",0,0,900,140,28);
            var styling=presenter.resultPanel.AddComponent<HatchResultStyling>();styling.background=background;styling.aura=aura;styling.resultText=presenter.resultText;presenter.gradeStyling=styling;
            presenter.resultPanel.SetActive(false);
            manager.txtPetSerial=Label("Serial",parent,"",0,-790,940,50,23);manager.txtPetSerial.raycastTarget=true;
            var tooltipPanel=Rect("SerialTooltip",parent,0,-710,700,120).gameObject;
            tooltipPanel.AddComponent<Image>().color=new Color(.1f,.12f,.2f,.98f);
            tooltipPanel.GetComponent<Image>().raycastTarget=false;
            var tooltip=manager.txtPetSerial.gameObject.AddComponent<PetSerialTooltip>();tooltip.panel=tooltipPanel;
            tooltip.tooltipText=Label("TooltipText",tooltipPanel.transform,"",0,0,680,110,24);manager.serialTooltip=tooltip;tooltipPanel.SetActive(false);
            manager.globalNoticePanel=Rect("GlobalNotice",parent,0,-850,980,80).gameObject;
            manager.txtGlobalNotice=Label("NoticeText",manager.globalNoticePanel.transform,"",0,0,960,80,23);manager.globalNoticePanel.SetActive(false);
            manager.txtStatus=controls.status=Label("Status",parent,"인증서·서버 주소·세션 연결 필요",0,-925,980,50,21);
            EditorSceneManager.SaveScene(scene,root+"/HTTPHatchLab.unity");AssetDatabase.SaveAssets();Selection.activeGameObject=systems;
            Debug.Log("HTTP Hatch Lab generated. Requires real HTTPS endpoint and session; native Play Mode remains to be verified.");
        }
    }
}
#endif
