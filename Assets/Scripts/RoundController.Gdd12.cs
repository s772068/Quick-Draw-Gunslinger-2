using System.Collections;
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
    private bool _trainingDrawLocked;
    private bool _trainingFireLocked;
    private bool _trainingStopAtMiddle;
    private bool _trainingAwaitingMiddle;
    private TrainingAllow _trainingAllowed = TrainingAllow.None;
    private Coroutine _trainingRoutine;

    private GameObject _trainingRoot;
    private Image _trainingOverlay;
    private RectTransform _trainingHighlightHost;
    private GameObject _trainingModal;
    private TMP_Text _trainingBody;
    private Button _trainingOk;
    private Button _trainingRetry;
    private Button _trainingReady;
    private Transform _highlightOriginalParent;
    private int _highlightOriginalSibling;
    private GameObject _highlightedObject;

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

        var all = Object.FindObjectsByType<LevelDefinition>(FindObjectsInactive.Include, FindObjectsSortMode.None);
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
        _phase = Phase.WaitingToStart;
        _bellFired = false;
        _trainingDrawLocked = true;
        _trainingFireLocked = true;
        _trainingStopAtMiddle = true;
        _trainingAwaitingMiddle = false;
        StopBellRoutine();
        StopCrossfireRoutine();
        StopEnemyRoutine();
        StopGunFxRoutine();
        SetVisible(playerGun, false);
        SetVisible(crossfire != null ? crossfire.gameObject : null, false);
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

        SetActionState(_drawActions, ActionVisualState.Inactive);
        SetActionState(_pullActions, ActionVisualState.Inactive);
        SetActionState(_fireActions, ActionVisualState.Inactive);
        SetIndicatorVisual(drawButton, ActionVisualState.Inactive);
        SetIndicatorVisual(pullButton, ActionVisualState.Inactive);
        SetIndicatorVisual(fireButton, ActionVisualState.Inactive);
        RefreshMenuButtonInteractable();
        WarmUpAudio();
        YandexGamesSdk.GameplayStart();
        NoteLevelStartTime();

        // 1) Wait for bell — on silent retry the 1.5s Ok-substitute IS the Bell delay (not doubled).
        if (_trainingSilent)
        {
            ShowTrainingOverlayOnly(FindBellHighlightTarget());
            _trainingFrozen = true;
            _trainingAllowed = TrainingAllow.None;
            yield return TrainingWait(1.5f);
            _trainingFrozen = false;
            ClearTrainingHighlight();
            HideTrainingModal();
        }
        else
        {
            yield return ShowTrainingStep(
                LocalizationTables.Keys.ModalWait,
                FindBellHighlightTarget(),
                TrainingAllow.Ok,
                requireOk: true);
            yield return TrainingWait(1.5f);
        }

        _phase = Phase.WaitingForBell;
        _bellFired = true;
        _phase = Phase.BellShown;
        MarkDuelTimer();
        NoteBellTime();
        LogLevelStartToBell();
        PlaySfx(sfxBell);
        SetBellState(fired: true);
        // No enemy shoot loop in training.

        // 2) Draw
        _trainingDrawLocked = false;
        SetActionState(_drawActions, ActionVisualState.Active);
        SetIndicatorVisual(drawButton, ActionVisualState.Active);
        yield return ShowTrainingStep(
            LocalizationTables.Keys.ModalDraw,
            FindActionHighlightTarget(_drawActions, drawButton),
            TrainingAllow.Draw,
            requireOk: false);
        while (_phase == Phase.BellShown)
            yield return null;
        ClearTrainingHighlight();
        HideTrainingModal();

        // 3) Pull
        yield return ShowTrainingStep(
            LocalizationTables.Keys.ModalPull,
            FindActionHighlightTarget(_pullActions, pullButton),
            TrainingAllow.Pull,
            requireOk: false);
        while (_phase == Phase.Drawn)
            yield return null;
        ClearTrainingHighlight();
        HideTrainingModal();

        // 4) Aim — Ok then unpause and move; Fire locked until middle
        _trainingFireLocked = true;
        yield return ShowTrainingStep(
            LocalizationTables.Keys.ModalAim,
            null,
            TrainingAllow.Ok,
            requireOk: true);
        _trainingAwaitingMiddle = true;
        while (_trainingAwaitingMiddle && (_phase == Phase.Aimed || _phase == Phase.AimedAfterMiss))
            yield return null;

        // 5) Fire at middle
        _trainingFireLocked = false;
        SetActionState(_fireActions, ActionVisualState.Active);
        SetIndicatorVisual(fireButton, ActionVisualState.Active);
        yield return ShowTrainingStep(
            LocalizationTables.Keys.ModalFire,
            FindActionHighlightTarget(_fireActions, fireButton),
            TrainingAllow.Fire,
            requireOk: false);
        while (_phase != Phase.Finished)
            yield return null;
        ClearTrainingHighlight();
        HideTrainingModal();

        yield return TrainingWait(1.5f);
        yield return ShowTrainingHitModal();
        _trainingRoutine = null;
    }

    private IEnumerator ShowTrainingStep(string bodyKey, GameObject highlight, TrainingAllow allow, bool requireOk)
    {
        if (_trainingSilent && requireOk)
        {
            // Silent Aim Ok (and any other Ok except Wait, which is handled in the sequence): 1.5s overlay pause.
            ShowTrainingOverlayOnly(highlight);
            _trainingAllowed = TrainingAllow.None;
            _trainingFrozen = true;
            yield return TrainingWait(1.5f);
            _trainingFrozen = false;
            ClearTrainingHighlight();
            HideTrainingModal();
            yield break;
        }

        if (_trainingSilent && !requireOk)
        {
            ShowTrainingOverlayOnly(highlight);
            _trainingFrozen = true;
            _trainingAllowed = allow;
            // Wait for player action on highlighted control
            Phase before = _phase;
            if (allow == TrainingAllow.Draw)
            {
                while (_phase == Phase.BellShown)
                    yield return null;
            }
            else if (allow == TrainingAllow.Pull)
            {
                while (_phase == Phase.Drawn)
                    yield return null;
            }
            else if (allow == TrainingAllow.Fire)
            {
                while (_phase != Phase.Finished)
                    yield return null;
            }
            else
            {
                yield return TrainingWait(1.5f);
            }

            _ = before;
            // Keep frozen between Pull → Aim so the crosshair does not drift before Aim Ok / silent pause.
            _trainingAllowed = TrainingAllow.None;
            yield break;
        }

        // Full modal path
        ShowTrainingModal(LocalizationTables.Get(bodyKey), highlight, showOk: requireOk, showHitButtons: false);
        _trainingFrozen = true;
        _trainingAllowed = allow;

        if (requireOk)
        {
            bool done = false;
            void OnOk() { done = true; }
            if (_trainingOk != null)
            {
                _trainingOk.onClick.RemoveAllListeners();
                _trainingOk.onClick.AddListener(OnOk);
            }

            while (!done)
            {
                if (Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame)
                    done = true;
                yield return null;
            }

            if (_trainingOk != null)
                _trainingOk.onClick.RemoveAllListeners();
            _trainingFrozen = false;
            _trainingAllowed = TrainingAllow.None;
            ClearTrainingHighlight();
            HideTrainingModal();
        }
        // Action steps: stay frozen/modal until phase advances (caller waits)
    }

    private IEnumerator ShowTrainingHitModal()
    {
        ShowTrainingModal(
            LocalizationTables.Get(LocalizationTables.Keys.ModalHit),
            null,
            showOk: false,
            showHitButtons: true);
        _trainingFrozen = true;
        _trainingAllowed = TrainingAllow.None;

        bool retry = false;
        bool ready = false;
        if (_trainingRetry != null)
        {
            _trainingRetry.onClick.RemoveAllListeners();
            _trainingRetry.onClick.AddListener(() => retry = true);
        }

        if (_trainingReady != null)
        {
            _trainingReady.onClick.RemoveAllListeners();
            _trainingReady.onClick.AddListener(() => ready = true);
        }

        while (!retry && !ready)
            yield return null;

        if (_trainingRetry != null)
            _trainingRetry.onClick.RemoveAllListeners();
        if (_trainingReady != null)
            _trainingReady.onClick.RemoveAllListeners();

        ClearTrainingHighlight();
        HideTrainingModal();
        _trainingFrozen = false;

        if (ready)
        {
            ExitTrainingToMenu();
            yield break;
        }

        // Retry silent
        TrainingSession.RequestStart(silentModals: true);
        TrainingSession.ConsumePending();
        _trainingSilent = true;
        _trainingActive = true;
        ApplyTrainingLevelPresentation();
        EnterWaitingToStart();
    }

    private void ExitTrainingToMenu()
    {
        TrainingSession.MarkCompletedAndExit();
        Time.timeScale = 1f;
        AudioListener.pause = false;
        _paused = false;
        _trainingFrozen = false;
        GamePauseGate.IsUserPaused = false;
        YandexGamesSdk.GameplayStop();
        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
    }

    private void NotifyTrainingLeftToMenu()
    {
        if (_trainingActive)
            TrainingSession.MarkCompletedAndExit();
    }

    private IEnumerator TrainingWait(float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            if (!_paused && !YandexGamesSdk.PlatformPaused)
                t += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private bool TrainingBlocksAction(TrainingAllow needed)
    {
        if (!_trainingActive)
            return false;
        if (_trainingDrawLocked && needed == TrainingAllow.Draw)
            return true;
        if (_trainingFireLocked && needed == TrainingAllow.Fire)
            return true;
        if (_trainingFrozen && _trainingAllowed != needed && needed != TrainingAllow.None)
            return true;
        return false;
    }

    private bool TrainingInputBlocked()
    {
        if (!_trainingActive)
            return false;
        return _trainingFrozen && _trainingAllowed == TrainingAllow.None;
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

        Transform hud = null;
        if (bottomPanel != null && bottomPanel.transform.parent != null)
            hud = bottomPanel.transform.parent;
        if (hud == null)
        {
            var canvas = FindFirstObjectByType<Canvas>();
            if (canvas != null)
                hud = canvas.transform.Find("HUD") ?? canvas.transform;
        }

        if (hud == null)
            return;

        var root = new GameObject("TrainingRoot", typeof(RectTransform));
        root.transform.SetParent(hud, false);
        var rootRt = root.GetComponent<RectTransform>();
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.offsetMin = Vector2.zero;
        rootRt.offsetMax = Vector2.zero;
        _trainingRoot = root;

        var overlayGo = new GameObject("Overlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        overlayGo.transform.SetParent(root.transform, false);
        var overlayRt = overlayGo.GetComponent<RectTransform>();
        overlayRt.anchorMin = Vector2.zero;
        overlayRt.anchorMax = Vector2.one;
        overlayRt.offsetMin = Vector2.zero;
        overlayRt.offsetMax = Vector2.zero;
        _trainingOverlay = overlayGo.GetComponent<Image>();
        _trainingOverlay.color = new Color(0f, 0f, 0f, 0.5f);
        _trainingOverlay.raycastTarget = true;

        var hostGo = new GameObject("HighlightHost", typeof(RectTransform));
        hostGo.transform.SetParent(root.transform, false);
        _trainingHighlightHost = hostGo.GetComponent<RectTransform>();
        _trainingHighlightHost.anchorMin = Vector2.zero;
        _trainingHighlightHost.anchorMax = Vector2.one;
        _trainingHighlightHost.offsetMin = Vector2.zero;
        _trainingHighlightHost.offsetMax = Vector2.zero;

        var modalGo = new GameObject("Modal", typeof(RectTransform));
        modalGo.transform.SetParent(root.transform, false);
        var modalRt = modalGo.GetComponent<RectTransform>();
        modalRt.anchorMin = Vector2.zero;
        modalRt.anchorMax = Vector2.one;
        modalRt.offsetMin = Vector2.zero;
        modalRt.offsetMax = Vector2.zero;
        _trainingModal = modalGo;

        var panel = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(modalGo.transform, false);
        var panelRt = panel.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.5f, 0.5f);
        panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.sizeDelta = new Vector2(520f, 280f);
        panelRt.anchoredPosition = new Vector2(0f, 80f);
        var panelImg = panel.GetComponent<Image>();
        panelImg.color = Color.white;
        panelImg.raycastTarget = true;

        var bodyGo = new GameObject("Body", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        bodyGo.transform.SetParent(panel.transform, false);
        var bodyRt = bodyGo.GetComponent<RectTransform>();
        bodyRt.anchorMin = new Vector2(0.5f, 1f);
        bodyRt.anchorMax = new Vector2(0.5f, 1f);
        bodyRt.pivot = new Vector2(0.5f, 1f);
        bodyRt.sizeDelta = new Vector2(460f, 140f);
        bodyRt.anchoredPosition = new Vector2(0f, -24f);
        _trainingBody = bodyGo.GetComponent<TextMeshProUGUI>();
        _trainingBody.fontSize = 26f;
        _trainingBody.alignment = TextAlignmentOptions.Center;
        _trainingBody.color = Color.black;
        _trainingBody.enableAutoSizing = true;
        _trainingBody.fontSizeMin = 12f;
        _trainingBody.fontSizeMax = 48f;

        _trainingOk = CreateTrainingButton(panel.transform, "Ok", new Vector2(0f, -90f));
        _trainingRetry = CreateTrainingButton(panel.transform, "Retry", new Vector2(-120f, -90f));
        _trainingReady = CreateTrainingButton(panel.transform, "Ready", new Vector2(120f, -90f));

        RaiseMenuAndSoundAboveTraining(hud);
        _trainingRoot.SetActive(false);
    }

    private static Button CreateTrainingButton(Transform parent, string name, Vector2 pos)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(200f, 64f);
        rt.anchoredPosition = pos;
        var img = go.GetComponent<Image>();
        img.color = Color.white;
        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;

        var labelGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelGo.transform.SetParent(go.transform, false);
        var labelRt = labelGo.GetComponent<RectTransform>();
        labelRt.anchorMin = Vector2.zero;
        labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = Vector2.zero;
        labelRt.offsetMax = Vector2.zero;
        var tmp = labelGo.GetComponent<TextMeshProUGUI>();
        tmp.text = name;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.black;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 12f;
        tmp.fontSizeMax = 48f;
        return btn;
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

    private void ShowTrainingOverlayOnly(GameObject highlight)
    {
        EnsureTrainingUi();
        if (_trainingRoot == null)
            return;
        _trainingRoot.SetActive(true);
        if (_trainingModal != null)
            _trainingModal.SetActive(false);
        if (_trainingOverlay != null)
            _trainingOverlay.gameObject.SetActive(true);
        SetTrainingHighlight(highlight);
        RaiseMenuAndSoundAboveTraining(_trainingRoot.transform.parent);
    }

    private void ShowTrainingModal(string body, GameObject highlight, bool showOk, bool showHitButtons)
    {
        EnsureTrainingUi();
        if (_trainingRoot == null)
            return;

        _trainingRoot.SetActive(true);
        if (_trainingOverlay != null)
            _trainingOverlay.gameObject.SetActive(true);
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

        SetTrainingHighlight(highlight);
        RaiseMenuAndSoundAboveTraining(_trainingRoot.transform.parent);
    }

    private void HideTrainingModal()
    {
        if (_trainingModal != null)
            _trainingModal.SetActive(false);
        if (_trainingRoot != null && _highlightedObject == null)
            _trainingRoot.SetActive(false);
        if (_trainingOverlay != null && _highlightedObject == null)
            _trainingOverlay.gameObject.SetActive(false);
    }

    private void SetTrainingHighlight(GameObject target)
    {
        ClearTrainingHighlight();
        if (target == null || _trainingHighlightHost == null)
            return;

        _highlightedObject = target;
        _highlightOriginalParent = target.transform.parent;
        _highlightOriginalSibling = target.transform.GetSiblingIndex();
        target.transform.SetParent(_trainingHighlightHost, true);
        target.transform.SetAsLastSibling();
    }

    private void ClearTrainingHighlight()
    {
        if (_highlightedObject == null)
            return;

        if (_highlightOriginalParent != null)
        {
            _highlightedObject.transform.SetParent(_highlightOriginalParent, true);
            int max = _highlightOriginalParent.childCount - 1;
            _highlightedObject.transform.SetSiblingIndex(Mathf.Clamp(_highlightOriginalSibling, 0, max));
        }

        _highlightedObject = null;
        _highlightOriginalParent = null;
    }

    private GameObject FindBellHighlightTarget()
    {
        if (_bellImages != null)
        {
            for (int i = 0; i < _bellImages.Count; i++)
            {
                if (_bellImages[i] != null)
                    return _bellImages[i].gameObject;
            }
        }

        var bell = GameObject.Find("Bell");
        return bell;
    }

    private static GameObject FindActionHighlightTarget(System.Collections.Generic.List<TouchActionButton> actions, Button indicator)
    {
        if (actions != null)
        {
            for (int i = 0; i < actions.Count; i++)
            {
                if (actions[i] != null && actions[i].button != null)
                    return actions[i].button.gameObject;
            }
        }

        return indicator != null ? indicator.gameObject : null;
    }
}
