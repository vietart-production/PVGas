using System.Collections.Generic;
using UnityEngine;

// Abstracts "where are people on the floor" so the mouse prototype and a
// future LIDAR input can both drive the same interaction logic without
// FloorIconSpawner/FloorAura/FloorExperienceManager changing at all.
public interface IFloorTracker
{
    // Positions in floor-local meters, origin at floor center.
    IReadOnlyList<Vector2> GetTrackedPositions();
}
