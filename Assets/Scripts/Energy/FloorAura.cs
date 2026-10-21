using System.Collections.Generic;
using UnityEngine;

// Simple visual halo that follows each currently-tracked floor position.
public class FloorAura : MonoBehaviour
{
    [SerializeField] private MonoBehaviour trackerSource; // must implement IFloorTracker
    [SerializeField] private RectTransform floorArea;
    [SerializeField] private Vector2 floorSizeMeters = new Vector2(5f, 4f);
    [SerializeField] private GameObject auraPrefab;

    private IFloorTracker tracker;
    private readonly List<RectTransform> pool = new List<RectTransform>();

    private void Awake() => tracker = trackerSource as IFloorTracker;

    private void Update()
    {
        if (tracker == null) return;
        var positions = tracker.GetTrackedPositions();

        while (pool.Count < positions.Count)
        {
            var go = Instantiate(auraPrefab, floorArea);
            pool.Add(go.GetComponent<RectTransform>());
        }

        for (int i = 0; i < pool.Count; i++)
        {
            bool active = i < positions.Count;
            pool[i].gameObject.SetActive(active);
            if (active)
                pool[i].anchoredPosition = FloorCoords.MetersToLocal(positions[i].pos, floorArea.rect, floorSizeMeters);
        }
    }
}
