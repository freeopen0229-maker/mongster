using System;
using UnityEditor;
using UnityEngine;
using Mongle.Slice.Server.Testing;

/// <summary>Tools/sync_editor_mock_sources.py가 생성한 Editor 전용 서버 소스를 사용한다.</summary>
public static class PetTradeMockTests
{
    [MenuItem("Mongle/Viewport Tests/Run Local Mock Trade")]
    public static void Run()
    {
        try{PetTradeMockScenario.Run(message=>Debug.Log(message));}
        catch(Exception e){Debug.LogException(e);throw;}
    }
}
