using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Mongle.Slice;
using Mongle.Slice.Testing;
using Mongle.VisualRenewal;

/// <summary>
/// 제작된 아기/성체 통합 리그를 받아 테스트 씬과 전용 카탈로그를 만든다.
/// 새 원화·웨이트·애니메이션·URP Asset을 자동 제작하는 메뉴는 아니다.
/// </summary>
public sealed class PetViewportLabSetup:EditorWindow
{
    GameObject babyRig,adultRig,costumePrefab;
    ParticleSystem auraPrefab;
    Texture2D babyMask,adultMask;
    string costumeSocket="Costume",auraSocket="Aura";

    [MenuItem("Mongle/Viewport Tests/Create Neulgui Lab")]
    public static void Open()=>GetWindow<PetViewportLabSetup>("Neulgui Viewport Lab");

    void OnGUI()
    {
        EditorGUILayout.HelpBox("Assign authored whole-body rig prefabs. Each needs one SpriteRenderer, Animator with clips and PetVisualBindings. This creates local preview data only.",MessageType.Info);
        babyRig=(GameObject)EditorGUILayout.ObjectField("Baby rig prefab",babyRig,typeof(GameObject),false);
        adultRig=(GameObject)EditorGUILayout.ObjectField("Adult rig prefab",adultRig,typeof(GameObject),false);
        auraPrefab=(ParticleSystem)EditorGUILayout.ObjectField("Aura prefab (required)",auraPrefab,typeof(ParticleSystem),false);
        costumePrefab=(GameObject)EditorGUILayout.ObjectField("Costume prefab (optional)",costumePrefab,typeof(GameObject),false);
        costumeSocket=EditorGUILayout.TextField("Costume socket ID",costumeSocket);
        auraSocket=EditorGUILayout.TextField("Aura socket ID",auraSocket);
        babyMask=(Texture2D)EditorGUILayout.ObjectField("Baby RGBA mask",babyMask,typeof(Texture2D),false);
        adultMask=(Texture2D)EditorGUILayout.ObjectField("Adult RGBA mask",adultMask,typeof(Texture2D),false);
        using(new EditorGUI.DisabledScope(EditorApplication.isPlaying))
            if(GUILayout.Button("Validate Inputs and Create Test Scene"))
            {
                try{CreateLab();}
                catch(Exception e){Debug.LogException(e);}
            }
    }

    static void ValidateRig(GameObject prefab,string socketRequired,string otherSocket)
    {
        if(!prefab||!PrefabUtility.IsPartOfPrefabAsset(prefab))throw new InvalidOperationException("Assign a saved rig prefab asset.");
        var binding=prefab.GetComponent<PetVisualBindings>();
        if(!binding)throw new InvalidOperationException("PetVisualBindings missing on prefab root: "+prefab.name);
        var map=new System.Collections.Generic.Dictionary<string,Transform>();binding.ValidateAndCollect(map);
        bool hasSkin=false;
        foreach(var component in binding.bodyRenderer.GetComponents<Component>())
            if(component&&component.GetType().FullName=="UnityEngine.U2D.Animation.SpriteSkin")hasSkin=true;
        if(!hasSkin)throw new InvalidOperationException("Body needs authored SpriteSkin. A static sprite is not a completed integrated rig: "+prefab.name);
        if(!binding.animator.runtimeAnimatorController||binding.animator.runtimeAnimatorController.animationClips.Length==0)
            throw new InvalidOperationException("Authored Animator controller/clips required: "+prefab.name);
        if(!string.IsNullOrEmpty(socketRequired)&&!map.ContainsKey(socketRequired))
            throw new InvalidOperationException("Socket missing: "+socketRequired+" on "+prefab.name);
        if(!string.IsNullOrEmpty(otherSocket)&&!map.ContainsKey(otherSocket))
            throw new InvalidOperationException("Costume socket missing: "+otherSocket+" on "+prefab.name);
    }

