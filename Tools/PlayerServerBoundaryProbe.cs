using System;
using System.Reflection;
public static class PlayerServerBoundaryProbe
{
    public static void Main(string[] args)
    {
        foreach(var type in Assembly.LoadFrom(args[0]).GetTypes())
            if(type.Namespace!=null&&type.Namespace.StartsWith("Mongle.Slice.Server",StringComparison.Ordinal))
                throw new Exception("Editor mock server type leaked into non-editor compilation: "+type.FullName);
        Console.WriteLine("[PASS BOUNDARY] Generated server types absent without UNITY_EDITOR. This is not an APK build.");
    }
}
