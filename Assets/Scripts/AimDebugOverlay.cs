#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Editor debug drawing for the aim path: waypoint dots, the unshaken trajectory, and player-shot dots.
/// Sizes are in the same local space as <see cref="AimTrajectory"/> samples.
/// </summary>
public class AimDebugOverlay : Graphic
{
    private static readonly Color WaypointColor = Color.white;
    private static readonly Color PathColor = new Color(0f, 1f, 1f, 1f);
    private static readonly Color ShotColor = new Color(0.6f, 0.2f, 0.9f, 1f);

    private Vector2 _start;
    private Vector2 _second;
    private Vector2 _middle;
    private Vector2 _third;
    private List<Vector2> _samples = new List<Vector2>();
    private List<Vector2> _shots = new List<Vector2>();
    private float _pointRadius = 3f;
    private float _lineHalf = 1f;
    private bool _hasPath;

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
        color = Color.white;
    }

    public void SetPath(AimTrajectory.Path path, float pointRadius, float lineThickness)
    {
        _hasPath = path.Samples != null && path.Samples.Count > 1;
        _start = path.Start;
        _second = path.Second;
        _middle = path.Middle;
        _third = path.Third;
        _samples = path.Samples ?? new List<Vector2>();
        _pointRadius = Mathf.Max(0.5f, pointRadius);
        _lineHalf = Mathf.Max(0.25f, lineThickness * 0.5f);
        SetVerticesDirty();
    }

    public void SetShots(List<Vector2> shots)
    {
        _shots = shots ?? new List<Vector2>();
        SetVerticesDirty();
    }

    public void ClearPath()
    {
        _hasPath = false;
        _samples = new List<Vector2>();
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (_hasPath)
        {
            for (int i = 0; i < _samples.Count - 1; i++)
                AddSegment(vh, _samples[i], _samples[i + 1], _lineHalf, PathColor);
            AddDisc(vh, _start, _pointRadius, WaypointColor);
            AddDisc(vh, _second, _pointRadius, WaypointColor);
            AddDisc(vh, _middle, _pointRadius, WaypointColor);
            AddDisc(vh, _third, _pointRadius, WaypointColor);
        }

        if (_shots == null)
            return;
        for (int i = 0; i < _shots.Count; i++)
            AddDisc(vh, _shots[i], _pointRadius, ShotColor);
    }

    private static void AddSegment(VertexHelper vh, Vector2 a, Vector2 b, float halfWidth, Color color)
    {
        Vector2 dir = b - a;
        if (dir.sqrMagnitude < 0.01f)
            return;
        Vector2 n = new Vector2(-dir.y, dir.x).normalized * halfWidth;
        int i = vh.currentVertCount;
        UIVertex v = UIVertex.simpleVert;
        v.color = color;
        v.position = a - n;
        vh.AddVert(v);
        v.position = a + n;
        vh.AddVert(v);
        v.position = b + n;
        vh.AddVert(v);
        v.position = b - n;
        vh.AddVert(v);
        vh.AddTriangle(i, i + 1, i + 2);
        vh.AddTriangle(i, i + 2, i + 3);
    }

    private static void AddDisc(VertexHelper vh, Vector2 center, float radius, Color color)
    {
        const int segments = 10;
        int hub = vh.currentVertCount;
        UIVertex v = UIVertex.simpleVert;
        v.color = color;
        v.position = center;
        vh.AddVert(v);
        int ring = vh.currentVertCount;
        for (int i = 0; i <= segments; i++)
        {
            float a = i / (float)segments * Mathf.PI * 2f;
            v.position = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
            vh.AddVert(v);
        }

        for (int i = 0; i < segments; i++)
            vh.AddTriangle(hub, ring + i, ring + i + 1);
    }
}
#endif
