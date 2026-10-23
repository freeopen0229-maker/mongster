using System;
using System.IO;
using Mongle;
using Mongle.Gameplay;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>컴포넌트 참조·Animator·UI를 자동 연결하는 기반 시스템 검증 씬. 모델은 기능 검사용 대체 도형이다.</summary>
public static class GameplayFoundationProject
{
    const string Scene="Assets/Scenes/GameplayFoundation.unity";
    static Transform Node(string name,Transform parent,Vector3 position){var t=new GameObject(name).transform;t.SetParent(parent,false);t.localPosition=position;return t;}
    [MenuItem("Mongle/12 Create Gameplay Foundation Scene")]
    public static void Create()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play Mode first.");
        if(!Application.isBatchMode&&!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
        Directory.CreateDirectory("Assets/Gameplay/Clips");Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var camera=new GameObject("Main Camera").AddComponent<Camera>();camera.tag="MainCamera";camera.transform.position=new Vector3(0,6,-7);camera.transform.LookAt(new Vector3(0,.7f,.3f));camera.allowHDR=true;camera.gameObject.AddComponent<AudioListener>();
        VfxPresentation cameraFx=null; // Built-in OnRenderImage bloom is incompatible with the URP 2D renderer.
        var light=new GameObject("Sun").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.2f;light.transform.rotation=Quaternion.Euler(50,-25,0);
        var floor=GameObject.CreatePrimitive(PrimitiveType.Plane);floor.name="Ground";floor.GetComponent<Renderer>().sharedMaterial=Surface("Ground",new Color(.18f,.24f,.22f));
        var services=new GameObject("Pet Data");services.SetActive(false);var online=services.AddComponent<Mongle.Slice.Client.PetRuntimeManager>();var data=services.AddComponent<PetRuntimeManager>();data.server=online;
        var feedback=new GameObject("Combat Feedback").AddComponent<CombatFeedback>();feedback.cameraFx=cameraFx;
        var player=new GameObject("Player");player.transform.position=new Vector3(0,.05f,-1.8f);
        var rb=player.AddComponent<Rigidbody>();rb.mass=1;rb.interpolation=RigidbodyInterpolation.Interpolate;rb.constraints=RigidbodyConstraints.FreezeRotation;
        var collider=player.AddComponent<CapsuleCollider>();collider.height=1.4f;collider.radius=.3f;collider.center=new Vector3(0,.7f,0);
        var motor=player.AddComponent<PetMotor>();var attack=player.AddComponent<PetAttackController>();
        var visual=Node("VisualRoot",player.transform,Vector3.zero);var motion=Node("ModelMotion",visual,Vector3.zero);
        var model=GameObject.CreatePrimitive(PrimitiveType.Capsule);model.name="Functional Test Model";model.transform.SetParent(motion,false);model.transform.localPosition=new Vector3(0,.7f,0);model.transform.localScale=new Vector3(.5f,.7f,.5f);UnityEngine.Object.DestroyImmediate(model.GetComponent<Collider>());model.GetComponent<Renderer>().sharedMaterial=Surface("Pet",new Color(.66f,.83f,.7f));
        var pivot=Node("WeaponPivot",motion,new Vector3(0,.8f,.1f));var tip=Node("WeaponTip",pivot,new Vector3(0,0,1.25f));
        var trail=tip.gameObject.AddComponent<TrailRenderer>();trail.sharedMaterial=TrailMaterial();trail.startWidth=.28f;trail.endWidth=0;trail.time=.18f;trail.minVertexDistance=.02f;trail.emitting=false;trail.startColor=new Color(.3f,2,2,1);trail.endColor=new Color(0,1,1,0);
        var animator=visual.gameObject.AddComponent<Animator>();animator.runtimeAnimatorController=Controller();animator.applyRootMotion=false;
        var bridge=visual.gameObject.AddComponent<PetAnimatorBridge>();bridge.data=data;
        var relay=visual.gameObject.AddComponent<PetAttackAnimationRelay>();relay.attack=attack;relay.bridge=bridge;
        attack.data=data;attack.animationBridge=bridge;attack.visualRoot=visual;attack.hitOrigin=tip;attack.weaponTip=tip;attack.trail=trail;attack.feedback=feedback;
        data.animationBridge=bridge;motor.data=data;motor.animationBridge=bridge;motor.visualRoot=visual;motor.viewCamera=camera;
        var hud=new GameObject("Mobile HUD");var doc=hud.AddComponent<UIDocument>();doc.panelSettings=Panel();
        var joystick=hud.AddComponent<MobilePetJoystick>();var ui=hud.AddComponent<PetGameplayHud>();ui.data=data;ui.attack=attack;ui.joystick=joystick;motor.joystick=joystick;
        for(int i=0;i<3;i++)
        {
            var enemy=GameObject.CreatePrimitive(PrimitiveType.Capsule);enemy.name="Training Target "+i;enemy.layer=8;enemy.transform.position=new Vector3((i-1)*1.8f,.65f,1.8f);enemy.transform.localScale=new Vector3(.5f,.65f,.5f);enemy.GetComponent<Renderer>().sharedMaterial=Surface("Target",new Color(.35f,.4f,.5f));enemy.AddComponent<VfxTarget>();
        }
        services.SetActive(true);
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),Scene);AssetDatabase.SaveAssets();
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(Scene,true)};
        Debug.Log("Gameplay foundation scene configured. Open Play Mode to verify joystick, action markers and care/rewards.");
    }
    static Material Surface(string name,Color color)
    {
        string path="Assets/Gameplay/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!m){m=new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));AssetDatabase.CreateAsset(m,path);}m.shader=Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");m.color=color;EditorUtility.SetDirty(m);return m;
    }
    static Material TrailMaterial()
    {
        string path="Assets/Gameplay/Trail.mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!m){m=new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));AssetDatabase.CreateAsset(m,path);}m.shader=Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");m.mainTexture=Resources.Load<Texture2D>("VFX/trace_01");EditorUtility.SetDirty(m);return m;
    }
    static PanelSettings Panel()
    {
        const string path="Assets/Gameplay/MobilePanel.asset";var panel=AssetDatabase.LoadAssetAtPath<PanelSettings>(path);
        if(!panel){panel=ScriptableObject.CreateInstance<PanelSettings>();AssetDatabase.CreateAsset(panel,path);}
        const string theme="Assets/Gameplay/RuntimeTheme.tss";
        if(!File.Exists(theme)){File.WriteAllText(theme,"@import url(\"unity-theme://default\");");AssetDatabase.ImportAsset(theme);}
        panel.themeStyleSheet=AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(theme);panel.scaleMode=PanelScaleMode.ScaleWithScreenSize;panel.referenceResolution=new Vector2Int(1280,720);EditorUtility.SetDirty(panel);return panel;
    }
    static void Curve(AnimationClip clip,string path,string property,float duration,params float[] values)
    {
        var keys=new Keyframe[values.Length];for(int i=0;i<keys.Length;i++)keys[i]=new Keyframe(i*duration/(keys.Length-1),values[i]);
        var curve=new AnimationCurve(keys);for(int i=0;i<keys.Length;i++){AnimationUtility.SetKeyLeftTangentMode(curve,i,AnimationUtility.TangentMode.ClampedAuto);AnimationUtility.SetKeyRightTangentMode(curve,i,AnimationUtility.TangentMode.ClampedAuto);}clip.SetCurve(path,typeof(Transform),property,curve);
    }
    static AnimationClip Clip(string name,float duration,bool loop,float bob)
    {
        string path="Assets/Gameplay/Clips/"+name+".anim";
        var clip=new AnimationClip{name=name,frameRate=60};Curve(clip,"ModelMotion","localPosition.y",duration,0,bob,0,bob,0);
        Curve(clip,"ModelMotion/WeaponPivot","localEulerAnglesRaw.y",duration,0,0,0);
        if(name=="Attack")
        {
            Curve(clip,"ModelMotion/WeaponPivot","localEulerAnglesRaw.y",duration,-70,-70,65,65,0);
            AnimationUtility.SetAnimationEvents(clip,new[]{Marker(.04f,"AttackStart"),Marker(.22f,"DamageWindowOpen"),Marker(.42f,"DamageWindowClose"),Marker(.72f,"AttackEnd")});
        }
        else if(!loop)AnimationUtility.SetAnimationEvents(clip,new[]{Marker(duration-.08f,"ReactionEnd")});
        var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=loop;AnimationUtility.SetAnimationClipSettings(clip,settings);
        var old=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);if(old){EditorUtility.CopySerialized(clip,old);UnityEngine.Object.DestroyImmediate(clip);return old;}AssetDatabase.CreateAsset(clip,path);return clip;
    }
    static AnimationEvent Marker(float t,string name){return new AnimationEvent{time=t,functionName=name};}
    static AnimatorController Controller()
    {
        const string path="Assets/Gameplay/Gameplay.controller";if(File.Exists(path))AssetDatabase.DeleteAsset(path);
        var ac=AnimatorController.CreateAnimatorControllerAtPath(path);ac.AddParameter("MoveSpeed",AnimatorControllerParameterType.Float);
        var sm=ac.layers[0].stateMachine;var locomotion=sm.AddState("Locomotion");sm.defaultState=locomotion;
        var blend=new BlendTree{name="Idle Walk Run",blendType=BlendTreeType.Simple1D,blendParameter="MoveSpeed",useAutomaticThresholds=false};AssetDatabase.AddObjectToAsset(blend,ac);
        blend.AddChild(Clip("Idle",3,true,.008f),0);blend.AddChild(Clip("Walk",1,true,.035f),.4f);blend.AddChild(Clip("Run",.65f,true,.06f),1);locomotion.motion=blend;
        foreach(string name in new[]{"Feed","Pet","Play","Attack"}){var state=sm.AddState(name);state.motion=Clip(name,.8f,false,name=="Play"?.12f:.025f);}
        return ac;
    }
    [MenuItem("Mongle/13 Validate Gameplay Foundation")]
    public static void Validate()
    {
        if(!File.Exists(Scene))Create();EditorSceneManager.OpenScene(Scene);
        var motor=UnityEngine.Object.FindFirstObjectByType<PetMotor>();var data=UnityEngine.Object.FindFirstObjectByType<PetRuntimeManager>();
        if(!motor||!data||!motor.joystick||!motor.visualRoot||!motor.animationBridge)throw new InvalidOperationException("Incomplete movement/data binding.");
        var attack=motor.GetComponent<PetAttackController>();if(!attack||!attack.hitOrigin||!attack.trail||!attack.feedback)throw new InvalidOperationException("Incomplete combat binding.");
        var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Gameplay/Clips/Attack.anim");
        string[] names={"AttackStart","DamageWindowOpen","DamageWindowClose","AttackEnd"};var events=AnimationUtility.GetAnimationEvents(clip);
        foreach(string name in names){bool found=false;foreach(var e in events)if(e.functionName==name)found=true;if(!found)throw new InvalidOperationException("Missing attack marker: "+name);}
        Debug.Log("GAMEPLAY FOUNDATION STRUCTURE PASS. Native Play Mode and device testing remain required.");
    }
    [MenuItem("Mongle/15 Build Foundation Windows")]
    public static void BuildWindows(){Build(BuildTarget.StandaloneWindows64,"Builds/Foundation-Windows/Mongle.exe");}
    [MenuItem("Mongle/16 Build Foundation Android")]
    public static void BuildAndroid(){EditorUserBuildSettings.buildAppBundle=false;Build(BuildTarget.Android,"Builds/Foundation-Android/Mongle.apk");}
    static void Build(BuildTarget target,string output){Validate();Directory.CreateDirectory(Path.GetDirectoryName(output));var result=BuildPipeline.BuildPlayer(new[]{Scene},output,target,BuildOptions.None);if(result.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new InvalidOperationException("Build failed: "+result.summary.result);}
}
