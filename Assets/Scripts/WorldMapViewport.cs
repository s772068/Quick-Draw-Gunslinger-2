using System;
using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
[DefaultExecutionOrder(100)]
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class WorldMapViewport : MonoBehaviour
{
    private static readonly Vector2 LandscapeSize = new Vector2(1600f, 900f);

    [SerializeField] private RectTransform sideBackground;
    [SerializeField] private Image menuBackground;
    [SerializeField] private Sprite landscapeMenuBackground;
    [SerializeField] private Image worldMapBackground;
    [SerializeField] private Sprite landscapeWorldMapBackground;
    [SerializeField] private WorldMapRoad landscapeRoad;
    [SerializeField] private RectLayout[] landscapeLayout = Array.Empty<RectLayout>();

    private RectTransform viewport;
    private Canvas canvas;
    private DrivenRectTransformTracker tracker;
    private Vector2 portraitSize;
    private Vector3 portraitScale;
    private Sprite portraitMenuBackground;
    private Sprite portraitWorldMapBackground;
    private RectLayout[] portraitLayout;
    private bool landscape;

    [Serializable]
    private struct RectLayout
    {
        public RectTransform target;
        public Vector2 position;
        public Vector2 size;
        public Vector3 scale;

        public RectLayout(RectTransform target)
        {
            this = default;
            this.target = target;
            if (target == null)
                return;

            position = target.anchoredPosition;
            size = target.sizeDelta;
            scale = target.localScale;
        }

        public void Apply()
        {
            if (target == null)
                return;

            target.anchoredPosition = position;
            target.sizeDelta = size;
            target.localScale = scale;
        }
    }

    private void OnEnable()
    {
        viewport = (RectTransform)transform;
        canvas = viewport.GetComponentInParent<Canvas>();
        if (canvas == null || sideBackground == null)
            return;

        tracker.Add(this, viewport, DrivenTransformProperties.AnchoredPosition);
        tracker.Add(this, sideBackground, DrivenTransformProperties.SizeDelta);

        if (Application.IsPlaying(gameObject) && landscapeLayout.Length > 0)
        {
            portraitSize = viewport.sizeDelta;
            portraitScale = viewport.localScale;
            portraitMenuBackground = menuBackground == null ? null : menuBackground.sprite;
            portraitWorldMapBackground = worldMapBackground == null ? null : worldMapBackground.sprite;
            portraitLayout = Array.ConvertAll(landscapeLayout, layout => new RectLayout(layout.target));
        }

        Canvas.preWillRenderCanvases += Fit;
    }

    private void OnDisable()
    {
        Canvas.preWillRenderCanvases -= Fit;
        if (landscape)
        {
            ApplyOrientation(false);
            viewport.localScale = portraitScale;
        }
        portraitLayout = null;
        tracker.Clear();
    }

    private void Fit()
    {
        if (canvas == null || sideBackground == null || canvas.scaleFactor <= 0f)
            return;

        // CanvasScaler updates in the same render pass, before this component.
        Vector2 size = canvas.pixelRect.size / canvas.scaleFactor;
        bool useLandscape = portraitLayout != null && size.x > size.y;
        if (useLandscape != landscape)
            ApplyOrientation(useLandscape);

        float fit = landscape
            ? Mathf.Min(size.x / LandscapeSize.x, size.y / LandscapeSize.y)
            : 1f;
        if (portraitLayout != null && viewport.localScale != portraitScale * fit)
            viewport.localScale = portraitScale * fit;

        Vector2 margin = Vector2.Max(Vector2.zero, (size - viewport.sizeDelta * fit) * .5f);
        Vector2 position = new Vector2(Mathf.Floor(margin.x), -Mathf.Floor(margin.y));
        Vector2 backgroundSize = new Vector2(Mathf.Ceil(size.x), Mathf.Ceil(size.y));
        if (viewport.anchoredPosition != position)
            viewport.anchoredPosition = position;
        if (sideBackground.sizeDelta != backgroundSize)
            sideBackground.sizeDelta = backgroundSize;
    }

    private void ApplyOrientation(bool useLandscape)
    {
        landscape = useLandscape;
        viewport.sizeDelta = landscape ? LandscapeSize : portraitSize;
        foreach (RectLayout rect in landscape ? landscapeLayout : portraitLayout)
            rect.Apply();
        if (menuBackground != null && landscapeMenuBackground != null)
            menuBackground.sprite = landscape ? landscapeMenuBackground : portraitMenuBackground;
        if (worldMapBackground != null && landscapeWorldMapBackground != null)
            worldMapBackground.sprite = landscape ? landscapeWorldMapBackground : portraitWorldMapBackground;
        if (landscapeRoad != null)
            landscapeRoad.enabled = landscape;
    }
}
