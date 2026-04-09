using UnityEditor;
using UnityEngine;
using System.Diagnostics;

public class BuildScript
{
    [MenuItem("Build/Build Host")]
    public static void BuildHost()
    {
        PlayerSettings.applicationIdentifier = "com.synarea.rehab.host";
        string path = "Builds/Host/Host.app";
        BuildPipeline.BuildPlayer(EditorBuildSettings.scenes, path, BuildTarget.StandaloneOSX, BuildOptions.None);
        SignBinary(path);
    }

    [MenuItem("Build/Build Client")]
    public static void BuildClient()
    {
        PlayerSettings.applicationIdentifier = "com.synarea.rehab.client";
        string path = "Builds/Client/Client.app";
        BuildPipeline.BuildPlayer(EditorBuildSettings.scenes, path, BuildTarget.StandaloneOSX, BuildOptions.None);
        SignBinary(path);
    }

    private static void SignBinary(string appPath)
    {
        string binPath = $"{appPath}/Contents/StreamingAssets/mediapipe-bridge-dist/mediapipe-bridge.bin";
        var p = new Process();
        p.StartInfo.FileName = "/usr/bin/codesign";
        p.StartInfo.Arguments = $"--force --deep --sign - \"{binPath}\"";
        p.StartInfo.UseShellExecute = false;
        p.Start();
        p.WaitForExit();
        UnityEngine.Debug.Log($"Codesign fatto per {appPath}");
    }
}