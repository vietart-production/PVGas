using System.Collections.Generic;
using UnityEngine;

// A tracked point needs a persistent id so downstream per-person state (e.g.
// FloorIconSpawner's "distance walked since last icon") stays attached to the
// same real person across frames - position alone can't tell "person moved"
// apart from "a different person now occupies this array slot" or "same
// click id but it got released and pressed somewhere else entirely".
public struct TrackedPoint
{
    public int id;
    public Vector2 pos;
}

// Abstracts "where are people on the floor" so the mouse prototype and a
// future LIDAR/OSC input can both drive the same interaction logic without
// FloorIconSpawner/FloorAura/FloorExperienceManager changing at all.
public interface IFloorTracker
{
    // Positions in floor-local meters, origin at floor center.
    IReadOnlyList<TrackedPoint> GetTrackedPositions();
}
