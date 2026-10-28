using UnityEditor;
using UnityEngine;

/// <summary>
/// GDD 0.92: WebGL template without Yandex SDK + Brotli for CrazyGames.
/// </summary>
public static class Gdd092CrazyGamesSetup
{
    [MenuItem("QDG2/Apply CrazyGames WebGL Setup (GDD 0.92)")]
    public static void Apply()
    {
        PlayerSettings.WebGL.template = "PROJECT:QDG2_CG";
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.dataCaching = true;
        Debug.Log("GDD 0.92 CG setup: template QDG2_CG, Brotli compression.");
    }

    [MenuItem("QDG2/Apply Yandex WebGL Setup (restore)")]
    public static void RestoreYandex()
    {
        PlayerSettings.WebGL.template = "PROJECT:QDG2";
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;
        Debug.Log("Restored Yandex WebGL: template QDG2, Gzip.");
    }
}
