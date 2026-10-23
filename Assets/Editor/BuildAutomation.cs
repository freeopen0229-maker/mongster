#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace FusionTamagotchi.EditorTools
{
    /// <summary>실제 Unity가 라이선스/UPM/컴파일을 통과한 뒤 호출하는 설정·빌드 진입점.</summary>
    public static class BuildAutomation
    {
        const string Generated="Assets/BuildGenerated";
        [MenuItem("Mongle/Build/Configure Development Project")]
        public static void Configure()
        {
            if(!File.Exists("Assets/Scenes/MainGame.unity"))throw new BuildFailedException("MainGame.unity is missing.");
            Directory.CreateDirectory(Generated);AssetDatabase.Refresh();
            const string rendererPath=Generated+"/PetRenderer2D.asset",pipelinePath=Generated+"/PetURP.asset";
            var renderer=AssetDatabase.LoadAssetAtPath<Renderer2DData>(rendererPath);
            if(!renderer)
            {
                renderer=ScriptableObject.CreateInstance<Renderer2DData>();
                ResourceReloader.ReloadAllNullIn(renderer,"Packages/com.unity.render-pipelines.universal");
                AssetDatabase.CreateAsset(renderer,rendererPath);
            }
            var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
            if(!pipeline){pipeline=UniversalRenderPipelineAsset.Create(renderer);AssetDatabase.CreateAsset(pipeline,pipelinePath);}
            const string spriteMaterialPath="Assets/Resources/MainGame/PetSprite.mat";
            var spriteShader=Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if(!spriteShader)throw new BuildFailedException("URP 2D sprite shader missing.");
            if(!AssetDatabase.LoadAssetAtPath<Material>(spriteMaterialPath))AssetDatabase.CreateAsset(new Material(spriteShader),spriteMaterialPath);
            GraphicsSettings.defaultRenderPipeline=pipeline;
            int selected=QualitySettings.GetQualityLevel();
            for(int i=0;i<QualitySettings.names.Length;i++){QualitySettings.SetQualityLevel(i,false);QualitySettings.renderPipeline=pipeline;}
            QualitySettings.SetQualityLevel(selected,false);
            PlayerSettings.companyName="FusionTamagotchi";PlayerSettings.productName="Neulgui Development";
            PlayerSettings.bundleVersion="0.1.0";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android,"com.fusiontamagotchi.neulgui.dev");
            PlayerSettings.defaultScreenWidth=1080;PlayerSettings.defaultScreenHeight=1920;
            PlayerSettings.defaultInterfaceOrientation=UIOrientation.Portrait;
            PlayerSettings.runInBackground=false;
            PlayerSettings.Android.forceInternetPermission=true;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android,ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures=AndroidArchitecture.ARM64;
            PlayerSettings.Android.useCustomKeystore=false; // Development/debug 서명. 스토어용 비밀키는 다루지 않는다.
            // 명시적인 테스트 프로젝트에서만 기본 legacy UI 입력을 사용한다.
            var playerAssets=AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if(playerAssets.Length>0)
            {
                var settings=new SerializedObject(playerAssets[0]);var input=settings.FindProperty("activeInputHandler");
                if(input!=null){input.intValue=0;settings.ApplyModifiedPropertiesWithoutUndo();}
            }
            const string scene="Assets/Scenes/MainGame.unity";
            if(!File.Exists(scene))throw new BuildFailedException("MainGame.unity is missing; refusing to deploy a test scene.");
            EditorSceneManager.OpenScene(scene);
            if(!UnityEngine.Object.FindFirstObjectByType<FusionTamagotchi.Client.MainGameRoot>())
                throw new BuildFailedException("MainGame entry point is missing.");
            if(string.IsNullOrEmpty(scene)||!File.Exists(scene))throw new BuildFailedException("Generated scene was not saved.");
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(scene,true)};
            AssetDatabase.SaveAssets();EditorSceneManager.SaveOpenScenes();
            Directory.CreateDirectory("Validation/NativeUnity");
            File.WriteAllText("Validation/NativeUnity/configured.json",JsonUtility.ToJson(new SetupRecord{unity=Application.unityVersion,scene=scene,
                pipeline=pipelinePath,packageName=PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android)},true));
            Debug.Log("DEVELOPMENT CONFIGURATION COMPLETE. Play Mode and rendering still require actual execution.");
        }
        [Serializable] sealed class SetupRecord{public string unity,scene,pipeline,packageName;}
        [MenuItem("Mongle/Build/Build Development APK")]
        public static void BuildAndroid()
        {
            if(EditorUserBuildSettings.activeBuildTarget!=BuildTarget.Android)
                throw new BuildFailedException("Launch Unity with -buildTarget Android for batch builds.");
            if(!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android,BuildTarget.Android))
                throw new BuildFailedException("Android Build Support is not installed.");
            Configure();EditorUserBuildSettings.buildAppBundle=false; // 반드시 APK를 생성한다.
            Directory.CreateDirectory("Builds/Android");
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=Array.ConvertAll(EditorBuildSettings.scenes,x=>x.path),
                locationPathName="Builds/Android/Neulgui-development.apk",target=BuildTarget.Android,
                options=BuildOptions.Development|BuildOptions.AllowDebugging|BuildOptions.CompressWithLz4});
            File.WriteAllText("Validation/NativeUnity/android-build-result.txt",report.summary.result+"; errors="+report.summary.totalErrors+"; bytes="+report.summary.totalSize);
            if(report.summary.result!=BuildResult.Succeeded||!File.Exists("Builds/Android/Neulgui-development.apk"))
                throw new BuildFailedException("Development APK build did not succeed.");
        }
    }
}
#endif
