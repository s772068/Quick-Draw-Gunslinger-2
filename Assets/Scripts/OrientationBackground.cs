using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Single Background Image: portrait → back_vert, landscape → back_hor.
/// Aspect from game window (Canvas.pixelRect), same rule as OrientationControls.
/// </summary>
[RequireComponent(typeof(Image))]
[ExecuteAlways]
[DisallowMultipleComponent]
public class OrientationBackground : MonoBehaviour
{
    [SerializeField] private Image targetImage;
    [SerializeField] private Sprite backVertSprite;
    [SerializeField] private Sprite backHorSprite;
    [Tooltip("Legacy aliases — used if backVert/backHor are empty.")]
    [SerializeField] private Sprite portraitSprite;
    [SerializeField] private Sprite landscapeSprite;
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
        if (targetImage == null)
            targetImage = GetComponent<Image>();
        ApplyForCurrentView(force: true);
    }

    private void Start()
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
        if (!Application.isPlaying)
            ApplyForCurrentView(force: true);
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

        Sprite vert = backVertSprite != null ? backVertSprite : portraitSprite;
        Sprite hor = backHorSprite != null ? backHorSprite : landscapeSprite;
        Sprite next = portrait ? vert : hor;
        if (next == null)
            next = portrait ? hor : vert;

        if (next != null && targetImage.sprite != next)
            targetImage.sprite = next;

        if (fillRectWithoutPreserveAspect)
            targetImage.preserveAspect = false;
    }

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
