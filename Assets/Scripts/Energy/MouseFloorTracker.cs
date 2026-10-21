using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Prototype IFloorTracker: maps the mouse position over the FloorArea rect
// to floor-local meters, only while the left button is held (click-hold =
// "person standing there"). Each press-down edge gets a fresh track id -
// releasing and clicking somewhere else must read as that person leaving
// and a (possibly different) one arriving, not as the same person
// teleporting/"walking" there in zero time. Swap for a LidarFloorTracker
// later - everything downstream only talks to IFloorTracker.
public class MouseFloorTracker : MonoBehaviour, IFloorTracker
{
    [SerializeField] private RectTransform floorArea;
    [SerializeField] private Vector2 floorSizeMeters = new Vector2(5f, 4f);

    private readonly List<TrackedPoint> result = new List<TrackedPoint>(1);
    private Camera floorCamera;
    private bool wasPressed;
    private int nextId;
    private int currentId;

    private void Awake()
    {
        var canvas = floorArea.GetComponentInParent<Canvas>();
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            floorCamera = canvas.worldCamera;
    }

    public IReadOnlyList<TrackedPoint> GetTrackedPositions()
    {
        result.Clear();
        bool pressed = Mouse.current != null && Mouse.current.leftButton.isPressed;
        if (pressed && !wasPressed) currentId = nextId++;
        wasPressed = pressed;
        if (!pressed) return result;

        Vector2 screenPos = Mouse.current.position.ReadValue();
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(floorArea, screenPos, floorCamera, out var local)
            && FloorCoords.LocalToMeters(local, floorArea.rect, floorSizeMeters, out var meters))
        {
            result.Add(new TrackedPoint { id = currentId, pos = meters });
        }
        return result;
    }
}
