using UnityEngine;
using UnityEngine.UI;

public sealed class TrainingShadeGraphic : MaskableGraphic, ICanvasRaycastFilter
{
    private const int SegmentsPerSide = 24;
    private Rect _highlight;

    public void SetHighlight(Rect highlight)
    {
        if (_highlight == highlight)
            return;

        _highlight = highlight;
        SetVerticesDirty();
    }

    public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
    {
        if (!HasHighlight(rectTransform.rect) ||
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPoint, eventCamera, out Vector2 point))
            return true;

        Vector2 normalized = (point - _highlight.center) / (_highlight.size * 0.5f);
        return normalized.sqrMagnitude >= 1f;
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Rect bounds = rectTransform.rect;
        if (!HasHighlight(bounds))
        {
            for (int i = 0; i < 4; i++)
                mesh.AddVert(Corner(bounds, i), color, Vector2.zero);
            mesh.AddTriangle(0, 1, 2);
            mesh.AddTriangle(0, 2, 3);
            return;
        }

        Vector2 center = _highlight.center;
        Vector2 radii = _highlight.size * 0.5f;
        // Include each rectangle corner so the shade reaches every edge of the viewport.
        for (int side = 0; side < 4; side++)
        {
            Vector2 from = Corner(bounds, side) - center;
            Vector2 to = Corner(bounds, (side + 1) % 4) - center;
            float start = Mathf.Atan2(from.y, from.x);
            float end = Mathf.Atan2(to.y, to.x);
            if (end <= start)
                end += Mathf.PI * 2f;

            for (int segment = 0; segment < SegmentsPerSide; segment++)
            {
                float angle = Mathf.Lerp(start, end, (float)segment / SegmentsPerSide);
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                float outer = DistanceToEdge(bounds, center, direction);
                float inner = 1f / (direction / radii).magnitude;
                mesh.AddVert(center + direction * outer, color, Vector2.zero);
                mesh.AddVert(center + direction * Mathf.Min(inner, outer), color, Vector2.zero);
            }
        }

        int vertices = mesh.currentVertCount;
        for (int i = 0; i < vertices; i += 2)
        {
            int next = (i + 2) % vertices;
            mesh.AddTriangle(i, next, i + 1);
            mesh.AddTriangle(next, next + 1, i + 1);
        }
    }

    private bool HasHighlight(Rect bounds) =>
        _highlight.width > 0f && _highlight.height > 0f && bounds.Contains(_highlight.center);

    private static Vector2 Corner(Rect rect, int index) => index switch
    {
        0 => rect.min,
        1 => new Vector2(rect.xMax, rect.yMin),
        2 => rect.max,
        _ => new Vector2(rect.xMin, rect.yMax)
    };

    private static float DistanceToEdge(Rect rect, Vector2 point, Vector2 direction)
    {
        float x = direction.x == 0f ? float.PositiveInfinity
            : ((direction.x > 0f ? rect.xMax : rect.xMin) - point.x) / direction.x;
        float y = direction.y == 0f ? float.PositiveInfinity
            : ((direction.y > 0f ? rect.yMax : rect.yMin) - point.y) / direction.y;
        return Mathf.Min(x, y);
    }
}
