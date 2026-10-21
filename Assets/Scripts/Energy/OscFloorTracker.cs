using System.Collections.Generic;
using UnityEngine;

// Thin IFloorTracker adapter over OscTouchSource (the real LiDAR/Augmenta
// input - see OscTouchSource.cs's own doc comment and the reference
// AugmentaOscInteraction.README.md this was ported from). OscTouchSource
// knows nothing about FloorIconSpawner/FloorAura/the rest of this project;
// this is the "whatever this project's interaction actually is" consumer
// the README describes, converting its ActivePoints (world-space, mapped
// through spaceRect/courtOrigin) into this project's floor-local meters so
// FloorCoords/FloorIconSpawner/FloorAura don't need to know OSC exists at all.
//
// `source.spaceRect` must be set to this same floorArea (courtOrigin =
// BottomLeft matches Unity's own rect convention, so InverseTransformPoint
// below lines back up with it) - see the README's "spaceRect" warning if
// tracked points ever land in the wrong place.
public class OscFloorTracker : MonoBehaviour, IFloorTracker
{
    [SerializeField] private OscTouchSource source;
    [SerializeField] private RectTransform floorArea;
    [SerializeField] private Vector2 floorSizeMeters = new Vector2(5f, 4f);

    private readonly List<TrackedPoint> result = new List<TrackedPoint>();

    public IReadOnlyList<TrackedPoint> GetTrackedPositions()
    {
        result.Clear();
        if (source == null || floorArea == null) return result;

        foreach (var p in source.ActivePoints)
        {
            Vector3 local = floorArea.InverseTransformPoint(p.world);
            if (FloorCoords.LocalToMeters(local, floorArea.rect, floorSizeMeters, out var meters))
                result.Add(new TrackedPoint { id = p.id, pos = meters });
        }
        return result;
    }
}
