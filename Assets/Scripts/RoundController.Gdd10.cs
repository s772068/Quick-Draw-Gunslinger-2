using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// GDD 1.0: city run, round config, new aim trajectory, hit marker, $ / stars.
/// </summary>
public partial class RoundController
{
    [SerializeField] private Sprite[] cityResultStars;

    private readonly CityRun _cityRun = new CityRun();
    private AimTrajectory.Path _aimPath;
    private bool _aimPathReady;
    private float _aimDistance;
    private RoundDefinition _activeRound;
    private LevelDefinition _activeLevel;
    private Image _hitMarker;
    private Image _backgroundImage;
    private float _levelStartTime;
    private float _bellTime;
    private float _pullTime;
    private bool _loggedBellToPull;
    private float _hitboxEnterTime;
    private bool _hasHitboxEnter;
    private string _roundBellPull = "";
    private string _roundPullHitbox = "";
    private string _roundTimeToMiddle = "";
    private readonly List<BalanceRow> _balanceRows = new List<BalanceRow>();
    private ILogHandler _yandexLogHandler;
    private int _restartCity;
    private int _restartRound = 1;
    private int _restartDollars;
    private float _restartBest = float.MaxValue;
    private bool _restartReady;
    private bool _crossfireWasOverHitbox;

    private void BindGdd10()
    {
        EnsureLevelSystemPresent();
        EnsureHitMarker();
        CacheBackground();

        int city = PlayerPrefs.GetInt(MainMenuController.SelectedCityPrefsKey, 0);
        city = Mathf.Clamp(city, 0, 2);
        if (!_cityRun.Active)
            _cityRun.Begin(city);

        ApplyRoundPresentation();
        RefreshRoundStartLabels();
        RefreshHudCounters();
    }

    private static void EnsureLevelSystemPresent()
    {
        if (LevelSystem.Instance != null)
            return;
        var existing = UnityEngine.Object.FindFirstObjectByType<LevelSystem>();
        if (existing != null)
            return;
        // Runtime fallback if scene object missing.
        var go = new GameObject("LevelSystem");
        go.AddComponent<LevelSystem>();
        go.AddComponent<ScoreSystem>();
    }

    private void CacheBackground()
    {
        if (_backgroundImage != null)
            return;
        var go = GameObject.Find("Background");
        if (go != null)
            _backgroundImage = go.GetComponent<Image>();
    }

