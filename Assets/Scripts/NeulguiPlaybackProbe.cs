#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Mongle
{
    // Runs inside actual Unity Play Mode; no alternate renderer or simulated engine.
    public sealed class NeulguiPlaybackProbe : MonoBehaviour
    {
        [Serializable] public class Result
        {
            public string unityVersion;
            public string scope = "Unity Play Mode motion telemetry and camera frames; visual review still required";
            public bool numericChecksPassed;
            public int observedFrames, capturedFrames, consoleErrors;
            public float maxPlantedFootDrift, maxReachError;
            public string[] states;
        }
        NeulguiController pet;
        float elapsed, nextCapture;
        int stage = -1, frames, captures, errors;
        readonly HashSet<string> states = new HashSet<string>();
        string output;
        void Start()
        {
            pet = FindFirstObjectByType<NeulguiController>();
            output = Path.GetFullPath("Validation/Playback");Directory.CreateDirectory(output);
            Application.logMessageReceived += Log;
        }
        void Log(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception)errors++;}
        void Update()
        {
            if(!pet)return;
            elapsed += Time.unscaledDeltaTime;
            int current=elapsed<2?0:elapsed<10?1:elapsed<13?2:elapsed<17?3:elapsed<20?4:5;
            if(current!=stage)
            {
                stage=current;
                switch(stage){case 0:pet.Rest();break;case 1:pet.Walk();break;case 2:pet.Rest();break;case 3:pet.Turn();break;case 4:pet.Pet();break;case 5:Finish();return;}
            }
            frames++;states.Add(pet.MotionState);
        }
        void LateUpdate()
        {
            if(!pet||elapsed<nextCapture||stage>=5)return;
            nextCapture=elapsed+1f/12;
            var camera=Camera.main;if(!camera)return;
            var previous=camera.targetTexture;var active=RenderTexture.active;
            var target=RenderTexture.GetTemporary(960,600,24);
            var texture=new Texture2D(960,600,TextureFormat.RGB24,false);
            try
            {
                camera.targetTexture=target;camera.Render();RenderTexture.active=target;
                texture.ReadPixels(new Rect(0,0,960,600),0,0);texture.Apply();
                File.WriteAllBytes(Path.Combine(output,"frame-"+(captures++).ToString("D5")+".png"),texture.EncodeToPNG());
            }
            finally{camera.targetTexture=previous;RenderTexture.active=active;RenderTexture.ReleaseTemporary(target);Destroy(texture);}
        }
        void Finish()
        {
            var result=new Result{unityVersion=Application.unityVersion,observedFrames=frames,capturedFrames=captures,consoleErrors=errors,maxPlantedFootDrift=pet.MaxPlantDrift,maxReachError=pet.MaxReachError,states=new List<string>(states).ToArray()};
            result.numericChecksPassed=errors==0&&captures>20&&states.Contains("Walk")&&states.Contains("Idle")&&states.Contains("Happy")&&states.Contains("Turn")&&pet.MaxPlantDrift<.005f&&pet.MaxReachError<.005f;
            File.WriteAllText(Path.Combine(output,"result.json"),JsonUtility.ToJson(result,true));
            Application.logMessageReceived-=Log;
            Debug.Log("NEULGUI PLAYBACK "+(result.numericChecksPassed?"NUMERIC PASS":"FAILED")+"; inspect captured frames for visual quality.");
            if(Application.isBatchMode)EditorApplication.Exit(result.numericChecksPassed?0:1);else EditorApplication.isPlaying=false;
        }
        void OnDestroy(){Application.logMessageReceived-=Log;}
    }
}
#endif
