#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
namespace Mongle
{
    public sealed class RpgVfxPlaybackProbe : MonoBehaviour
    {
        [Serializable]public class Result
        {
            public string unityVersion,scope="Unity VFX Play Mode and camera capture. Visual quality and audible output require review.";
            public bool numericChecksPassed,audioClockAdvanced;
            public int casts,hits,capturedFrames,consoleErrors;
            public float elapsedSeconds;
            public double maxVisualLatenessSeconds;
        }
        SkillVfxDriver driver;float elapsed,nextCapture;int nextSkill,captures,errors;double startDsp,maxLateness;
        string output;
        void Start(){driver=FindFirstObjectByType<SkillVfxDriver>();startDsp=AudioSettings.dspTime;output=Path.GetFullPath("Validation/VFXPlayback");Directory.CreateDirectory(output);Application.logMessageReceived+=Log;}
        void Log(string text,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception)errors++;}
        void Update()
        {
            elapsed+=Time.unscaledDeltaTime;
            if(nextSkill<4&&elapsed>=1+nextSkill*4)
            {
                foreach(var target in FindObjectsByType<VfxTarget>(FindObjectsSortMode.None))target.ResetTarget();
                driver.aim=new Vector3(1.2f,.8f,0);driver.CastSkill(nextSkill++);
            }
            if(driver.LastImpactActualDsp>0)maxLateness=Math.Max(maxLateness,Math.Max(0,driver.LastImpactActualDsp-driver.LastImpactScheduledDsp));
            if(elapsed>=18)Finish();
        }
        void LateUpdate()
        {
            if(elapsed<nextCapture||elapsed>=18)return;nextCapture=elapsed+1f/12;
            var camera=Camera.main;if(!camera)return;var previous=camera.targetTexture;var active=RenderTexture.active;
            var rt=RenderTexture.GetTemporary(960,600,24,RenderTextureFormat.DefaultHDR);var image=new Texture2D(960,600,TextureFormat.RGB24,false);
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,960,600),0,0);image.Apply();File.WriteAllBytes(Path.Combine(output,"frame-"+(captures++).ToString("D5")+".png"),image.EncodeToPNG());}
            finally{camera.targetTexture=previous;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);Destroy(image);}
        }
        void Finish()
        {
            enabled=false;var result=new Result{unityVersion=Application.unityVersion,casts=driver.Casts,hits=driver.Hits,capturedFrames=captures,consoleErrors=errors,elapsedSeconds=elapsed,audioClockAdvanced=AudioSettings.dspTime>startDsp+.5,maxVisualLatenessSeconds=maxLateness};
            result.numericChecksPassed=result.casts==4&&result.hits>=4&&errors==0&&captures>24&&result.audioClockAdvanced;
            File.WriteAllText(Path.Combine(output,"result.json"),JsonUtility.ToJson(result,true));Application.logMessageReceived-=Log;
            Debug.Log("VFX PLAYBACK "+(result.numericChecksPassed?"NUMERIC PASS":"FAILED")+". Final visual and audio review is separate.");
            if(Application.isBatchMode)EditorApplication.Exit(result.numericChecksPassed?0:1);else EditorApplication.isPlaying=false;
        }
        void OnDestroy(){Application.logMessageReceived-=Log;}
    }
}
#endif
