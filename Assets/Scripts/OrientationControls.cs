using UnityEngine;

/// <summary>
/// Portrait (height &gt;= width): VerticalControls on, HorizontalControls off.
/// Landscape: HorizontalControls on, VerticalControls off.
/// Same aspect rule as OrientationBackground (GDD 0.4).
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class OrientationControls : MonoBehaviour
{
    [SerializeField] private GameObject verticalControls;
    [SerializeField] private GameObject horizontalControls;

    private int _lastWidth = -1;
    private int _lastHeight = -1;
    private bool _lastPortrait;

    private void Awake()
    {
        AutoFindIfNeeded();
        Apply(force: true);
    }

    private void OnEnable()
    {
        AutoFindIfNeeded();
        Apply(force: true);
    }

    private void Start()
    {
        Apply(force: true);
    }

    private void Update()
    {
        Apply(force: false);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        AutoFindIfNeeded();
        if (!Application.isPlaying)
            Apply(force: true);
    }
#endif

    public void Configure(GameObject vertical, GameObject horizontal)
    {
        if (vertical != null)
            verticalControls = vertical;
        if (horizontal != null)
            horizontalControls = horizontal;
        Apply(force: true);
    }

    public void ApplyNow()
    {
        AutoFindIfNeeded();
        Apply(force: true);
    }

    private void AutoFindIfNeeded()
    {
        Transform root = transform;
        if (verticalControls == null)
        {
            var t = root.Find("VerticalControls");
            if (t == null && root.parent != null)
                t = root.parent.Find("VerticalControls");
            if (t != null)
                verticalControls = t.gameObject;
        }

        if (horizontalControls == null)
        {
            var t = root.Find("HorizontalControls");
            if (t == null && root.parent != null)
                t = root.parent.Find("HorizontalControls");
            if (t != null)
                horizontalControls = t.gameObject;
        }
    }

    private void Apply(bool force)
    {
        AutoFindIfNeeded();
        if (verticalControls == null && horizontalControls == null)
            return;

        GetViewSize(out int w, out int h);
        bool portrait = h >= w;

        if (!force && w == _lastWidth && h == _lastHeight && portrait == _lastPortrait)
        {
            // Still enforce exclusivity in case something re-enabled the wrong root.
            EnforceExclusive(portrait);
            return;
        }

        _lastWidth = w;
        _lastHeight = h;
        _lastPortrait = portrait;
        EnforceExclusive(portrait);
    }

    private void EnforceExclusive(bool portrait)
    {
        // Vertical orientation → only VerticalControls.
        // Horizontal orientation → only HorizontalControls.
        if (verticalControls != null && verticalControls.activeSelf != portrait)
            verticalControls.SetActive(portrait);
        if (horizontalControls != null && horizontalControls.activeSelf != !portrait)
            horizontalControls.SetActive(!portrait);
    }

    private void GetViewSize(out int width, out int height)
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            canvas = GetComponent<Canvas>();

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