    private void EnsureHitMarker()
    {
        if (_hitMarker != null || crossfire == null)
            return;
        Transform parent = crossfire.parent != null ? crossfire.parent : crossfire;
        var existing = parent.Find("HitSprite");
        if (existing != null && existing.TryGetComponent(out _hitMarker))
        {
            _hitMarker.sprite = shotFlashSprite;
            _hitMarker.preserveAspect = true;
            return;
        }

        var go = new GameObject("HitSprite", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(48f, 48f);
        rt.anchoredPosition = Vector2.zero;
        _hitMarker = go.GetComponent<Image>();
        _hitMarker.sprite = shotFlashSprite;
        _hitMarker.color = Color.white;
        _hitMarker.preserveAspect = true;
        _hitMarker.raycastTarget = false;
        go.SetActive(false);
    }

    private void ApplyRoundPresentation()
    {
        _activeLevel = LevelSystem.Instance != null
            ? LevelSystem.Instance.GetLevel(_cityRun.CityIndex)
            : null;
        _activeRound = _activeLevel != null ? _activeLevel.GetRound(_cityRun.RoundNumber) : null;

        if (_activeRound != null)
        {
            enemyMissChance = _activeRound.enemyMissChance;
            _activeShotSprite = _activeRound.enemyShotSprite;
            HideShotFlash();
            if (_activeRound.enemySprite != null && enemyBodyImage != null)
            {
                enemyBodyImage.sprite = _activeRound.enemySprite;
                enemyBodySprite = _activeRound.enemySprite;
                _enemyIdleSprite = _activeRound.enemySprite;
            }
        }

        _loggedBellToPull = false;
        _hasHitboxEnter = false;
        _roundBellPull = "";
        _roundPullHitbox = "";
        _roundTimeToMiddle = "";
        ClearDebugShots();

        if (_activeLevel != null)
            ApplyLevelBackground(_activeLevel.backgroundHorizontal, _activeLevel.backgroundVertical);

        if (_hitMarker != null)
            _hitMarker.gameObject.SetActive(false);
        SyncHitMask();
        RefreshHitboxDebug();
    }

    private void ApplyLevelBackground(Sprite horizontal, Sprite vertical)
    {
        CacheBackground();
        OrientationBackground orientation = _backgroundImage != null
            ? _backgroundImage.GetComponent<OrientationBackground>()
            : null;
        if (orientation != null)
        {
            orientation.UseLevelSprites(horizontal, vertical);
            return;
        }

        if (_backgroundImage == null)
            return;

        bool landscape = Screen.width >= Screen.height;
        Sprite bg = landscape ? horizontal : vertical;
        if (bg != null)
            _backgroundImage.sprite = bg;
    }

    private void CaptureRestartSnapshot()
    {
        if (!_cityRun.Active)
            return;

        _restartCity = _cityRun.CityIndex;
        _restartRound = _cityRun.RoundNumber;
        _restartDollars = _cityRun.RunDollars;
        _restartBest = _cityRun.BestFactTime;
        _restartReady = true;
    }

#if UNITY_EDITOR
    private void EditorRestartRound()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        _paused = false;
        GamePauseGate.IsUserPaused = false;

        if (_restartReady)
            _cityRun.Restore(_restartCity, _restartRound, _restartDollars, _restartBest);
        else if (!_cityRun.Active)
        {
            int city = PlayerPrefs.GetInt(MainMenuController.SelectedCityPrefsKey, 0);
            _cityRun.Begin(Mathf.Clamp(city, 0, 2));
        }

        if (_enemyMissRoutine != null)
        {
            StopCoroutine(_enemyMissRoutine);
            _enemyMissRoutine = null;
        }

        if (_enemyMissRoot != null)
            _enemyMissRoot.SetActive(false);

        EnterWaitingToStart();
        SetPanelActive(roundStartPanel, false);
        OnStartClicked();
    }
#endif

    private void DebugEvent(string message)
    {
#if UNITY_EDITOR
        if (!_hitboxDebugVisible)
            return;
        Debug.Log(message);
#endif
    }

    private void TimingDebugEvent(string message)
    {
#if UNITY_EDITOR
        if (!_timingDebugEnabled)
            return;
        Debug.Log(message);
#endif
    }

    private void LogRoundOpponent()
    {
        string name = _activeRound != null ? _activeRound.LocalizedEnemyName : "?";
        DebugEvent("Противник: " + name);
    }

    private void LogLevelStartToBell()
    {
        if (_levelStartTime <= 0f || _bellTime <= 0f)
            return;
        float seconds = Mathf.Max(0f, _bellTime - _levelStartTime);
        DebugEvent("Старт → Bell: " + seconds.ToString("0.000", CultureInfo.InvariantCulture) + " с");
    }

    private void LogBellToDraw()
    {
        DebugEvent("Bell → Draw: " + FactTimeFromBell().ToString("0.000", CultureInfo.InvariantCulture) + " с");
    }

    private void LogPlayerReaction(bool hit)
    {
        string outcome = hit ? "попадание" : "промах";
        DebugEvent("Pull → выстрел игрока: "
                  + FactTimeFromPull().ToString("0.000", CultureInfo.InvariantCulture)
                  + " с (" + outcome + ")");
        LogHitboxToShot(hit);
    }

    private void NoteTimingHitboxEntered()
    {
        _hitboxEnterTime = Time.time;
        _hasHitboxEnter = true;
        if (_pullTime <= 0f)
            return;
        float seconds = Mathf.Max(0f, _hitboxEnterTime - _pullTime);
        _roundPullHitbox = FormatSeconds(seconds);
        TimingDebugEvent("Pull → Hitbox: " + _roundPullHitbox + " с");
    }

