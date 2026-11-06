using System;
using System.Runtime.InteropServices;

/// <summary>
/// Interstitial router. GameMonetize showBanner() has no close callback, so the placement
/// returns immediately and pause/resume arrives later via SDK_GAME_PAUSE / SDK_GAME_START.
/// Otherwise the call falls through to Yandex showFullscreenAdv.
/// </summary>
public static class PlatformAds
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern int GameMonetize_IsAvailable();
    [DllImport("__Internal")] private static extern void GameMonetize_ShowBanner();
#endif

    public static bool GameMonetizeAvailable
    {
        get
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            try { return GameMonetize_IsAvailable() != 0; }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning("GameMonetize availability check failed: " + e.Message);
                return false;
            }
#else
            return false;
#endif
        }
    }

    /// <summary>
    /// Request a fullscreen video ad at a player-initiated break.
    /// onClosed(wasShown): Yandex reports the real result. GameMonetize invokes onClosed(true)
    /// immediately so the round counter advances and the series summary is not blocked.
    /// </summary>
    public static void ShowInterstitial(Action<bool> onClosed = null)
    {
        if (GameMonetizeAvailable)
        {
            try
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                GameMonetize_ShowBanner();
#endif
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning("GameMonetize showBanner failed: " + e.Message);
            }

            onClosed?.Invoke(true);
            return;
        }

        YandexGamesSdk.ShowFullscreenAdv(onClosed);
    }
}
