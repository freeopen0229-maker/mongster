using Mongle;
using UnityEditor;
using UnityEngine;
[InitializeOnLoad]
public static class RpgVfxPlaybackValidation
{
    const string Pending="Mongle.VfxPlaybackPending";
    static RpgVfxPlaybackValidation(){EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Pending,false)){SessionState.SetBool(Pending,false);new GameObject("VFX Playback Probe").AddComponent<RpgVfxPlaybackProbe>();}};}
    [MenuItem("Mongle/09 Run RPG VFX Playback Validation")]
    public static void Run(){RpgVfxProject.Validate();SessionState.SetBool(Pending,true);EditorApplication.isPlaying=true;}
}
