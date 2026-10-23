using System;
using System.IO;
using Mongle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class RpgVfxProject
{
    public const string ScenePath="Assets/Scenes/RpgVfxLab.unity";
    [MenuItem("Mongle/06 Create RPG VFX Scene")]
    public static void Create()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play Mode first.");
        if(!File.Exists("Assets/Neulgui.prefab"))NeulguiProject.Create();
        if(!Application.isBatchMode&&!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
        Directory.CreateDirectory("Assets/Animation/Skills");Directory.CreateDirectory("Assets/VFX/Materials");AssetDatabase.Refresh();
        ImportTexturesAndSound();
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var camera=new GameObject("Main Camera").AddComponent<Camera>();camera.tag="MainCamera";camera.transform.position=new Vector3(0,4.2f,-8.5f);camera.transform.LookAt(new Vector3(0,.8f,.3f));camera.fieldOfView=45;camera.backgroundColor=new Color(.025f,.04f,.08f);camera.clearFlags=CameraClearFlags.SolidColor;camera.allowHDR=true;
        camera.gameObject.AddComponent<AudioListener>();var presentation=camera.gameObject.AddComponent<VfxPresentation>();
        var sun=new GameObject("Moonlight").AddComponent<Light>();sun.type=LightType.Directional;sun.color=new Color(.5f,.68f,1);sun.intensity=1;sun.transform.rotation=Quaternion.Euler(45,-35,0);
        var floor=GameObject.CreatePrimitive(PrimitiveType.Plane);floor.name="Training Arena";floor.transform.localScale=new Vector3(1.3f,1,1);floor.GetComponent<Renderer>().sharedMaterial=Surface("Arena",new Color(.065f,.09f,.13f));
        var feedback=new GameObject("Combat Feedback").AddComponent<CombatFeedback>();feedback.cameraFx=presentation;
        var driver=new GameObject("Skill Timeline").AddComponent<SkillVfxDriver>();driver.feedback=feedback;driver.presentation=presentation;
        var pet=PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Neulgui.prefab")) as GameObject;
        pet.name="Neulgui Caster";pet.transform.position=new Vector3(-2.7f,0,0);pet.GetComponent<NeulguiController>().enabled=false;driver.caster=pet.transform;
        driver.skills=new RpgSkillDefinition[4];
        var colors=new[]{new Color(1,.28f,.035f),new Color(.12f,.86f,1),new Color(.55f,.35f,1),new Color(.15f,1,.64f)};
        for(int i=0;i<4;i++)
        {
            string path="Assets/Animation/Skills/"+(RpgSkill)i+".asset";var s=AssetDatabase.LoadAssetAtPath<RpgSkillDefinition>(path);
            if(!s){s=ScriptableObject.CreateInstance<RpgSkillDefinition>();AssetDatabase.CreateAsset(s,path);}
            s.kind=(RpgSkill)i;s.color=colors[i];s.windup=i==3?.8f:i==2?.4f:.28f;s.travel=i>=2?.01f:.52f;s.damage=i==3?40:25;s.radius=i==3?1.65f:i==2?1.1f:.8f;s.knockback=i==3?3.2f:2;s.strength=i==3?1.45f:1;s.cooldown=i==3?2:1;driver.skills[i]=s;EditorUtility.SetDirty(s);
        }
        for(int i=0;i<3;i++)
        {
            var target=GameObject.CreatePrimitive(PrimitiveType.Capsule);target.name="Training Wisp "+(i+1);target.layer=8;
            target.transform.position=new Vector3(1.2f+i*.65f,.65f,i==0?0:i==1?.9f:-.9f);target.transform.localScale=new Vector3(.5f,.65f,.5f);
            target.GetComponent<Renderer>().sharedMaterial=Surface("Wisp",new Color(.3f,.45f,.5f));target.AddComponent<VfxTarget>();
        }
        EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();Selection.activeGameObject=driver.gameObject;
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true),new EditorBuildSettingsScene("Assets/Scenes/NeulguiProof.unity",true)};
        Debug.Log("RPG VFX scene constructed. Shader compilation, playback and final visual quality still require engine validation.");
    }
    static Material Surface(string name,Color color)
    {
        string path="Assets/VFX/Materials/"+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!material){material=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(material,path);}
        material.color=color;material.EnableKeyword("_EMISSION");material.SetColor("_EmissionColor",color*.2f);EditorUtility.SetDirty(material);return material;
    }
    static void ImportTexturesAndSound()
    {
        foreach(var path in Directory.GetFiles("Assets/Resources/VFX","*.png"))
        {
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;if(importer==null)continue;
            importer.textureType=TextureImporterType.Default;importer.alphaIsTransparency=true;importer.mipmapEnabled=true;importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Trilinear;importer.maxTextureSize=2048;importer.SaveAndReimport();
        }
        foreach(var path in Directory.GetFiles("Assets/Resources/VFX","*.ogg"))
        {
            var importer=AssetImporter.GetAtPath(path) as AudioImporter;if(importer==null)continue;
            var settings=importer.defaultSampleSettings;settings.loadType=AudioClipLoadType.DecompressOnLoad;settings.compressionFormat=AudioCompressionFormat.PCM;settings.preloadAudioData=true;importer.defaultSampleSettings=settings;importer.SaveAndReimport();
        }
    }
    [MenuItem("Mongle/07 Validate RPG VFX Resources")]
    public static void Validate()
    {
        if(!File.Exists(ScenePath))Create();EditorSceneManager.OpenScene(ScenePath);
        foreach(string shader in new[]{"Energy","Bloom"})
        {
            var resource=Resources.Load<Shader>("VFX/"+shader);if(!resource||ShaderUtil.ShaderHasError(resource))throw new InvalidOperationException("Shader unavailable or failed compilation: "+shader);
        }
        foreach(string name in new[]{"fire_01","slash_01","spark_01","arcane_sigil_ai","light_01","circle_01","trace_01","magic_01","smoke_01","flare_01"})if(!Resources.Load<Texture2D>("VFX/"+name))throw new InvalidOperationException("Texture missing: "+name);
        foreach(string name in new[]{"impact_thump","impact_snap","impact_tail"})if(!Resources.Load<AudioClip>("VFX/"+name))throw new InvalidOperationException("Audio missing: "+name);
        var driver=UnityEngine.Object.FindFirstObjectByType<SkillVfxDriver>();if(!driver||driver.skills.Length!=4||!driver.feedback||!driver.caster)throw new InvalidOperationException("Incomplete skill scene");
        Debug.Log("RPG VFX RESOURCES PASS. Run playback for hit, freeze, shake, audio and particle behaviour.");
    }
    [MenuItem("Mongle/08 Import Official Kenney Unity Samples")]
    public static void ImportSamples(){AssetDatabase.ImportPackage("Tools/kenney_particlePack_samples.unitypackage",false);}
    [MenuItem("Mongle/10 Build VFX Windows")]
    public static void BuildWindows(){Build(UnityEditor.BuildTarget.StandaloneWindows64,"Builds/VFX-Windows/MongleVFX.exe");}
    [MenuItem("Mongle/11 Build VFX Android APK")]
    public static void BuildAndroid(){EditorUserBuildSettings.buildAppBundle=false;Build(UnityEditor.BuildTarget.Android,"Builds/VFX-Android/MongleVFX.apk");}
    static void Build(UnityEditor.BuildTarget target,string output)
    {
        Validate();Directory.CreateDirectory(Path.GetDirectoryName(output));
        var result=BuildPipeline.BuildPlayer(new[]{ScenePath},output,target,BuildOptions.None);
        if(result.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new InvalidOperationException("VFX build failed: "+result.summary.result);
    }
}
