using System.Collections.Generic;
using UnityEngine;

// The one stable IFloorTracker that FloorIconSpawner/FloorAura/FloorExperienceManager
// all point at - switching between Mouse (dev/test, no hardware needed) and
// Osc (real LiDAR/Augmenta input, see OscTouchSource.cs) is just flipping
// `mode` here in the Inspector; nothing else ever needs re-wiring again.
public class FloorTrackerSwitch : MonoBehaviour, IFloorTracker
{
    public enum Mode { Mouse, Osc }

    [SerializeField] private Mode mode = Mode.Mouse;
    [SerializeField] private MouseFloorTracker mouseTracker;
    [SerializeField] private OscFloorTracker oscTracker;

    private static readonly List<TrackedPoint> Empty = new List<TrackedPoint>();

    public IReadOnlyList<TrackedPoint> GetTrackedPositions()
    {
        IFloorTracker active = mode == Mode.Mouse ? (IFloorTracker)mouseTracker : oscTracker;
        return active != null ? active.GetTrackedPositions() : Empty;
    }
}