    void CreateLab()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play Mode before creating the lab.");
        if(!GraphicsSettings.currentRenderPipeline||!GraphicsSettings.currentRenderPipeline.GetType().Name.Contains("UniversalRenderPipelineAsset"))
            throw new InvalidOperationException("Configure an URP Asset in Graphics/Quality first.");
        if(!auraPrefab||!PrefabUtility.IsPartOfPrefabAsset(auraPrefab))
            throw new InvalidOperationException("Assign a saved ParticleSystem aura prefab.");
        ValidateRig(babyRig,null,costumePrefab?costumeSocket:null);
        ValidateRig(adultRig,auraSocket,costumePrefab?costumeSocket:null);
        if(costumePrefab&&!PrefabUtility.IsPartOfPrefabAsset(costumePrefab))
            throw new InvalidOperationException("Costume must be a prefab asset.");
        var shader=Shader.Find("Mongle/VisualRenewal/PetLocalGlow");
        if(!shader||ShaderUtil.ShaderHasError(shader))throw new InvalidOperationException("PetLocalGlow shader missing/has compiler errors.");
        if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
        string folder=AssetDatabase.GenerateUniqueAssetPath("Assets/NeulguiViewportLab");
        AssetDatabase.CreateFolder("Assets",Path.GetFileName(folder));
        var babyMat=MakeMaterial(shader,babyMask,0,folder+"/BabyCommon.mat");
        var adultMat=MakeMaterial(shader,adultMask,0,folder+"/AdultCommon.mat");
        var previewMat=MakeMaterial(shader,adultMask,1.5f,folder+"/AdultUnique_PREVIEW.mat");
        var baby=Definition(folder,"BabyCommon",babyRig,GrowthStage.Baby,PetGrade.Common,babyMat,false);
        var adult=Definition(folder,"AdultCommon",adultRig,GrowthStage.Adult,PetGrade.Common,adultMat,false);
        var preview=Definition(folder,"AdultUnique_PREVIEW",adultRig,GrowthStage.Adult,PetGrade.Unique,previewMat,true);
        var catalog=CreateInstance<PetVisualCatalog>();catalog.entries=new[]{baby,adult,preview};catalog.ValidateCatalog();
        AssetDatabase.CreateAsset(catalog,folder+"/LocalPreviewCatalog.asset");

        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var camera=new GameObject("Main Camera").AddComponent<Camera>();camera.tag="MainCamera";
        camera.orthographic=true;camera.orthographicSize=2.5f;camera.transform.position=new Vector3(0,1,-10);
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.12f,.16f,.19f);camera.allowHDR=true;
        camera.gameObject.AddComponent<AudioListener>();
        var actor=new GameObject("LOCAL Neulgui Preview");
        var mount=new GameObject("ViewMount").transform;mount.SetParent(actor.transform,false);
        var visual=actor.AddComponent<PetVisualManager>();visual.catalog=catalog;visual.viewMount=mount;
        visual.motionRoot=actor.transform;visual.turnRoot=actor.transform;
        var test=actor.AddComponent<PetTestController>();test.visuals=visual;
        test.costumeId=costumePrefab?"test-costume":"";test.adultPreviewGrade=PetGrade.Unique;
        string scene=folder+"/NeulguiViewportLab.unity";
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),scene);
        AssetDatabase.SaveAssets();Selection.activeGameObject=actor;
        Debug.Log("LOCAL LAB CREATED: "+scene+". Enter Play Mode, select PetTestController, then use Equip buttons. Adult Unique is an FX preview only. Configure camera Post Processing + Bloom Volume to inspect emission.");
        if(!adultMask)Debug.LogWarning("No adult effect mask assigned: aura can attach, but body emission remains masked off.");
    }

    static Material MakeMaterial(Shader shader,Texture2D mask,float glow,string path)
    {
        var material=new Material(shader);if(mask)material.SetTexture("_MaskTex",mask);
        material.SetFloat("_GlowStrength",glow);material.SetFloat("_RimStrength",glow>0?.25f:0);
        AssetDatabase.CreateAsset(material,path);return material;
    }
    PetVisualDefinition Definition(string folder,string name,GameObject rig,GrowthStage stage,PetGrade grade,Material mat,bool aura)
    {
        var d=CreateInstance<PetVisualDefinition>();d.speciesId=0;d.stage=stage;d.grade=grade;d.rigPrefab=rig;
        d.bodyMaterials=new PetBodyMaterials{low=mat,standard=mat,showcase=mat};
        d.costumes=costumePrefab?new[]{new PetCostumeAttachment{costumeId="test-costume",socketId=costumeSocket,prefab=costumePrefab}}:Array.Empty<PetCostumeAttachment>();
        d.effects=aura?new[]{new PetSocketEffect{socketId=auraSocket,prefab=auraPrefab,
            minimumQuality=VisualQuality.Standard,simulationSpace=ParticleSystemSimulationSpace.Local}}:Array.Empty<PetSocketEffect>();
        AssetDatabase.CreateAsset(d,folder+"/"+name+".asset");return d;
    }
}
