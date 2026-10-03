using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// Duel core loop for GDD 0.4. Scene objects stay editable in Scene view.
/// </summary>
public partial class RoundController : MonoBehaviour
{
    public enum ActionVisualState
    {
        Inactive = 0,
        Active = 1,
        Pressed = 2
    }

    [System.Serializable]
    public class TouchActionButton
    {
        public Button button;
        public Image image;
        [Tooltip("Optional fallbacks if Button Sprite State slots are empty.")]
        public Sprite inactiveSprite;
        public Sprite activeSprite;
        public Sprite pressedSprite;
    }

    [Header("World")]
    [SerializeField] private RectTransform enemyBody;
    [SerializeField] private Image enemyBodyImage;
    [SerializeField] private Sprite enemyBodySprite;
    [SerializeField] private Sprite enemyFiredSprite;
    [SerializeField] private RectTransform crossfire;
    [SerializeField] private Transform crossfireStart;
    [SerializeField] private Transform crossfireTarget;
    [SerializeField] private GameObject playerGun;
    [SerializeField] private RectTransform playerGunRect;
    [SerializeField] private RectTransform gunDirection;
    [SerializeField] private Image gunImage;
    [SerializeField] private Sprite playerGunExtendedSprite;
    [SerializeField] private Image aimDimOverlay;
    [SerializeField] [Range(0f, 1f)] private float opaqueAlphaThreshold = 0.1f;

    [Header("HUD Panels")]
    [SerializeField] private GameObject roundStartPanel;
    [SerializeField] private GameObject bottomPanel;
    [SerializeField] private GameObject roundFinishPanel;
    [SerializeField] private TMP_Text roundResultText;
    [SerializeField] private TMP_Text readyLabel;
    [SerializeField] private GameObject verticalControls;
    [SerializeField] private GameObject horizontalControls;
    [SerializeField] private GameObject topPanel;
    [SerializeField] private CanvasGroup gameplayHudGroup;

    [Header("Buttons")]
    [SerializeField] private Button startButton;
    [FormerlySerializedAs("signalButton")]
    [SerializeField] private Button bellButton;
    [SerializeField] private Button drawButton;
    [SerializeField] private Button pullButton;
    [SerializeField] private Button fireButton;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button menuButton;
    [SerializeField] private Button soundButton;
    [SerializeField] private Image soundImage;
    [SerializeField] private Sprite soundOnSprite;
    [SerializeField] private Sprite soundOffSprite;

    [Header("Pause Menu")]
    [SerializeField] private GameObject pauseMenuRoot;
    [SerializeField] private Button pauseExitButton;
    [SerializeField] private Button pauseContinueButton;

    [Header("Touch Controls")]
    [SerializeField] private TouchActionButton drawTouch;
    [SerializeField] private TouchActionButton pullTouch;
    [SerializeField] private TouchActionButton fireTouch;

    [Header("Bell")]
    [SerializeField] private Sprite bellSprite1;
    [SerializeField] private Sprite bellSprite2;

    [Header("Timing")]
    [FormerlySerializedAs("signalDelaySeconds")]
    [SerializeField] private float bellDelaySeconds = 1f;
    [FormerlySerializedAs("crossfireMoveDuration")]
    [SerializeField] private float aimDuration = 1f;
    [SerializeField] private float aimAfterMissDuration = 0.5f;
    [SerializeField] private float missBounceUp = 40f;
    [SerializeField] private float missBounceDuration = 0.12f;
    [SerializeField] private float missToMidDuration = 0.25f;
    [SerializeField] private Vector2 enemyShotDelayRange = new Vector2(1.4f, 2.2f);
    [SerializeField] private Vector2 enemyReshotDelayRange = new Vector2(0.5f, 0.7f);
    [SerializeField] [Range(0f, 1f)] private float enemyMissChance = 0.25f;

    [Header("Crossfire Shake")]
    [Tooltip("How far the crosshair wobbles sideways from the straight line (UI units). Higher = stronger shake.")]
    [FormerlySerializedAs("aimShakeAmplitude")]
    [SerializeField] private float crossfireShakeIntensity = 12f;
    [Tooltip("How many full side-to-side cycles per second. Higher = faster tremor.")]
    [FormerlySerializedAs("aimShakeFrequency")]
    [SerializeField] private float crossfireShakeFrequency = 18f;
    [Tooltip("0 = shake stays full strength until the end. 1 = shake fades to ~0 by the time the crosshair reaches the target.")]
    [SerializeField] [Range(0f, 1f)] private float crossfireShakeFadeByEnd = 0.35f;

    [Header("Aim Feel")]
    [SerializeField] private Color aimDimColor = new Color(0f, 0f, 0f, 0.45f);
    [Tooltip("Clear radius inside the crosshair stays at full brightness (UI units). 0 = auto from Crossfire size.")]
    [SerializeField] private float aimDimHoleRadius = 0f;
    [Tooltip("Soft edge of the bright hole (UI units).")]
    [SerializeField] private float aimDimHoleSoftness = 18f;
    [SerializeField] private float gunShakeAmount = 6f;
    [SerializeField] private float gunShakeDuration = 0.18f;
    [SerializeField] private float gunRecoilAmount = 28f;
    [SerializeField] private float gunRecoilDuration = 0.2f;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip sfxEagle;
    [SerializeField] private AudioClip sfxBell;
    [SerializeField] private AudioClip sfxDraw;
    [SerializeField] private AudioClip sfxGunPull;
    [SerializeField] private AudioClip sfxPlayerShot;
    [SerializeField] private AudioClip sfxEnemyShot;
    [SerializeField] private AudioClip sfxEnemyDie;
    [SerializeField] private AudioClip sfxPlayerHit;
    [SerializeField] private AudioClip sfxMissPlayer;
    [SerializeField] private AudioClip sfxMissEnemy;

    private enum Phase
    {
        WaitingToStart,
        WaitingForBell,
        BellShown,
        Drawn,
        Aimed,
        MissRecovery,
        AimedAfterMiss,
        Finished
    }

    private Phase _phase = Phase.WaitingToStart;
    private bool _bellFired;
    private bool _waitingForRepull;
    private bool _repullRequested;
    private bool _paused;
    private Coroutine _bellRoutine;
    private Coroutine _crossfireRoutine;
    private Coroutine _enemyRoutine;
    private Coroutine _gunFxRoutine;

