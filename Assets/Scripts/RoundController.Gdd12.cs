using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// GDD 1.2 training mode: overlay modals, fixed balance, always-hit, no ads/enemy.
/// </summary>
public partial class RoundController
{
    private enum TrainingAllow
    {
        None,
        Ok,
        Draw,
        Pull,
        Fire
    }

    private bool _trainingActive;
    private bool _trainingSilent;
    private bool _trainingFrozen;
    private bool _trainingStopAtMiddle;
    private bool _trainingAwaitingMiddle;
    private TrainingAllow _trainingAllowed = TrainingAllow.None;
    private Coroutine _trainingRoutine;

    private GameObject _trainingRoot;
    private TrainingShadeGraphic _trainingOverlay;
    private readonly Vector3[] _trainingHighlightCorners = new Vector3[4];
    private Func<GameObject> _findTrainingHighlight;
    private GameObject _trainingModal;
    private TMP_Text _trainingBody;
    private Button _trainingOk;
    private Button _trainingRetry;
    private Button _trainingReady;

    private void BindGdd12Training()
    {
        if (!TrainingSession.ConsumePending())
        {
            _trainingActive = false;
            return;
        }

        _trainingActive = true;
        _trainingSilent = TrainingSession.SilentModals;
        EnsureTrainingUi();
        ApplyTrainingLevelPresentation();
    }

    private bool IsTraining => _trainingActive;
    private bool TrainingPaused => _paused || YandexGamesSdk.PlatformPaused;

    private void ApplyTrainingLevelPresentation()
    {
        LevelDefinition training = FindTrainingLevel();
        if (training == null)
            return;

        _activeLevel = training;
        _activeRound = training.GetRound(1);
        if (_activeRound != null)
        {
            enemyMissChance = 1f;
            _activeShotSprite = null;
            if (_activeRound.enemySprite != null && enemyBodyImage != null)
            {
                enemyBodyImage.sprite = _activeRound.enemySprite;
                enemyBodySprite = _activeRound.enemySprite;
                _enemyIdleSprite = _activeRound.enemySprite;
            }
        }

        if (_activeLevel != null)
            ApplyLevelBackground(_activeLevel.backgroundHorizontal, _activeLevel.backgroundVertical);
        SyncHitMask();
    }

