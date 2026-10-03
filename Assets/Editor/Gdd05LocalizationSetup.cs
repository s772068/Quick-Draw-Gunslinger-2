using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

/// <summary>
/// Creates Unity Localization Settings + UI String Table (EN/RU) for GDD 0.5.
/// Menu: QDG2 → Setup Unity Localization Tables
/// </summary>
public static class Gdd05LocalizationSetup
{
    private const string Folder = "Assets/Localization";
    private const string TableName = "UI";

    private static readonly (string key, string en, string ru)[] Entries =
    {
        ("game_title", "Quick Draw Gunslinger 2", "Ковбои: быстрая рука 2"),
        ("menu_begin", "Begin", "Начать игру"),
        ("start_series", "Start Series", "Начать серию"),
        ("restart", "Restart", "Заново"),
        ("win", "Win", "Победа"),
        ("lose", "Lose", "Поражение"),
        ("continue", "Continue", "Продолжить"),
        ("exit", "Exit", "Выйти"),
        ("ready", "Get Ready!", "Приготовиться!"),
        ("falsestart", "False start!", "Фальстарт!"),
        ("enemywins", "The enemy was faster", "Противник был быстрее"),
        ("finish_series", "Finish series", "Завершить серию"),
        ("continue_series", "Continue series", "Продолжить серию"),
        ("restart_series", "New series", "Новая серия"),
        ("yes", "Yes", "Да"),
        ("no", "No", "Нет"),
        ("are_you_sure", "Finish the series and save your result?", "Завершить серию и записать результат?"),
        ("series_result", "Series complete.\nYour result:", "Серия завершена.\nВаш результат:"),
        ("sure_hand", "Sure Hand", "Твёрдая рука"),
        ("fastest_hand", "Fastest Hand", "Быстрая рука"),
        ("steady_hand", "Steady Hand", "Верная рука"),
        ("leaderboards_title", "Leaderboards", "Таблицы лидеров"),
        ("score_label", "Score:", "Очки:"),
        ("place_label", "Place:", "Место:"),
        ("auth_prompt", "Sign in to save your result on the leaderboard.", "Войдите, чтобы записать результат в таблицу лидеров."),
        ("auth_login", "Sign in", "Войти"),
        ("auth_skip", "Don't save", "Без записи"),
        ("leaderboard_empty", "No scores yet", "Пока нет результатов"),
        ("your_record", "Your record: {0}", "Ваш рекорд: {0}"),
    };

    [MenuItem("QDG2/Setup Unity Localization Tables")]
    public static void Setup()
    {
        try
        {
            EnsureAddressables();
            EnsureFolder(Folder);
            EnsureLocalizationSettings();

            Locale en = EnsureLocale("en", "English (en)");
            Locale ru = EnsureLocale("ru", "Russian (ru)");
            if (en == null || ru == null)
            {
                EditorUtility.DisplayDialog("Localization Setup", "Failed to create EN/RU locales.", "OK");
                return;
            }

            StringTableCollection collection = FindOrCreateCollection(TableName);
            if (collection == null)
            {
                EditorUtility.DisplayDialog("Localization Setup", "Failed to create String Table Collection UI.", "OK");
                return;
            }

            EnsureTable(collection, en);
            EnsureTable(collection, ru);
            FillEntries(collection, en, ru);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("Unity Localization ready: String Table '" + TableName + "' (EN+RU). Settings=" +
                      (LocalizationEditorSettings.ActiveLocalizationSettings != null));
            EditorUtility.DisplayDialog(
                "Localization Setup",
                "Created/updated String Table \"" + TableName + "\" (EN+RU).\n\nOpen: Window → Asset Management → Localization Tables",
                "OK");
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
            EditorUtility.DisplayDialog("Localization Setup Failed", e.Message, "OK");
        }
    }

