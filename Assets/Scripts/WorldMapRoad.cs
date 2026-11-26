using UnityEngine;
using UnityEngine.UI;

public sealed class WorldMapRoad : MaskableGraphic
{
    private const int StepsPerSection = 24;
    private static readonly Color32 RoadEdge = new Color32(209, 137, 67, 110);
    private static readonly Color32 RoadSurface = new Color32(255, 195, 115, 255);

    // Positions run from the foreground through Dry Creek and Dead Well to Raven Rock.
    private static readonly Vector2[] Points =
    {
        new Vector2(.66f, 0f),
        new Vector2(.57f, .34f),
        new Vector2(.25f, .53f),
        new Vector2(.69f, .73f)
    };

    private static readonly float[] Widths = { .047f, .040f, .029f, .017f };

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Rect rect = rectTransform.rect;
        if (rect.width <= 0f || rect.height <= 0f)
            return;

        for (int section = 0; section < Points.Length - 1; section++)
        {
            Vector2 start = Points[section];
            Vector2 end = Points[section + 1];
            Vector2 startTangent = .5f * (end - Points[Mathf.Max(0, section - 1)]);
            if (section == 0)
                startTangent.x = 0f;
            Vector2 endTangent = .5f * (Points[Mathf.Min(Points.Length - 1, section + 2)] - start);
            Vector2 a = 2f * (start - end) + startTangent + endTangent;
            Vector2 b = 3f * (end - start) - 2f * startTangent - endTangent;

            for (int step = section == 0 ? 0 : 1; step <= StepsPerSection; step++)
            {
                float t = (float)step / StepsPerSection;
                Vector2 center = ((a * t + b) * t + startTangent) * t + start;
                Vector2 tangent = (3f * a * t + 2f * b) * t + startTangent;
                Vector2 normal = new Vector2(-tangent.y * rect.height, tangent.x * rect.width).normalized;
                Vector2 position = rect.min + Vector2.Scale(center, rect.size);
                Vector2 offset = normal * (Mathf.Lerp(Widths[section], Widths[section + 1], t) * rect.height);
                int row = mesh.currentVertCount;
                mesh.AddVert(position - offset, RoadEdge, Vector2.zero);
                mesh.AddVert(position - offset * .88f, RoadSurface, Vector2.zero);
                mesh.AddVert(position + offset * .88f, RoadSurface, Vector2.zero);
                mesh.AddVert(position + offset, RoadEdge, Vector2.zero);

                if (row == 0)
                    continue;

                for (int lane = 0; lane < 3; lane++)
                {
                    int previous = row - 4 + lane;
                    int current = row + lane;
                    mesh.AddTriangle(previous, current, previous + 1);
                    mesh.AddTriangle(previous + 1, current, current + 1);
                }
            }
        }
    }
}
