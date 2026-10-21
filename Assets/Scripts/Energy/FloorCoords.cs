using UnityEngine;

// Shared meters<->UI-local-pixel conversion for the floor area, so the
// tracker, spawner, and aura all agree on the exact same mapping.
public static class FloorCoords
{
    public static Vector2 MetersToLocal(Vector2 meters, Rect floorRect, Vector2 floorSizeMeters)
    {
        float px = Mathf.Lerp(floorRect.xMin, floorRect.xMax, meters.x / floorSizeMeters.x + 0.5f);
        float py = Mathf.Lerp(floorRect.yMin, floorRect.yMax, meters.y / floorSizeMeters.y + 0.5f);
        return new Vector2(px, py);
    }

    public static bool LocalToMeters(Vector2 local, Rect floorRect, Vector2 floorSizeMeters, out Vector2 meters)
    {
        float u = Mathf.InverseLerp(floorRect.xMin, floorRect.xMax, local.x);
        float v = Mathf.InverseLerp(floorRect.yMin, floorRect.yMax, local.y);
        meters = new Vector2((u - 0.5f) * floorSizeMeters.x, (v - 0.5f) * floorSizeMeters.y);
        return u >= 0f && u <= 1f && v >= 0f && v <= 1f;
    }
}
