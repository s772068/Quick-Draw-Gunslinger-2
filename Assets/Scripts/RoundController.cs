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
[DefaultExecutionOrder(50)]
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
    [SerializeField] private Sprite shotFlashSprite;
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
    [SerializeField] private GameObject roundWinPanel;
    [SerializeField] private GameObject roundLosePanel;
    [SerializeField] private GameObject levelCompletePanel;
    [SerializeField] private GameObject areYouSureAbortPanel;
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

    [Header("Gun")]
    [SerializeField] private GunDefinition gun;

    [Header("Balance / Timing")]
    [Tooltip("Enemy first-shot K floor: K = floor + span * decay^(round-1). Round 1 → 2.0s.")]
    [SerializeField] private float enemyShotKFloor = 0.75f;
    [Tooltip("Enemy first-shot K span multiplied by decay^(round-1).")]
    [SerializeField] private float enemyShotKSpan = 1.75f;
    [Tooltip("Enemy first-shot K decay per round (0.83).")]
    [SerializeField] private float enemyShotKDecay = 0.83f;
    [Tooltip("Enemy first-shot L floor: L = floor + span * decay^(round-1). Round 1 → 2.5s.")]
    [SerializeField] private float enemyShotLFloor = 0.9f;
    [Tooltip("Enemy first-shot L span multiplied by decay^(round-1).")]
    [SerializeField] private float enemyShotLSpan = 1.6f;
    [Tooltip("Enemy first-shot L decay per round (0.85).")]
    [SerializeField] private float enemyShotLDecay = 0.85f;
    [Tooltip("Delay between enemy miss and the next enemy shot (seconds). X = min, Y = max.")]
    [SerializeField] private Vector2 enemyReshotDelayRange = new Vector2(0.5f, 0.7f);
    [Tooltip("Chance the enemy misses on a shot (0–1).")]
    [SerializeField] [Range(0f, 1f)] private float enemyMissChance = 0.25f;

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
    private bool _audioWarmed;
    private Coroutine _bellRoutine;
    private Coroutine _crossfireRoutine;
    private Coroutine _enemyRoutine;
    private Coroutine _gunFxRoutine;
    private Coroutine _shotFlashRoutine;
    private Image _shotFlash;
    private Sprite _activeShotSprite;

    private ColorBlock _defaultColors;
    private Vector3 _gunRestLocalPos;
    private Quaternion _gunRestLocalRot;
    private Sprite _enemyIdleSprite;
    private Material _aimDimMaterial;
    private PlayerGunAim _gunAim;
    private Texture2D _hitMask;
    private Sprite _hitMaskSource;
#if UNITY_EDITOR
    private Image _hitboxDebugImage;
    private Sprite _hitboxDebugSprite;
    private Texture2D _hitboxDebugTexture;
    private Sprite _hitboxDebugSource;
    private bool _hitboxDebugVisible;
    private bool _timingDebugEnabled;
    private AimDebugOverlay _aimDebug;
    private readonly List<Vector2> _debugShotPoints = new List<Vector2>();
#endif
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

    private GunDefinition Gun
    {
        get
        {
            if (gun == null)
                gun = FindFirstObjectByType<GunDefinition>();
            return gun;
        }
    }

    private Vector2 bellDelayRange =>
        _activeLevel != null ? _activeLevel.bellDelayRange : new Vector2(0.8f, 1.4f);

    private Vector2 timeToMiddleRange =>
        Gun != null ? Gun.timeToMiddleRange : new Vector2(2f, 3f);

    private float crossfireShakeIntensity => Gun != null ? Gun.crossfireShakeIntensity : 50f;
    private float crossfireShakeFrequency => Gun != null ? Gun.crossfireShakeFrequency : 3f;
    private float crossfireShakeFadeByEnd => Gun != null ? Gun.crossfireShakeFadeByEnd : 0.35f;
    private float missBounceUp => Gun != null ? Gun.missBounceUp : 40f;
    private float missBounceDuration => Gun != null ? Gun.missBounceDuration : 0.12f;
    private float missToMidDuration => Gun != null ? Gun.missToMidDuration : 0.25f;

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
        audioSource.playOnAwake = false;
        PreloadAudioClips();

        ApplyGunArt();
        EnsureAimDimOverlay();
        EnsurePauseMenu();
        EnsureOrientationControls();
        EnsureSafeAreaPadding();
        EnsureGunAim();
        CollectActionButtons();
        ConfigureBells();
        ConfigureBottomPanelAsIndicators();
        ApplyHintVisibility();
        WireButtons();
        RefreshSoundVisual();
        ApplyLocalizedTexts();
        BindSeriesUi();
        BindGdd08Ui();
        BindGdd10();
        BindGdd12Training();
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
#if UNITY_EDITOR
        if (_timingDebugEnabled)
        {
            _timingDebugEnabled = false;
            SetTimingDebug(false);
        }
