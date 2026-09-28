using UnityEditor;
using UnityEngine;

/// <summary>
/// GDD 0.5: WebGL / Yandex build defaults + Unity Localization Tables.
/// </summary>
public static class Gdd05Setup
{
    private const string MenuPath = "QDG2/Apply GDD 0.5 Setup";

    [MenuItem(MenuPath)]
    public static void Apply()
    {
        ApplyWebGlPlayerSettings();
        Gdd05LocalizationSetup.Setup();
        AssetDatabase.SaveAssets();
        Debug.Log("GDD 0.5 setup applied: WebGL template QDG2 + Unity Localization Tables (UI EN/RU).");
    }

    private static void ApplyWebGlPlayerSettings()
    {
        PlayerSettings.companyName = "QDG2";
        PlayerSettings.productName = "Quick Draw Gunslinger 2";
        PlayerSettings.WebGL.template = "PROJECT:QDG2";
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.dataCaching = true;
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.FullWithoutStacktrace;
        PlayerSettings.SetStackTraceLogType(LogType.Exception, StackTraceLogType.None);

        PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
        PlayerSettings.allowedAutorotateToPortrait = true;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = true;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;
    }
}
