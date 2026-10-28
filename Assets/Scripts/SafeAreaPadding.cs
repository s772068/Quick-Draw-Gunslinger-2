using UnityEngine;

/// <summary>
/// Keeps HUD duel controls inside the visible area with edge padding on every platform.
/// Buttons use fixed center-anchored positions — shrinking HUD anchors clips them.
/// This scales Vertical/HorizontalControls to fit instead.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class SafeAreaPadding : MonoBehaviour
{
    [SerializeField] private float edgePadding = 24f;
    [SerializeField] private RectTransform verticalControls;
    [SerializeField] private RectTransform horizontalControls;
    [SerializeField] private RectTransform topPanel;

    private RectTransform _hud;
    private Canvas _canvas;
    private Vector2 _lastHudSize;
    private Rect _lastSafe;
    private float _lastPad = float.NaN;
    private Vector2 _verticalDesignPos;
    private Vector2 _horizontalDesignPos;
    private Vector2 _topDesignSizeDelta;
    private Vector2 _topDesignPos;
    private bool _designCaptured;

    private void Awake()
    {
        Cache();
        CaptureDesign();
        Apply(force: true);
    }

    private void OnEnable()
    {
        Cache();
        CaptureDesign();
        Apply(force: true);
    }

    private void LateUpdate()
    {
        Apply(force: false);
    }

    private void Cache()
    {
        _hud = transform as RectTransform;
        _canvas = GetComponentInParent<Canvas>();
        AutoFind();
    }

    private void AutoFind()
    {
        if (verticalControls == null)
        {
            Transform t = transform.Find("VerticalControls");
            if (t != null)
                verticalControls = t as RectTransform;
        }

        if (horizontalControls == null)
        {
            Transform t = transform.Find("HorizontalControls");
            if (t != null)
                horizontalControls = t as RectTransform;
        }

        if (topPanel == null)
        {
            Transform t = transform.Find("TopPanel");
            if (t != null)
                topPanel = t as RectTransform;
        }
    }

    private void CaptureDesign()
    {
        if (_designCaptured)
            return;

        AutoFind();
        if (verticalControls != null)
            _verticalDesignPos = verticalControls.anchoredPosition;
        if (horizontalControls != null)
            _horizontalDesignPos = horizontalControls.anchoredPosition;
        if (topPanel != null)
        {
            _topDesignSizeDelta = topPanel.sizeDelta;
            _topDesignPos = topPanel.anchoredPosition;
        }

        _designCaptured = true;
    }

    private void Apply(bool force)
    {
        if (_hud == null)
            _hud = transform as RectTransform;
        if (_hud == null)
            return;

        AutoFind();
        CaptureDesign();

        // Full-bleed HUD — never shrink anchors.
        _hud.anchorMin = Vector2.zero;
        _hud.anchorMax = Vector2.one;
        _hud.offsetMin = Vector2.zero;
        _hud.offsetMax = Vector2.zero;

        Rect hudRect = _hud.rect;
        Rect safe = Screen.safeArea;
        float pad = Mathf.Max(0f, edgePadding);

        if (!force
            && hudRect.size == _lastHudSize
            && Approximately(safe, _lastSafe)
            && Mathf.Approximately(pad, _lastPad))
            return;

        _lastHudSize = hudRect.size;
        _lastSafe = safe;
        _lastPad = pad;

        GetSafeInsetsCanvas(out float insetL, out float insetB, out float insetR, out float insetT);
        float padL = Mathf.Max(pad, insetL);
        float padB = Mathf.Max(pad, insetB);
        float padR = Mathf.Max(pad, insetR);
        float padT = Mathf.Max(pad, insetT);

        float leftRoom = Mathf.Max(1f, hudRect.width * 0.5f - padL);
        float rightRoom = Mathf.Max(1f, hudRect.width * 0.5f - padR);
        float bottomRoom = Mathf.Max(1f, hudRect.height * 0.5f - padB);
        float topRoom = Mathf.Max(1f, hudRect.height * 0.5f - padT);
        float availHalfW = Mathf.Min(leftRoom, rightRoom);
        float availHalfH = Mathf.Min(bottomRoom, topRoom);

        float shiftX = (padL - padR) * 0.5f;
        float shiftY = (padB - padT) * 0.5f;

        FitGroup(verticalControls, _verticalDesignPos, availHalfW, availHalfH, shiftX, shiftY);
        FitGroup(horizontalControls, _horizontalDesignPos, availHalfW, availHalfH, shiftX, shiftY);
        FitTopPanel(padL, padR, padT);
    }

    private static void FitGroup(
        RectTransform group,
        Vector2 designPos,
        float availHalfW,
        float availHalfH,
        float shiftX,
        float shiftY)
    {
        if (group == null)
            return;

        group.localScale = Vector3.one;
        GetDesignHalfExtents(group, out float needX, out float needY);
        needX = Mathf.Max(1f, needX);
        needY = Mathf.Max(1f, needY);

        float scale = Mathf.Min(1f, availHalfW / needX, availHalfH / needY);
        group.localScale = new Vector3(scale, scale, 1f);
        group.anchoredPosition = designPos + new Vector2(shiftX, shiftY);
    }

    private void FitTopPanel(float padL, float padR, float padT)
    {
        if (topPanel == null)
            return;

        float side = Mathf.Max(Mathf.Abs(_topDesignSizeDelta.x), padL + padR);
        topPanel.sizeDelta = new Vector2(-side, _topDesignSizeDelta.y);
        topPanel.anchoredPosition = new Vector2(
            _topDesignPos.x + (padL - padR) * 0.5f,
            -Mathf.Max(Mathf.Abs(_topDesignPos.y), padT));
    }

    private static void GetDesignHalfExtents(RectTransform group, out float halfW, out float halfH)
    {
        halfW = 0f;
        halfH = 0f;
        if (group == null)
            return;

        for (int i = 0; i < group.childCount; i++)
        {
            var child = group.GetChild(i) as RectTransform;
            if (child == null)
                continue;

            Vector2 pos = child.anchoredPosition;
            Vector2 size = child.sizeDelta;
            if (size.x < 1f || size.y < 1f)
            {
                Vector2 rectSize = child.rect.size;
                if (rectSize.x >= 1f)
                    size.x = rectSize.x;
                if (rectSize.y >= 1f)
                    size.y = rectSize.y;
            }

            halfW = Mathf.Max(halfW, Mathf.Abs(pos.x) + size.x * 0.5f);
            halfH = Mathf.Max(halfH, Mathf.Abs(pos.y) + size.y * 0.5f);
        }

        if (halfW < 1f)
            halfW = 520f;
        if (halfH < 1f)
            halfH = 780f;
    }

    private void GetSafeInsetsCanvas(out float left, out float bottom, out float right, out float top)
    {
        left = bottom = right = top = 0f;
        if (_canvas == null)
            _canvas = GetComponentInParent<Canvas>();
        if (_canvas == null)
            return;

        Rect pixel = _canvas.pixelRect;
        if (pixel.width < 1f || pixel.height < 1f)
            return;

        Rect safe = Screen.safeArea;
        float sf = _canvas.scaleFactor > 0.01f ? _canvas.scaleFactor : 1f;

        left = Mathf.Max(0f, (safe.xMin - pixel.xMin) / sf);
        bottom = Mathf.Max(0f, (safe.yMin - pixel.yMin) / sf);
        right = Mathf.Max(0f, (pixel.xMax - safe.xMax) / sf);
        top = Mathf.Max(0f, (pixel.yMax - safe.yMax) / sf);
    }

    private static bool Approximately(Rect a, Rect b)
    {
        return Mathf.Approximately(a.x, b.x)
            && Mathf.Approximately(a.y, b.y)
            && Mathf.Approximately(a.width, b.width)
            && Mathf.Approximately(a.height, b.height);
    }
}
