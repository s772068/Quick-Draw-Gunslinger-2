using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

/// <summary>
/// GDD 0.5 facade over Unity Localization String Tables (collection "UI").
/// WebGL: language once from Yandex SDK (fallback EN).
/// Editor: respects Game View locale dropdown; does not force system language.
/// </summary>
public static class LocalizationTables
{
    public const string TableName = "UI";
    public const string DefaultLanguage = "en";

    public static class Keys
    {
        public const string GameTitle = "game_title";
        public const string MenuBegin = "menu_begin";
        public const string StartSeries = "start_series";
        public const string Restart = "restart";
        public const string Win = "win";
        public const string Lose = "lose";
        public const string Continue = "continue";
        public const string Exit = "exit";
    }

    private static string _language = DefaultLanguage;
    private static bool _initStarted;
    private static bool _ready;
    private static bool _sdkLocaleApplied;
    private static bool _applyingLocale;

    public static string Language => _language;
    public static bool IsRussian => string.Equals(_language, "ru", StringComparison.OrdinalIgnoreCase);
    public static bool IsReady => _ready;

    public static event Action LanguageChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInit();
    }

    public static void EnsureInit()
    {
        if (_initStarted)
            return;
        _initStarted = true;

        var go = new GameObject("LocalizationTablesBootstrap");
        UnityEngine.Object.DontDestroyOnLoad(go);
        go.hideFlags = HideFlags.HideAndDontSave;
        go.AddComponent<LocalizationTablesRunner>().Run();
    }

    /// <summary>
    /// WebGL / Yandex: apply platform language once at boot.
    /// Editor: ignored so the Game View locale dropdown stays in control.
    /// </summary>
    public static void SetLanguageFromSdk(string sdkLang)
    {
        EnsureInit();

#if UNITY_EDITOR
        // Editor Play Mode: keep Unity Localization Game View selector as source of truth.
        return;
#else
        if (_sdkLocaleApplied)
            return;

        _sdkLocaleApplied = true;
        ApplyLocaleCode(MapSdkLanguage(sdkLang), invokeEvent: true);
#endif
    }

    public static string MapSdkLanguage(string sdkLang)
    {
        if (string.IsNullOrWhiteSpace(sdkLang))
            return DefaultLanguage;

        string code = sdkLang.Trim().ToLowerInvariant();
        if (code.StartsWith("ru", StringComparison.Ordinal))
            return "ru";
        if (code.StartsWith("en", StringComparison.Ordinal))
            return "en";
        return DefaultLanguage;
    }

    public static string Get(string key)
    {
        if (string.IsNullOrEmpty(key))
            return string.Empty;

        SyncLanguageFromSelectedLocale();

        try
        {
            if (LocalizationSettings.InitializationOperation.IsDone &&
                LocalizationSettings.SelectedLocale != null)
            {
                string value = LocalizationSettings.StringDatabase.GetLocalizedString(TableName, key);
                if (!string.IsNullOrEmpty(value) &&
                    !value.StartsWith("No translation found", StringComparison.Ordinal))
                    return value;
            }
        }
        catch (Exception)
        {
            // Tables / SelectedLocale not ready yet.
        }

        return GetEmbedded(key);
    }

    internal static void MarkReady()
    {
        _ready = true;
    }

    internal static void ApplyLocaleCode(string code, bool invokeEvent)
    {
        string mapped = string.IsNullOrEmpty(code) ? DefaultLanguage : code;
        _language = mapped;

        try
        {
            if (!LocalizationSettings.InitializationOperation.IsDone)
            {
                if (invokeEvent)
                    LanguageChanged?.Invoke();
                return;
            }

            var available = LocalizationSettings.AvailableLocales;
            if (available == null || available.Locales == null || available.Locales.Count == 0)
            {
                if (invokeEvent)
                    LanguageChanged?.Invoke();
                return;
            }

            Locale locale = available.GetLocale(mapped);
            if (locale == null)
                locale = available.GetLocale(DefaultLanguage);
            if (locale == null && available.Locales.Count > 0)
                locale = available.Locales[0];

            if (locale != null && LocalizationSettings.SelectedLocale != locale)
            {
                _applyingLocale = true;
                try
                {
                    LocalizationSettings.SelectedLocale = locale;
                }
                finally
                {
                    _applyingLocale = false;
                }
            }

            SyncLanguageFromSelectedLocale();
        }
        catch (Exception e)
        {
            Debug.LogWarning("LocalizationTables locale apply failed: " + e.Message);
        }

        if (invokeEvent)
            LanguageChanged?.Invoke();
    }

    private static void OnSelectedLocaleChanged(Locale locale)
    {
        if (_applyingLocale)
            return;

        SyncLanguageFromSelectedLocale();
        LanguageChanged?.Invoke();
    }

    private static void SyncLanguageFromSelectedLocale()
    {
        Locale selected = null;
        try
        {
            if (LocalizationSettings.InitializationOperation.IsDone)
                selected = LocalizationSettings.SelectedLocale;
        }
        catch (Exception)
        {
            return;
        }

        if (selected == null || string.IsNullOrEmpty(selected.Identifier.Code))
            return;

        string code = MapSdkLanguage(selected.Identifier.Code);
        if (!string.Equals(_language, code, StringComparison.OrdinalIgnoreCase))
            _language = code;
    }

    private static string GetEmbedded(string key)
    {
        bool ru = IsRussian;
        switch (key)
        {
            case Keys.GameTitle:
                return ru ? "Ковбои: быстрая рука 2" : "Quick Draw Gunslinger 2";
            case Keys.MenuBegin:
                return ru ? "Начать игру" : "Begin";
            case Keys.StartSeries:
                return ru ? "Начать серию" : "Start Series";
            case Keys.Restart:
                return ru ? "Заново" : "Restart";
            case Keys.Win:
                return ru ? "Победа" : "Win";
            case Keys.Lose:
                return ru ? "Поражение" : "Lose";
            case Keys.Continue:
                return ru ? "Продолжить" : "Continue";
            case Keys.Exit:
                return ru ? "Выйти" : "Exit";
            default:
                return key;
        }
    }

    private sealed class LocalizationTablesRunner : MonoBehaviour
    {
        public void Run()
        {
            StartCoroutine(InitRoutine());
        }

        private IEnumerator InitRoutine()
        {
            yield return LocalizationSettings.InitializationOperation;

            LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
            LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;

#if UNITY_EDITOR
            // Game View locale dropdown is the source of truth in the Editor.
            SyncLanguageFromSelectedLocale();
            if (string.IsNullOrEmpty(_language))
                _language = DefaultLanguage;
#else
            // Player / WebGL: re-apply SDK language now that LocalizationSettings is ready.
            if (_sdkLocaleApplied)
                ApplyLocaleCode(_language, invokeEvent: false);
            else
            {
                SyncLanguageFromSelectedLocale();
                if (string.IsNullOrEmpty(_language))
                    _language = DefaultLanguage;
            }
#endif

            MarkReady();
            LanguageChanged?.Invoke();
        }

        private void OnDestroy()
        {
            LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
        }
    }
}