    private void LogHitboxToShot(bool hit)
    {
        if (!_hasHitboxEnter && hit)
            NoteTimingHitboxEntered();
        string hitboxHit = "";
        if (_hasHitboxEnter)
        {
            float seconds = Mathf.Max(0f, Time.time - _hitboxEnterTime);
            hitboxHit = FormatSeconds(seconds);
            string outcome = hit ? "hit" : "miss";
            TimingDebugEvent("Hitbox → shot: " + hitboxHit + " с (" + outcome + ")");
        }

        if (!TimingDebugActive)
            return;
        _balanceRows.Add(new BalanceRow
        {
            Round = _cityRun.RoundNumber,
            BellPull = _roundBellPull,
            PullHitbox = _roundPullHitbox,
            HitboxHit = hitboxHit,
            TimeToMiddle = _roundTimeToMiddle,
            Result = hit ? "hit" : "miss"
        });
    }

    private static string FormatSeconds(float seconds)
    {
        return seconds.ToString("0.000", CultureInfo.InvariantCulture);
    }

    private bool TimingDebugActive
    {
        get
        {
#if UNITY_EDITOR
            return _timingDebugEnabled;
#else
            return false;
#endif
        }
    }

    private void SetTimingDebug(bool enabled)
    {
        if (enabled)
        {
            _balanceRows.Clear();
            SilenceYandexLogs();
            Debug.Log("Режим балансировки включен");
            return;
        }

        try
        {
            WriteBalanceCsv();
        }
        finally
        {
            RestoreYandexLogs();
        }
    }

    private void SilenceYandexLogs()
    {
        if (_yandexLogHandler != null)
            return;
        _yandexLogHandler = Debug.unityLogger.logHandler;
        Debug.unityLogger.logHandler = new YandexLogSilence(_yandexLogHandler);
    }

    private void RestoreYandexLogs()
    {
        if (_yandexLogHandler == null)
            return;
        Debug.unityLogger.logHandler = _yandexLogHandler;
        _yandexLogHandler = null;
    }

    private void WriteBalanceCsv()
    {
        string dir = @"d:\00_Active_Projects\GitReps\Quick Draw Gunslinger 2\Docs\balance_log";
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "balance_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + ".csv");
        var sb = new StringBuilder();
        sb.AppendLine("round,timetomiddle,bell-pull,pull-hitbox,hitbox-hit,result");
        for (int i = 0; i < _balanceRows.Count; i++)
        {
            BalanceRow row = _balanceRows[i];
            sb.Append(row.Round.ToString(CultureInfo.InvariantCulture));
            sb.Append(',');
            sb.Append(row.TimeToMiddle);
            sb.Append(',');
            sb.Append(row.BellPull);
            sb.Append(',');
            sb.Append(row.PullHitbox);
            sb.Append(',');
            sb.Append(row.HitboxHit);
            sb.Append(',');
            sb.Append(row.Result);
            sb.AppendLine();
        }

        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        _balanceRows.Clear();
        Debug.Log("Создан файл: " + path);
    }

    private struct BalanceRow
    {
        public int Round;
        public string BellPull;
        public string PullHitbox;
        public string HitboxHit;
        public string TimeToMiddle;
        public string Result;
    }

    private sealed class YandexLogSilence : ILogHandler
    {
        private readonly ILogHandler _inner;

        public YandexLogSilence(ILogHandler inner)
        {
            _inner = inner;
        }

        public void LogFormat(LogType logType, UnityEngine.Object context, string format, params object[] args)
        {
            string rendered = format;
            if (args != null && args.Length > 0)
            {
                try { rendered = string.Format(format, args); }
                catch (FormatException) { rendered = format; }
            }

            if (IsYandexMessage(rendered))
                return;
            _inner.LogFormat(logType, context, format, args);
        }

        public void LogException(Exception exception, UnityEngine.Object context)
        {
            if (exception != null && IsYandexMessage(exception.Message))
                return;
            _inner.LogException(exception, context);
        }

