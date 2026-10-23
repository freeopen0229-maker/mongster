using System;
using System.IO;
using System.Linq;
using Mongle;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class NeulguiProject
{
    const string ScenePath = "Assets/Scenes/NeulguiProof.unity";
    const string Art = "Assets/Art/Neulgui/";
    static NeulguiProject()
    {
        EditorApplication.delayCall += () =>
        {
            if (!Application.isBatchMode && !File.Exists(ScenePath)) Create();
        };
    }
    static Transform Node(string name, Transform parent, Vector3 position)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false); t.localPosition = position; return t;
    }
    static Vector3 Point(float x, float y) { return new Vector3((x - 150) / 100, (260 - y) / 100, 0); }
    static Sprite Import(string name, Vector2 pivot)
    {
        string path = Art + name + ".png";
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("Missing painted part: " + path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.filterMode = FilterMode.Bilinear;
        var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Custom;
        settings.spritePivot = pivot; importer.SetTextureSettings(settings); importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
    static Transform Painted(string name, Transform parent, float px, float py, int order)
    {
        var t = Node(name, parent, Vector3.zero);
        var sr = t.gameObject.AddComponent<SpriteRenderer>();
        sr.sprite = Import(name, new Vector2(px / 301f, 1 - py / 280f)); sr.sortingOrder = order;
        return t;
    }
    static Transform Segment(string name, Transform parent, int order, bool far)
    {
        var t = Node(name, parent, Vector3.zero); var sr = t.gameObject.AddComponent<SpriteRenderer>();
        sr.sprite = Import("joint", new Vector2(.5f, 1)); sr.sortingOrder = order;
        if (far) sr.color = new Color(.88f, .85f, .80f); return t;
    }
    [MenuItem("Mongle/01 Create Neulgui Scene")]
    public static void Create()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before rebuilding.");
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Directory.CreateDirectory("Assets/Scenes"); Directory.CreateDirectory("Assets/Animation");
        AssetDatabase.Refresh();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var camera = new GameObject("Main Camera").AddComponent<Camera>(); camera.tag = "MainCamera";
        camera.transform.position = new Vector3(0, 1.5f, -10); camera.orthographic = true; camera.orthographicSize = 3;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.90f, .94f, .87f);
        var ground = new GameObject("Meadow").AddComponent<SpriteRenderer>();
        ground.sprite = Import("ground", new Vector2(.5f,.5f)); ground.sortingOrder = -10;
        ground.transform.position = new Vector3(0, -1.05f, 0); ground.transform.localScale = new Vector3(8, 5, 1);
        var root = new GameObject("Neulgui"); var controller = root.AddComponent<NeulguiController>();
        var shadow = Node("Shadow",root.transform,new Vector3(0,-.025f,0)).gameObject.AddComponent<SpriteRenderer>();
        shadow.sprite = Import("shadow",new Vector2(.5f,.5f)); shadow.sortingOrder = -1;
        shadow.transform.localScale = new Vector3(.58f,.17f,1);
        var flip = Node("Flip", root.transform, Vector3.zero);
        var motion = Node("BodyMotion", flip, Vector3.zero);
        var tail = Node("TailPivot", motion, Point(69,140)); Painted("tail",tail,69,140,0);
        var patch = Node("BodyPatch", motion, new Vector3(.18f,.82f,0)).gameObject.AddComponent<SpriteRenderer>();
        patch.sprite = Import("bodyPatch",new Vector2(.5f,.5f)); patch.sortingOrder=5; patch.transform.localScale=new Vector3(.89f,1.18f,1);
        Painted("body",motion,150,260,6);
        var farArm=Node("FarArmPivot",motion,Point(206,148));Painted("armFar",farArm,206,148,7);
        var head=Node("HeadPivot",motion,Point(178,112));
        var farEar=Node("FarEarPivot",head,Point(207,58)-Point(178,112));Painted("earFar",farEar,207,58,8);
        Painted("head",head,178,112,9);
        var eyelids=Painted("eyesClosed",head,178,112,10);eyelids.gameObject.SetActive(false);
        var nearEar=Node("NearEarPivot",head,Point(140,52)-Point(178,112));Painted("earNear",nearEar,140,52,12);
        var nearArm=Node("NearArmPivot",motion,Point(144,152));Painted("armNear",nearArm,144,152,11);
        controller.legs=new PetLeg[2];
        for(int i=0;i<2;i++)
        {
            bool far=i==1;var legRoot=Node(far?"FarLeg":"NearLeg",root.transform,Vector3.zero);
            var upper=Segment("Upper",legRoot,far?1:3,far);var lower=Segment("Lower",legRoot,far?1:3,far);
            var paw=Painted(far?"pawFar":"pawNear",legRoot,far?193:118,259,far?2:4);
            controller.legs[i]=new PetLeg{upper=upper,lower=lower,paw=paw,neutral=far?.44f:-.28f,offset=far?.5f:0,bend=far?1:-1};
        }
        controller.flip=flip;controller.bodyMotion=motion;controller.eyesClosed=eyelids;
        var animator=flip.gameObject.AddComponent<Animator>();animator.runtimeAnimatorController=CreateAnimator();animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;controller.animator=animator;
        var prefab=PrefabUtility.SaveAsPrefabAsset(root,"Assets/Neulgui.prefab");
        if(!prefab)throw new InvalidOperationException("Prefab creation failed.");
        PlayerSettings.companyName="Mongle";PlayerSettings.productName="Neulgui Motion Proof";
        PlayerSettings.defaultScreenWidth=1280;PlayerSettings.defaultScreenHeight=800;
        PlayerSettings.defaultInterfaceOrientation=UIOrientation.LandscapeLeft;
        EditorSceneManager.SaveScene(scene,ScenePath);
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)};
        AssetDatabase.SaveAssets();Selection.activeGameObject=root;
        Debug.Log("NEULGUI: scene, prefab, painted layers and native Animator created. Play Mode validation is still required.");
    }
    static void Curve(AnimationClip clip,string path,string property,float length,params float[] values)
    {
        var keys=new Keyframe[values.Length];for(int i=0;i<keys.Length;i++)keys[i]=new Keyframe(length*i/(values.Length-1),values[i]);
        var curve=new AnimationCurve(keys);for(int i=0;i<keys.Length;i++){AnimationUtility.SetKeyLeftTangentMode(curve,i,AnimationUtility.TangentMode.ClampedAuto);AnimationUtility.SetKeyRightTangentMode(curve,i,AnimationUtility.TangentMode.ClampedAuto);}
        clip.SetCurve(path,typeof(Transform),property,curve);
    }
    static AnimationClip Clip(string name,float duration,float bob,float head,float ears,bool loop)
    {
        var clip=new AnimationClip{name=name,frameRate=60};
        Curve(clip,"BodyMotion","localPosition.y",duration,0,bob,0,-bob*.4f,0);
        Curve(clip,"BodyMotion","localEulerAnglesRaw.z",duration,0,head*.15f,0,-head*.15f,0);
        Curve(clip,"BodyMotion/HeadPivot","localEulerAnglesRaw.z",duration,0,head,0,-head*.5f,0);
        Curve(clip,"BodyMotion/HeadPivot/NearEarPivot","localEulerAnglesRaw.z",duration,0,-ears*.3f,ears,ears*.25f,0);
        Curve(clip,"BodyMotion/HeadPivot/FarEarPivot","localEulerAnglesRaw.z",duration,0,0,-ears*.6f,ears*.4f,0);
        Curve(clip,"BodyMotion/TailPivot","localEulerAnglesRaw.z",duration,0,head*2,0,-head*2,0);
        Curve(clip,"BodyMotion/NearArmPivot","localEulerAnglesRaw.z",duration,0,head*3,0,-head*3,0);
        Curve(clip,"BodyMotion/FarArmPivot","localEulerAnglesRaw.z",duration,0,-head*2,0,head*2,0);
        var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=loop;settings.loopBlend=loop;AnimationUtility.SetAnimationClipSettings(clip,settings);
        string path="Assets/Animation/"+name+".anim";
        var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if(existing){EditorUtility.CopySerialized(clip,existing);UnityEngine.Object.DestroyImmediate(clip);return existing;}
        AssetDatabase.CreateAsset(clip,path);return clip;
    }
    static AnimatorController CreateAnimator()
    {
        const string path="Assets/Animation/Neulgui.controller";
        if(File.Exists(path))AssetDatabase.DeleteAsset(path);
        var ac=AnimatorController.CreateAnimatorControllerAtPath(path);
        foreach(string p in new[]{"Walking","Happy","Turning"})ac.AddParameter(p,AnimatorControllerParameterType.Bool);
        ac.AddParameter("GaitRate",AnimatorControllerParameterType.Float);ac.parameters=ac.parameters.Select(p=>{if(p.name=="GaitRate")p.defaultFloat=1;return p;}).ToArray();
        var sm=ac.layers[0].stateMachine;
        var clips=new[]{Clip("Idle",3.6f,.007f,.7f,.7f,true),Clip("Walk",.48f/.62f,.013f,1.2f,2.4f,true),Clip("Happy",1.4f,.025f,3.5f,3,true),Clip("Turn",.34f,-.018f,2,1,false)};
        string[] names={"Idle","Walk","Happy","Turn"};
        for(int i=0;i<names.Length;i++)
        {
            var state=sm.AddState(names[i]);state.motion=clips[i];if(i==0)sm.defaultState=state;
            if(i==1){state.speedParameter="GaitRate";state.speedParameterActive=true;}
            var tr=sm.AddAnyStateTransition(state);tr.canTransitionToSelf=false;tr.hasExitTime=false;tr.duration=.12f;
            tr.AddCondition(i==3?AnimatorConditionMode.If:AnimatorConditionMode.IfNot,0,"Turning");
            if(i!=3)tr.AddCondition(i==2?AnimatorConditionMode.If:AnimatorConditionMode.IfNot,0,"Happy");
            if(i<2)tr.AddCondition(i==1?AnimatorConditionMode.If:AnimatorConditionMode.IfNot,0,"Walking");
        }
        return ac;
    }
    [MenuItem("Mongle/02 Validate Scene And Rig")]
    public static void Validate()
    {
        if(!File.Exists(ScenePath))Create();
        EditorSceneManager.OpenScene(ScenePath);
        var pet=UnityEngine.Object.FindFirstObjectByType<NeulguiController>();
        if(!pet||!pet.animator||pet.legs.Length!=2)throw new InvalidOperationException("Incomplete pet rig.");
        foreach(var sr in pet.GetComponentsInChildren<SpriteRenderer>(true))if(!sr.sprite)throw new InvalidOperationException("Missing sprite: "+sr.name);
        foreach(var leg in pet.legs)if(!leg.upper||!leg.lower||!leg.paw)throw new InvalidOperationException("Incomplete leg.");
        for(int i=0;i<1000;i++)
        {
            var hip=new Vector2(0,.4f);var foot=new Vector2(Mathf.Sin(i*.1f)*.20f,.02f+Mathf.Abs(Mathf.Cos(i*.1f))*.1f);
            var knee=NeulguiMath.Knee(hip,foot,-1);
            if(Mathf.Abs(Vector2.Distance(hip,knee)-.27f)>.0001f||Mathf.Abs(Vector2.Distance(knee,foot)-.27f)>.0001f)throw new InvalidOperationException("Leg segment length changed.");
        }
        var ac=pet.animator.runtimeAnimatorController;
        foreach(var c in ac.animationClips)if(c.length<=0||AnimationUtility.GetCurveBindings(c).Length<5)throw new InvalidOperationException("Missing motion curves: "+c.name);
        Debug.Log("NEULGUI VALIDATION PASS: sprites, hierarchy, IK lengths, Animator clips. Visual quality must be assessed in Play Mode.");
    }
    [MenuItem("Mongle/03 Build Windows")]
    public static void BuildWindows(){Build(BuildTarget.StandaloneWindows64,"Builds/Windows/Neulgui.exe");}
    [MenuItem("Mongle/04 Build Android APK")]
    public static void BuildAndroid(){EditorUserBuildSettings.buildAppBundle=false;Build(BuildTarget.Android,"Builds/Android/Neulgui.apk");}
    static void Build(BuildTarget target,string output)
    {
        Validate();Directory.CreateDirectory(Path.GetDirectoryName(output));
        var report=BuildPipeline.BuildPlayer(new[]{ScenePath},output,target,BuildOptions.None);
        if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new InvalidOperationException("Unity build failed: "+report.summary.result);
        Debug.Log("NEULGUI BUILD SUCCEEDED: "+Path.GetFullPath(output));
    }
}