#endif
    }

    private void Start()
    {
        // After OrientationBackground.Start, so the level art wins the first frame.
        ApplyRoundPresentation();
        if (IsTraining)
            ApplyTrainingLevelPresentation();
        // GDD: eagle SFX on GameScene load, not on round Start.
        PlaySfx(sfxEagle);
    }

    private void Update()
    {
#if UNITY_EDITOR
        if (EditorRestartPressed())
        {
            EditorRestartRound();
            return;
        }
#endif
        if (_paused || YandexGamesSdk.PlatformPaused)
            return;
        HandleKeyboardInput();
    }

#if UNITY_EDITOR
    private static bool EditorRestartPressed()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null || !kb.rKey.wasPressedThisFrame)
            return false;
        if (YandexGamesSdk.PlatformPaused)
            return false;

        var selected = UnityEngine.EventSystems.EventSystem.current != null
            ? UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject
            : null;
        if (selected == null)
            return true;

        return !IsEditorTextInputFocused();
    }

    private static bool IsEditorTextInputFocused()
    {
        var selected = UnityEngine.EventSystems.EventSystem.current != null
            ? UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject
            : null;
        if (selected == null)
            return false;

        var field = selected.GetComponent<TMP_InputField>();
        return field != null && field.isFocused;
    }
#endif

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
            if (ShouldOpenPauseMenuOnPlatformPause())
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
            if (ShouldOpenPauseMenuOnPlatformPause())
                OpenPauseMenu();
            else
                SetGameplayHudInteractable(false);
        }
        else if (!_paused)
        {
            // Ads call SetGameplayHudInteractable(false) while RoundFinish/Summary are open;
            // Reassert alone only restores D/P/F — re-enable panel buttons too.
            SetGameplayHudInteractable(true);
            ReassertActionInteractable();
            RefreshMenuButtonInteractable();
        }
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
#if UNITY_EDITOR
        if (kb.aKey.wasPressedThisFrame && !IsEditorTextInputFocused())
            ToggleHitboxDebug();
        if (kb.eKey.wasPressedThisFrame && !IsEditorTextInputFocused())
            ToggleTimingDebug();
