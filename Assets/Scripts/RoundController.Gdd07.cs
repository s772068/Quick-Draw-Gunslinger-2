using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class RoundController
{
    private enum RoundOutcome
    {
        None = 0,
        Win = 1,
        FalseStart = 2,
        EnemyWin = 3
    }

    private readonly SeriesRun _series = new SeriesRun();
    private float _duelStartTime;
    private bool _duelTimerOn;
    private RoundOutcome _outcome = RoundOutcome.None;
    private bool _submitting;
    private GameObject _seriesSummary;
    private GameObject _areYouSure;
    private GameObject _authPrompt;

    private void BindSeriesUi()
    {
        if (roundFinishPanel != null)
        {
            _areYouSure = FindChild(roundFinishPanel, "AreYouSure");
            _authPrompt = SceneUiFactory.EnsureAuthPrompt(roundFinishPanel.transform);
            Transform hud = roundFinishPanel.transform.parent;
            if (hud != null)
            {
                Transform summary = hud.Find("SeriesSummary");
                if (summary != null)
                    _seriesSummary = summary.gameObject;
            }
        }
        HideSeriesOverlays();

        WireChild(roundFinishPanel, "FinishSeriesBtn", OnFinishSeriesClicked);
        WireChild(roundFinishPanel, "ContinueSeriesBtn", OnContinueSeriesClicked);
        WireChild(roundFinishPanel, "RestartSeriesBtn", OnRestartSeriesClicked);
        WireChild(roundFinishPanel, "ExitBtn", OnPauseExitClicked);
        WireChild(_areYouSure, "Yes", OnYesClicked);
        WireChild(_areYouSure, "No", OnNoClicked);
        WireChild(_authPrompt, "LoginBtn", OnLoginClicked);
        WireChild(_authPrompt, "SkipBtn", OnSkipSaveClicked);
        WireChild(_seriesSummary, "RestartSeriesBtn", OnRestartSeriesClicked);
        WireChild(_seriesSummary, "ExitBtn", OnPauseExitClicked);
        RefreshCounters();
    }

    private void BeginSeriesIfNeeded()
    {
        if (_series.Started)
            return;

        _series.Begin();
        RefreshCounters();
    }

    private void MarkDuelTimer()
    {
        _duelStartTime = Time.time;
        _duelTimerOn = true;
    }

    private float TakeDuelSeconds()
    {
        if (!_duelTimerOn)
            return 0f;

        _duelTimerOn = false;
        return Mathf.Max(0f, Time.time - _duelStartTime);
    }

    private void PresentRoundFinish(RoundOutcome outcome)
    {
        bool win = outcome == RoundOutcome.Win;
        SetPanelActive(_seriesSummary, false);
        SetPanelActive(_areYouSure, false);
        SetPanelActive(_authPrompt, false);
        SetPanelActive(roundFinishPanel, true);

        SetChildActive(roundFinishPanel, "Reason", !win);
        SetChildActive(roundFinishPanel, "RestartSeriesBtn", !win);
        SetChildActive(roundFinishPanel, "ExitBtn", !win);
        SetChildActive(roundFinishPanel, "ContinueSeriesBtn", win);
        SetChildActive(roundFinishPanel, "FinishSeriesBtn", win);

        RefreshOutcomeTexts();
        RefreshCounters();
        SceneTextLocalizer.Apply();
        RefreshOutcomeTexts();
    }

    private void RefreshOutcomeTexts()
    {
        if (_outcome == RoundOutcome.None)
            return;

        bool win = _outcome == RoundOutcome.Win;
        if (roundResultText != null)
            roundResultText.text = LocalizationTables.Get(win ? LocalizationTables.Keys.Win : LocalizationTables.Keys.Lose);

        if (!win)
        {
            string reason = _outcome == RoundOutcome.FalseStart
                ? LocalizationTables.Keys.FalseStart
                : LocalizationTables.Keys.EnemyWins;
            SetChildText(roundFinishPanel, "Reason", LocalizationTables.Get(reason));
        }
    }

    private void RefreshCounters()
    {
        SetNamedText("SureScore", _series.SureText);
        SetNamedText("FastestScore", _series.FastestText);
        SetNamedText("SteadyScore", _series.SteadyText);
    }

    private void HideSeriesOverlays()
    {
        SetPanelActive(_seriesSummary, false);
        SetPanelActive(_areYouSure, false);
        SetPanelActive(_authPrompt, false);
    }

    private void TryContinueSeriesHotkey()
    {
        if (_phase != Phase.Finished || _outcome != RoundOutcome.Win)
            return;
        if (_areYouSure != null && _areYouSure.activeInHierarchy)
            return;
        if (_authPrompt != null && _authPrompt.activeInHierarchy)
            return;
        if (_seriesSummary != null && _seriesSummary.activeInHierarchy)
            return;
        if (roundFinishPanel == null || !roundFinishPanel.activeInHierarchy)
            return;

        Transform continueBtn = roundFinishPanel.transform.Find("ContinueSeriesBtn");
        if (continueBtn == null || !continueBtn.gameObject.activeInHierarchy)
            return;

        OnContinueSeriesClicked();
    }

    private void OnContinueSeriesClicked()
    {
        if (_paused || YandexGamesSdk.PlatformPaused)
            return;

        EnterWaitingToStart();
        SetPanelActive(roundStartPanel, false);
        RefreshCounters();
        OnStartClicked();
    }

    private void OnRestartSeriesClicked()
    {
        if (_paused || YandexGamesSdk.PlatformPaused)
            return;

        _series.Clear();
        _outcome = RoundOutcome.None;
        _submitting = false;
        EnterWaitingToStart();
        SetPanelActive(roundStartPanel, false);
        RefreshCounters();
        OnStartClicked();
    }

    private void OnFinishSeriesClicked()
    {
        if (_paused || YandexGamesSdk.PlatformPaused || _outcome != RoundOutcome.Win)
            return;

        SetPanelActive(_areYouSure, true);
        if (_areYouSure != null)
            _areYouSure.transform.SetAsLastSibling();
        SetPanelActive(_authPrompt, false);
    }

    private void OnNoClicked()
    {
        SetPanelActive(_areYouSure, false);
    }

    private void OnYesClicked()
    {
        if (_submitting)
            return;

        SetPanelActive(_areYouSure, false);

#if UNITY_EDITOR
        CommitAndShowSummary(sendToYandex: false);
#else
        if (!YandexGamesSdk.SdkAvailable)
        {
            CommitAndShowSummary(sendToYandex: false);
            return;
        }

        if (YandexLeaderboards.Instance == null)
        {
            CommitAndShowSummary(sendToYandex: false);
            return;
        }

        YandexLeaderboards.Instance.QueryAuthorized(authorized =>
        {
            if (authorized)
                CommitAndShowSummary(sendToYandex: true);
            else
                ShowAuthPrompt();
        });
#endif
    }

    private void OnLoginClicked()
    {
        if (YandexLeaderboards.Instance == null)
        {
            CommitAndShowSummary(sendToYandex: false);
            return;
        }

        YandexLeaderboards.Instance.OpenAuthDialog(ok =>
        {
            if (!ok)
                return;

            CommitAndShowSummary(sendToYandex: true);
        });
    }

    private void OnSkipSaveClicked()
    {
        CommitAndShowSummary(sendToYandex: false);
    }

    private void ShowAuthPrompt()
    {
        SetPanelActive(_areYouSure, false);
        SetPanelActive(_authPrompt, true);
        if (_authPrompt != null)
            _authPrompt.transform.SetAsLastSibling();
        SceneTextLocalizer.Apply();
    }

    private void CommitAndShowSummary(bool sendToYandex)
    {
        _submitting = true;
        PersonalRecords.Commit(_series);
        SetPanelActive(roundFinishPanel, false);
        SetPanelActive(_areYouSure, false);
        SetPanelActive(_authPrompt, false);
        SetPanelActive(_seriesSummary, true);
        if (_seriesSummary != null)
            _seriesSummary.transform.SetAsLastSibling();

        RefreshCounters();
        SetChildText(_seriesSummary, "SeriesResultText", LocalizationTables.Get(LocalizationTables.Keys.SeriesResult));
        SceneTextLocalizer.Apply();
        SetChildText(_seriesSummary, "SeriesResultText", LocalizationTables.Get(LocalizationTables.Keys.SeriesResult));

        bool editorPlaces = sendToYandex == false;
        SetPlace("SurePlace", _series.Wins > 0, editorPlaces);
        SetPlace("FastestPlace", _series.TryBestSeconds(out _), editorPlaces);
        SetPlace("SteadyPlace", _series.TrySteadySeconds(out _), editorPlaces);

        if (sendToYandex)
            StartCoroutine(SubmitSeries());
        else
            _submitting = false;
    }

    private IEnumerator SubmitSeries()
    {
        if (YandexLeaderboards.Instance == null)
        {
            _submitting = false;
            yield break;
        }

        if (_series.Wins > 0)
            yield return YandexLeaderboards.Instance.Submit(YandexLeaderboards.SureBoard, _series.Wins);

        if (_series.TryBestSeconds(out float best))
            yield return YandexLeaderboards.Instance.Submit(YandexLeaderboards.FastestBoard, SeriesRun.ToMilliseconds(best));

        if (_series.TrySteadySeconds(out float steady))
            yield return YandexLeaderboards.Instance.Submit(YandexLeaderboards.SteadyBoard, SeriesRun.ToMilliseconds(steady));

        yield return FillPlace(YandexLeaderboards.SureBoard, "SurePlace", _series.Wins > 0);
        if (_series.TryBestSeconds(out _))
            yield return FillPlace(YandexLeaderboards.FastestBoard, "FastestPlace", true);
        if (_series.TrySteadySeconds(out _))
            yield return FillPlace(YandexLeaderboards.SteadyBoard, "SteadyPlace", true);

        _submitting = false;
    }

    private IEnumerator FillPlace(string board, string objectName, bool hasScore)
    {
        if (!hasScore)
        {
            SetNamedText(objectName, SeriesRun.Dash);
            yield break;
        }

        int rank = 0;
        bool done = false;
        YandexLeaderboards.Instance.LoadRank(board, value =>
        {
            rank = value;
            done = true;
        });

        float wait = 0f;
        while (!done && wait < 8f)
        {
            wait += Time.unscaledDeltaTime;
            yield return null;
        }

        SetNamedText(objectName, rank > 0 ? rank.ToString() : SeriesRun.Dash);
    }

    private static void SetPlace(string objectName, bool hasScore, bool editorPlaceholder)
    {
        if (!hasScore)
        {
            SetNamedText(objectName, SeriesRun.Dash);
            return;
        }

        SetNamedText(objectName, editorPlaceholder ? "1" : SeriesRun.Dash);
    }

    private static void WireChild(GameObject root, string childName, UnityEngine.Events.UnityAction action)
    {
        if (root == null)
            return;

        Transform t = root.transform.Find(childName);
        if (t == null)
            return;

        var button = t.GetComponent<Button>();
        if (button == null)
            return;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }

    private static void SetChildActive(GameObject root, string childName, bool active)
    {
        if (root == null)
            return;

        Transform t = root.transform.Find(childName);
        if (t != null)
            t.gameObject.SetActive(active);
    }

    private static void SetChildText(GameObject root, string childName, string value)
    {
        if (root == null)
            return;

        Transform t = root.name == childName ? root.transform : root.transform.Find(childName);
        if (t == null)
            return;

        var tmp = t.GetComponent<TMP_Text>();
        if (tmp == null)
            tmp = t.GetComponentInChildren<TMP_Text>(true);
        if (tmp != null)
            tmp.text = value;
    }

    private static void SetNamedText(string objectName, string value)
    {
        TMP_Text[] labels = Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < labels.Length; i++)
        {
            if (labels[i] != null && labels[i].gameObject.name == objectName)
                labels[i].text = value;
        }
    }

    private static GameObject FindChild(GameObject root, string childName)
    {
        if (root == null)
            return null;

        Transform t = root.transform.Find(childName);
        return t != null ? t.gameObject : null;
    }
}
