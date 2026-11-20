using UnityEngine;

[ExecuteAlways]
[DefaultExecutionOrder(100)]
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class WorldMapViewport : MonoBehaviour
{
    [SerializeField] private RectTransform sideBackground;

    private RectTransform viewport;
    private Canvas canvas;
    private DrivenRectTransformTracker tracker;

    private void OnEnable()
    {
        viewport = (RectTransform)transform;
        canvas = viewport.GetComponentInParent<Canvas>();
        if (canvas == null || sideBackground == null)
            return;

        tracker.Add(this, viewport, DrivenTransformProperties.AnchoredPosition);
        tracker.Add(this, sideBackground, DrivenTransformProperties.SizeDelta);
        Canvas.preWillRenderCanvases += Fit;
    }

    private void OnDisable()
    {
        Canvas.preWillRenderCanvases -= Fit;
        tracker.Clear();
    }

    private void Fit()
    {
        if (canvas == null || sideBackground == null || canvas.scaleFactor <= 0f)
            return;

        // CanvasScaler updates in the same render pass, before this component.
        Vector2 size = canvas.pixelRect.size / canvas.scaleFactor;
        Vector2 margin = Vector2.Max(Vector2.zero, (size - viewport.sizeDelta) * .5f);
        Vector2 position = new Vector2(Mathf.Floor(margin.x), -Mathf.Floor(margin.y));
        Vector2 backgroundSize = new Vector2(Mathf.Ceil(size.x), Mathf.Ceil(size.y));
        if (viewport.anchoredPosition != position)
            viewport.anchoredPosition = position;
        if (sideBackground.sizeDelta != backgroundSize)
            sideBackground.sizeDelta = backgroundSize;
    }
}
