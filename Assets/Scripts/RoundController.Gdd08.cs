using System.Collections;
using System.Globalization;
using TMPro;
using UnityEngine;

/// <summary>
/// GDD 0.8: pause/menu gates, EnemyMissMessage, balance progression, ads cadence, debug delay.
/// </summary>
public partial class RoundController
{
    private const int RoundsPerInterstitial = 10;
    private const float EnemyMissAnimDuration = 0.7f;
    private const float EnemyMissRisePx = 100f;

    private static int _roundsSinceLastAd;

    private GameObject _enemyMissRoot;
    private CanvasGroup _enemyMissGroup;
    private RectTransform _enemyMissRect;
    private TMP_Text _enemyMissLabel;
    private Vector2 _enemyMissHomePos;
    private Coroutine _enemyMissRoutine;
    private TMP_Text _enemyShotDelayDebug;

    private void BindGdd08Ui()
    {
        Transform hud = roundStartPanel != null
            ? roundStartPanel.transform.parent
            : (roundFinishPanel != null ? roundFinishPanel.transform.parent : null);
        if (hud == null)
            return;

        Transform miss = hud.Find("EnemyMissMessage");
        if (miss != null)
        {
            _enemyMissRoot = miss.gameObject;
            _enemyMissRect = miss as RectTransform;
            if (_enemyMissRect != null)
                _enemyMissHomePos = _enemyMissRect.anchoredPosition;

            _enemyMissGroup = miss.GetComponent<CanvasGroup>();
            if (_enemyMissGroup == null)
                _enemyMissGroup = miss.gameObject.AddComponent<CanvasGroup>();

            _enemyMissLabel = miss.GetComponentInChildren<TMP_Text>(true);
            _enemyMissRoot.SetActive(false);
        }

        TMP_Text[] labels = hud.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < labels.Length; i++)
        {
            if (labels[i] != null && labels[i].gameObject.name == "EnemyShotDelay_Debug")
            {
                _enemyShotDelayDebug = labels[i];
                break;
            }
        }

        ClearEnemyShotDelayDebug();
        RefreshMenuButtonInteractable();
    }

    private bool IsModalOverlayOpen()
    {
        return IsPanelShowing(roundStartPanel)
               || IsPanelShowing(roundFinishPanel)
               || IsPanelShowing(_seriesSummary)
               || IsPanelShowing(_areYouSure)
               || IsPanelShowing(_authPrompt)
               || IsPanelShowing(pauseMenuRoot);
    }

    private static bool IsPanelShowing(GameObject panel)
    {
        return panel != null && panel.activeInHierarchy;
    }

    private bool IsDuelInProgress()
    {
        return _phase != Phase.WaitingToStart && _phase != Phase.Finished;
    }

    private void RefreshMenuButtonInteractable()
    {
        if (menuButton == null)
            return;

        // Active only while the duel is running and no modal overlays are up.
        bool active = IsDuelInProgress()
                      && !_paused
                      && !IsPanelShowing(roundStartPanel)
                      && !IsPanelShowing(roundFinishPanel)
                      && !IsPanelShowing(_seriesSummary)
                      && !IsPanelShowing(_areYouSure)
                      && !IsPanelShowing(_authPrompt)
                      && !IsPanelShowing(pauseMenuRoot);
        menuButton.interactable = active;
    }

    private bool ShouldOpenPauseMenuOnPlatformPause()
    {
        if (_paused)
            return false;
        // GDD 0.8: while RoundStart / RoundFinish / etc. are up, game is already stopped.
        if (IsModalOverlayOpen())
            return false;
        return IsDuelInProgress();
    }

    private void ShowEnemyMissMessage()
    {
        if (_enemyMissRoot == null)
            return;

        if (_enemyMissRoutine != null)
            StopCoroutine(_enemyMissRoutine);
        _enemyMissRoutine = StartCoroutine(EnemyMissMessageRoutine());
    }

    private IEnumerator EnemyMissMessageRoutine()
    {
        if (_enemyMissLabel != null)
            _enemyMissLabel.text = LocalizationTables.Get(LocalizationTables.Keys.EnemyMiss);

        _enemyMissRoot.SetActive(true);
        if (_enemyMissRect != null)
            _enemyMissRect.anchoredPosition = _enemyMissHomePos;
        if (_enemyMissGroup != null)
            _enemyMissGroup.alpha = 1f;

        float elapsed = 0f;
        while (elapsed < EnemyMissAnimDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / EnemyMissAnimDuration);
            if (_enemyMissRect != null)
                _enemyMissRect.anchoredPosition = _enemyMissHomePos + Vector2.up * (EnemyMissRisePx * t);
            if (_enemyMissGroup != null)
                _enemyMissGroup.alpha = 1f - t;
            yield return null;
        }

        if (_enemyMissGroup != null)
            _enemyMissGroup.alpha = 0f;
        _enemyMissRoot.SetActive(false);
        if (_enemyMissRect != null)
            _enemyMissRect.anchoredPosition = _enemyMissHomePos;
        _enemyMissRoutine = null;
    }

    private void ClearEnemyShotDelayDebug()
    {
        if (_enemyShotDelayDebug != null)
            _enemyShotDelayDebug.text = "—";
    }

    private void WriteEnemyShotDelayDebug(float delaySeconds)
    {
        if (_enemyShotDelayDebug != null)
            _enemyShotDelayDebug.text = delaySeconds.ToString("0.00", CultureInfo.InvariantCulture);
    }

    private float RollBellDelay()
    {
        float min = Mathf.Min(bellDelayRange.x, bellDelayRange.y);
        float max = Mathf.Max(bellDelayRange.x, bellDelayRange.y);
        return Random.Range(min, max);
    }

    private float RollEnemyShotDelayForCurrentRound()
    {
        int n = Mathf.Max(1, _series.Wins + 1);
        float k = enemyShotKFloor + enemyShotKSpan * Mathf.Pow(enemyShotKDecay, n - 1);
        float l = enemyShotLFloor + enemyShotLSpan * Mathf.Pow(enemyShotLDecay, n - 1);
        if (l < k)
        {
            float swap = k;
            k = l;
            l = swap;
        }

        return Random.Range(k, l);
    }

    private void NoteRoundFinishedForAds()
    {
        _roundsSinceLastAd++;
        if (_roundsSinceLastAd < RoundsPerInterstitial)
            return;

        // Between rounds only — never mid-input during the duel.
        YandexGamesSdk.ShowFullscreenAdv(wasShown =>
        {
            if (wasShown)
                _roundsSinceLastAd = 0;
        });
    }

    private void ShowFinishSeriesAdThen(System.Action continueAction)
    {
        YandexGamesSdk.ShowFullscreenAdv(wasShown =>
        {
            if (wasShown)
                _roundsSinceLastAd = 0;
            continueAction?.Invoke();
        });
    }

    private void StyleKeyboardHints()
    {
        // Desktop: show D/P/F. Mobile: hide. Scene TMP style stays as authored.
        bool show = !Application.isMobilePlatform;
        for (int i = 0; i < _hintObjects.Count; i++)
        {
            if (_hintObjects[i] != null)
                _hintObjects[i].SetActive(show);
        }
    }
}
