using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Prototype IFloorTracker: maps the mouse position over the FloorArea rect
// to floor-local meters, only while the left button is held (click-hold =
// "person standing there"). Swap for a LidarFloorTracker later - everything
// downstream only talks to IFloorTracker.
public class MouseFloorTracker : MonoBehaviour, IFloorTracker
{
    [SerializeField] private RectTransform floorArea;
    [SerializeField] private Vector2 floorSizeMeters = new Vector2(5f, 4f);

    private readonly List<Vector2> result = new List<Vector2>(1);
    private Camera floorCamera;

    private void Awake()
    {
        var canvas = floorArea.GetComponentInParent<Canvas>();
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            floorCamera = canvas.worldCamera;
    }

    public IReadOnlyList<Vector2> GetTrackedPositions()
    {
        result.Clear();
        if (Mouse.current == null || !Mouse.current.leftButton.isPressed) return result;

        Vector2 screenPos = Mouse.current.position.ReadValue();
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(floorArea, screenPos, floorCamera, out var local)
            && FloorCoords.LocalToMeters(local, floorArea.rect, floorSizeMeters, out var meters))
        {
            result.Add(meters);
        }
        return result;
    }
}
