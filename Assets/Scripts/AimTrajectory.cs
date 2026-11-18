using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// GDD 1.1 crosshair path: Start(225°) → Second → Middle → Third → Start → …
/// Constant speed so Middle is first reached at TimeToMiddle seconds from Pull.
/// </summary>
public static class AimTrajectory
{
    public struct Path
    {
        public Vector2 Middle;
        public float Radius;
        public Vector2 Start;
        public Vector2 Second;
        public Vector2 Third;
        public float Speed;
        public List<Vector2> Samples;
        public float LengthToMiddle;
        public float TotalLength;
    }

    public static Path Build(RectTransform enemyBody, float radius, float timeToMiddle)
    {
        Vector2 middle = MiddlePointLocal(enemyBody);
        float r = Mathf.Max(40f, radius);
        Vector2 start = PointOnCircle(middle, r, 225f * Mathf.Deg2Rad);

        PickSecondThird(middle, r, out Vector2 second, out Vector2 third);

        var samples = new List<Vector2>(256);
        AppendArc(samples, start, second, r, middle);
        AppendArc(samples, second, middle, r, middle);
        int middleIndex = samples.Count - 1;
        samples[middleIndex] = middle;
        AppendArc(samples, middle, third, r, middle);
        AppendArc(samples, third, start, r, middle);

        float lengthToMiddle = PolyLength(samples, 0, middleIndex);
        float total = PolyLength(samples, 0, samples.Count - 1);
        float t = Mathf.Max(0.05f, timeToMiddle);
        float speed = lengthToMiddle / t;

        return new Path
        {
            Middle = middle,
            Radius = r,
            Start = start,
            Second = second,
            Third = third,
            Speed = Mathf.Max(10f, speed),
            Samples = samples,
            LengthToMiddle = lengthToMiddle,
            TotalLength = Mathf.Max(0.01f, total)
        };
    }

    public static Vector2 SampleAtDistance(Path path, float distance)
    {
        if (path.Samples == null || path.Samples.Count == 0)
            return Vector2.zero;

        float loop = path.TotalLength;
        float d = distance % loop;
        if (d < 0f)
            d += loop;

        float walked = 0f;
        for (int i = 0; i < path.Samples.Count - 1; i++)
        {
            Vector2 a = path.Samples[i];
            Vector2 b = path.Samples[i + 1];
            float seg = Vector2.Distance(a, b);
            if (walked + seg >= d)
            {
                float t = seg > 0.0001f ? (d - walked) / seg : 0f;
                return Vector2.Lerp(a, b, t);
            }
            walked += seg;
        }

        return path.Samples[path.Samples.Count - 1];
    }

    public static Vector2 ClampToAllowedCircle(Path path, Vector2 pos)
    {
        Vector2 delta = pos - path.Middle;
        float max = path.Radius * 1.1f;
        float mag = delta.magnitude;
        if (mag <= max || mag < 0.0001f)
            return pos;
        return path.Middle + delta / mag * max;
    }

    public static Vector2 MiddlePointLocal(RectTransform enemyBody)
    {
        if (enemyBody == null)
            return Vector2.zero;
        Rect rect = enemyBody.rect;
        float x = rect.xMin + rect.width * 0.5f;
        float y = rect.yMax - rect.height * 0.33f;
        return new Vector2(x, y);
    }

    private static void PickSecondThird(Vector2 middle, float r, out Vector2 second, out Vector2 third)
    {
        // Allowed quadrants: TL (90-180), TR (0-90), BR (270-360). Not BL (180-270).
        int qA = Random.Range(0, 3);
        int qB = (qA + 1 + Random.Range(0, 2)) % 3;
        second = PointOnCircle(middle, r, RandomAngleInQuadrant(qA));
        third = PointOnCircle(middle, r, RandomAngleInQuadrant(qB));
    }

    private static float RandomAngleInQuadrant(int q)
    {
        // 0 = TR, 1 = TL, 2 = BR
        switch (q)
        {
            case 1: return Random.Range(90f, 180f) * Mathf.Deg2Rad;
            case 2: return Random.Range(270f, 360f) * Mathf.Deg2Rad;
            default: return Random.Range(0f, 90f) * Mathf.Deg2Rad;
        }
    }

    private static Vector2 PointOnCircle(Vector2 c, float r, float rad)
    {
        return c + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * r;
    }

    private static void AppendArc(List<Vector2> samples, Vector2 from, Vector2 to, float radiusLimit, Vector2 circleCenter)
    {
        Vector2 mid = (from + to) * 0.5f;
        Vector2 chord = to - from;
        Vector2 n = chord.sqrMagnitude > 0.01f
            ? new Vector2(-chord.y, chord.x).normalized
            : Vector2.up;
        // Bulge outward from chord, preferring away from circle center for figure-eight feel.
        Vector2 away = mid - circleCenter;
        float side = away.sqrMagnitude > 0.01f ? Mathf.Sign(Vector2.Dot(away, n)) : 1f;
        if (Mathf.Abs(side) < 0.1f)
            side = 1f;
        Vector2 ctrl = mid + n * side * Vector2.Distance(from, to) * 0.4f;

        int steps = 28;
        int startIdx = samples.Count == 0 ? 0 : 1;
        for (int i = startIdx; i <= steps; i++)
        {
            float t = i / (float)steps;
            Vector2 p = Quad(from, ctrl, to, t);
            Vector2 d = p - circleCenter;
            float max = radiusLimit * 1.1f;
            if (d.magnitude > max)
                p = circleCenter + d.normalized * max;
            samples.Add(p);
        }
    }

    private static Vector2 Quad(Vector2 a, Vector2 b, Vector2 c, float t)
    {
        float u = 1f - t;
        return u * u * a + 2f * u * t * b + t * t * c;
    }

    private static float PolyLength(List<Vector2> pts, int from, int to)
    {
        float len = 0f;
        int end = Mathf.Min(to, pts.Count - 1);
        for (int i = from; i < end; i++)
            len += Vector2.Distance(pts[i], pts[i + 1]);
        return len;
    }
}
