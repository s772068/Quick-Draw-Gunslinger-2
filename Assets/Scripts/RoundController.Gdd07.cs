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
    private TMP_InputField _nicknameInput;
    private Button _httpSubmitBtn;

    private void BindSeriesUi()
    {
        Transform hud = null;
        if (roundStartPanel != null)
            hud = roundStartPanel.transform.parent;
        else if (roundWinPanel != null)
            hud = roundWinPanel.transform.parent;
        else if (roundFinishPanel != null)
            hud = roundFinishPanel.transform.parent;

        if (hud != null)
        {
            if (roundWinPanel == null)
            {
                Transform t = hud.Find("RoundWin");
                if (t != null)
                    roundWinPanel = t.gameObject;
            }

            if (roundLosePanel == null)
            {
                Transform t = hud.Find("RoundLose");
                if (t != null)
                    roundLosePanel = t.gameObject;
            }

            if (levelCompletePanel == null)
            {
                Transform t = hud.Find("LevelComplete");
                if (t != null)
                    levelCompletePanel = t.gameObject;
            }

            if (areYouSureAbortPanel == null)
            {
                Transform t = hud.Find("AreYouSureAbort");
                if (t != null)
                    areYouSureAbortPanel = t.gameObject;
            }
        }

        _seriesSummary = levelCompletePanel;
        _areYouSure = areYouSureAbortPanel;
        _authPrompt = null;
        CacheNicknameUi();
        HideSeriesOverlays();
        SetPanelActive(roundWinPanel, false);
        SetPanelActive(roundLosePanel, false);
        SetPanelActive(levelCompletePanel, false);
        SetPanelActive(areYouSureAbortPanel, false);

        WireChild(roundStartPanel, "ExitBtn", OnAbortToMapClicked);
        WireChild(roundStartPanel, "Map", OnAbortToMapClicked);
        WireChild(roundWinPanel, "Next", OnNextRoundGdd10);
        WireChild(roundWinPanel, "Map", OnAbortToMapClicked);
        WireChild(roundLosePanel, "Retry", OnRetryCityGdd10);
        WireChild(roundLosePanel, "Map", OnPauseExitClicked);
        WireChild(areYouSureAbortPanel, "Yes", OnAbortConfirmedClicked);
        WireChild(areYouSureAbortPanel, "No", OnNoClicked);
        WireChild(levelCompletePanel, "Retry", OnRetryCityGdd10);
        WireChild(levelCompletePanel, "Map", OnPauseExitClicked);
        WireChild(levelCompletePanel, "NextCity", OnNextCityGdd10);
        WireChild(levelCompletePanel, "SubmitBtn", OnHttpSubmitClicked);
        RefreshCounters();
    }

    private void CacheNicknameUi()
    {
        _nicknameInput = null;
        _httpSubmitBtn = null;
        if (levelCompletePanel == null)
            return;

        Transform input = levelCompletePanel.transform.Find("InputNickname");
        if (input != null)
            _nicknameInput = input.GetComponent<TMP_InputField>();

        Transform submit = levelCompletePanel.transform.Find("SubmitBtn");
        if (submit != null)
            _httpSubmitBtn = submit.GetComponent<Button>();
    }

    private void OnAbortToMapClicked()
    {
        if (_paused || YandexGamesSdk.PlatformPaused)
            return;

        if (areYouSureAbortPanel == null)
        {
            OnPauseExitClicked();
            return;
        }

        SetPanelActive(areYouSureAbortPanel, true);
        areYouSureAbortPanel.transform.SetAsLastSibling();
        RefreshMenuButtonInteractable();
    }

    private void OnAbortConfirmedClicked()
    {
        SetPanelActive(areYouSureAbortPanel, false);
        _series.Clear();
        _cityRun.Clear();
        Time.timeScale = 1f;
        AudioListener.pause = false;
        _paused = false;
        GamePauseGate.IsUserPaused = false;
        OnPauseExitClicked();
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
        SetPanelActive(roundWinPanel, false);
        SetPanelActive(roundLosePanel, false);
        SetPanelActive(levelCompletePanel, false);
        SetPanelActive(areYouSureAbortPanel, false);
        SetPanelActive(_seriesSummary, false);
        SetPanelActive(_areYouSure, false);
        SetPanelActive(_authPrompt, false);
        SetPanelActive(roundFinishPanel, false);

        if (win)
            PresentRoundWinGdd10();
        else
            PresentRoundLoseGdd10();

        RefreshOutcomeTexts();
        RefreshCounters();
        RefreshHudCounters();
    }

    private void RefreshOutcomeTexts()
    {
        if (_outcome == RoundOutcome.None)
            return;

        bool win = _outcome == RoundOutcome.Win;
        string title = LocalizationTables.Get(win ? LocalizationTables.Keys.Win : LocalizationTables.Keys.Lose);
        if (roundResultText != null)
            roundResultText.text = title;

        if (win)
        {
            SetChildText(roundWinPanel, "Title", title);
        }
        else
        {
            SetChildText(roundLosePanel, "Title", title);
            string reason = _outcome == RoundOutcome.FalseStart
                ? LocalizationTables.Keys.FalseStart
                : LocalizationTables.Keys.EnemyWins;
            SetChildText(roundLosePanel, "Reason", LocalizationTables.Get(reason));
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
        SetPanelActive(roundWinPanel, false);
        SetPanelActive(roundLosePanel, false);
        SetPanelActive(levelCompletePanel, false);
        SetPanelActive(areYouSureAbortPanel, false);
        SetPanelActive(_seriesSummary, false);
        SetPanelActive(_areYouSure, false);
        SetPanelActive(_authPrompt, false);
    }

    private void TryContinueSeriesHotkey()
    {
        if (_phase != Phase.Finished || _outcome != RoundOutcome.Win)
            return;
        if (areYouSureAbortPanel != null && areYouSureAbortPanel.activeInHierarchy)
            return;
        if (levelCompletePanel != null && levelCompletePanel.activeInHierarchy)
            return;
        if (roundWinPanel == null || !roundWinPanel.activeInHierarchy)
            return;

        Transform nextBtn = roundWinPanel.transform.Find("Next");
        if (nextBtn == null || !nextBtn.gameObject.activeInHierarchy)
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
        RefreshMenuButtonInteractable();
    }

    private void OnNoClicked()
    {
        SetPanelActive(areYouSureAbortPanel, false);
        SetPanelActive(_areYouSure, false);
        RefreshMenuButtonInteractable();
    }

    private void OnHttpSubmitClicked()
    {
        if (_submitting || !PlatformLeaderboards.UseHttp)
            return;

        string nick = _nicknameInput != null ? _nicknameInput.text : PlayerIdentity.Nickname;
        nick = PlayerIdentity.SanitizeName(nick);
        if (string.IsNullOrEmpty(nick))
        {
            Debug.LogWarning("Nickname required (2–16 chars)");
            return;
        }

        PlayerIdentity.Nickname = nick;
        if (_nicknameInput != null)
            _nicknameInput.text = nick;

        SetChildActive(levelCompletePanel, "InputNickname", false);
        SetChildActive(levelCompletePanel, "SubmitBtn", false);
        StartCoroutine(SubmitCityBoards());
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
        _submitting = false;
        SetPanelActive(_areYouSure, false);
        SetPanelActive(_authPrompt, true);
        if (_authPrompt != null)
            _authPrompt.transform.SetAsLastSibling();
        SceneTextLocalizer.Apply();
        RefreshMenuButtonInteractable();
    }

    private void CommitAndShowSummary(bool sendToYandex)
    {
        _submitting = true;
        // Interstitial on confirmed finish-series. Yandex may skip it; GameMonetize returns immediately and pauses from SDK events.
        ShowFinishSeriesAdThen(() => CommitAndShowSummaryAfterAd(sendToYandex));
    }

    private void CommitAndShowSummaryAfterAd(bool sendToYandex)
    {
        PersonalRecords.Commit(_series);
        SetPanelActive(roundWinPanel, false);
        SetPanelActive(roundLosePanel, false);
        SetPanelActive(roundFinishPanel, false);
        SetPanelActive(areYouSureAbortPanel, false);
        SetPanelActive(_areYouSure, false);
        SetPanelActive(_authPrompt, false);
        SetPanelActive(levelCompletePanel, true);
        if (levelCompletePanel != null)
            levelCompletePanel.transform.SetAsLastSibling();

        RefreshCounters();
        SetChildButtonInteractable(levelCompletePanel, "Retry", true);
        SetChildButtonInteractable(levelCompletePanel, "Map", true);
        SetChildButtonInteractable(levelCompletePanel, "NextCity", true);
        RefreshMenuButtonInteractable();
        SetChildText(levelCompletePanel, "CityName", LocalizationTables.Get(LocalizationTables.Keys.SeriesResult));
        SceneTextLocalizer.Apply();

        bool httpPath = !sendToYandex && PlatformLeaderboards.UseHttp;
        PrepareNicknameUi(httpPath);

        if (sendToYandex)
            StartCoroutine(SubmitSeriesYandex());
        else
            _submitting = false;
    }

    private void PrepareNicknameUi(bool showHttpSubmit)
    {
        CacheNicknameUi();
        SetChildActive(levelCompletePanel, "InputNickname", showHttpSubmit);
        SetChildActive(levelCompletePanel, "SubmitBtn", showHttpSubmit);

        if (!showHttpSubmit)
            return;

        if (_nicknameInput != null)
        {
            _nicknameInput.characterLimit = 16;
            if (PlayerIdentity.HasNickname)
                _nicknameInput.text = PlayerIdentity.Nickname;
            if (_nicknameInput.placeholder is TMP_Text ph)
                ph.text = LocalizationTables.Get(LocalizationTables.Keys.NicknamePlaceholder);
        }

        if (_httpSubmitBtn != null)
        {
            _httpSubmitBtn.interactable = true;
            var label = _httpSubmitBtn.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
                label.text = LocalizationTables.Get(LocalizationTables.Keys.SubmitScore);
        }
    }

    private IEnumerator SubmitSeriesYandex()
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

    private IEnumerator SubmitSeriesHttp(string nick)
    {
        _submitting = true;
        if (_httpSubmitBtn != null)
            _httpSubmitBtn.interactable = false;

        if (_series.Wins > 0)
            yield return PlatformLeaderboards.Submit(PlatformLeaderboards.SureBoard, _series.Wins, nick);

        if (_series.TryBestSeconds(out float best))
            yield return PlatformLeaderboards.Submit(PlatformLeaderboards.FastestBoard, SeriesRun.ToMilliseconds(best), nick);

        if (_series.TrySteadySeconds(out float steady))
            yield return PlatformLeaderboards.Submit(PlatformLeaderboards.SteadyBoard, SeriesRun.ToMilliseconds(steady), nick);

        yield return FillPlace(PlatformLeaderboards.SureBoard, "SurePlace", _series.Wins > 0);
        if (_series.TryBestSeconds(out _))
            yield return FillPlace(PlatformLeaderboards.FastestBoard, "FastestPlace", true);
        if (_series.TrySteadySeconds(out _))
            yield return FillPlace(PlatformLeaderboards.SteadyBoard, "SteadyPlace", true);

        if (_httpSubmitBtn != null)
            _httpSubmitBtn.interactable = true;
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
        PlatformLeaderboards.LoadRank(board, value =>
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