    private static void EnsureAddressables()
    {
        if (AddressableAssetSettingsDefaultObject.Settings == null)
            AddressableAssetSettingsDefaultObject.GetSettings(true);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        string[] parts = path.Split('/');
        string cur = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = cur + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(cur, parts[i]);
            cur = next;
        }
    }

    private static Locale EnsureLocale(string code, string displayName)
    {
        foreach (Locale existing in LocalizationEditorSettings.GetLocales())
        {
            if (existing != null && existing.Identifier.Code == code)
                return existing;
        }

        // Avoid Locale.CreateLocale(SystemLanguage) — in Unity 6 it can yield an empty name
        // and then CreateAsset("Assets/Localization/.asset") fails.
        Locale locale = ScriptableObject.CreateInstance<Locale>();
        locale.Identifier = new LocaleIdentifier(code);
        locale.LocaleName = displayName;
        locale.name = displayName;

        string assetPath = Folder + "/" + displayName + ".asset";
        if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(assetPath)))
            AssetDatabase.DeleteAsset(assetPath);

        AssetDatabase.CreateAsset(locale, assetPath);
        LocalizationEditorSettings.AddLocale(locale);
        EditorUtility.SetDirty(locale);
        return AssetDatabase.LoadAssetAtPath<Locale>(assetPath);
    }

    private static StringTableCollection FindOrCreateCollection(string name)
    {
        foreach (StringTableCollection c in LocalizationEditorSettings.GetStringTableCollections())
        {
            if (c != null && c.TableCollectionName == name)
                return c;
        }

        return LocalizationEditorSettings.CreateStringTableCollection(name, Folder);
    }

    private static void EnsureTable(StringTableCollection collection, Locale locale)
    {
        if (locale == null)
            return;
        if (collection.GetTable(locale.Identifier) != null)
            return;

        collection.AddNewTable(locale.Identifier);
    }

    private static void FillEntries(StringTableCollection collection, Locale en, Locale ru)
    {
        var enTable = collection.GetTable(en.Identifier) as StringTable;
        var ruTable = collection.GetTable(ru.Identifier) as StringTable;
        if (enTable == null || ruTable == null)
        {
            Debug.LogError("UI string tables missing for EN/RU. en=" + (enTable != null) + " ru=" + (ruTable != null));
            return;
        }

        Undo.RecordObject(enTable, "Fill UI_en");
        Undo.RecordObject(ruTable, "Fill UI_ru");
        Undo.RecordObject(collection.SharedData, "Fill UI Shared Data");

        for (int i = 0; i < Entries.Length; i++)
        {
            var e = Entries[i];

            StringTableEntry enEntry = enTable.GetEntry(e.key) ?? enTable.AddEntry(e.key, e.en);
            enEntry.Value = e.en;

            StringTableEntry ruEntry = ruTable.GetEntry(e.key) ?? ruTable.AddEntry(e.key, e.ru);
            ruEntry.Value = e.ru;
        }

        EditorUtility.SetDirty(enTable);
        EditorUtility.SetDirty(ruTable);
        EditorUtility.SetDirty(collection.SharedData);
        EditorUtility.SetDirty(collection);
    }

    private static void EnsureLocalizationSettings()
    {
        LocalizationSettings settings = LocalizationEditorSettings.ActiveLocalizationSettings;
        if (settings == null)
        {
            const string settingsPath = Folder + "/Localization Settings.asset";
            settings = AssetDatabase.LoadAssetAtPath<LocalizationSettings>(settingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<LocalizationSettings>();
                AssetDatabase.CreateAsset(settings, settingsPath);
            }

            LocalizationEditorSettings.ActiveLocalizationSettings = settings;
        }

        var so = new SerializedObject(settings);
        SerializedProperty projectLocale = so.FindProperty("m_ProjectLocaleIdentifier");
        if (projectLocale != null)
        {
            SerializedProperty code = projectLocale.FindPropertyRelative("m_Code");
            if (code != null)
                code.stringValue = "en";
        }

        SerializedProperty sync = so.FindProperty("m_InitializeSynchronously");
        if (sync != null)
            sync.boolValue = true;

        // Prefer English when selectors fail (no command-line / unknown system lang).
        SerializedProperty selectors = so.FindProperty("m_StartupSelectors");
        // Leave default selectors; LocalesProvider resolves AvailableLocales from project locales.

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
    }
}
