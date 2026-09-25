using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// Duel core loop for GDD 0.3. Scene objects stay editable in Scene view.
/// </summary>
public class RoundController : MonoBehaviour
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
    [SerializeField] private Image aimDimOverlay;
    [SerializeField] [Range(0f, 1f)] private float opaqueAlphaThreshold = 0.1f;

    [Header("HUD Panels")]
    [SerializeField] private GameObject roundStartPanel;
    [SerializeField] private GameObject bottomPanel;
    [SerializeField] private GameObject roundFinishPanel;
    [SerializeField] private Text roundResultText;

    [Header("Buttons")]
    [SerializeField] private Button startButton;
    [FormerlySerializedAs("signalButton")]
    [SerializeField] private Button bellButton;
    [SerializeField] private Button drawButton;
    [SerializeField] private Button pullButton;
    [SerializeField] private Button fireButton;
    [SerializeField] private Button restartButton;

    [Header("Touch Controls (GDD 0.3)")]
    [SerializeField] private TouchActionButton drawTouch;
    [SerializeField] private TouchActionButton pullTouch;
    [SerializeField] private TouchActionButton fireTouch;

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
    private Coroutine _bellRoutine;
    private Coroutine _crossfireRoutine;
    private Coroutine _enemyRoutine;
    private Coroutine _gunFxRoutine;

    private ColorBlock _defaultColors;
    private Color _bellDisabledColor;
    private Color _bellPressedColor;
    private Vector3 _gunRestLocalPos;
    private Sprite _enemyIdleSprite;
    private Material _aimDimMaterial;
    private static readonly int HoleCenterId = Shader.PropertyToID("_HoleCenter");
    private static readonly int HoleRadiusId = Shader.PropertyToID("_HoleRadius");
    private static readonly int HoleSoftnessId = Shader.PropertyToID("_HoleSoftness");
    private static readonly Color PressedTint = new Color(0.55f, 0.55f, 0.55f, 1f);
    private static readonly Color DisabledTint = new Color(0.75f, 0.75f, 0.75f, 0.6f);
    private static readonly Color ActiveTint = Color.white;

    private void Awake()
    {
        if (drawButton != null)
            _defaultColors = drawButton.colors;

        if (enemyBodyImage == null && enemyBody != null)
            enemyBodyImage = enemyBody.GetComponent<Image>();

        if (playerGunRect == null && playerGun != null)
            playerGunRect = playerGun.GetComponent<RectTransform>();

        if (playerGunRect != null)
            _gunRestLocalPos = playerGunRect.localPosition;

        if (enemyBodyImage != null)
        {
            _enemyIdleSprite = enemyBodySprite != null ? enemyBodySprite : enemyBodyImage.sprite;
            if (enemyBodySprite == null)
                enemyBodySprite = _enemyIdleSprite;
        }

        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        EnsureAimDimOverlay();
        ConfigureBellAsCodeDrivenIndicator();
        ConfigureBottomPanelAsIndicators();
        WireButtons();
        EnterWaitingToStart();
    }

    private void Update()
    {
        HandleKeyboardInput();
    }

    private void HandleKeyboardInput()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null)
            return;

        // GDD 0.3: Draw=D, Pull=P, Fire=F
        if (kb.dKey.wasPressedThisFrame)
            OnDrawClicked();
        if (kb.pKey.wasPressedThisFrame)
            OnPullClicked();
        if (kb.fKey.wasPressedThisFrame)
            OnFireClicked();
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

    /// <summary>
    /// AimDim must sit above Background/Enemy/Gun but below CrossfireBlock,
    /// otherwise the background covers the dim and nothing is visible.
    /// </summary>
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

    private void ConfigureBellAsCodeDrivenIndicator()
    {
        if (bellButton == null)
            return;

        _bellDisabledColor = bellButton.colors.disabledColor;
        _bellPressedColor = bellButton.colors.pressedColor;
        bellButton.transition = Selectable.Transition.None;
        bellButton.interactable = false;
        bellButton.onClick.RemoveAllListeners();
    }

    /// <summary>
    /// GDD 0.3: BottomPanel Draw/Pull/Fire are visual indicators only (not clickable).
    /// </summary>
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
            restartButton.onClick.AddListener(OnRestartClicked);
        }

        WireTouchButton(drawTouch, OnDrawClicked);
        WireTouchButton(pullTouch, OnPullClicked);
        WireTouchButton(fireTouch, OnFireClicked);
    }

    private static void WireTouchButton(TouchActionButton touch, UnityEngine.Events.UnityAction action)
    {
        if (touch == null || touch.button == null)
            return;

        touch.button.onClick.RemoveAllListeners();
        touch.button.onClick.AddListener(action);
        // Keep Sprite Swap from the Inspector (Disabled / Highlighted / Pressed).
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
        SetPanelActive(bottomPanel, true);

        SetVisible(playerGun, false);
        SetVisible(crossfire != null ? crossfire.gameObject : null, false);
        SetAimDim(false);
        ResetCrossfireToStart();
        SetBellState(pressed: false);
        RestoreEnemyIdleArt();
        ResetGunPose();

        SetActionState(drawButton, drawTouch, ActionVisualState.Inactive);
        SetActionState(pullButton, pullTouch, ActionVisualState.Inactive);
        SetActionState(fireButton, fireTouch, ActionVisualState.Inactive);
    }

    private void OnStartClicked()
    {
        if (_phase != Phase.WaitingToStart)
            return;

        SetPanelActive(roundStartPanel, false);
        _phase = Phase.WaitingForBell;
        SetActionState(drawButton, drawTouch, ActionVisualState.Active);
        PlaySfx(sfxEagle);
        _bellRoutine = StartCoroutine(BellCountdown());
    }

    private IEnumerator BellCountdown()
    {
        yield return new WaitForSeconds(bellDelaySeconds);

        if (_phase != Phase.WaitingForBell)
            yield break;

        _bellFired = true;
        _phase = Phase.BellShown;
        SetBellState(pressed: true);
        PlaySfx(sfxBell);
        _enemyRoutine = StartCoroutine(EnemyShootLoop());
    }

    private void OnDrawClicked()
    {
        if (_phase == Phase.WaitingForBell && !_bellFired)
        {
            FinishRound("Lose");
            return;
        }

        if (_phase != Phase.BellShown)
            return;

        SetActionState(drawButton, drawTouch, ActionVisualState.Pressed);
        SetActionState(pullButton, pullTouch, ActionVisualState.Active);
        SetVisible(playerGun, true);
        PlaySfx(sfxDraw);
        _phase = Phase.Drawn;
    }

    private void OnPullClicked()
    {
        // First pull of the round.
        if (_phase == Phase.Drawn)
        {
            SetActionState(pullButton, pullTouch, ActionVisualState.Pressed);
            SetActionState(fireButton, fireTouch, ActionVisualState.Active);
            SetVisible(crossfire != null ? crossfire.gameObject : null, true);
            SetAimDim(true);
            PlaySfx(sfxGunPull);
            PlayGunShake();
            StartAimMove(fromStart: true);
            _phase = Phase.Aimed;
            return;
        }

        // Re-pull after miss (GDD 0.2).
        if (_phase == Phase.MissRecovery && _waitingForRepull)
        {
            _repullRequested = true;
            SetActionState(pullButton, pullTouch, ActionVisualState.Pressed);
            SetActionState(fireButton, fireTouch, ActionVisualState.Active);
            PlaySfx(sfxGunPull);
            PlayGunShake();
        }
    }

    private void OnFireClicked()
    {
        if (_phase != Phase.Aimed && _phase != Phase.AimedAfterMiss)
            return;

        PlaySfx(sfxPlayerShot);
        PlayGunRecoil();
        SetActionState(fireButton, fireTouch, ActionVisualState.Pressed);

        if (IsCrossfireOverOpaqueEnemy())
        {
            StopCrossfireRoutine();
            StopEnemyRoutine();
            PlaySfx(sfxEnemyDie);
            FinishRound("Win");
            return;
        }

        PlaySfx(sfxMissPlayer);
        BeginMissRecovery();
    }

    private void OnRestartClicked()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void FinishRound(string result)
    {
        _phase = Phase.Finished;
        StopBellRoutine();
        StopCrossfireRoutine();
        StopEnemyRoutine();

        SetAimDim(false);
        SetActionState(drawButton, drawTouch, ActionVisualState.Inactive);
        SetActionState(pullButton, pullTouch, ActionVisualState.Inactive);
        SetActionState(fireButton, fireTouch, ActionVisualState.Inactive);

        if (roundResultText != null)
            roundResultText.text = result;

        SetPanelActive(roundFinishPanel, true);
    }

    private void BeginMissRecovery()
    {
        StopCrossfireRoutine();
        _waitingForRepull = true;
        _repullRequested = false;
        _phase = Phase.MissRecovery;
        SetActionState(fireButton, fireTouch, ActionVisualState.Inactive);
        SetActionState(pullButton, pullTouch, ActionVisualState.Active);
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

        // Bounce up.
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

        // Move toward midpoint of X.
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

        // Hang at mid until re-pull.
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

        // Perpendicular unit for shake (in local XY).
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
        // First shot after Bell.
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
            FinishRound("Lose");
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
            // Up then back.
            float arc = t < 0.35f ? t / 0.35f : 1f - (t - 0.35f) / 0.65f;
            playerGunRect.localPosition = Vector3.Lerp(_gunRestLocalPos, up, arc);
            yield return null;
        }

        ResetGunPose();
        _gunFxRoutine = null;
    }

    private void ResetGunPose()
    {
        if (playerGunRect != null)
            playerGunRect.localPosition = _gunRestLocalPos;
    }

    private void PlaySfx(AudioClip clip)
    {
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

    private void SetBellState(bool pressed)
    {
        if (bellButton == null)
            return;

        bellButton.interactable = false;
        var image = bellButton.targetGraphic as Graphic;
        if (image == null)
            return;

        image.color = pressed ? _bellPressedColor : _bellDisabledColor;
    }

    private void SetActionState(Button indicator, TouchActionButton touch, ActionVisualState state)
    {
        SetIndicatorVisual(indicator, state);
        SetTouchVisual(touch, state);
    }

    private void SetIndicatorVisual(Button button, ActionVisualState state)
    {
        if (button == null)
            return;

        button.interactable = false;

        var colors = _defaultColors;
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

    /// <summary>
    /// Drives touch Button Sprite Swap:
    /// Inactive → Disabled, Active → Highlighted, Pressed → Pressed (stays after click).
    /// Sprites come from the Button's Sprite State in the Inspector.
    /// </summary>
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
                // Queue is this action: clickable + Highlighted sprite.
                button.transition = Selectable.Transition.SpriteSwap;
                button.interactable = true;
                if (image != null && highlighted != null)
                    image.sprite = highlighted;
                break;

            case ActionVisualState.Pressed:
                // Stay on Pressed art after the action. Disable clicks without
                // letting SpriteSwap overwrite with DisabledSprite.
                button.interactable = false;
                button.transition = Selectable.Transition.None;
                if (image != null && pressed != null)
                    image.sprite = pressed;
                break;

            default:
                // Level start / not yet available: Disabled sprite.
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