    private ColorBlock _defaultColors;
    private Vector3 _gunRestLocalPos;
    private Quaternion _gunRestLocalRot;
    private Sprite _enemyIdleSprite;
    private Material _aimDimMaterial;
    private PlayerGunAim _gunAim;
    private readonly List<TouchActionButton> _drawActions = new List<TouchActionButton>();
    private readonly List<TouchActionButton> _pullActions = new List<TouchActionButton>();
    private readonly List<TouchActionButton> _fireActions = new List<TouchActionButton>();
    private readonly List<Image> _bellImages = new List<Image>();
    private readonly List<GameObject> _hintObjects = new List<GameObject>();
    private static readonly int HoleCenterId = Shader.PropertyToID("_HoleCenter");
    private static readonly int HoleRadiusId = Shader.PropertyToID("_HoleRadius");
    private static readonly int HoleSoftnessId = Shader.PropertyToID("_HoleSoftness");
    private static readonly Color PressedTint = new Color(0.55f, 0.55f, 0.55f, 1f);
    private static readonly Color DisabledTint = new Color(0.75f, 0.75f, 0.75f, 0.6f);
    private static readonly Color ActiveTint = Color.white;

    private void Awake()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        GameAudioSettings.ApplyListenerVolume();

        AutoBindSceneRefs();

        if (drawButton != null)
            _defaultColors = drawButton.colors;

        if (enemyBodyImage == null && enemyBody != null)
            enemyBodyImage = enemyBody.GetComponent<Image>();

        if (playerGunRect == null && playerGun != null)
            playerGunRect = playerGun.GetComponent<RectTransform>();

        if (playerGunRect != null)
        {
            _gunRestLocalPos = playerGunRect.localPosition;
            _gunRestLocalRot = playerGunRect.localRotation;
        }

