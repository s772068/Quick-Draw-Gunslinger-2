using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Swaps Background Image sprite by game-window aspect (not monitor):
/// height &gt;= width → portrait sprite, otherwise landscape.
/// </summary>
[RequireComponent(typeof(Image))]
[ExecuteAlways]
public class OrientationBackground : MonoBehaviour
{
    [SerializeField] private Image targetImage;
    [SerializeField] private Sprite portraitSprite;
    [SerializeField] private Sprite landscapeSprite;
    [Tooltip("If on, Preserve Aspect is forced off so each art fills the stretch Rect Transform.")]
    [SerializeField] private bool fillRectWithoutPreserveAspect = true;

    private int _lastWidth = -1;
    private int _lastHeight = -1;
    private bool _lastPortrait;

    private void Awake()
    {
        if (targetImage == null)
            targetImage = GetComponent<Image>();

        ApplyForCurrentView(force: true);
    }

    private void OnEnable()
    {
        ApplyForCurrentView(force: true);
    }

    private void Update()
    {
        GetViewSize(out int w, out int h);
        if (w != _lastWidth || h != _lastHeight)
            ApplyForCurrentView(force: false);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (targetImage == null)
            targetImage = GetComponent<Image>();
    }
#endif

    private void ApplyForCurrentView(bool force)
    {
        if (targetImage == null)
            return;

        GetViewSize(out int w, out int h);
        bool portrait = h >= w;

        if (!force && w == _lastWidth && h == _lastHeight && portrait == _lastPortrait)
            return;

        _lastWidth = w;
        _lastHeight = h;
        _lastPortrait = portrait;

        Sprite next = portrait ? portraitSprite : landscapeSprite;
        if (next == null)
            next = portrait ? landscapeSprite : portraitSprite;

        if (next != null && targetImage.sprite != next)
            targetImage.sprite = next;

        if (fillRectWithoutPreserveAspect)
            targetImage.preserveAspect = false;
    }

    /// <summary>
    /// Prefer the Canvas pixel rect (browser/game window). Fall back to Screen.
    /// </summary>
    private void GetViewSize(out int width, out int height)
    {
        Canvas canvas = targetImage != null ? targetImage.canvas : null;
        if (canvas == null)
            canvas = GetComponentInParent<Canvas>();

        if (canvas != null)
        {
            Rect pixel = canvas.pixelRect;
            int pw = Mathf.RoundToInt(pixel.width);
            int ph = Mathf.RoundToInt(pixel.height);
            if (pw > 0 && ph > 0)
            {
                width = pw;
                height = ph;
                return;
            }
        }

        width = Mathf.Max(1, Screen.width);
        height = Mathf.Max(1, Screen.height);
    }
}