        private static bool IsYandexMessage(string message)
        {
            if (string.IsNullOrEmpty(message))
                return false;
            return message.IndexOf("Yandex", StringComparison.OrdinalIgnoreCase) >= 0
                   || message.IndexOf("LoadingAPI", StringComparison.OrdinalIgnoreCase) >= 0
                   || message.IndexOf("GameplayAPI", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }

    private void LogEnemyShot(bool missed)
    {
        string outcome = missed ? "промах" : "попадание";
        DebugEvent("Bell → выстрел противника: "
                  + FactTimeFromBell().ToString("0.000", CultureInfo.InvariantCulture)
                  + " с (" + outcome + ")");
    }

    private void LogCrossfireEnteredHitbox()
    {
        DebugEvent("Прицел над хитбоксом: "
                  + FactTimeFromBell().ToString("0.000", CultureInfo.InvariantCulture)
                  + " с от Bell");
    }

    private float FactTimeFromPull()
    {
        if (_pullTime <= 0f)
            return 0f;
        return Mathf.Max(0f, Time.time - _pullTime);
    }

    private void LogBellToPull()
    {
        string seconds = FormatSeconds(FactTimeFromBell());
        _roundBellPull = seconds;
        DebugEvent("Bell → Pull: " + seconds + " с");
        TimingDebugEvent("Bell → Pull: " + seconds + " с");
    }

    private void LogMiddleCrossed()
    {
        DebugEvent("Прицел над MiddlePoint: "
                  + FactTimeFromBell().ToString("0.000", CultureInfo.InvariantCulture)
                  + " с от Bell");
    }

    private bool CrossedMiddle(float prev, float next)
    {
        float loop = _aimPath.TotalLength;
        float mark = _aimPath.LengthToMiddle;
        if (loop < 0.01f)
            return prev < mark && next >= mark;

        if (mark <= prev)
        {
            float laps = Mathf.Floor((prev - mark) / loop) + 1f;
            mark += laps * loop;
        }

        return next >= mark;
    }

    private void RefreshRoundStartLabels()
    {
        if (roundStartPanel == null)
            return;
        string city = _activeLevel != null ? _activeLevel.LocalizedName : ("City " + (_cityRun.CityIndex + 1));
        string enemy = _activeRound != null ? _activeRound.LocalizedEnemyName : ("Round " + _cityRun.RoundNumber);
        SetChildText(roundStartPanel, "TitleLine/CityName", city);
        SetChildText(roundStartPanel, "CityName", city);
        SetChildText(roundStartPanel, "RoundLabel", _cityRun.RoundNumber + " / 5");
        string numberedEnemy = $"{enemy} ({_cityRun.RoundNumber} / 5)";
        SetChildText(roundStartPanel, "EnemyName", numberedEnemy);
        SetChildText(roundStartPanel, "EnemyLine/EnemyName", numberedEnemy);
        SetChildText(roundStartPanel, "Best/BestValue", "$" + CityProgress.GetBestDollars(_cityRun.CityIndex));
        SetChildText(roundStartPanel, "RunDollars", "Your gain if you leave now");
        SetChildText(roundStartPanel, "RunDollarsValue", "$" + _cityRun.RunDollars);
    }

    private void RefreshHudCounters()
    {
        SetNamedText("RunDollarsHud", "$" + _cityRun.RunDollars);
        SetNamedText("RoundHud", _cityRun.RoundNumber + " / 5");
        // Reuse old counter slots if present.
        SetNamedText("SureScore", "$" + _cityRun.RunDollars);
        SetNamedText("FastestScore", _cityRun.RoundNumber + "/5");
    }

    private void NoteLevelStartTime()
    {
        _levelStartTime = Time.time;
    }

    private void NoteBellTime()
    {
        _bellTime = Time.time;
    }

    private void NotePullTime()
    {
        _pullTime = Time.time;
    }

    private float FactTimeFromBell()
    {
        if (_bellTime <= 0f)
            return 0f;
        return Mathf.Max(0f, Time.time - _bellTime);
    }

    private float RollTimeToMiddle()
    {
        float min = Mathf.Min(timeToMiddleRange.x, timeToMiddleRange.y);
        float max = Mathf.Max(timeToMiddleRange.x, timeToMiddleRange.y);
        if (max <= min)
            return Mathf.Max(0.05f, min);
        return UnityEngine.Random.Range(min, max);
    }

    private float CurrentEnemyReactionTime()
    {
        if (_activeRound != null)
            return _activeRound.EnemyReactionTime;
        return 2.2f;
    }

    private float CurrentAimRadius()
    {
        if (Gun != null)
            return Mathf.Max(40f, Gun.aimRadius);
        return 300f;
    }

    private void StartGdd10AimLoop()
    {
        StopCrossfireRoutine();
        NotePullTime();
        _hasHitboxEnter = false;
        _crossfireWasOverHitbox = false;
        if (!_loggedBellToPull)
        {
            _loggedBellToPull = true;
            LogBellToPull();
        }

        float timeToMiddle = TrainingTimeToMiddleOrRoll();
        _roundTimeToMiddle = FormatSeconds(timeToMiddle);
        if (TimingDebugActive)
            TimingDebugEvent("TimeToMiddle: " + _roundTimeToMiddle + " с");
        _aimPath = AimTrajectory.Build(enemyBody, CurrentAimRadius(), timeToMiddle);
        _aimPathReady = true;
        _aimDistance = 0f;
        RefreshAimDebug();
        if (crossfire != null)
        {
            // Crossfire lives in enemy parent space typically; convert middle-local to crossfire parent.
            Vector3 world = enemyBody != null ? enemyBody.TransformPoint(_aimPath.Start) : (Vector3)_aimPath.Start;
            if (crossfire.parent != null)
                crossfire.position = world;
            else
                SetCrossfireLocalPosition(_aimPath.Start);
            NoteCrossfireOverHitbox();
        }

        _crossfireRoutine = StartCoroutine(Gdd10AimLoopRoutine());
    }

    private IEnumerator Gdd10AimLoopRoutine()
    {
        if (!_aimPathReady || crossfire == null)
            yield break;

        while (_phase == Phase.Aimed || _phase == Phase.AimedAfterMiss)
        {
            if (_paused || _trainingFrozen || YandexGamesSdk.PlatformPaused)
            {
                yield return null;
                continue;
            }

            float prevDistance = _aimDistance;
            _aimDistance += _aimPath.Speed * Time.deltaTime;
            if (CrossedMiddle(prevDistance, _aimDistance))
            {
                LogMiddleCrossed();
                if (_trainingActive && _trainingStopAtMiddle)
                {
                    _aimDistance = _aimPath.LengthToMiddle;
                    Vector2 middleLocal = _aimPath.Middle;
                    Vector3 middleWorld = enemyBody != null
                        ? enemyBody.TransformPoint(middleLocal)
                        : (Vector3)middleLocal;
                    crossfire.position = middleWorld;
                    UpdateAimDimHole();
                    NoteCrossfireOverHitbox();
                    OnTrainingReachedMiddle();
                    while (_phase == Phase.Aimed || _phase == Phase.AimedAfterMiss)
                        yield return null;
                    break;
                }
            }

            Vector2 localOnEnemy = AimTrajectory.SampleAtDistance(_aimPath, _aimDistance);

            // Shake perpendicular to travel.
            float fade = 1f;
            float shake = Mathf.Sin(Time.time * crossfireShakeFrequency * Mathf.PI * 2f)
                          * crossfireShakeIntensity * fade;
            Vector2 tangent = AimTrajectory.SampleAtDistance(_aimPath, _aimDistance + 4f) - localOnEnemy;
            Vector2 perp = tangent.sqrMagnitude > 0.0001f
                ? new Vector2(-tangent.y, tangent.x).normalized
                : Vector2.right;
            Vector2 shaken = AimTrajectory.ClampToAllowedCircle(_aimPath, localOnEnemy + perp * shake);

            Vector3 world = enemyBody != null ? enemyBody.TransformPoint(shaken) : (Vector3)shaken;
            crossfire.position = world;
            UpdateAimDimHole();
            NoteCrossfireOverHitbox();
            yield return null;
        }

        _crossfireRoutine = null;
    }

    private void ShowHitMarkerAtCrossfire()
    {
        EnsureHitMarker();
        if (_hitMarker == null || crossfire == null)
            return;
        _hitMarker.gameObject.SetActive(true);
        _hitMarker.rectTransform.position = crossfire.position;
        _hitMarker.transform.SetAsLastSibling();
    }

    private void PresentRoundWinGdd10()
    {
        float fact = FactTimeFromBell();
        _cityRun.RegisterWin(fact);
        RefreshHudCounters();

        bool final = _cityRun.IsFinalRound;
        SetChildActive(roundWinPanel, "NextEnemyLine", !final);
        SetChildActive(roundWinPanel, "SecuredLine", final);
        SetChildActive(roundWinPanel, "ControlBtns/ContinueBtn", !final);
        SetChildActive(roundWinPanel, "ControlBtns/CollectBtn", final);
        SetChildActive(roundWinPanel, "Next", !final);
        if (!final)
        {
            int nextNumber = _cityRun.RoundNumber + 1;
            var next = _activeLevel?.GetRound(nextNumber);
            string nextName = next != null ? next.LocalizedEnemyName : "Enemy";
            SetChildText(roundWinPanel, "NextEnemyLine/EnemyName", $"{nextName} ({nextNumber} / 5)");
        }

        string defeated = _activeRound != null ? _activeRound.LocalizedEnemyName : "Enemy";
        SetChildText(roundWinPanel, "TitleLine/WinLoseTitle", defeated + " Defeated");
        SetChildText(roundWinPanel, "ResultRoundLine/Text", "Bounty for\n" + defeated);
        SetChildText(roundWinPanel, "ResultRoundLine/Value", "$" + _cityRun.LastRoundDollars);

        SetPanelActive(roundWinPanel, true);
        if (roundWinPanel != null)
            roundWinPanel.transform.SetAsLastSibling();

        SetChildText(roundWinPanel, "Title", LocalizationTables.Get(LocalizationTables.Keys.Win));
        SetChildText(roundWinPanel, "RoundDollars", "$" + _cityRun.LastRoundDollars);
        SetChildText(roundWinPanel, "RunDollars", "$" + _cityRun.RunDollars);
        SetChildText(roundWinPanel, "StarsLine/ResultText/Value", "$" + _cityRun.RunDollars);
        string factTime = _cityRun.LastFactTime.ToString("0.00", CultureInfo.InvariantCulture) + "s";
        SetChildText(roundWinPanel, "FactTime", factTime);
        SetChildText(roundWinPanel, "TimeLine/ResultText", factTime);
        SceneTextLocalizer.Apply();
        RefreshMenuButtonInteractable();
    }

    private void PresentRoundLoseGdd10()
    {
        string enemy = _activeRound != null ? _activeRound.LocalizedEnemyName : "The opponent";
        SetChildText(roundLosePanel, "TitleLine/WinLoseTitle", enemy + " Defeated You");
        SetChildText(roundLosePanel, "StarsLine/ResultText", $"You lost ${_cityRun.RunDollars}");
        _cityRun.Clear();
        SetPanelActive(roundLosePanel, true);
        if (roundLosePanel != null)
            roundLosePanel.transform.SetAsLastSibling();
        SetChildText(roundLosePanel, "Title", LocalizationTables.Get(LocalizationTables.Keys.Lose));
        SceneTextLocalizer.Apply();
        RefreshMenuButtonInteractable();
    }

    private void CommitCityClearAndShowLevelComplete()
    {
        if (_paused || YandexGamesSdk.PlatformPaused || !_cityRun.IsFinalRound ||
            roundWinPanel == null || !roundWinPanel.activeInHierarchy)
            return;

        SetPanelActive(roundWinPanel, false);
        int stars = _activeLevel != null
            ? _activeLevel.StarsForTotal(_cityRun.RunDollars)
            : (ScoreSystem.Instance != null ? ScoreSystem.Instance.StarsForTotal(_cityRun.RunDollars) : 0);
        float best = _cityRun.BestFactTime < float.MaxValue ? _cityRun.BestFactTime : _cityRun.LastFactTime;
        bool isNew = _cityRun.RunDollars > CityProgress.GetBestDollars(_cityRun.CityIndex);
        CityProgress.CommitCityClear(_cityRun.CityIndex, _cityRun.RunDollars, stars, best);

        SetPanelActive(levelCompletePanel, true);
        if (levelCompletePanel != null)
            levelCompletePanel.transform.SetAsLastSibling();

        string city = _activeLevel != null ? _activeLevel.LocalizedName : "City";
        SetChildText(levelCompletePanel, "CityName", city);
        SetChildText(levelCompletePanel, "TitleLine/CityNameComplete", city + " Complete!");
        SetChildText(levelCompletePanel, "TotalDollars", "$" + _cityRun.RunDollars);
        SetChildText(levelCompletePanel, "StarsLine/ResultText/Value", "$" + _cityRun.RunDollars);
        SetChildText(levelCompletePanel, "EarningsLine/Value", "$" + _cityRun.RunDollars);
        Image starsIcon = levelCompletePanel?.transform.Find("StarsLine/StarsIcon")?.GetComponent<Image>();
        if (starsIcon != null && cityResultStars != null && stars < cityResultStars.Length)
            starsIcon.sprite = cityResultStars[stars];
        int bestStored = CityProgress.GetBestDollars(_cityRun.CityIndex);
        SetChildText(levelCompletePanel, "RecordLabel",
            isNew ? "New Record" : ("Best: $" + bestStored));

        bool needNick = !PlayerIdentity.HasNickname;
        PrepareNicknameUi(needNick);

        bool nextOpen = CityProgress.IsUnlocked(_cityRun.CityIndex + 1);
        SetChildActive(levelCompletePanel, "ControlBtns/ToNextBtn", nextOpen);
        SetChildActive(levelCompletePanel, "NextCity", nextOpen);

        if (!needNick)
            HttpLeaderboards.Instance?.FlushPending();

        SceneTextLocalizer.Apply();
        RefreshMenuButtonInteractable();
    }

    private void OnNextRoundGdd10()
    {
        if (_paused || YandexGamesSdk.PlatformPaused || _cityRun.IsFinalRound ||
            roundWinPanel == null || !roundWinPanel.activeInHierarchy)
            return;
        _cityRun.AdvanceRound();
        SetPanelActive(roundWinPanel, false);
        EnterWaitingToStart();
        OnStartClicked();
    }

    private void OnRetryCityGdd10()
    {
        if (_paused || YandexGamesSdk.PlatformPaused)
            return;
        int city = PlayerPrefs.GetInt(MainMenuController.SelectedCityPrefsKey, _cityRun.CityIndex);
        _cityRun.Begin(city);
        ApplyRoundPresentation();
        RefreshRoundStartLabels();
        RefreshHudCounters();
        EnterWaitingToStart();
        SetPanelActive(roundLosePanel, false);
        SetPanelActive(levelCompletePanel, false);
        SetPanelActive(roundStartPanel, true);
    }

    private void OnNextCityGdd10()
    {
        int next = _cityRun.CityIndex + 1;
        if (!CityProgress.IsUnlocked(next))
            return;
        PlayerPrefs.SetInt(MainMenuController.SelectedCityPrefsKey, next);
        PlayerPrefs.Save();
        _cityRun.Begin(next);
        ApplyRoundPresentation();
        RefreshRoundStartLabels();
        RefreshHudCounters();
        SetPanelActive(levelCompletePanel, false);
        EnterWaitingToStart();
        SetPanelActive(roundStartPanel, true);
    }
}