#endif
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

        if (roundWinPanel == null)
        {
            var t = hud.Find("RoundWin");
            if (t != null)
                roundWinPanel = t.gameObject;
        }

        if (roundLosePanel == null)
        {
            var t = hud.Find("RoundLose");
            if (t != null)
                roundLosePanel = t.gameObject;
        }

        if (levelCompletePanel == null)
        {
            var t = hud.Find("LevelComplete");
            if (t != null)
                levelCompletePanel = t.gameObject;
        }

        if (areYouSureAbortPanel == null)
        {
            var t = hud.Find("AreYouSureAbort");
            if (t != null)
                areYouSureAbortPanel = t.gameObject;
        }

        // Legacy alias: prefer RoundWin, fall back to RoundFinish if still in scene.
        if (roundFinishPanel == null)
        {
            if (roundWinPanel != null)
                roundFinishPanel = roundWinPanel;
            else
            {
                var t = hud.Find("RoundFinish");
                if (t != null)
                    roundFinishPanel = t.gameObject;
            }
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
        StyleKeyboardHints();
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

    private void EnsureSafeAreaPadding()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
            return;

        Transform hud = canvas.transform.Find("HUD");
        if (hud == null)
            return;

        if (hud.GetComponent<SafeAreaPadding>() == null)
            hud.gameObject.AddComponent<SafeAreaPadding>();
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

        ReleaseHitMask();
#if UNITY_EDITOR
        ReleaseHitboxDebugSprite();
#endif
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
            pauseExitButton.onClick.AddListener(OnPauseMapAbortClicked);
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
        ClearEnemyShotDelayDebug();
        ApplyRoundPresentation();
        if (IsTraining)
            ApplyTrainingLevelPresentation();
        RefreshRoundStartLabels();
        RefreshHudCounters();
        RefreshMenuButtonInteractable();
        CaptureRestartSnapshot();
        _crossfireWasOverHitbox = false;

        if (IsTraining)
        {
            EnterTrainingInsteadOfRoundStart();
            return;
        }
    }

    private void OnStartClicked()
    {
        if (_paused || YandexGamesSdk.PlatformPaused || _phase != Phase.WaitingToStart)
            return;

        // User gesture unlocks WebGL AudioContext; warm clips before the first bell.
        WarmUpAudio();

        BeginSeriesIfNeeded();
        if (!_cityRun.Active)
        {
            int city = PlayerPrefs.GetInt(MainMenuController.SelectedCityPrefsKey, 0);
            _cityRun.Begin(Mathf.Clamp(city, 0, 2));
            ApplyRoundPresentation();
            RefreshRoundStartLabels();
        }
        SetPanelActive(roundStartPanel, false);
        _phase = Phase.WaitingForBell;
        SetActionState(_drawActions, ActionVisualState.Active);
        SetIndicatorVisual(drawButton, ActionVisualState.Active);
        ClearEnemyShotDelayDebug();
        RefreshMenuButtonInteractable();
        YandexGamesSdk.GameplayStart();
        NoteLevelStartTime();
        LogRoundOpponent();
        _bellRoutine = StartCoroutine(BellCountdown());
    }

    private IEnumerator BellCountdown()
    {
        yield return new WaitForSeconds(RollBellDelay());

        if (_phase != Phase.WaitingForBell)
            yield break;

        // WebGL: ensure listener is live before icon+sfx (ads may have paused it).
        AudioListener.pause = false;
        WarmUpAudio();

        _bellFired = true;
        _phase = Phase.BellShown;
        MarkDuelTimer();
        NoteBellTime();
        LogLevelStartToBell();
        // Kick audio first, then flip the icon so they stay aligned on slow WebGL.
        PlaySfx(sfxBell);
        SetBellState(fired: true);
        _enemyRoutine = StartCoroutine(EnemyShootLoop());
    }

    private void OnDrawClicked()
    {
        if (YandexGamesSdk.PlatformPaused)
            return;
        if (_paused && !(_trainingActive && _trainingAllowed == TrainingAllow.Draw))
            return;
        if (TrainingBlocksAction(TrainingAllow.Draw))
            return;

        if (_phase == Phase.WaitingForBell && !_bellFired)
        {
            if (_trainingActive)
                return;
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
        LogBellToDraw();
    }

    private void OnPullClicked()
    {
        if (YandexGamesSdk.PlatformPaused)
            return;
        if (_paused && !(_trainingActive && _trainingAllowed == TrainingAllow.Pull))
            return;
        if (TrainingBlocksAction(TrainingAllow.Pull))
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
            _phase = Phase.Aimed;
            StartGdd10AimLoop();
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
        if (YandexGamesSdk.PlatformPaused)
            return;
        if (_paused && !(_trainingActive && _trainingAllowed == TrainingAllow.Fire))
            return;
        if (TrainingBlocksAction(TrainingAllow.Fire))
            return;

        if (_phase != Phase.Aimed && _phase != Phase.AimedAfterMiss)
            return;

        PlaySfx(sfxPlayerShot);
        PlayGunRecoil();
        SetActionState(_fireActions, ActionVisualState.Pressed);
        SetIndicatorVisual(fireButton, ActionVisualState.Pressed);
        NoteDebugPlayerShot();

        bool hit = _trainingActive || IsCrossfireOverOpaqueEnemy();
        if (hit)
        {
            LogPlayerReaction(hit: true);
            StopCrossfireRoutine();
            StopEnemyRoutine();
            ShowHitMarkerAtCrossfire();
            PlaySfx(sfxEnemyDie);
            FinishRound(RoundOutcome.Win);
            return;
        }

        LogPlayerReaction(hit: false);
        PlaySfx(sfxMissPlayer);
        if (TimingDebugActive)
        {
            StopCrossfireRoutine();
            StopEnemyRoutine();
            FinishRound(RoundOutcome.EnemyWin);
            return;
        }

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

    private void OnPauseMapAbortClicked()
    {
        SetPanelActive(pauseMenuRoot, false);
        if (areYouSureAbortPanel != null)
        {
            SetPanelActive(areYouSureAbortPanel, true);
            areYouSureAbortPanel.transform.SetAsLastSibling();
            RefreshMenuButtonInteractable();
            return;
        }

        OnPauseExitClicked();
    }

    private void OnPauseExitClicked()
    {
        NotifyTrainingLeftToMenu();
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

        // Keep Sound + PauseMenu buttons usable; Menu follows duel state.
        if (soundButton != null)
            soundButton.interactable = true;
        if (pauseContinueButton != null)
            pauseContinueButton.interactable = true;
        if (pauseExitButton != null)
            pauseExitButton.interactable = true;
        RefreshMenuButtonInteractable();
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
        RefreshMenuButtonInteractable();
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
                SetChildButtonInteractable(roundFinishPanel, "FinishSeriesBtn", true);
                SetChildButtonInteractable(roundFinishPanel, "ContinueSeriesBtn", true);
                SetChildButtonInteractable(roundFinishPanel, "RestartSeriesBtn", true);
                SetChildButtonInteractable(roundFinishPanel, "ExitBtn", true);
                SetChildButtonInteractable(_seriesSummary, "RestartSeriesBtn", true);
                SetChildButtonInteractable(_seriesSummary, "ExitBtn", true);
                SetChildButtonInteractable(_areYouSure, "Yes", true);
                SetChildButtonInteractable(_areYouSure, "No", true);
                SetChildButtonInteractable(_authPrompt, "LoginBtn", true);
                SetChildButtonInteractable(_authPrompt, "SkipBtn", true);
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
        if (outcome == RoundOutcome.Win && !_trainingActive)
            _series.AddWin(TakeDuelSeconds());

        StopBellRoutine();
        StopCrossfireRoutine();
        StopEnemyRoutine();
        StopGunFxRoutine();

        SetAimDim(false);
        if (_gunAim != null)
            _gunAim.Freeze();
        SetActionState(_drawActions, ActionVisualState.Inactive);
        SetActionState(_pullActions, ActionVisualState.Inactive);
        SetActionState(_fireActions, ActionVisualState.Inactive);
        SetIndicatorVisual(drawButton, ActionVisualState.Inactive);
        SetIndicatorVisual(pullButton, ActionVisualState.Inactive);
        SetIndicatorVisual(fireButton, ActionVisualState.Inactive);

        YandexGamesSdk.GameplayStop();
        if (_trainingActive)
        {
            RefreshMenuButtonInteractable();
            return;
        }

        PresentRoundFinish(outcome);
        RefreshMenuButtonInteractable();
        NoteRoundFinishedForAds();
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
        if (crossfire == null)
            yield break;

        Vector3 current = crossfire.localPosition;
        Vector3 bounce = current + Vector3.up * missBounceUp;
        Vector3 mid = _aimPathReady
            ? (Vector3)AimTrajectory.ClampToAllowedCircle(_aimPath, (Vector2)current)
            : current;

        float elapsed = 0f;
        float bounceDur = Mathf.Max(0.01f, missBounceDuration);
        while (elapsed < bounceDur)
        {
            if (_repullRequested)
            {
                _waitingForRepull = false;
                _phase = Phase.AimedAfterMiss;
                _crossfireRoutine = null;
                StartGdd10AimLoop();
                yield break;
            }

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / bounceDur);
            Vector3 p = Vector3.Lerp(current, bounce, t);
            if (_aimPathReady)
                p = AimTrajectory.ClampToAllowedCircle(_aimPath, p);
            SetCrossfireLocalPosition(p);
            yield return null;
        }

        elapsed = 0f;
        float toMidDur = Mathf.Max(0.01f, missToMidDuration);
        Vector3 fromBounce = bounce;
        while (elapsed < toMidDur)
        {
            if (_repullRequested)
            {
                _waitingForRepull = false;
                _phase = Phase.AimedAfterMiss;
                _crossfireRoutine = null;
                StartGdd10AimLoop();
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

        _waitingForRepull = false;
        _phase = Phase.AimedAfterMiss;
        _crossfireRoutine = null;
        StartGdd10AimLoop();
    }

    private void StartAimMove(bool fromStart)
    {
        // GDD 1.0: trajectory loop replaces linear Start→Target aim.
        NotePullTime();
        _phase = Phase.Aimed;
        StartGdd10AimLoop();
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
        float delay = CurrentEnemyReactionTime();
        // Persist the rolled delay even if the player wins before the enemy fires.
        WriteEnemyShotDelayDebug(delay);
        yield return new WaitForSeconds(delay);

        while (_phase != Phase.Finished)
        {
            bool enemyMissed = Random.value < enemyMissChance;
            ShowEnemyFiredArt();
            LogEnemyShot(enemyMissed);
            PlaySfx(sfxEnemyShot);

            if (enemyMissed)
            {
                PlaySfx(sfxMissEnemy);
                ShowEnemyMissMessage();
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

        Sprite fired = _activeShotSprite != null ? _activeShotSprite : enemyFiredSprite;
        if (fired != null)
            enemyBodyImage.sprite = fired;
        SyncHitMask();
        RefreshHitboxDebug();
        ShowShotFlash();
    }

    private void RestoreEnemyIdleArt()
    {
        if (enemyBodyImage == null)
            return;

        Sprite idle = enemyBodySprite != null ? enemyBodySprite : _enemyIdleSprite;
        if (idle != null)
            enemyBodyImage.sprite = idle;
        SyncHitMask();
        RefreshHitboxDebug();
        HideShotFlash();
    }

    private bool IsCrossfireOverOpaqueEnemy()
    {
        if (crossfire == null || enemyBody == null)
            return false;

        Image image = enemyBodyImage != null ? enemyBodyImage : enemyBody.GetComponent<Image>();
        if (image == null || image.sprite == null)
            return false;

        SyncHitMask();
        if (_hitMask == null)
            return false;

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

        int px = Mathf.Clamp(Mathf.FloorToInt(uv.x * _hitMask.width), 0, _hitMask.width - 1);
        int py = Mathf.Clamp(Mathf.FloorToInt(uv.y * _hitMask.height), 0, _hitMask.height - 1);
        return _hitMask.GetPixel(px, py).a >= opaqueAlphaThreshold;
    }

    private void SyncHitMask()
    {
        Image image = enemyBodyImage != null ? enemyBodyImage : (enemyBody != null ? enemyBody.GetComponent<Image>() : null);
        Sprite sprite = image != null ? image.sprite : null;
        if (sprite == _hitMaskSource && _hitMask != null)
            return;

        ReleaseHitMask();
        if (sprite == null)
            return;

        _hitMask = ReadSpritePixels(sprite);
        _hitMaskSource = _hitMask != null ? sprite : null;
    }

    private static Texture2D ReadSpritePixels(Sprite sprite)
    {
        Texture source = sprite.texture;
        if (source == null)
            return null;

        Rect tr = sprite.textureRect;
        int x0 = Mathf.Clamp(Mathf.FloorToInt(tr.x), 0, Mathf.Max(0, source.width - 1));
        int y0 = Mathf.Clamp(Mathf.FloorToInt(tr.y), 0, Mathf.Max(0, source.height - 1));
        int w = Mathf.Clamp(Mathf.RoundToInt(tr.width), 1, source.width - x0);
        int h = Mathf.Clamp(Mathf.RoundToInt(tr.height), 1, source.height - y0);

        if (source is Texture2D readable && readable.isReadable)
        {
            var copy = new Texture2D(w, h, TextureFormat.RGBA32, false);
            copy.SetPixels(readable.GetPixels(x0, y0, w, h));
            copy.Apply(false, false);
            copy.filterMode = FilterMode.Point;
            copy.wrapMode = TextureWrapMode.Clamp;
            return copy;
        }

        var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
        Graphics.Blit(
            source,
            rt,
            new Vector2(w / (float)source.width, h / (float)source.height),
            new Vector2(x0 / (float)source.width, y0 / (float)source.height));
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;
        var blitted = new Texture2D(w, h, TextureFormat.RGBA32, false);
        blitted.ReadPixels(new Rect(0f, 0f, w, h), 0, 0, false);
        blitted.Apply(false, false);
        blitted.filterMode = FilterMode.Point;
        blitted.wrapMode = TextureWrapMode.Clamp;
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);
        return blitted;
    }

    private void ReleaseHitMask()
    {
        if (_hitMask != null)
        {
            Destroy(_hitMask);
            _hitMask = null;
        }

        _hitMaskSource = null;
    }

    private void NoteCrossfireOverHitbox()
    {
        if (crossfire == null || !crossfire.gameObject.activeInHierarchy)
        {
            _crossfireWasOverHitbox = false;
            return;
        }

        if (_phase != Phase.Aimed && _phase != Phase.AimedAfterMiss && _phase != Phase.MissRecovery)
            return;

        bool over = IsCrossfireOverOpaqueEnemy();
        if (over && !_crossfireWasOverHitbox)
        {
            LogCrossfireEnteredHitbox();
            NoteTimingHitboxEntered();
        }
        _crossfireWasOverHitbox = over;
    }

    private void SetCrossfireLocalPosition(Vector3 localPos)
    {
        if (crossfire != null)
            crossfire.localPosition = localPos;
        NoteCrossfireOverHitbox();
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

    private AudioClip[] AllSfxClips()
    {
        return new[]
        {
            sfxEagle, sfxBell, sfxDraw, sfxGunPull, sfxPlayerShot,
            sfxEnemyShot, sfxEnemyDie, sfxPlayerHit, sfxMissPlayer, sfxMissEnemy
        };
    }

    private void PreloadAudioClips()
    {
        AudioClip[] clips = AllSfxClips();
        for (int i = 0; i < clips.Length; i++)
        {
            AudioClip clip = clips[i];
            if (clip == null)
                continue;
            if (clip.loadState == AudioDataLoadState.Unloaded)
                clip.LoadAudioData();
        }
    }

    /// <summary>
    /// WebGL often delays the first PlayOneShot of a clip (~0.5s). Silent warm-up after a
    /// user gesture makes the first real bell land with the icon.
    /// </summary>
    private void WarmUpAudio()
    {
        if (_audioWarmed || audioSource == null)
            return;

        AudioListener.pause = false;
        AudioClip[] clips = AllSfxClips();
        for (int i = 0; i < clips.Length; i++)
        {
            AudioClip clip = clips[i];
            if (clip == null)
                continue;
            if (clip.loadState == AudioDataLoadState.Unloaded)
                clip.LoadAudioData();
            audioSource.PlayOneShot(clip, 0f);
        }

        _audioWarmed = true;
    }

    private void PlaySfx(AudioClip clip)
    {
        if (!GameAudioSettings.SoundEnabled)
            return;
        if (audioSource == null || clip == null)
            return;
        if (GameAudioSettings.PlatformMuted)
            return;
        AudioListener.pause = false;
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

    private void ShowShotFlash()
    {
        if (shotFlashSprite == null || enemyBody == null)
            return;

        EnsureShotFlash();
        if (_shotFlash == null)
            return;

        _shotFlash.sprite = shotFlashSprite;
        float size = Mathf.Max(48f, enemyBody.rect.width * 0.28f);
        _shotFlash.rectTransform.sizeDelta = new Vector2(size, size);
        _shotFlash.gameObject.SetActive(true);
        _shotFlash.transform.SetAsLastSibling();
        if (_shotFlashRoutine != null)
            StopCoroutine(_shotFlashRoutine);
        _shotFlashRoutine = StartCoroutine(ShotFlashRoutine());
    }

    private void HideShotFlash()
    {
        if (_shotFlashRoutine != null)
        {
            StopCoroutine(_shotFlashRoutine);
            _shotFlashRoutine = null;
        }

        if (_shotFlash != null)
            _shotFlash.gameObject.SetActive(false);
    }

    private IEnumerator ShotFlashRoutine()
    {
        yield return new WaitForSeconds(0.3f);
        if (_shotFlash != null)
            _shotFlash.gameObject.SetActive(false);
        _shotFlashRoutine = null;
    }

    private void EnsureShotFlash()
    {
        if (_shotFlash != null || enemyBody == null)
            return;

        Transform existing = enemyBody.Find("ShotFlash");
        GameObject go;
        if (existing != null)
        {
            go = existing.gameObject;
        }
        else
        {
            go = new GameObject("ShotFlash", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(enemyBody, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            float size = Mathf.Max(48f, enemyBody.rect.width * 0.28f);
            rt.sizeDelta = new Vector2(size, size);
        }

        _shotFlash = go.GetComponent<Image>();
        _shotFlash.raycastTarget = false;
        _shotFlash.preserveAspect = true;
        _shotFlash.color = Color.white;
        go.SetActive(false);
    }

#if !UNITY_EDITOR
    private void NoteDebugPlayerShot() { }
    private void ClearDebugShots() { }
    private void RefreshAimDebug() { }
    private void ToggleTimingDebug() { }
#endif

    private void RefreshHitboxDebug()
    {
#if UNITY_EDITOR
        if (!_hitboxDebugVisible)
            return;
        RebuildHitboxDebug();
#endif
    }

#if UNITY_EDITOR
    private void ToggleHitboxDebug()
    {
        _hitboxDebugVisible = !_hitboxDebugVisible;
        if (!_hitboxDebugVisible)
        {
            if (_hitboxDebugImage != null)
                _hitboxDebugImage.gameObject.SetActive(false);
            if (_aimDebug != null)
                _aimDebug.gameObject.SetActive(false);
            return;
        }

        RebuildHitboxDebug();
        RefreshAimDebug();
    }

    private void ToggleTimingDebug()
    {
        _timingDebugEnabled = !_timingDebugEnabled;
        SetTimingDebug(_timingDebugEnabled);
    }

    private void NoteDebugPlayerShot()
    {
        if (!_hitboxDebugVisible || crossfire == null || enemyBody == null)
            return;

        _debugShotPoints.Add(enemyBody.InverseTransformPoint(crossfire.position));
        RefreshAimDebug();
    }

    private void ClearDebugShots()
    {
        _debugShotPoints.Clear();
        if (_aimDebug != null)
            _aimDebug.SetShots(_debugShotPoints);
    }

    private void RefreshAimDebug()
    {
        if (!_hitboxDebugVisible)
            return;

        EnsureAimDebug();
        if (_aimDebug == null)
            return;

        _aimDebug.gameObject.SetActive(true);
        _aimDebug.transform.SetAsLastSibling();
        float radius = ScreenPixelsToEnemyLocal(3f);
        float thickness = ScreenPixelsToEnemyLocal(2f);
        if (_aimPathReady)
            _aimDebug.SetPath(_aimPath, radius, thickness);
        else
            _aimDebug.ClearPath();
        _aimDebug.SetShots(_debugShotPoints);
    }

    private void EnsureAimDebug()
    {
        if (_aimDebug != null || enemyBody == null)
            return;

        Transform existing = enemyBody.Find("AimDebugOverlay");
        GameObject go;
        if (existing != null)
        {
            go = existing.gameObject;
        }
        else
        {
            go = new GameObject("AimDebugOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(AimDebugOverlay));
            go.transform.SetParent(enemyBody, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = enemyBody.pivot;
        }

        _aimDebug = go.GetComponent<AimDebugOverlay>();
        _aimDebug.raycastTarget = false;
        go.SetActive(false);
    }

    private float ScreenPixelsToEnemyLocal(float pixels)
    {
        if (enemyBody == null)
            return pixels;

        Canvas canvas = enemyBody.GetComponentInParent<Canvas>();
        Camera cam = null;
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            cam = canvas.worldCamera;

        Vector2 origin = RectTransformUtility.WorldToScreenPoint(cam, enemyBody.TransformPoint(Vector3.zero));
        Vector2 unit = RectTransformUtility.WorldToScreenPoint(cam, enemyBody.TransformPoint(new Vector3(100f, 0f, 0f)));
        float screenPerLocal = (unit - origin).magnitude / 100f;
        if (screenPerLocal < 0.001f)
            return pixels;
        return pixels / screenPerLocal;
    }

    private void RebuildHitboxDebug()
    {
        Image source = enemyBodyImage != null ? enemyBodyImage : (enemyBody != null ? enemyBody.GetComponent<Image>() : null);
        if (source == null || source.sprite == null)
        {
            if (_hitboxDebugImage != null)
                _hitboxDebugImage.gameObject.SetActive(false);
            return;
        }

        SyncHitMask();
        if (_hitMask == null)
        {
            if (_hitboxDebugImage != null)
                _hitboxDebugImage.gameObject.SetActive(false);
            return;
        }

        EnsureHitboxDebugImage();
        if (_hitboxDebugImage == null)
            return;

        if (_hitboxDebugSource == source.sprite && _hitboxDebugSprite != null)
        {
            _hitboxDebugImage.gameObject.SetActive(true);
            return;
        }

        Sprite sprite = source.sprite;
        int w = _hitMask.width;
        int h = _hitMask.height;
        Color[] src = _hitMask.GetPixels();
        var solid = new bool[src.Length];
        for (int i = 0; i < src.Length; i++)
            solid[i] = src[i].a >= opaqueAlphaThreshold;

        var dst = new Color[src.Length];
        Color fill = new Color(0f, 0f, 0f, 0.5f);
        Color outline = new Color(1f, 0f, 0f, 1f);
        const int stroke = 2;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                if (!solid[i])
                    continue;

                bool edge = false;
                for (int oy = -stroke; oy <= stroke && !edge; oy++)
                {
                    int ny = y + oy;
                    for (int ox = -stroke; ox <= stroke; ox++)
                    {
                        int nx = x + ox;
                        if ((uint)nx >= (uint)w || (uint)ny >= (uint)h || !solid[ny * w + nx])
                        {
                            edge = true;
                            break;
                        }
                    }
                }

                dst[i] = edge ? outline : fill;
            }
        }

        ReleaseHitboxDebugSprite();
        _hitboxDebugTexture = new Texture2D(w, h, TextureFormat.RGBA32, false);
        _hitboxDebugTexture.filterMode = FilterMode.Point;
        _hitboxDebugTexture.wrapMode = TextureWrapMode.Clamp;
        _hitboxDebugTexture.SetPixels(dst);
        _hitboxDebugTexture.Apply(false, false);
        _hitboxDebugSprite = Sprite.Create(
            _hitboxDebugTexture,
            new Rect(0f, 0f, w, h),
            new Vector2(0.5f, 0.5f),
            sprite.pixelsPerUnit);
        _hitboxDebugSource = sprite;
        _hitboxDebugImage.sprite = _hitboxDebugSprite;
        _hitboxDebugImage.color = Color.white;
        _hitboxDebugImage.preserveAspect = false;
        _hitboxDebugImage.gameObject.SetActive(true);
        _hitboxDebugImage.transform.SetAsLastSibling();
    }

    private void EnsureHitboxDebugImage()
    {
        if (_hitboxDebugImage != null || enemyBody == null)
            return;

        Transform existing = enemyBody.Find("HitboxDebug");
        GameObject go;
        if (existing != null)
        {
            go = existing.gameObject;
        }
        else
        {
            go = new GameObject("HitboxDebug", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(enemyBody, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
        }

        _hitboxDebugImage = go.GetComponent<Image>();
        _hitboxDebugImage.raycastTarget = false;
        _hitboxDebugImage.type = Image.Type.Simple;
        go.SetActive(false);
    }

    private void ReleaseHitboxDebugSprite()
    {
        if (_hitboxDebugSprite != null)
        {
            Destroy(_hitboxDebugSprite);
            _hitboxDebugSprite = null;
        }

        if (_hitboxDebugTexture != null)
        {
            Destroy(_hitboxDebugTexture);
            _hitboxDebugTexture = null;
        }

        _hitboxDebugSource = null;
    }
#endif
}
