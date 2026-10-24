using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Mongle.OfflinePreview;

namespace FusionTamagotchi.Client
{
    /// <summary>Explicit development-only playable sandbox, entirely separate from online accounts and gateways.</summary>
    public sealed class OfflineGamePreview:MonoBehaviour
    {
        const string SaveKey="Mongster.OfflinePreview.v1";
        readonly List<GameObject> owned=new List<GameObject>();
        readonly List<Enemy> enemies=new List<Enemy>();
        readonly List<Particle> particles=new List<Particle>();
        readonly List<UnityEngine.Object> assets=new List<UnityEngine.Object>();
        sealed class Enemy{public Transform root;public SpriteRenderer body;public float hp;public float speed;public float attackAt;}
        sealed class Particle{public Transform root;public Vector3 velocity;public float life;public SpriteRenderer sprite;}
        PreviewSave save;Camera viewCamera;Transform pet,room,field;Transform[] ears;
        Material material;Sprite disc;Canvas canvas;RectTransform safe,content;GameObject homeUI,battleUI,resultUI;
        Text summary,condition,message,battleStats,battleTitle,resultText;Image hpFill;PreviewJoystick stick;
        Button attackButton,dashButton,stageButton;Button[] careButtons;int stage=1,defeated,total;bool fighting,settled;
        float health,maxHealth,nextAttack,nextDamage,dashUntil,nextDash,careAt,elapsed,saveAt,flashUntil;Vector2 facing=Vector2.up;
        Color cream=new Color(.94f,.96f,.88f),teal=new Color(.18f,.56f,.49f),gold=new Color(1,.8f,.34f);
        void Awake()
        {
            Application.targetFrameRate=60;
            try{save=JsonUtility.FromJson<PreviewSave>(PlayerPrefs.GetString(SaveKey,""));}catch(Exception){save=null;}
            if(save==null||!save.Valid())save=new PreviewSave();
            material=Resources.Load<Material>("MainGame/PetSprite");
            if(!material){Debug.LogError("Preview sprite material missing.");enabled=false;return;}
            disc=MakeDisc();
            viewCamera=Own(new GameObject("Preview Camera")).AddComponent<Camera>();viewCamera.tag="MainCamera";
            viewCamera.orthographic=true;viewCamera.orthographicSize=7.7f;viewCamera.transform.position=new Vector3(0,0,-10);viewCamera.clearFlags=CameraClearFlags.SolidColor;viewCamera.backgroundColor=new Color(.06f,.13f,.17f);
            viewCamera.gameObject.AddComponent<AudioListener>();
            room=Own(new GameObject("Pet Garden")).transform;field=Own(new GameObject("Exploration Field")).transform;
            BuildGarden();BuildField();pet=BuildPet();
            canvas=Own(new GameObject("Offline Game UI",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster))).GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=canvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1080,1920);scaler.matchWidthOrHeight=.5f;
            safe=Panel(canvas.transform,"Safe Area");ApplySafeArea();
            content=Box(safe,"Portrait content",0,0,1080,1920);
            Own(new GameObject("Preview EventSystem",typeof(EventSystem),typeof(StandaloneInputModule)));
            BuildHome();BuildBattle();BuildResult();GoHome("늘귀와 함께 하루를 시작해요. 먹이를 주거나 숲으로 떠나 보세요.");
        }
        GameObject Own(GameObject g){owned.Add(g);return g;}
        void ApplySafeArea()
        {
            var a=Screen.safeArea;safe.anchorMin=new Vector2(a.x/Screen.width,a.y/Screen.height);safe.anchorMax=new Vector2(a.xMax/Screen.width,a.yMax/Screen.height);safe.offsetMin=safe.offsetMax=Vector2.zero;
        }
        Sprite MakeDisc()
        {
            var texture=new Texture2D(64,64,TextureFormat.RGBA32,false);var colors=new Color[4096];
            for(int y=0;y<64;y++)for(int x=0;x<64;x++){float d=Vector2.Distance(new Vector2(x,y),new Vector2(31.5f,31.5f));colors[y*64+x]=new Color(1,1,1,Mathf.Clamp01(31-d));}
            texture.SetPixels(colors);texture.Apply();assets.Add(texture);var sprite=Sprite.Create(texture,new UnityEngine.Rect(0,0,64,64),new Vector2(.5f,.5f),64);assets.Add(sprite);return sprite;
        }
        SpriteRenderer Dot(Transform parent,string name,Vector3 pos,Vector3 size,Color color,int order=0)
        {
            var g=new GameObject(name);g.transform.SetParent(parent,false);g.transform.localPosition=pos;g.transform.localScale=size;var s=g.AddComponent<SpriteRenderer>();s.sprite=disc;s.sharedMaterial=material;s.color=color;s.sortingOrder=order;return s;
        }
        void BuildGarden()
        {
            Dot(room,"Garden island",new Vector3(0,.4f,0),new Vector3(7,4,1),new Color(.19f,.36f,.29f));
            Dot(room,"Garden lawn",new Vector3(0,.65f,0),new Vector3(6.4f,3.4f,1),new Color(.27f,.47f,.34f),1);
            Dot(room,"Warm path",new Vector3(0,-.6f,0),new Vector3(4,1.2f,1),new Color(.49f,.49f,.31f),2);
            var random=new System.Random(41);
            for(int i=0;i<25;i++)
            {
                float x=(float)(random.NextDouble()*6-3),y=(float)(random.NextDouble()*2.4-.4);
                if(Mathf.Abs(x)<1.3f)continue;
                Dot(room,"Garden flower",new Vector3(x,y,0),Vector3.one*.13f,i%2==0?new Color(.95f,.73f,.42f):new Color(.69f,.82f,.88f),3);
            }
            Dot(room,"Pet shadow",new Vector3(0,-.2f,0),new Vector3(1.9f,.32f,1),new Color(.08f,.19f,.15f,.6f),4);
        }
        void BuildField()
        {
            Dot(field,"Forest floor",Vector3.zero,new Vector3(10,18,1),new Color(.12f,.26f,.25f),-8);
            var random=new System.Random(8);
            for(int i=0;i<48;i++)
            {
                float x=(float)(random.NextDouble()*7-3.5),y=(float)(random.NextDouble()*12-6);
                Dot(field,"Grass",new Vector3(x,y,0),new Vector3(.1f,.35f,1),new Color(.2f,.39f,.3f),-7);
            }
            for(int i=0;i<12;i++)
            {
                float x=i%2==0?-3.7f:3.7f,y=-5.3f+(i/2)*2.1f;
                Dot(field,"Forest tree",new Vector3(x,y,0),new Vector3(1.5f,1.6f,1),new Color(.1f,.33f,.25f),-6);
                Dot(field,"Tree highlight",new Vector3(x-.1f,y+.2f,0),new Vector3(.85f,.8f,1),new Color(.16f,.43f,.29f),-5);
            }
        }
        Transform BuildPet()
        {
            var root=Own(new GameObject("Neulgui Preview Pet")).transform;var earList=new List<Transform>();
            string[] names={"tail","earFar","pawFar","armFar","body","head","pawNear","armNear","earNear"};
            for(int i=0;i<names.Length;i++)
            {
                var texture=Resources.Load<Texture2D>("MainGame/"+names[i]);if(!texture)continue;
                var sprite=Sprite.Create(texture,new UnityEngine.Rect(0,0,texture.width,texture.height),new Vector2(.5f,.5f),100);assets.Add(sprite);
                var g=new GameObject(names[i]);g.transform.SetParent(root,false);var s=g.AddComponent<SpriteRenderer>();s.sprite=sprite;s.sharedMaterial=material;s.sortingOrder=10+i;
                if(names[i].StartsWith("ear"))earList.Add(g.transform);
            }
            ears=earList.ToArray();return root;
        }
        RectTransform Panel(Transform parent,string name)
        {var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;return r;}
        RectTransform Box(Transform parent,string name,float x,float y,float w,float h)
        {var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);r.anchoredPosition=new Vector2(x,y);r.sizeDelta=new Vector2(w,h);return r;}
        Text Label(Transform parent,string text,float x,float y,float w,float h,int size,Color? color=null)
        {var t=Box(parent,text,x,y,w,h).gameObject.AddComponent<Text>();t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.text=text;t.fontSize=size;t.alignment=TextAnchor.MiddleCenter;t.color=color??cream;t.raycastTarget=false;return t;}
        Button Button(Transform parent,string text,float x,float y,float w,float h,UnityEngine.Events.UnityAction action,Color? color=null)
        {var r=Box(parent,text,x,y,w,h);var image=r.gameObject.AddComponent<Image>();image.color=color??new Color(.16f,.32f,.32f);var b=r.gameObject.AddComponent<Button>();b.onClick.AddListener(action);Label(r,text,0,0,w-20,h-8,29);return b;}
        void Header(Transform parent,string title,string subtitle)
        {Label(parent,"M O N G S T E R",0,820,950,55,27,gold);Label(parent,title,0,735,980,105,55);Label(parent,subtitle,0,620,970,90,25,new Color(.66f,.83f,.78f));}
        void BuildHome()
        {
            homeUI=Panel(content,"Pet Home").gameObject;
            Header(homeUI.transform,"늘귀의 작은 하루","개발용 오프라인 체험 · 온라인 계정과 별도 저장");
            summary=Label(homeUI.transform,"",0,500,970,105,32);
            condition=Label(homeUI.transform,"",0,-45,980,105,29);
            var list=new List<Button>();string[] names={"먹이 주기","교감하기","씻겨 주기","놀아주기","휴식하기","먹이 구매 · 5전"};
            for(int i=0;i<names.Length;i++){int action=i;list.Add(Button(homeUI.transform,names[i],i%2==0?-250:250,-200-(i/2)*125,460,100,()=>Care(action)));}
            careButtons=list.ToArray();
            stageButton=Button(homeUI.transform,"",0,-610,960,115,StartBattle,teal);
            Button(homeUI.transform,"탐험 지역 바꾸기",0,-745,960,90,()=>{stage=stage%(Math.Min(3,save.bestStage+1))+1;Refresh();});
            message=Label(homeUI.transform,"",0,-875,970,145,25);
        }
        void BuildBattle()
        {
            battleUI=Panel(content,"Exploration HUD").gameObject;
            battleTitle=Label(battleUI.transform,"",0,800,980,85,36,gold);
            battleStats=Label(battleUI.transform,"",0,700,980,90,29);
            var bar=Box(battleUI.transform,"Health background",0,605,900,32).gameObject.AddComponent<Image>();bar.color=new Color(.1f,.14f,.18f);
            hpFill=Box(bar.transform,"Health",0,0,900,32).gameObject.AddComponent<Image>();hpFill.sprite=disc;hpFill.color=teal;hpFill.type=Image.Type.Filled;hpFill.fillMethod=Image.FillMethod.Horizontal;hpFill.fillOrigin=0;
            var pad=Box(battleUI.transform,"Joystick",-325,-650,230,230);var padImage=pad.gameObject.AddComponent<Image>();padImage.sprite=disc;padImage.color=new Color(.24f,.48f,.44f,.8f);
            stick=pad.gameObject.AddComponent<PreviewJoystick>();stick.knob=Box(pad,"Knob",0,0,100,100);var knob=stick.knob.gameObject.AddComponent<Image>();knob.sprite=disc;knob.color=new Color(.72f,.92f,.8f);knob.raycastTarget=false;
            Label(battleUI.transform,"이동",-325,-810,280,50,26);
            attackButton=Button(battleUI.transform,"공격",310,-605,250,165,Attack,teal);
            dashButton=Button(battleUI.transform,"회피",310,-790,250,120,Dash);
            Button(battleUI.transform,"돌아가기",0,-900,300,75,()=>GoHome("탐험을 마치고 집으로 돌아왔어요."));
            Label(battleUI.transform,"조이스틱 이동 · 가까이서 공격 · 회피 중 무적",0,500,980,65,24);
        }
        void BuildResult()
        {
            resultUI=Panel(content,"Exploration Result").gameObject;
            var shade=resultUI.AddComponent<Image>();shade.color=new Color(.03f,.08f,.1f,.94f);
            Label(resultUI.transform,"탐험 기록",0,400,900,110,52,gold);
            resultText=Label(resultUI.transform,"",0,80,930,430,36);
            Button(resultUI.transform,"집으로 돌아가기",0,-340,800,120,()=>GoHome("수고했어요! 먹이와 휴식으로 다음 탐험을 준비해요."),teal);
        }
        string StageName()=>stage==1?"햇살 숲":stage==2?"반딧불 습지":"달빛 언덕";
        void Refresh()
        {
            if(summary)summary.text=$"Lv.{save.level} 늘귀  ·  경험치 {save.xp}/{save.level*30}\n전 {save.coins}  ·  먹이 {save.food}  ·  탐험 성공 {save.wins}회";
            if(condition)condition.text=$"포만감 {save.hunger:F0}  친밀도 {save.bond:F0}\n청결 {save.clean:F0}  활력 {save.energy:F0}";
            if(stageButton)stageButton.GetComponentInChildren<Text>().text=$"{StageName()} 탐험하기 · 활력 10";
            foreach(var b in careButtons??Array.Empty<Button>())b.interactable=Time.unscaledTime>=careAt;
        }
        void Care(int action)
        {
            if(fighting||Time.unscaledTime<careAt)return;
            bool ok=action==5?PreviewRules.BuyFood(save):PreviewRules.Care(save,action);
            if(!ok){message.text=action==0?"배가 부르거나 먹이가 부족해요.":action==5?"먹이를 사려면 5전이 필요해요.":"조금 쉬고 다시 해볼까요?";return;}
            careAt=Time.unscaledTime+1.3f;string[] lines={"냠냠! 포만감과 친밀도가 올랐어요.","늘귀가 행복해해요!","보송보송! 깨끗해졌어요.","함께 놀며 경험치를 얻었어요.","활력이 회복됐어요.","먹이 1개를 샀어요."};message.text=lines[action];Burst(pet.position,new Color(1,.79f,.36f),12);Save();Refresh();
        }
        void StartBattle()
        {
            if(!PreviewRules.Begin(save,stage)){message.text="활력이 부족해요. 먼저 휴식해 주세요.";return;}
            ClearEnemies();ClearParticles();fighting=true;settled=false;defeated=0;total=4+stage*2;
            homeUI.SetActive(false);battleUI.SetActive(true);resultUI.SetActive(false);room.gameObject.SetActive(false);field.gameObject.SetActive(true);
            pet.position=new Vector3(0,-3,0);pet.localScale=Vector3.one*.32f;pet.rotation=Quaternion.identity;
            maxHealth=85+save.level*12+(float)save.bond*.2f;health=maxHealth;nextAttack=nextDash=nextDamage=0;dashUntil=0;
            for(int i=0;i<total;i++)
            {
                float angle=i*Mathf.PI*2/total;var root=new GameObject("Forest Slime").transform;root.SetParent(field,false);root.position=new Vector3(Mathf.Cos(angle)*2.4f,1+Mathf.Sin(angle)*2.5f,0);
                Color tint=stage==1?new Color(.51f,.81f,.53f):stage==2?new Color(.45f,.66f,.9f):new Color(.84f,.58f,.87f);
                var body=Dot(root,"Slime",Vector3.zero,new Vector3(.67f,.58f,1),tint,5);
                Dot(root,"Eye left",new Vector3(-.13f,.06f,0),Vector3.one*.11f,Color.white,6);Dot(root,"Eye right",new Vector3(.13f,.06f,0),Vector3.one*.11f,Color.white,6);
                Dot(root,"Pupil left",new Vector3(-.12f,.05f,0),Vector3.one*.05f,new Color(.08f,.15f,.18f),7);Dot(root,"Pupil right",new Vector3(.14f,.05f,0),Vector3.one*.05f,new Color(.08f,.15f,.18f),7);
                enemies.Add(new Enemy{root=root,body=body,hp=22+stage*8,speed=.45f+stage*.12f,attackAt=0});
            }
            battleTitle.text=StageName()+" · 오프라인 탐험";Save();
        }
        void Attack()
        {
            if(!fighting||settled||Time.time<nextAttack)return;nextAttack=Time.time+.43f;
            Vector3 origin=pet.position+(Vector3)facing*.3f;Burst(origin,gold,8);bool hit=false;
            for(int i=enemies.Count-1;i>=0;i--)
            {
                var enemy=enemies[i];if(Vector2.Distance(origin,enemy.root.position)>1.2f)continue;
                hit=true;enemy.hp-=14+save.level*2.5f+(float)save.bond*.04f;enemy.root.position+=(enemy.root.position-pet.position).normalized*.3f;Burst(enemy.root.position,Color.white,5);
                if(enemy.hp<=0){Destroy(enemy.root.gameObject);enemies.RemoveAt(i);defeated++;}
            }
            if(hit)flashUntil=Time.time+.12f;
            if(enemies.Count==0)Complete(true);
        }
        void Dash()
        {if(!fighting||settled||Time.time<nextDash)return;dashUntil=Time.time+.24f;nextDash=Time.time+1.7f;Burst(pet.position,new Color(.55f,.9f,.95f),7);}
        void Complete(bool won)
        {
            if(settled)return;settled=true;fighting=false;
            if(won){PreviewRules.Victory(save,stage);resultText.text=$"{StageName()} 탐험 성공!\n\n처치 {defeated}/{total}\n체험 전 +{15+stage*5}\n경험치 +{20+stage*10}\n\n늘귀 Lv.{save.level}";}
            else resultText.text="늘귀가 지쳤어요.\n\n집에서 돌보고 다시 도전해요.\n기존 체험 기록은 유지돼요.";
            Save();battleUI.SetActive(false);resultUI.SetActive(true);
        }
        void GoHome(string line)
        {
            fighting=false;settled=false;ClearEnemies();ClearParticles();room.gameObject.SetActive(true);field.gameObject.SetActive(false);
            homeUI.SetActive(true);battleUI.SetActive(false);resultUI.SetActive(false);pet.position=new Vector3(0,1.5f,0);pet.localScale=Vector3.one*.95f;pet.rotation=Quaternion.identity;
            message.text=line;Save();Refresh();
        }
        void Update()
        {
            if(save==null||!pet)return;
            content.localScale=Vector3.one*Mathf.Min(safe.rect.width/1080f,safe.rect.height/1920f);
            float dt=Mathf.Min(Time.deltaTime,.05f);elapsed+=dt;PreviewRules.Tick(save,dt);
            if(fighting&&!settled)
            {
                Vector2 movement=stick?stick.Value:Vector2.zero;
                movement+=new Vector2(Input.GetAxisRaw("Horizontal"),Input.GetAxisRaw("Vertical"));movement=Vector2.ClampMagnitude(movement,1);
                if(movement.sqrMagnitude>.02f)facing=movement.normalized;
                bool dashing=Time.time<dashUntil;Vector2 direction=dashing?facing:movement;
                Vector3 p=pet.position+(Vector3)direction*dt*(dashing?10:2.6f);
                pet.position=new Vector3(Mathf.Clamp(p.x,-3,3),Mathf.Clamp(p.y,-4.4f,3.6f),0);
                pet.rotation=Quaternion.Euler(0,0,-movement.x*8+Mathf.Sin(elapsed*12)*movement.magnitude*3);
                foreach(var e in enemies)
                {
                    Vector3 delta=pet.position-e.root.position;float distance=delta.magnitude;
                    if(distance>.48f)e.root.position+=delta.normalized*e.speed*dt;
                    if(distance<.68f&&!dashing&&Time.time>=nextDamage){health-=7+stage*2;nextDamage=Time.time+.65f;Burst(pet.position,new Color(1,.45f,.4f),6);}
                }
                if(health<=0)Complete(false);
                hpFill.fillAmount=Mathf.Clamp01(health/maxHealth);battleStats.text=$"체력 {Mathf.CeilToInt(Mathf.Max(0,health))}/{Mathf.CeilToInt(maxHealth)}  ·  처치 {defeated}/{total}";
                attackButton.interactable=Time.time>=nextAttack;dashButton.interactable=Time.time>=nextDash;
                if(Input.GetKeyDown(KeyCode.Space))Attack();if(Input.GetKeyDown(KeyCode.LeftShift))Dash();
            }
            else if(homeUI.activeSelf)
            {
                pet.position=new Vector3(0,1.5f+Mathf.Sin(elapsed*2)*.04f,0);pet.localScale=Vector3.one*(.95f+Mathf.Sin(elapsed*1.8f)*.009f);Refresh();
            }
            for(int i=0;i<ears.Length;i++)ears[i].localRotation=Quaternion.Euler(0,0,Mathf.Sin(elapsed*2.7f+i)*3);
            for(int i=particles.Count-1;i>=0;i--)
            {
                var p=particles[i];p.life-=dt;if(p.life<=0){Destroy(p.root.gameObject);particles.RemoveAt(i);continue;}
                p.root.position+=p.velocity*dt;p.velocity*=Mathf.Pow(.12f,dt);var color=p.sprite.color;color.a=p.life/.5f;p.sprite.color=color;
            }
            if(Time.unscaledTime>saveAt){saveAt=Time.unscaledTime+10;Save();}
            if(Input.GetKeyDown(KeyCode.Escape)&&!homeUI.activeSelf)GoHome("집으로 돌아왔어요.");
        }
        void Burst(Vector3 position,Color color,int count)
        {
            for(int i=0;i<count&&particles.Count<160;i++)
            {
                float angle=UnityEngine.Random.value*Mathf.PI*2;var sprite=Dot(null,"Spark",position,Vector3.one*UnityEngine.Random.Range(.045f,.1f),color,30);
                particles.Add(new Particle{root=sprite.transform,sprite=sprite,life=.5f,velocity=new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0)*UnityEngine.Random.Range(.8f,2.4f)});
            }
        }
        void ClearEnemies(){foreach(var e in enemies)if(e.root)Destroy(e.root.gameObject);enemies.Clear();}
        void ClearParticles(){foreach(var p in particles)if(p.root)Destroy(p.root.gameObject);particles.Clear();}
        void Save(){if(save!=null&&save.Valid()){PlayerPrefs.SetString(SaveKey,JsonUtility.ToJson(save));PlayerPrefs.Save();}}
        void OnApplicationPause(bool value){if(value)Save();}
        void OnApplicationQuit(){Save();}
        void OnDestroy(){Save();ClearEnemies();ClearParticles();foreach(var g in owned)if(g)Destroy(g);foreach(var a in assets)if(a)Destroy(a);}
    }
}