        if (enemyBodyImage != null)
        {
            _enemyIdleSprite = enemyBodySprite != null ? enemyBodySprite : enemyBodyImage.sprite;
            if (enemyBodySprite == null)
                enemyBodySprite = _enemyIdleSprite;
        }

        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        ApplyGunArt();
        EnsureAimDimOverlay();
        EnsurePauseMenu();
        EnsureOrientationControls();
        EnsureGunAim();
        CollectActionButtons();
        ConfigureBells();
        ConfigureBottomPanelAsIndicators();
        ApplyHintVisibility();
        WireButtons();
        RefreshSoundVisual();
        ApplyLocalizedTexts();
        BindSeriesUi();
        EnterWaitingToStart();
        YandexGamesSdk.GameplayStop();
    }

    private void OnEnable()
    {
        LocalizationTables.LanguageChanged += ApplyLocalizedTexts;
        YandexGamesSdk.PlatformPausedChanged += OnPlatformPauseChanged;
    }

    private void OnDisable()
    {
        LocalizationTables.LanguageChanged -= ApplyLocalizedTexts;
        YandexGamesSdk.PlatformPausedChanged -= OnPlatformPauseChanged;
        GamePauseGate.IsUserPaused = false;
    }

    private void Start()
    {
        // GDD: eagle SFX on GameScene load, not on round Start.
        PlaySfx(sfxEagle);
    }

    private void Update()
    {
        if (_paused || YandexGamesSdk.PlatformPaused)
            return;
        HandleKeyboardInput();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        // On Yandex, game_api_pause/resume covers focus + ads.
        if (YandexGamesSdk.SdkAvailable)
            return;

        // Always mute on focus loss (req 1.3). Full pause only in player builds.
        GameAudioSettings.SetPlatformMuted(!hasFocus);
#if !UNITY_EDITOR
        if (!hasFocus)
        {
            if (!_paused)
                OpenPauseMenu();
        }
        else if (!GamePauseGate.IsUserPaused)
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }
#endif
    }

    private void OnPlatformPauseChanged()
    {
        // Platform pause freezes input via Update gate; menu state stays as-is.
        if (YandexGamesSdk.PlatformPaused)
        {
            if (!_paused)
                OpenPauseMenu();
            else
                SetGameplayHudInteractable(false);
        }
        else if (!_paused)
            ReassertActionInteractable();
    }

    private void ApplyLocalizedTexts()
    {
        SceneTextLocalizer.Apply();
        SetButtonLabel(startButton, LocalizationTables.Keys.StartSeries);
        SetButtonLabel(pauseContinueButton, LocalizationTables.Keys.Continue);
        SetButtonLabel(pauseExitButton, LocalizationTables.Keys.Exit);

        if (readyLabel != null)
            readyLabel.text = LocalizationTables.Get(LocalizationTables.Keys.Ready);

        RefreshOutcomeTexts();
        RefreshCounters();
    }

    private static void SetButtonLabel(Button button, string key)
    {
        if (button == null)
            return;

        var tmp = button.GetComponentInChildren<TMP_Text>(true);
        if (tmp != null)
            tmp.text = LocalizationTables.Get(key);

        var ui = button.GetComponentInChildren<Text>(true);
        if (ui != null)
            ui.text = LocalizationTables.Get(key);
    }

    private void HandleKeyboardInput()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null)
            return;

        if (kb.dKey.wasPressedThisFrame)
            OnDrawClicked();
        if (kb.pKey.wasPressedThisFrame)
            OnPullClicked();
        if (kb.fKey.wasPressedThisFrame)
            OnFireClicked();
        if (kb.spaceKey.wasPressedThisFrame)
            TryContinueSeriesHotkey();
    }

    private void AutoBindSceneRefs()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
            return;

        Transform hud = canvas.transform.Find("HUD");
        if (hud == null)
            hud = canvas.transform;

        if (verticalControls == null)
        {
            var t = hud.Find("VerticalControls");
            if (t != null)
                verticalControls = t.gameObject;
        }

        if (horizontalControls == null)
        {
            var t = hud.Find("HorizontalControls");
            if (t != null)
                horizontalControls = t.gameObject;
        }

        if (topPanel == null)
        {
            var t = hud.Find("TopPanel");
            if (t != null)
                topPanel = t.gameObject;
        }

        if (roundStartPanel == null)
        {
            var t = hud.Find("RoundStart");
            if (t == null)
                t = hud.Find("RoundStartPanel");
            if (t != null)
                roundStartPanel = t.gameObject;
        }

        if (readyLabel == null && roundStartPanel != null)
        {
            var t = roundStartPanel.transform.Find("ReadyLabel");
            if (t != null)
                readyLabel = t.GetComponent<TMP_Text>();
        }

        if (readyLabel == null)
        {
            var readyGo = GameObject.Find("ReadyLabel");
            if (readyGo != null)
                readyLabel = readyGo.GetComponent<TMP_Text>();
        }

        if (roundFinishPanel == null)
        {
            var t = hud.Find("RoundFinish");
            if (t != null)
                roundFinishPanel = t.gameObject;
        }

        if (topPanel != null)
        {
            if (menuButton == null)
            {
                var t = topPanel.transform.Find("Menu");
                if (t != null)
                    menuButton = t.GetComponent<Button>();
            }

            if (soundButton == null)
            {
                var t = topPanel.transform.Find("Sound");
                if (t != null)
                    soundButton = t.GetComponent<Button>();
            }
        }

        if (soundImage == null && soundButton != null)
            soundImage = soundButton.targetGraphic as Image;

        if (playerGun != null)
        {
            if (gunDirection == null)
            {
                var t = playerGun.transform.Find("Direction");
                if (t != null)
                    gunDirection = t as RectTransform;
            }

            if (gunImage == null)
            {
                var gun = playerGun.transform.Find("Gun");
                if (gun != null)
                    gunImage = gun.GetComponent<Image>();
            }
        }

        if (pauseMenuRoot == null)
        {
            var t = hud.Find("PauseMenu");
            if (t != null)
                pauseMenuRoot = t.gameObject;
        }
    }

    private void CollectActionButtons()
    {
        _drawActions.Clear();
        _pullActions.Clear();
        _fireActions.Clear();
        _bellImages.Clear();
        _hintObjects.Clear();

        CollectFromRoot(verticalControls != null ? verticalControls.transform : null);
        CollectFromRoot(horizontalControls != null ? horizontalControls.transform : null);

        // Keep inspector vertical refs as fallback if discovery failed.
        AddActionUnique(_drawActions, drawTouch);
        AddActionUnique(_pullActions, pullTouch);
        AddActionUnique(_fireActions, fireTouch);
    }

    private void CollectFromRoot(Transform root)
    {
        if (root == null)
            return;

        CollectAction(root, "DrawBtn", _drawActions);
        CollectAction(root, "PullBtn", _pullActions);
        CollectAction(root, "FireBtn", _fireActions);

        Transform bell = root.Find("Bell");
        if (bell != null)
        {
            var img = bell.GetComponent<Image>();
            if (img != null && !_bellImages.Contains(img))
                _bellImages.Add(img);

            var btn = bell.GetComponent<Button>();
            if (btn != null)
            {
                // SpriteSwap + non-interactable forces DisabledSprite and overrides Image.sprite.
                btn.transition = Selectable.Transition.None;
                btn.interactable = false;
                btn.onClick.RemoveAllListeners();
            }
        }

        foreach (var hint in root.GetComponentsInChildren<Transform>(true))
        {
            if (hint.name == "Hint" && !_hintObjects.Contains(hint.gameObject))
                _hintObjects.Add(hint.gameObject);
        }
    }

    private static void CollectAction(Transform root, string childName, List<TouchActionButton> list)
    {
        Transform t = root.Find(childName);
        if (t == null)
            return;

        var button = t.GetComponent<Button>();
        var image = t.GetComponent<Image>();
        if (button == null)
            return;

        var action = new TouchActionButton
        {
            button = button,
            image = image,
            inactiveSprite = button.spriteState.disabledSprite,
            activeSprite = button.spriteState.highlightedSprite,
            pressedSprite = button.spriteState.pressedSprite
        };
        AddActionUnique(list, action);
    }

    private static void AddActionUnique(List<TouchActionButton> list, TouchActionButton action)
    {
        if (action == null || action.button == null)
            return;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] != null && list[i].button == action.button)
                return;
        }
        list.Add(action);
    }

    private void ApplyGunArt()
    {
        if (gunImage == null || playerGunExtendedSprite == null)
            return;
        gunImage.sprite = playerGunExtendedSprite;
    }

    private void ApplyHintVisibility()
    {
        bool show = !Input.touchSupported;
        for (int i = 0; i < _hintObjects.Count; i++)
        {
            if (_hintObjects[i] != null)
                _hintObjects[i].SetActive(show);
        }
    }

    private void EnsureOrientationControls()
    {
        if (verticalControls == null || horizontalControls == null)
            return;

        Transform parent = verticalControls.transform.parent;
        if (parent == null)
            return;

        var existing = parent.GetComponent<OrientationControls>();
        if (existing == null)
            existing = parent.gameObject.AddComponent<OrientationControls>();

        existing.Configure(verticalControls, horizontalControls);
    }

    private void EnsureGunAim()
    {
        if (playerGun == null)
            return;

        _gunAim = playerGun.GetComponent<PlayerGunAim>();
        if (_gunAim == null)
            _gunAim = playerGun.AddComponent<PlayerGunAim>();
        _gunAim.Configure(playerGunRect, gunDirection, crossfire);
        _gunAim.SetAiming(false);
    }

    private void EnsurePauseMenu()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
            return;

        Transform hud = canvas.transform.Find("HUD");
        if (hud == null)
            hud = canvas.transform;

        if (pauseMenuRoot == null)
            pauseMenuRoot = SceneUiFactory.EnsurePauseMenu(hud);

        if (pauseContinueButton == null && pauseMenuRoot != null)
        {
            var t = pauseMenuRoot.transform.Find("Panel/PauseContinue");
            if (t == null)
                t = pauseMenuRoot.transform.Find("Panel/Continue");
            if (t != null)
                pauseContinueButton = t.GetComponent<Button>();
        }

        if (pauseExitButton == null && pauseMenuRoot != null)
        {
            var t = pauseMenuRoot.transform.Find("Panel/PauseExit");
            if (t == null)
                t = pauseMenuRoot.transform.Find("Panel/Exit");
            if (t != null)
                pauseExitButton = t.GetComponent<Button>();
        }

        SetPanelActive(pauseMenuRoot, false);
    }

    private static Button CreatePauseButton(Transform parent, string name, string label, Vector2 anchoredPos)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(280f, 70f);
        rt.anchoredPosition = anchoredPos;

        var img = go.GetComponent<Image>();
        img.color = new Color(0.85f, 0.75f, 0.55f, 1f);

        var button = go.GetComponent<Button>();
        button.targetGraphic = img;

        var textGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(go.transform, false);
        StretchFull(textGo.GetComponent<RectTransform>());
        var text = textGo.GetComponent<TextMeshProUGUI>();
        text.text = label;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.black;
        text.fontSize = 28;
        text.raycastTarget = false;
        var font = TMP_Settings.defaultFontAsset;
        if (font != null)
        {
            text.font = font;
            text.fontSharedMaterial = font.material;
        }

        return button;
    }

    private static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
    }

    private void EnsureAimDimOverlay()
    {
        Transform parent = null;
        Transform crossfireBlock = null;

        if (crossfire != null)
        {
            crossfireBlock = crossfire.parent;
            if (crossfireBlock != null)
                parent = crossfireBlock.parent;
        }

        if (parent == null && enemyBody != null)
            parent = enemyBody.parent;

        if (parent == null)
            return;

        if (aimDimOverlay == null)
        {
            var existing = parent.Find("AimDim");
            if (existing != null)
                aimDimOverlay = existing.GetComponent<Image>();
        }

        if (aimDimOverlay == null)
        {
            var go = new GameObject("AimDim", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            aimDimOverlay = go.GetComponent<Image>();
            aimDimOverlay.raycastTarget = false;
            go.SetActive(false);
        }

        aimDimOverlay.color = aimDimColor;
        EnsureAimDimMaterial();
        PlaceAimDimUnderCrossfire(parent, crossfireBlock);
    }

    private void EnsureAimDimMaterial()
    {
        if (aimDimOverlay == null)
            return;

        if (_aimDimMaterial == null)
        {
            var shader = Shader.Find("UI/AimDimHole");
            if (shader == null)
            {
                Debug.LogWarning("[RoundController] Shader UI/AimDimHole not found. Aim dim hole disabled.");
                return;
            }

            _aimDimMaterial = new Material(shader);
            _aimDimMaterial.SetColor("_Color", Color.white);
        }

        aimDimOverlay.material = _aimDimMaterial;
        UpdateAimDimHole();
    }

    private void LateUpdate()
    {
        if (aimDimOverlay != null && aimDimOverlay.gameObject.activeInHierarchy)
            UpdateAimDimHole();
    }

    private void OnDestroy()
    {
        if (_paused)
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            GamePauseGate.IsUserPaused = false;
        }

        if (_aimDimMaterial != null)
        {
            if (Application.isPlaying)
                Destroy(_aimDimMaterial);
            else
                DestroyImmediate(_aimDimMaterial);
            _aimDimMaterial = null;
        }
    }

    private void UpdateAimDimHole()
    {
        if (_aimDimMaterial == null || aimDimOverlay == null || crossfire == null)
            return;

        var dimRt = aimDimOverlay.rectTransform;
        Canvas canvas = aimDimOverlay.canvas;
        Camera eventCam = null;
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            eventCam = canvas.worldCamera;

        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(eventCam, crossfire.position);
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(dimRt, screenPoint, eventCam, out Vector2 localPoint))
            return;

        float radius = aimDimHoleRadius;
        if (radius <= 0.01f)
            radius = Mathf.Max(crossfire.rect.width, crossfire.rect.height) * 0.45f;

        _aimDimMaterial.SetVector(HoleCenterId, new Vector4(localPoint.x, localPoint.y, 0f, 0f));
        _aimDimMaterial.SetFloat(HoleRadiusId, radius);
        _aimDimMaterial.SetFloat(HoleSoftnessId, Mathf.Max(0f, aimDimHoleSoftness));
    }

    private static void PlaceAimDimUnderCrossfire(Transform parent, Transform crossfireBlock)
    {
        if (parent == null)
            return;

        var dim = parent.Find("AimDim");
        if (dim == null)
            return;

        dim.SetParent(parent, false);

        if (crossfireBlock != null && crossfireBlock.parent == parent)
            dim.SetSiblingIndex(crossfireBlock.GetSiblingIndex());
        else
            dim.SetAsLastSibling();
    }

    private void ConfigureBells()
    {
        if (bellSprite1 == null || bellSprite2 == null)
        {
            for (int i = 0; i < _bellImages.Count; i++)
            {
                var btn = _bellImages[i] != null ? _bellImages[i].GetComponent<Button>() : null;
                if (btn == null)
                    continue;
                if (bellSprite1 == null)
                    bellSprite1 = btn.spriteState.disabledSprite;
                if (bellSprite2 == null)
                    bellSprite2 = btn.spriteState.pressedSprite;
                if (bellSprite1 != null && bellSprite2 != null)
                    break;
            }
        }

        SetBellState(fired: false);
    }

    private void ConfigureBottomPanelAsIndicators()
    {
        ConfigureIndicatorButton(drawButton);
        ConfigureIndicatorButton(pullButton);
        ConfigureIndicatorButton(fireButton);
    }

    private static void ConfigureIndicatorButton(Button button)
    {
        if (button == null)
            return;

        button.transition = Selectable.Transition.None;
        button.interactable = false;
        button.onClick.RemoveAllListeners();

        if (button.targetGraphic != null)
            button.targetGraphic.raycastTarget = false;
    }

    private void WireButtons()
    {
        if (startButton != null)
        {
            startButton.onClick.RemoveAllListeners();
            startButton.onClick.AddListener(OnStartClicked);
        }

        if (restartButton != null)
        {
            restartButton.onClick.RemoveAllListeners();
            if (restartButton.gameObject.name == "FinishSeriesBtn")
                restartButton.onClick.AddListener(OnFinishSeriesClicked);
            else
                restartButton.onClick.AddListener(OnRestartSeriesClicked);
        }

        if (menuButton != null)
        {
            menuButton.onClick.RemoveAllListeners();
            menuButton.onClick.AddListener(OnMenuClicked);
        }

        if (soundButton != null)
        {
            soundButton.onClick.RemoveAllListeners();
            soundButton.onClick.AddListener(OnSoundClicked);
        }

        if (pauseContinueButton != null)
        {
            pauseContinueButton.onClick.RemoveAllListeners();
            pauseContinueButton.onClick.AddListener(OnPauseContinueClicked);
        }

        if (pauseExitButton != null)
        {
            pauseExitButton.onClick.RemoveAllListeners();
            pauseExitButton.onClick.AddListener(OnPauseExitClicked);
        }

        for (int i = 0; i < _drawActions.Count; i++)
            WireTouchButton(_drawActions[i], OnDrawClicked);
        for (int i = 0; i < _pullActions.Count; i++)
            WireTouchButton(_pullActions[i], OnPullClicked);
        for (int i = 0; i < _fireActions.Count; i++)
            WireTouchButton(_fireActions[i], OnFireClicked);
    }

    private static void WireTouchButton(TouchActionButton touch, UnityEngine.Events.UnityAction action)
    {
        if (touch == null || touch.button == null)
            return;

        touch.button.onClick.RemoveAllListeners();
        touch.button.onClick.AddListener(action);
        touch.button.transition = Selectable.Transition.SpriteSwap;

        if (touch.image == null)
            touch.image = touch.button.targetGraphic as Image;
    }

    private void EnterWaitingToStart()
    {
        _phase = Phase.WaitingToStart;
        _bellFired = false;
        _waitingForRepull = false;
        _repullRequested = false;
        StopBellRoutine();
        StopCrossfireRoutine();
        StopEnemyRoutine();
        StopGunFxRoutine();

        SetPanelActive(roundStartPanel, true);
        SetPanelActive(roundFinishPanel, false);
        HideSeriesOverlays();
        SetPanelActive(bottomPanel, true);
        SetPanelActive(pauseMenuRoot, false);

        SetVisible(playerGun, false);
        SetVisible(crossfire != null ? crossfire.gameObject : null, false);
        SetAimDim(false);
        ResetCrossfireToStart();
        SetBellState(fired: false);
        RestoreEnemyIdleArt();
        ResetGunPose();
        if (_gunAim != null)
            _gunAim.SetAiming(false);

        SetActionState(_drawActions, ActionVisualState.Inactive);
        SetActionState(_pullActions, ActionVisualState.Inactive);
        SetActionState(_fireActions, ActionVisualState.Inactive);
        SetIndicatorVisual(drawButton, ActionVisualState.Inactive);
        SetIndicatorVisual(pullButton, ActionVisualState.Inactive);
        SetIndicatorVisual(fireButton, ActionVisualState.Inactive);
    }

    private void OnStartClicked()
    {
        if (_paused || YandexGamesSdk.PlatformPaused || _phase != Phase.WaitingToStart)
            return;

        BeginSeriesIfNeeded();
        SetPanelActive(roundStartPanel, false);
        _phase = Phase.WaitingForBell;
        SetActionState(_drawActions, ActionVisualState.Active);
        SetIndicatorVisual(drawButton, ActionVisualState.Active);
        YandexGamesSdk.GameplayStart();
        _bellRoutine = StartCoroutine(BellCountdown());
    }

    private IEnumerator BellCountdown()
    {
        yield return new WaitForSeconds(bellDelaySeconds);

        if (_phase != Phase.WaitingForBell)
            yield break;

        _bellFired = true;
        _phase = Phase.BellShown;
        MarkDuelTimer();
        SetBellState(fired: true);
        PlaySfx(sfxBell);
        _enemyRoutine = StartCoroutine(EnemyShootLoop());
    }

    private void OnDrawClicked()
    {
        if (_paused || YandexGamesSdk.PlatformPaused)
            return;

        if (_phase == Phase.WaitingForBell && !_bellFired)
        {
            FinishRound(RoundOutcome.FalseStart);
            return;
        }

        if (_phase != Phase.BellShown)
            return;

        SetActionState(_drawActions, ActionVisualState.Pressed);
        SetActionState(_pullActions, ActionVisualState.Active);
        SetIndicatorVisual(drawButton, ActionVisualState.Pressed);
        SetIndicatorVisual(pullButton, ActionVisualState.Active);
        SetVisible(playerGun, true);
        PlaySfx(sfxDraw);
        _phase = Phase.Drawn;
    }

    private void OnPullClicked()
    {
        if (_paused || YandexGamesSdk.PlatformPaused)
            return;

        if (_phase == Phase.Drawn)
        {
            SetActionState(_pullActions, ActionVisualState.Pressed);
            SetActionState(_fireActions, ActionVisualState.Active);
            SetIndicatorVisual(pullButton, ActionVisualState.Pressed);
            SetIndicatorVisual(fireButton, ActionVisualState.Active);
            SetVisible(crossfire != null ? crossfire.gameObject : null, true);
            SetAimDim(true);
            if (_gunAim != null)
                _gunAim.SetAiming(true);
            PlaySfx(sfxGunPull);
            PlayGunShake();
            StartAimMove(fromStart: true);
            _phase = Phase.Aimed;
            return;
        }

        if (_phase == Phase.MissRecovery && _waitingForRepull)
        {
            _repullRequested = true;
            SetActionState(_pullActions, ActionVisualState.Pressed);
            SetActionState(_fireActions, ActionVisualState.Active);
            SetIndicatorVisual(pullButton, ActionVisualState.Pressed);
            SetIndicatorVisual(fireButton, ActionVisualState.Active);
            PlaySfx(sfxGunPull);
            PlayGunShake();
        }
    }

    private void OnFireClicked()
    {
        if (_paused || YandexGamesSdk.PlatformPaused)
            return;

        if (_phase != Phase.Aimed && _phase != Phase.AimedAfterMiss)
            return;

        PlaySfx(sfxPlayerShot);
        PlayGunRecoil();
        SetActionState(_fireActions, ActionVisualState.Pressed);
        SetIndicatorVisual(fireButton, ActionVisualState.Pressed);

        if (IsCrossfireOverOpaqueEnemy())
        {
            StopCrossfireRoutine();
            StopEnemyRoutine();
            PlaySfx(sfxEnemyDie);
            FinishRound(RoundOutcome.Win);
            return;
        }

        PlaySfx(sfxMissPlayer);
        BeginMissRecovery();
    }

    private void OnMenuClicked()
    {
        if (_paused || YandexGamesSdk.PlatformPaused)
            return;
        OpenPauseMenu();
    }

    private void OnSoundClicked()
    {
        GameAudioSettings.ToggleSound();
        RefreshSoundVisual();
    }

    private void OnPauseContinueClicked()
    {
        ClosePauseMenu();
    }

    private void OnPauseExitClicked()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        _paused = false;
        GamePauseGate.IsUserPaused = false;
        YandexGamesSdk.GameplayStop();
        SceneManager.LoadScene("MainMenu");
    }

    private void OpenPauseMenu()
    {
        _paused = true;
        GamePauseGate.IsUserPaused = true;
        Time.timeScale = 0f;
        AudioListener.pause = true;
        YandexGamesSdk.GameplayStop();
        SetGameplayHudInteractable(false);
        if (pauseMenuRoot != null)
            pauseMenuRoot.transform.SetAsLastSibling();
        SetPanelActive(pauseMenuRoot, true);

        // Keep TopPanel Menu/Sound usable; PauseMenu buttons usable.
        if (menuButton != null)
            menuButton.interactable = true;
        if (soundButton != null)
            soundButton.interactable = true;
        if (pauseContinueButton != null)
            pauseContinueButton.interactable = true;
        if (pauseExitButton != null)
            pauseExitButton.interactable = true;
    }

    private void ClosePauseMenu()
    {
        SetPanelActive(pauseMenuRoot, false);
        SetGameplayHudInteractable(true);
        _paused = false;
        GamePauseGate.IsUserPaused = false;

        if (!YandexGamesSdk.PlatformPaused)
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            if (_phase != Phase.WaitingToStart && _phase != Phase.Finished)
                YandexGamesSdk.GameplayStart();
        }

        // Re-apply action interactable states for current phase visuals.
        ReassertActionInteractable();
    }

    private void SetGameplayHudInteractable(bool interactable)
    {
        if (!interactable)
        {
            for (int i = 0; i < _drawActions.Count; i++)
                if (_drawActions[i]?.button != null)
                    _drawActions[i].button.interactable = false;
            for (int i = 0; i < _pullActions.Count; i++)
                if (_pullActions[i]?.button != null)
                    _pullActions[i].button.interactable = false;
            for (int i = 0; i < _fireActions.Count; i++)
                if (_fireActions[i]?.button != null)
                    _fireActions[i].button.interactable = false;

            if (startButton != null)
                startButton.interactable = false;
            if (restartButton != null)
                restartButton.interactable = false;

            SetChildButtonInteractable(roundFinishPanel, "FinishSeriesBtn", false);
            SetChildButtonInteractable(roundFinishPanel, "ContinueSeriesBtn", false);
            SetChildButtonInteractable(roundFinishPanel, "RestartSeriesBtn", false);
            SetChildButtonInteractable(roundFinishPanel, "ExitBtn", false);
            SetChildButtonInteractable(_seriesSummary, "RestartSeriesBtn", false);
            SetChildButtonInteractable(_seriesSummary, "ExitBtn", false);
        }
        else if (startButton != null && _phase == Phase.WaitingToStart)
        {
            startButton.interactable = true;
        }
        else if (_phase == Phase.Finished)
        {
            if (restartButton != null)
                restartButton.interactable = true;
            SetChildButtonInteractable(roundFinishPanel, "FinishSeriesBtn", true);
            SetChildButtonInteractable(roundFinishPanel, "ContinueSeriesBtn", true);
            SetChildButtonInteractable(roundFinishPanel, "RestartSeriesBtn", true);
            SetChildButtonInteractable(roundFinishPanel, "ExitBtn", true);
            SetChildButtonInteractable(_seriesSummary, "RestartSeriesBtn", true);
            SetChildButtonInteractable(_seriesSummary, "ExitBtn", true);
        }
    }

    private static void SetChildButtonInteractable(GameObject root, string childName, bool interactable)
    {
        if (root == null)
            return;
        Transform t = root.transform.Find(childName);
        if (t == null)
            return;
        var button = t.GetComponent<Button>();
        if (button != null)
            button.interactable = interactable;
    }

    private void ReassertActionInteractable()
    {
        switch (_phase)
        {
            case Phase.WaitingToStart:
                SetActionState(_drawActions, ActionVisualState.Inactive);
                SetActionState(_pullActions, ActionVisualState.Inactive);
                SetActionState(_fireActions, ActionVisualState.Inactive);
                if (startButton != null)
                    startButton.interactable = true;
                break;
            case Phase.WaitingForBell:
            case Phase.BellShown:
                SetActionState(_drawActions, ActionVisualState.Active);
                SetActionState(_pullActions, ActionVisualState.Inactive);
                SetActionState(_fireActions, ActionVisualState.Inactive);
                break;
            case Phase.Drawn:
                SetActionState(_drawActions, ActionVisualState.Pressed);
                SetActionState(_pullActions, ActionVisualState.Active);
                SetActionState(_fireActions, ActionVisualState.Inactive);
                break;
            case Phase.Aimed:
            case Phase.AimedAfterMiss:
                SetActionState(_drawActions, ActionVisualState.Pressed);
                SetActionState(_pullActions, ActionVisualState.Pressed);
                SetActionState(_fireActions, ActionVisualState.Active);
                break;
            case Phase.MissRecovery:
                SetActionState(_drawActions, ActionVisualState.Pressed);
                SetActionState(_pullActions, ActionVisualState.Active);
                SetActionState(_fireActions, ActionVisualState.Inactive);
                break;
            case Phase.Finished:
                SetActionState(_drawActions, ActionVisualState.Inactive);
                SetActionState(_pullActions, ActionVisualState.Inactive);
                SetActionState(_fireActions, ActionVisualState.Inactive);
                if (restartButton != null)
                    restartButton.interactable = true;
                break;
        }
    }

    private void RefreshSoundVisual()
    {
        if (soundImage == null)
            return;
        bool on = GameAudioSettings.SoundEnabled;
        Sprite sprite = on ? soundOnSprite : soundOffSprite;
        if (sprite != null)
            soundImage.sprite = sprite;
    }

    private void FinishRound(RoundOutcome outcome)
    {
        _phase = Phase.Finished;
        _outcome = outcome;
        if (outcome == RoundOutcome.Win)
            _series.AddWin(TakeDuelSeconds());

        StopBellRoutine();
        StopCrossfireRoutine();
        StopEnemyRoutine();

        SetAimDim(false);
        if (_gunAim != null)
            _gunAim.SetAiming(false);
        SetActionState(_drawActions, ActionVisualState.Inactive);
        SetActionState(_pullActions, ActionVisualState.Inactive);
        SetActionState(_fireActions, ActionVisualState.Inactive);
        SetIndicatorVisual(drawButton, ActionVisualState.Inactive);
        SetIndicatorVisual(pullButton, ActionVisualState.Inactive);
        SetIndicatorVisual(fireButton, ActionVisualState.Inactive);

        YandexGamesSdk.GameplayStop();
        PresentRoundFinish(outcome);
    }

    private void BeginMissRecovery()
    {
        StopCrossfireRoutine();
        _waitingForRepull = true;
        _repullRequested = false;
        _phase = Phase.MissRecovery;
        SetActionState(_fireActions, ActionVisualState.Inactive);
        SetActionState(_pullActions, ActionVisualState.Active);
        SetIndicatorVisual(fireButton, ActionVisualState.Inactive);
        SetIndicatorVisual(pullButton, ActionVisualState.Active);
        _crossfireRoutine = StartCoroutine(MissRecoveryRoutine());
    }

    private IEnumerator MissRecoveryRoutine()
    {
        if (crossfire == null || crossfireStart == null || crossfireTarget == null)
            yield break;

        Vector3 start = crossfireStart.localPosition;
        Vector3 end = crossfireTarget.localPosition;
        Vector3 mid = Vector3.Lerp(start, end, 0.5f);
        Vector3 current = crossfire.localPosition;
        Vector3 bounce = current + Vector3.up * missBounceUp;

        float elapsed = 0f;
        float bounceDur = Mathf.Max(0.01f, missBounceDuration);
        while (elapsed < bounceDur)
        {
            if (_repullRequested)
            {
                _phase = Phase.AimedAfterMiss;
                _waitingForRepull = false;
                yield return AimFromTo(crossfire.localPosition, end, aimAfterMissDuration, withShake: true);
                _crossfireRoutine = null;
                yield break;
            }

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / bounceDur);
            SetCrossfireLocalPosition(Vector3.Lerp(current, bounce, t));
            yield return null;
        }

        elapsed = 0f;
        float toMidDur = Mathf.Max(0.01f, missToMidDuration);
        Vector3 fromBounce = bounce;
        while (elapsed < toMidDur)
        {
            if (_repullRequested)
            {
                _phase = Phase.AimedAfterMiss;
                _waitingForRepull = false;
                yield return AimFromTo(crossfire.localPosition, end, aimAfterMissDuration, withShake: true);
                _crossfireRoutine = null;
                yield break;
            }

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / toMidDur);
            SetCrossfireLocalPosition(Vector3.Lerp(fromBounce, mid, t));
            yield return null;
        }

        SetCrossfireLocalPosition(mid);

        while (!_repullRequested)
            yield return null;

        _phase = Phase.AimedAfterMiss;
        _waitingForRepull = false;
        yield return AimFromTo(mid, end, aimAfterMissDuration, withShake: true);
        _crossfireRoutine = null;
    }

    private void StartAimMove(bool fromStart)
    {
        StopCrossfireRoutine();
        if (fromStart)
            ResetCrossfireToStart();

        Vector3 from = fromStart || crossfireStart == null
            ? (crossfireStart != null ? crossfireStart.localPosition : Vector3.zero)
            : crossfire.localPosition;
        Vector3 to = crossfireTarget != null ? crossfireTarget.localPosition : from;
        _crossfireRoutine = StartCoroutine(AimMoveRoutine(from, to, aimDuration));
    }

    private IEnumerator AimMoveRoutine(Vector3 from, Vector3 to, float duration)
    {
        yield return AimFromTo(from, to, duration, withShake: true);
        _crossfireRoutine = null;
    }

    private IEnumerator AimFromTo(Vector3 from, Vector3 to, float duration, bool withShake)
    {
        if (crossfire == null)
            yield break;

        float dur = Mathf.Max(0.01f, duration);
        float elapsed = 0f;
        SetCrossfireLocalPosition(from);

        Vector3 delta = to - from;
        Vector3 dir = delta.sqrMagnitude > 0.0001f ? delta.normalized : Vector3.right;
        Vector3 perp = new Vector3(-dir.y, dir.x, 0f);

        while (elapsed < dur)
        {
            if (_phase == Phase.Finished)
                yield break;

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / dur);
            Vector3 pos = Vector3.Lerp(from, to, t);
            if (withShake)
            {
                float fade = 1f - t * crossfireShakeFadeByEnd;
                float shake = Mathf.Sin(elapsed * crossfireShakeFrequency * Mathf.PI * 2f)
                    * crossfireShakeIntensity
                    * fade;
                pos += perp * shake;
            }

            SetCrossfireLocalPosition(pos);
            yield return null;
        }

        SetCrossfireLocalPosition(to);
    }

    private IEnumerator EnemyShootLoop()
    {
        float delay = Random.Range(enemyShotDelayRange.x, enemyShotDelayRange.y);
        yield return new WaitForSeconds(delay);

        while (_phase != Phase.Finished)
        {
            bool enemyMissed = Random.value < enemyMissChance;
            ShowEnemyFiredArt();
            PlaySfx(sfxEnemyShot);

            if (enemyMissed)
            {
                PlaySfx(sfxMissEnemy);
                float reshot = Random.Range(enemyReshotDelayRange.x, enemyReshotDelayRange.y);
                yield return new WaitForSeconds(reshot);
                continue;
            }

            PlaySfx(sfxPlayerHit);
            FinishRound(RoundOutcome.EnemyWin);
            yield break;
        }
    }

    private void ShowEnemyFiredArt()
    {
        if (enemyBodyImage == null)
            return;

        if (enemyFiredSprite != null)
            enemyBodyImage.sprite = enemyFiredSprite;
    }

    private void RestoreEnemyIdleArt()
    {
        if (enemyBodyImage == null)
            return;

        Sprite idle = enemyBodySprite != null ? enemyBodySprite : _enemyIdleSprite;
        if (idle != null)
            enemyBodyImage.sprite = idle;
    }

    private bool IsCrossfireOverOpaqueEnemy()
    {
        if (crossfire == null || enemyBody == null)
            return false;

        Image image = enemyBodyImage != null ? enemyBodyImage : enemyBody.GetComponent<Image>();
        if (image == null || image.sprite == null)
            return false;

        Sprite sprite = image.sprite;
        Texture2D texture = sprite.texture;
        if (texture == null)
            return false;

        if (!texture.isReadable)
        {
            Debug.LogWarning("[RoundController] Enemy texture is not Read/Write enabled.");
            return false;
        }

        Canvas canvas = image.canvas;
        Camera eventCam = null;
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            eventCam = canvas.worldCamera;

        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(eventCam, crossfire.position);
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(enemyBody, screenPoint, eventCam, out Vector2 localPoint))
            return false;

        Rect rect = enemyBody.rect;
        if (!rect.Contains(localPoint))
            return false;

        Vector2 uv = new Vector2(
            (localPoint.x - rect.xMin) / rect.width,
            (localPoint.y - rect.yMin) / rect.height);

        Rect tr = sprite.textureRect;
        int px = Mathf.Clamp(Mathf.FloorToInt(tr.x + uv.x * tr.width), 0, texture.width - 1);
        int py = Mathf.Clamp(Mathf.FloorToInt(tr.y + uv.y * tr.height), 0, texture.height - 1);

        return texture.GetPixel(px, py).a >= opaqueAlphaThreshold;
    }

    private void SetCrossfireLocalPosition(Vector3 localPos)
    {
        if (crossfire != null)
            crossfire.localPosition = localPos;
    }

    private void ResetCrossfireToStart()
    {
        if (crossfire == null || crossfireStart == null)
            return;
        crossfire.localPosition = crossfireStart.localPosition;
    }

    private void SetAimDim(bool on)
    {
        if (aimDimOverlay != null)
            aimDimOverlay.gameObject.SetActive(on);
    }

    private void PlayGunShake()
    {
        if (playerGunRect == null)
            return;
        StopGunFxRoutine();
        _gunFxRoutine = StartCoroutine(GunShakeRoutine());
    }

    private void PlayGunRecoil()
    {
        if (playerGunRect == null)
            return;
        StopGunFxRoutine();
        _gunFxRoutine = StartCoroutine(GunRecoilRoutine());
    }

    private IEnumerator GunShakeRoutine()
    {
        float elapsed = 0f;
        float dur = Mathf.Max(0.01f, gunShakeDuration);
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = 1f - Mathf.Clamp01(elapsed / dur);
            Vector2 offset = Random.insideUnitCircle * gunShakeAmount * t;
            playerGunRect.localPosition = _gunRestLocalPos + (Vector3)offset;
            yield return null;
        }

        ResetGunPose();
        _gunFxRoutine = null;
    }

    private IEnumerator GunRecoilRoutine()
    {
        float elapsed = 0f;
        float dur = Mathf.Max(0.01f, gunRecoilDuration);
        Vector3 up = _gunRestLocalPos + Vector3.up * gunRecoilAmount;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / dur);
            float arc = t < 0.35f ? t / 0.35f : 1f - (t - 0.35f) / 0.65f;
            playerGunRect.localPosition = Vector3.Lerp(_gunRestLocalPos, up, arc);
            yield return null;
        }

        ResetGunPose();
        _gunFxRoutine = null;
    }

    private void ResetGunPose()
    {
        if (playerGunRect == null)
            return;
        playerGunRect.localPosition = _gunRestLocalPos;
        // Keep aim rotation if currently aiming; otherwise rest rotation.
        if (_gunAim == null || _phase == Phase.WaitingToStart || _phase == Phase.WaitingForBell
            || _phase == Phase.BellShown || _phase == Phase.Drawn || _phase == Phase.Finished)
        {
            playerGunRect.localRotation = _gunRestLocalRot;
        }
    }

    private void PlaySfx(AudioClip clip)
    {
        if (!GameAudioSettings.SoundEnabled)
            return;
        if (audioSource == null || clip == null)
            return;
        audioSource.PlayOneShot(clip);
    }

    private void StopBellRoutine()
    {
        if (_bellRoutine == null)
            return;
        StopCoroutine(_bellRoutine);
        _bellRoutine = null;
    }

    private void StopCrossfireRoutine()
    {
        if (_crossfireRoutine == null)
            return;
        StopCoroutine(_crossfireRoutine);
        _crossfireRoutine = null;
    }

    private void StopEnemyRoutine()
    {
        if (_enemyRoutine == null)
            return;
        StopCoroutine(_enemyRoutine);
        _enemyRoutine = null;
    }

    private void StopGunFxRoutine()
    {
        if (_gunFxRoutine == null)
            return;
        StopCoroutine(_gunFxRoutine);
        _gunFxRoutine = null;
    }

    private void SetBellState(bool fired)
    {
        Sprite sprite = fired ? bellSprite2 : bellSprite1;
        if (sprite == null)
            sprite = fired ? bellSprite1 : bellSprite2;

        for (int i = 0; i < _bellImages.Count; i++)
            ApplyBellImage(_bellImages[i], sprite);

        if (bellButton != null)
            ApplyBellImage(bellButton.targetGraphic as Image, sprite);
    }

    /// <summary>
    /// SpriteSwap keeps showing DisabledSprite via Image.overrideSprite even after sprite is changed.
    /// </summary>
    private static void ApplyBellImage(Image image, Sprite sprite)
    {
        if (image == null || sprite == null)
            return;

        var btn = image.GetComponent<Button>();
        if (btn != null)
        {
            btn.transition = Selectable.Transition.None;
            btn.interactable = false;
            btn.enabled = false;
        }

        image.overrideSprite = null;
        image.sprite = sprite;
    }

    private void SetActionState(List<TouchActionButton> actions, ActionVisualState state)
    {
        for (int i = 0; i < actions.Count; i++)
            SetTouchVisual(actions[i], state);
    }

    private void SetIndicatorVisual(Button button, ActionVisualState state)
    {
        if (button == null)
            return;

        button.interactable = false;

        var colors = _defaultColors.Equals(default(ColorBlock)) ? button.colors : _defaultColors;
        colors.disabledColor = DisabledTint;
        colors.pressedColor = PressedTint;
        colors.selectedColor = colors.normalColor;
        button.colors = colors;

        var image = button.targetGraphic as Graphic;
        if (image == null)
            return;

        switch (state)
        {
            case ActionVisualState.Active:
                image.color = ActiveTint;
                break;
            case ActionVisualState.Pressed:
                image.color = PressedTint;
                break;
            default:
                image.color = DisabledTint;
                break;
        }
    }

    private static void SetTouchVisual(TouchActionButton touch, ActionVisualState state)
    {
        if (touch == null || touch.button == null)
            return;

        Button button = touch.button;
        Image image = touch.image != null ? touch.image : button.targetGraphic as Image;
        SpriteState spriteState = button.spriteState;

        Sprite disabled = spriteState.disabledSprite != null ? spriteState.disabledSprite : touch.inactiveSprite;
        Sprite highlighted = spriteState.highlightedSprite != null ? spriteState.highlightedSprite : touch.activeSprite;
        Sprite pressed = spriteState.pressedSprite != null ? spriteState.pressedSprite : touch.pressedSprite;

        switch (state)
        {
            case ActionVisualState.Active:
                button.transition = Selectable.Transition.SpriteSwap;
                button.interactable = true;
                if (image != null && highlighted != null)
                    image.sprite = highlighted;
                break;

            case ActionVisualState.Pressed:
                button.interactable = false;
                button.transition = Selectable.Transition.None;
                if (image != null && pressed != null)
                    image.sprite = pressed;
                break;

            default:
                button.transition = Selectable.Transition.SpriteSwap;
                button.interactable = false;
                if (image != null && disabled != null)
                    image.sprite = disabled;
                break;
        }

        if (image != null)
            image.color = Color.white;
    }

    private static void SetPanelActive(GameObject panel, bool active)
    {
        if (panel != null)
            panel.SetActive(active);
    }

    private static void SetVisible(GameObject go, bool visible)
    {
        if (go != null)
            go.SetActive(visible);
    }
}
