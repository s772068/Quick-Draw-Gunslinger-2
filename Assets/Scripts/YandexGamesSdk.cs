using System;
using System.Collections;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

/// <summary>
/// Yandex Games SDK bridge (GDD 0.5/0.8): lang, LoadingAPI, GameplayAPI, pause/resume, fullscreen ads.
/// Startup interstitial is shown by the platform; handle it via game_api_pause/resume only.
/// </summary>
[DefaultExecutionOrder(-1000)]
public class YandexGamesSdk : MonoBehaviour
{
    public static YandexGamesSdk Instance { get; private set; }

    public static bool IsReady { get; private set; }
    public static bool SdkAvailable { get; private set; }
    public static string SdkLanguage { get; private set; } = "en";
    public static bool PlatformPaused { get; private set; }

    public static event Action PlatformPausedChanged;
    public static event Action SdkReady;

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern int YandexGames_IsAvailable();
    [DllImport("__Internal")] private static extern void YandexGames_GetLanguage(byte[] buffer, int bufferSize);
    [DllImport("__Internal")] private static extern void YandexGames_LoadingReady();
    [DllImport("__Internal")] private static extern void YandexGames_GameplayStart();
    [DllImport("__Internal")] private static extern void YandexGames_GameplayStop();
    [DllImport("__Internal")] private static extern void YandexGames_BindUnityTarget(string gameObject, string pauseMethod, string resumeMethod);
    [DllImport("__Internal")] private static extern void YandexGames_ShowFullscreenAdv(string gameObject, string openMethod, string closeMethod, string errorMethod);
#endif

    private bool _gameplayActive;
    private bool _loadingReadySent;
    private bool _resumeGameplayAfterPlatformPause;
    private bool _advInFlight;
    private Action<bool> _advClosed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureExists()
    {
        if (Instance != null)
            return;

        var go = new GameObject("YandexGamesSdk");
        DontDestroyOnLoad(go);
        go.AddComponent<YandexGamesSdk>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        Bootstrap();
    }

    private void Bootstrap()
    {
        SdkLanguage = ReadLanguage();
        // Editor: LocalizationTables ignores this and follows Game View locale.
        // WebGL: applies ysdk.environment.i18n.lang once (fallback EN).
        LocalizationTables.SetLanguageFromSdk(SdkLanguage);

#if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            SdkAvailable = YandexGames_IsAvailable() != 0;
            if (SdkAvailable)
                YandexGames_BindUnityTarget(gameObject.name, nameof(OnYandexPause), nameof(OnYandexResume));
        }
        catch (Exception e)
        {
            SdkAvailable = false;
            Debug.LogWarning("YandexGamesSdk bind failed: " + e.Message);
        }
#else
        SdkAvailable = false;
#endif

        IsReady = true;
        SdkReady?.Invoke();
        StartCoroutine(SendLoadingReadyNextFrame());
    }

    private IEnumerator SendLoadingReadyNextFrame()
    {
        yield return null;
        NotifyLoadingReady();
    }

    private static string ReadLanguage()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            if (YandexGames_IsAvailable() == 0)
                return "en";

            var buf = new byte[32];
            YandexGames_GetLanguage(buf, buf.Length);
            string lang = Encoding.UTF8.GetString(buf).TrimEnd('\0', ' ', '\r', '\n');
            return string.IsNullOrEmpty(lang) ? "en" : lang;
        }
        catch (Exception e)
        {
            Debug.LogWarning("YandexGamesSdk language failed: " + e.Message);
            return "en";
        }
#else
        return Application.systemLanguage == SystemLanguage.Russian ? "ru" : "en";