    private static LevelDefinition FindTrainingLevel()
    {
        if (LevelSystem.Instance != null)
        {
            for (int i = 0; i < LevelSystem.Instance.LevelCount; i++)
            {
                LevelDefinition level = LevelSystem.Instance.GetLevel(i);
                if (level != null && level.cityId == "training")
                    return level;
            }
        }

        var all = FindObjectsByType<LevelDefinition>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].cityId == "training")
                return all[i];
        }

        return null;
    }

    private void EnterTrainingInsteadOfRoundStart()
    {
        SetPanelActive(roundStartPanel, false);
        SetPanelActive(roundFinishPanel, false);
        SetPanelActive(roundWinPanel, false);
        SetPanelActive(roundLosePanel, false);
        SetPanelActive(levelCompletePanel, false);
        HideSeriesOverlays();
        SetPanelActive(bottomPanel, true);
        SetPanelActive(pauseMenuRoot, false);

        if (_trainingRoutine != null)
            StopCoroutine(_trainingRoutine);
        _trainingRoutine = StartCoroutine(TrainingSequenceRoutine());
    }

    private IEnumerator TrainingSequenceRoutine()
    {
        while (true)
        {
            yield return PlayTrainingAttempt();
            bool retry = false;
            yield return ShowTrainingHitModal(() => retry = true);
            if (!retry)
                break;

            _trainingSilent = true;
        }

        _trainingRoutine = null;
        OnPauseExitClicked();
    }

    private IEnumerator PlayTrainingAttempt()
    {
        _phase = Phase.WaitingToStart;
        _bellFired = false;
        _trainingAllowed = TrainingAllow.None;
        _trainingFrozen = false;
        _trainingStopAtMiddle = true;
        _trainingAwaitingMiddle = false;
        StopBellRoutine();
        StopCrossfireRoutine();
        StopEnemyRoutine();
        StopGunFxRoutine();
        SetVisible(playerGun, false);
        SetVisible(crossfire != null ? crossfire.gameObject : null, false);
        SetVisible(_hitMarker != null ? _hitMarker.gameObject : null, false);
        SetAimDim(false);
        ResetCrossfireToStart();
        SetBellState(fired: false);
        RestoreEnemyIdleArt();
        ResetGunPose();
        if (_gunAim != null)
        {
            _gunAim.Unfreeze();
            _gunAim.SetAiming(false);
        }

        RefreshTrainingActions();
        RefreshMenuButtonInteractable();
        WarmUpAudio();
        YandexGamesSdk.GameplayStart();
        NoteLevelStartTime();

        // 1) Wait for bell — on silent retry the 1.5s Ok-substitute IS the Bell delay (not doubled).
        if (_trainingSilent)
        {
            ShowTrainingOverlayOnly(FindBellHighlightTarget);
            _trainingFrozen = true;
            yield return TrainingWait(1.5f);
            _trainingFrozen = false;
            HideTrainingModal();
        }
        else
        {
            yield return ShowTrainingStep(
                LocalizationTables.Keys.ModalWait,
                FindBellHighlightTarget,
                TrainingAllow.Ok);
            yield return TrainingWait(1.5f);
        }

        _bellFired = true;
        _phase = Phase.BellShown;
        MarkDuelTimer();
        NoteBellTime();
        LogLevelStartToBell();
        PlaySfx(sfxBell);
        SetBellState(fired: true);
        // No enemy shoot loop in training.

        // 2) Draw
        yield return ShowTrainingStep(
            LocalizationTables.Keys.ModalDraw,
            () => FindActionHighlightTarget(_drawActions, drawButton),
            TrainingAllow.Draw);

        // 3) Pull. The crosshair can start moving as soon as Pull is accepted on a retry.
        _trainingAwaitingMiddle = true;
        yield return ShowTrainingStep(
            LocalizationTables.Keys.ModalPull,
            () => FindActionHighlightTarget(_pullActions, pullButton),
            TrainingAllow.Pull);

        // 4) The first pass explains aiming before releasing the crosshair.
        if (!_trainingSilent)
        {
            ShowTrainingModal(LocalizationTables.Get(LocalizationTables.Keys.ModalAim),
                null, showOk: false, showHitButtons: false);
            yield return TrainingWait(1.5f);
            HideTrainingModal();
            _trainingFrozen = false;
        }

        while (_trainingAwaitingMiddle && (_phase == Phase.Aimed || _phase == Phase.AimedAfterMiss))
            yield return null;

        // 5) Fire at middle
        yield return ShowTrainingStep(
            LocalizationTables.Keys.ModalFire,
            () => FindActionHighlightTarget(_fireActions, fireButton),
            TrainingAllow.Fire);

        yield return TrainingWait(1.5f);
    }

    private IEnumerator ShowTrainingStep(string bodyKey, Func<GameObject> findHighlight, TrainingAllow allow)
    {
        while (TrainingPaused)
            yield return null;

        bool requireOk = allow == TrainingAllow.Ok;
        _trainingAllowed = allow;
        _trainingFrozen = true;
        RefreshTrainingActions();

        if (_trainingSilent)
            ShowTrainingOverlayOnly(findHighlight);
        else
            ShowTrainingModal(LocalizationTables.Get(bodyKey), findHighlight, showOk: requireOk, showHitButtons: false);

        if (!requireOk)
        {
            Phase before = _phase;
            while (_phase == before || TrainingPaused)
                yield return null;
        }
        else
        {
            bool done = false;
            void OnOk()
            {
                if (!TrainingPaused)
                    done = true;
            }

            if (_trainingOk != null)
                _trainingOk.onClick.AddListener(OnOk);

            while (!done || TrainingPaused)
            {
                if (Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame)
                    OnOk();
                yield return null;
            }

            if (_trainingOk != null)
                _trainingOk.onClick.RemoveListener(OnOk);
        }

        _trainingFrozen = allow == TrainingAllow.Pull && !_trainingSilent;
        _trainingAllowed = TrainingAllow.None;
        HideTrainingModal();
    }

    private IEnumerator ShowTrainingHitModal(Action requestRetry)
    {
        while (TrainingPaused)
            yield return null;

        ShowTrainingModal(
            LocalizationTables.Get(LocalizationTables.Keys.ModalHit),
            null,
            showOk: false,
            showHitButtons: true);
        _trainingFrozen = true;
        _trainingAllowed = TrainingAllow.None;

        bool retry = false;
        bool ready = false;
        void OnRetry()
        {
            if (!TrainingPaused)
                retry = true;
        }

        void OnReady()
        {
            if (!TrainingPaused)
                ready = true;
        }

        if (_trainingRetry != null)
            _trainingRetry.onClick.AddListener(OnRetry);

        if (_trainingReady != null)
            _trainingReady.onClick.AddListener(OnReady);

        while ((!retry && !ready) || TrainingPaused)
            yield return null;

        if (_trainingRetry != null)
            _trainingRetry.onClick.RemoveListener(OnRetry);
        if (_trainingReady != null)
            _trainingReady.onClick.RemoveListener(OnReady);

        HideTrainingModal();
        _trainingFrozen = false;

        if (retry && !ready)
            requestRetry();
    }

    private void NotifyTrainingLeftToMenu()
    {
        if (_trainingActive)
            TrainingSession.MarkCompletedAndExit();
    }

    private IEnumerator TrainingWait(float seconds)
    {
        float elapsed = 0f;
        while (elapsed < seconds || TrainingPaused)
        {
            yield return null;
            if (!TrainingPaused)
                elapsed += Time.unscaledDeltaTime;
        }
    }

    private bool TrainingBlocksAction(TrainingAllow needed)
    {
        return IsTraining && _trainingAllowed != needed;
    }

    private void RefreshTrainingActions()
    {
        bool aimed = _phase == Phase.Aimed;
        var allowed = TrainingPaused ? TrainingAllow.None : _trainingAllowed;
        SetTrainingAction(_drawActions, drawButton, allowed == TrainingAllow.Draw && _phase == Phase.BellShown, _phase == Phase.Drawn || aimed);
        SetTrainingAction(_pullActions, pullButton, allowed == TrainingAllow.Pull && _phase == Phase.Drawn, aimed);
        SetTrainingAction(_fireActions, fireButton, allowed == TrainingAllow.Fire && aimed, pressed: false);
    }

    private void SetTrainingAction(List<TouchActionButton> actions, Button indicator, bool active, bool pressed)
    {
        var state = pressed ? ActionVisualState.Pressed : ActionVisualState.Inactive;
        if (active)
            state = ActionVisualState.Active;
        SetActionState(actions, state);
        SetIndicatorVisual(indicator, state);
    }

    private void OnTrainingReachedMiddle()
    {
        if (!_trainingActive || !_trainingAwaitingMiddle)
            return;
        _trainingAwaitingMiddle = false;
        // Keep phase Aimed; freeze aim by stopping loop caller.
    }

    private float TrainingTimeToMiddleOrRoll()
    {
        if (_trainingActive)
            return 3f;
        return RollTimeToMiddle();
    }

    private void EnsureTrainingUi()
    {
        if (_trainingRoot != null)
            return;

        var canvas = FindFirstObjectByType<Canvas>();
        Transform hud = canvas != null ? canvas.transform.Find("HUD") : null;
        hud ??= bottomPanel != null ? bottomPanel.transform.parent : canvas?.transform;
        _trainingRoot = SceneUiFactory.EnsureTrainingRoot(hud);
        if (_trainingRoot == null)
            return;

        Transform modal = _trainingRoot.transform.Find("Modal");
        _trainingModal = modal.gameObject;
        Transform panel = modal.Find("Panel");
        _trainingBody = panel.Find("Body").GetComponent<TMP_Text>();
        _trainingOk = panel.Find("Ok").GetComponent<Button>();
        _trainingRetry = panel.Find("Retry").GetComponent<Button>();
        _trainingReady = panel.Find("Ready").GetComponent<Button>();

        var overlayGo = new GameObject("Overlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(TrainingShadeGraphic));
        overlayGo.transform.SetParent(_trainingRoot.transform, false);
        overlayGo.transform.SetAsFirstSibling();
        _trainingOverlay = overlayGo.GetComponent<TrainingShadeGraphic>();
        _trainingOverlay.rectTransform.anchorMin = Vector2.zero;
        _trainingOverlay.rectTransform.anchorMax = Vector2.one;
        _trainingOverlay.rectTransform.offsetMin = Vector2.zero;
        _trainingOverlay.rectTransform.offsetMax = Vector2.zero;
        _trainingOverlay.color = new Color32(0, 0, 0, 200);

        RaiseMenuAndSoundAboveTraining(hud);
    }

    private void RaiseMenuAndSoundAboveTraining(Transform hud)
    {
        Transform top = hud.Find("TopPanel");
        if (top != null)
            top.SetAsLastSibling();
        if (_trainingRoot != null)
            _trainingRoot.transform.SetAsLastSibling();
        if (top != null)
            top.SetAsLastSibling();
    }

    private void ShowTrainingOverlayOnly(Func<GameObject> findHighlight)
    {
        EnsureTrainingUi();
        if (_trainingRoot == null)
            return;
        _trainingRoot.SetActive(true);
        if (_trainingModal != null)
            _trainingModal.SetActive(false);
        _findTrainingHighlight = findHighlight;
        RefreshTrainingShade();
        RaiseMenuAndSoundAboveTraining(_trainingRoot.transform.parent);
    }

    private void ShowTrainingModal(string body, Func<GameObject> findHighlight, bool showOk, bool showHitButtons)
    {
        EnsureTrainingUi();
        if (_trainingRoot == null)
            return;

        _trainingRoot.SetActive(true);
        if (_trainingModal != null)
            _trainingModal.SetActive(true);
        if (_trainingBody != null)
            _trainingBody.text = body;

        if (_trainingOk != null)
        {
            _trainingOk.gameObject.SetActive(showOk);
            SetButtonLabel(_trainingOk, LocalizationTables.Keys.Ok);
        }

        if (_trainingRetry != null)
        {
            _trainingRetry.gameObject.SetActive(showHitButtons);
            SetButtonLabel(_trainingRetry, LocalizationTables.Keys.TrainingRetry);
        }

        if (_trainingReady != null)
        {
            _trainingReady.gameObject.SetActive(showHitButtons);
            SetButtonLabel(_trainingReady, LocalizationTables.Keys.TrainingReady);
        }

        _findTrainingHighlight = findHighlight;
        RefreshTrainingShade();
        RaiseMenuAndSoundAboveTraining(_trainingRoot.transform.parent);
    }

    private void HideTrainingModal()
    {
        _findTrainingHighlight = null;
        if (_trainingRoot != null)
            _trainingRoot.SetActive(false);
    }

    private void RefreshTrainingShade()
    {
        if (_trainingOverlay == null || !_trainingOverlay.gameObject.activeInHierarchy)
            return;

        var target = _findTrainingHighlight?.Invoke()?.transform as RectTransform;
        if (target == null || !target.gameObject.activeInHierarchy)
        {
            _trainingOverlay.SetHighlight(default);
            return;
        }

        // Measure the live button each frame so the cutout follows changes of orientation.
        target.GetWorldCorners(_trainingHighlightCorners);
        var overlay = _trainingOverlay.rectTransform;
        Vector2 min = overlay.InverseTransformPoint(_trainingHighlightCorners[0]);
        Vector2 max = min;
        foreach (Vector3 corner in _trainingHighlightCorners)
        {
            Vector2 point = overlay.InverseTransformPoint(corner);
            min = Vector2.Min(min, point);
            max = Vector2.Max(max, point);
        }

        _trainingOverlay.SetHighlight(Rect.MinMaxRect(min.x, min.y, max.x, max.y));
    }

    private GameObject FindBellHighlightTarget()
    {
        if (_bellImages != null)
        {
            for (int i = 0; i < _bellImages.Count; i++)
            {
                if (_bellImages[i] != null && _bellImages[i].gameObject.activeInHierarchy)
                    return _bellImages[i].gameObject;
            }
        }

        var bell = GameObject.Find("Bell");
        return bell;
    }

    private static GameObject FindActionHighlightTarget(List<TouchActionButton> actions, Button indicator)
    {
        if (actions != null)
        {
            for (int i = 0; i < actions.Count; i++)
            {
                var button = actions[i]?.button;
                if (button != null && button.gameObject.activeInHierarchy)
                    return button.gameObject;
            }
        }

        return indicator != null ? indicator.gameObject : null;
    }
}
