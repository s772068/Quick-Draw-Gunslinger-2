using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pushes Localization Tables strings onto scene labels, including texts laid out by hand.
/// Hint D/P/F and live counters are left alone.
/// </summary>
public static class SceneTextLocalizer
{
    public static void Apply()
    {
        TMP_Text[] labels = Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < labels.Length; i++)
        {
            TMP_Text label = labels[i];
            if (label == null)
                continue;

            string key = KeyFor(label.transform);
            if (string.IsNullOrEmpty(key))
                continue;

            label.text = LocalizationTables.Get(key);
        }
    }

    public static void SetButton(Button button, string key)
    {
        if (button == null || string.IsNullOrEmpty(key))
            return;

        TMP_Text tmp = button.GetComponentInChildren<TMP_Text>(true);
        if (tmp != null)
            tmp.text = LocalizationTables.Get(key);
    }

    private static string KeyFor(Transform t)
    {
        if (t.name == "Hint")
            return null;

        switch (t.name)
        {
            case "FastestScore":
            case "SteadyScore":
            case "SureScore":
            case "FastestPlace":
            case "SteadyPlace":
            case "SurePlace":
            case "RoundResult":
            case "Reason":
            case "BoardLabel":
            case "LocalBest":
            case "Rank":
            case "Name":
            case "Score":
            case "EmptyLabel":
                return null;
        }

        Transform parent = t.parent;
        if (parent != null)
        {
            string fromParent = KeyForObjectName(parent.name);
            if (!string.IsNullOrEmpty(fromParent))
                return fromParent;
        }

        if (t.name == "Label" && parent != null && parent.name == "LeaderboardsPanel")
            return LocalizationTables.Keys.LeaderboardsTitle;

        return KeyForObjectName(t.name);
    }

    private static string KeyForObjectName(string name)
    {
        switch (name)
        {
            case "StartBtn": return LocalizationTables.Keys.StartSeries;
            case "Begin": return LocalizationTables.Keys.MenuBegin;
            case "Title": return LocalizationTables.Keys.GameTitle;
            case "ReadyLabel": return LocalizationTables.Keys.Ready;
            case "Continue":
            case "PauseContinue": return LocalizationTables.Keys.Continue;
            case "Exit":
            case "ExitBtn":
            case "PauseExit": return LocalizationTables.Keys.Exit;
            case "FinishSeriesBtn": return LocalizationTables.Keys.FinishSeries;
            case "ContinueSeriesBtn": return LocalizationTables.Keys.ContinueSeries;
            case "RestartSeriesBtn": return LocalizationTables.Keys.RestartSeries;
            case "Yes": return LocalizationTables.Keys.Yes;
            case "No": return LocalizationTables.Keys.No;
            case "SureText": return LocalizationTables.Keys.AreYouSure;
            case "SeriesResultText": return LocalizationTables.Keys.SeriesResult;
            case "ScoreText": return LocalizationTables.Keys.ScoreLabel;
            case "PlaceText": return LocalizationTables.Keys.PlaceLabel;
            case "AuthText": return LocalizationTables.Keys.AuthPrompt;
            case "LoginBtn": return LocalizationTables.Keys.AuthLogin;
            case "SkipBtn": return LocalizationTables.Keys.AuthSkip;
            default: return null;
        }
    }
}