#endif
    }

    public void NotifyLoadingReady()
    {
        if (_loadingReadySent)
            return;
        _loadingReadySent = true;

#if UNITY_WEBGL && !UNITY_EDITOR
        try { YandexGames_LoadingReady(); }
        catch (Exception e) { Debug.LogWarning("LoadingAPI.ready failed: " + e.Message); }
#else
        Debug.Log("[Yandex] LoadingAPI.ready()");
#endif
    }

    public static void GameplayStart()
    {
        if (Instance == null)
            return;
        Instance.InternalGameplayStart();
    }

    public static void GameplayStop()
    {
        if (Instance == null)
            return;
        Instance.InternalGameplayStop();
    }

    /// <summary>
    /// Fullscreen interstitial via ysdk.adv.showFullscreenAdv.
    /// Frequency is enforced by Yandex; onClose may report wasShown=false.
    /// </summary>
    public static void ShowFullscreenAdv(Action<bool> onClosed = null)
    {
        if (Instance == null)
        {
            onClosed?.Invoke(false);
            return;
        }

        Instance.InternalShowFullscreenAdv(onClosed);
    }

    private void InternalShowFullscreenAdv(Action<bool> onClosed)
    {
        if (_advInFlight)
        {
            onClosed?.Invoke(false);
            return;
        }

        _advInFlight = true;
        _advClosed = onClosed;

#if UNITY_WEBGL && !UNITY_EDITOR
        if (!SdkAvailable)
        {
            FinishAdv(false);
            return;
        }

        try
        {
            YandexGames_ShowFullscreenAdv(
                gameObject.name,
                nameof(OnFullscreenAdvOpen),
                nameof(OnFullscreenAdvClose),
                nameof(OnFullscreenAdvError));
        }
        catch (Exception e)
        {
            Debug.LogWarning("ShowFullscreenAdv failed: " + e.Message);
            FinishAdv(false);
        }
#else
        Debug.Log("[Yandex] showFullscreenAdv() skipped (editor/non-WebGL)");
        StartCoroutine(FinishAdvNextFrame(false));
#endif
    }

    private IEnumerator FinishAdvNextFrame(bool wasShown)
    {
        yield return null;
        FinishAdv(wasShown);
    }

    /// <summary>JS SendMessage callback.</summary>
    public void OnFullscreenAdvOpen(string _)
    {
        // Platform also fires game_api_pause; keep audio/time gated there.
        Debug.Log("[Yandex] fullscreen adv open");
    }

    /// <summary>JS SendMessage callback. Payload "1" / "0".</summary>
    public void OnFullscreenAdvClose(string payload)
    {
        bool wasShown = payload == "1";
        FinishAdv(wasShown);
    }

    /// <summary>JS SendMessage callback.</summary>
    public void OnFullscreenAdvError(string message)
    {
        Debug.LogWarning("[Yandex] fullscreen adv error: " + message);
        FinishAdv(false);
    }

    private void FinishAdv(bool wasShown)
    {
        if (!_advInFlight)
            return;

        _advInFlight = false;
        Action<bool> cb = _advClosed;
        _advClosed = null;
        cb?.Invoke(wasShown);
    }

    private void InternalGameplayStart()
    {
        if (PlatformPaused)
            return;
        if (_gameplayActive)
            return;
        _gameplayActive = true;

#if UNITY_WEBGL && !UNITY_EDITOR
        try { YandexGames_GameplayStart(); }
        catch (Exception e) { Debug.LogWarning("GameplayAPI.start failed: " + e.Message); }
#else
        Debug.Log("[Yandex] GameplayAPI.start()");
#endif
    }

    private void InternalGameplayStop()
    {
        if (!_gameplayActive)
            return;
        _gameplayActive = false;

#if UNITY_WEBGL && !UNITY_EDITOR
        try { YandexGames_GameplayStop(); }
        catch (Exception e) { Debug.LogWarning("GameplayAPI.stop failed: " + e.Message); }
#else
        Debug.Log("[Yandex] GameplayAPI.stop()");
#endif
    }

    /// <summary>Called from JS via SendMessage on game_api_pause.</summary>
    public void OnYandexPause()
    {
        if (PlatformPaused)
            return;

        PlatformPaused = true;
        _resumeGameplayAfterPlatformPause = _gameplayActive && !GamePauseGate.IsUserPaused;
        GameAudioSettings.SetPlatformMuted(true);
        Time.timeScale = 0f;
        AudioListener.pause = true;
        InternalGameplayStop();
        PlatformPausedChanged?.Invoke();
    }

    /// <summary>Called from JS via SendMessage on game_api_resume.</summary>
    public void OnYandexResume()
    {
        if (!PlatformPaused)
            return;

        PlatformPaused = false;
        GameAudioSettings.SetPlatformMuted(false);

        if (!GamePauseGate.IsUserPaused)
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            if (_resumeGameplayAfterPlatformPause)
                InternalGameplayStart();
        }

        _resumeGameplayAfterPlatformPause = false;
        PlatformPausedChanged?.Invoke();
    }
}

/// <summary>
/// Tracks in-game pause menu so platform resume does not unpause over an open menu.
/// </summary>
public static class GamePauseGate
{
    public static bool IsUserPaused { get; set; }
}
