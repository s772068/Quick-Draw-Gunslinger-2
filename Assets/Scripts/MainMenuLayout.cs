using UnityEngine;

/// <summary>
/// Keeps MainMenu readable on Free Aspect / Yandex iframes.
/// Leaderboards sit on the bottom; Begin sits above them.
/// Title is left as authored in the scene.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class MainMenuLayout : MonoBehaviour
{
    [SerializeField] private RectTransform beginButton;
    [SerializeField] private RectTransform leaderboardsPanel;

    [SerializeField] private float bottomPadding = 28f;
    [SerializeField] private float beginGap = 24f;
    [SerializeField] private float minPanelHeight = 340f;
    [SerializeField] private float maxPanelHeight = 760f;
    [SerializeField] private float beginHeight = 110f;
    [SerializeField] private float panelWidth = 600f;
    [SerializeField] private float beginWidth = 420f;

    private float _lastW = -1f;
    private float _lastH = -1f;

    private void OnEnable()
    {
        Apply(force: true);
    }

    private void OnRectTransformDimensionsChange()
    {
        Apply(force: false);
    }

    private void LateUpdate()
    {
        Apply(force: false);
    }

    private void AutoBind()
    {
        Transform root = transform;

        if (beginButton == null)
        {
            Transform t = root.Find("Begin");
            if (t == null)
                t = root.Find("Start");
            if (t != null)
                beginButton = t as RectTransform;
        }

        if (leaderboardsPanel == null)
        {
            Transform t = root.Find("LeaderboardsPanel");
            if (t != null)
                leaderboardsPanel = t as RectTransform;
        }
    }

    private void Apply(bool force)
    {
        AutoBind();

        var root = transform as RectTransform;
        if (root == null)
            return;

        float w = root.rect.width;
        float h = root.rect.height;
        if (w < 8f || h < 8f)
            return;

        if (!force && Mathf.Abs(w - _lastW) < 0.5f && Mathf.Abs(h - _lastH) < 0.5f)
            return;

        _lastW = w;
        _lastH = h;

        float panelH = Mathf.Clamp(h * 0.46f, minPanelHeight, maxPanelHeight);
        float room = h - 140f - beginHeight - beginGap - bottomPadding;
        if (room < panelH)
            panelH = Mathf.Max(minPanelHeight * 0.85f, room);

        float panelW = Mathf.Min(panelWidth, Mathf.Max(280f, w - 48f));
        float btnW = Mathf.Min(beginWidth, Mathf.Max(220f, w - 80f));

        if (leaderboardsPanel != null)
        {
            leaderboardsPanel.anchorMin = new Vector2(0.5f, 0f);
            leaderboardsPanel.anchorMax = new Vector2(0.5f, 0f);
            leaderboardsPanel.pivot = new Vector2(0.5f, 0f);
            leaderboardsPanel.sizeDelta = new Vector2(panelW, panelH);
            leaderboardsPanel.anchoredPosition = new Vector2(0f, bottomPadding);
        }

        if (beginButton != null)
        {
            beginButton.anchorMin = new Vector2(0.5f, 0f);
            beginButton.anchorMax = new Vector2(0.5f, 0f);
            beginButton.pivot = new Vector2(0.5f, 0f);
            beginButton.sizeDelta = new Vector2(btnW, beginHeight);
            beginButton.anchoredPosition = new Vector2(0f, bottomPadding + panelH + beginGap);
        }
    }
}
