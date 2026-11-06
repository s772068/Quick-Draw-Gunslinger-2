using UnityEditor;

/// <summary>
/// GameMonetize WebGL: template QDG2_GM, Brotli, decompression fallback required by the portal.
/// </summary>
public static class Gdd093GameMonetizeSetup
{
    [MenuItem("QDG2/Apply GameMonetize WebGL Setup")]
    public static void Apply()
    {
        PlayerSettings.WebGL.template = "PROJECT:QDG2_GM";
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.dataCaching = true;
        UnityEngine.Debug.Log("GameMonetize WebGL: template QDG2_GM, Brotli, decompression fallback on.");
    }
}
