using Mongle;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class NeulguiPlaybackValidation
{
    const string Pending="Mongle.NeulguiPlaybackPending";
    static NeulguiPlaybackValidation()
    {
        EditorApplication.playModeStateChanged+=change=>
        {
            if(change!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool(Pending,false))return;
            SessionState.SetBool(Pending,false);
            new GameObject("UnityPlaybackValidation").AddComponent<NeulguiPlaybackProbe>();
        };
    }
    [MenuItem("Mongle/05 Run Playback Validation")]
    public static void Run()
    {
        NeulguiProject.Validate();SessionState.SetBool(Pending,true);EditorApplication.isPlaying=true;
    }
}
