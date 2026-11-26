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
        ("sure_hand_desc", "Longest win streak", "Самая продолжительная серия побед"),
        ("fastest_hand_desc", "Best single-round time", "Лучший результат в одном раунде"),
        ("steady_hand_desc", "Average of the last 10 wins in one series", "Средний результат за последние 10 побед в одной серии"),
        ("leaderboards_title", "Leaderboards", "Таблицы лидеров"),
        ("score_label", "Score:", "Очки:"),
        ("place_label", "Place:", "Место:"),
        ("auth_prompt", "Sign in to save your result on the leaderboard.", "Войдите, чтобы записать результат в таблицу лидеров."),
        ("auth_login", "Sign in", "Войти"),
        ("auth_skip", "Don't save", "Без записи"),
        ("leaderboard_empty", "No scores yet", "Пока нет результатов"),
        ("your_record", "Your record: {0}", "Ваш рекорд: {0}"),
        ("enemy_miss", "The opponent missed!", "Противник промахнулся!"),
        ("nickname_placeholder", "Nickname (2–16)", "Ник (2–16)"),
        ("submit_score", "Submit", "Отправить"),
        ("towns", "Towns", "Города"),
        ("menu_training", "Training", "Обучение"),
        ("modal_wait", "The duel begins. Get ready to hear the bell", "Начинается дуэль. Приготовься услышать звук колокола"),
        ("modal_draw", "The start signal is given — draw your revolver!", "Сигнал к старту дан, доставай револьвер!"),
        ("modal_pull", "Pull the hammer!", "Взведи курок!"),
        ("modal_aim", "Wait until the sight lines up on the enemy", "Жди, пока прицел наведётся на противника"),
        ("modal_fire", "Shoot!", "Стреляй!"),
        ("modal_hit", "Nice shot! In a real duel, you'll need to shoot first.", "Ты поразил противника. Ты закончил обучение, но помни: в реальной дуэли всё это нужно сделать до того, как выстрелит противник!"),
        ("training_retry", "Retry", "Повторить"),
        ("training_ready", "I'm ready", "Я готов"),
        ("ok", "OK", "Ок"),
        ("play", "PLAY", "ИГРАТЬ"),
        ("locked", "LOCKED", "ЗАКРЫТО"),
        ("menu_start", "Start", "Начать"),
        ("explored_towns", "Explored Towns", "Открытые города"),
        ("net_worth_leaderboard", "NET WORTH LEADERBOARD", "РЕЙТИНГ КАПИТАЛА"),
        ("player_heading", "PLAYER", "ИГРОК"),
        ("leaderboard_loading", "Loading…", "Загрузка…"),
        ("leaderboard_unavailable", "Leaderboard unavailable", "Таблица недоступна"),
        ("player_you", "You", "Вы"),
        ("pause", "Pause", "Пауза"),
        ("abort_run", "Abort run?", "Прервать прохождение?"),
        ("abort_loss", "You will lose ${0}", "Вы потеряете ${0}"),
        ("town_top_result", "Top-1 result for this town", "Лучший результат в городе"),
        ("first_target", "First Bounty Target:", "Первый противник:"),
        ("next_target", "Next Bounty Target:", "Следующий противник:"),
        ("town_secured", "Town secured", "Город зачищен"),
        ("town_earnings", "Total Earnings for This Town", "Всего заработано в городе"),
        ("gain_if_leave", "Your gain if you leave now", "Заработок при выходе"),
        ("enemy", "Opponent", "Противник"),
        ("city", "Town", "Город"),
        ("enemy_defeated", "{0} Defeated", "Победа: {0}"),
        ("enemy_defeated_you", "{0} Defeated You", "Поражение: {0}"),
        ("bounty_for", "Bounty for\n{0}", "Награда за победу\n{0}"),
        ("you_lost", "You lost ${0}", "Вы потеряли ${0}"),
        ("city_complete", "{0} Complete!", "{0}: пройдено!"),
        ("new_record", "New Record", "Новый рекорд"),
        ("best_amount", "Best: ${0}", "Рекорд: ${0}"),
        ("rank_number", "You ranked #{0}", "Ваше место: №{0}"),
        ("rank_missing", "Rank: —", "Место: —"),
        ("local_result", "Local result (Editor)", "Результат сохранён локально (редактор)"),
        ("submission_retry", "Submission will retry", "Отправим повторно"),
        ("submitting_score", "Submitting score…", "Отправляем результат…"),
        ("score_on_leaderboard", "Score on leaderboard", "Результат в рейтинге"),
        ("seconds_short", "{0}s", "{0} с"),
        ("enemy_shot_delay", "Enemy Shot delay", "Задержка выстрела противника"),
        ("city_drycreek", "Dry Creek", "Сухой Ручей"),
        ("city_deadwell", "Dead Well", "Мёртвый Колодец"),
        ("city_ravenrock", "Raven Rock", "Воронья Скала"),
        ("enemy_bungler", "Bungler", "Недотёпа"),
        ("enemy_cowboy", "Cowboy", "Ковбой"),
        ("enemy_deputy", "Deputy", "Помощник шерифа"),
        ("enemy_sheriff", "Sheriff", "Шериф"),
        ("enemy_gunslinger", "Gunslinger", "Стрелок"),
        ("enemy_reaction_time", "Enemy reaction time:\n{0}", "Время реакции противника:\n{0}"),
    };

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
            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog(
                    "Localization Setup",
                    "Created/updated String Table \"" + TableName + "\" (EN+RU).\n\nOpen: Window → Asset Management → Localization Tables",
                    "OK");
            }
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
