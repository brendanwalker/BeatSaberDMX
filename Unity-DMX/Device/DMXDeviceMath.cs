using UnityEngine;

public static class DmxDeviceMath
{
    public static float SqrDistance(Vector3 first, Vector3 second)
    {
        return (first.x - second.x) * (first.x - second.x) +
                (first.y - second.y) * (first.y - second.y) +
                (first.z - second.z) * (first.z - second.z);
    }

    // Adapted from https://github.com/jbruening/unity3d-extensions/blob/master/Vector3X.cs
    public static bool IsPointWithinRadiusOfSegment(Vector3 lineP1, Vector3 lineP2, float radius, Vector3 point)
    {
        float rSqr = radius * radius;
        Vector3 v = lineP2 - lineP1;
        Vector3 w = point - lineP1;

        // Closest point is p1
        float c1 = Vector3.Dot(w, v);
        if (c1 <= 0)
        {
            return SqrDistance(point, lineP1) <= rSqr;
        }

        // Closest point is p2
        float c2 = Vector3.Dot(v, v);
        if (c2 <= c1)
        {
            return SqrDistance(point, lineP2) <= rSqr;
        }

        // Closest point along segment
        float b = c1 / c2;
        Vector3 pb = lineP1 + b * v;

        return SqrDistance(point, pb) <= rSqr;
    }

    public static bool IsPointWithinOrientedBox(
        Vector3 center, Vector3 xAxis, Vector3 yAxis, Vector3 zAxis, Vector3 extents, 
        Vector3 point)
    {
        Vector3 offset = point - center;

        return
            Mathf.Abs(Vector3.Dot(offset, xAxis)) <= extents.x &&
            Mathf.Abs(Vector3.Dot(offset, yAxis)) <= extents.y &&
            Mathf.Abs(Vector3.Dot(offset, zAxis)) <= extents.z;
    }
}
