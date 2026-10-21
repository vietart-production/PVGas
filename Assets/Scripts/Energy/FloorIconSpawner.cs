using System;
using System.Collections.Generic;
using UnityEngine;

// Spawns a FloorIcon wherever a tracked point has moved past a random
// 2-3m threshold since its last spawn, picking from energy types not yet
// shown (a shuffled "bag" - never repeats until all 8 have appeared once).
public class FloorIconSpawner : MonoBehaviour
{
    [SerializeField] private MonoBehaviour trackerSource; // must implement IFloorTracker
    [SerializeField] private RectTransform floorArea;
    [SerializeField] private Vector2 floorSizeMeters = new Vector2(5f, 4f);
    [SerializeField] private GameObject iconPrefab;
    [SerializeField] private EnergyTopicData[] energyTypes;
    [SerializeField] private float minSpawnDistance = 2f;
    [SerializeField] private float maxSpawnDistance = 3f;

    // Vector2 payload is the icon's local anchoredPosition inside floorArea
    // (pixel space, not meters) - what the cross-canvas beam needs.
    public event Action<EnergyTopicData, Vector2> IconSpawned;
    public HashSet<string> SeenTypeIds { get; } = new HashSet<string>();

    // FloorExperienceManager keeps this in lockstep with its own state, so
    // an icon only ever appears when the beam side is also ready to fire -
    // otherwise a footstep during Waiting/Finale would spawn a "phantom"
    // icon with no beam (the listener silently discards it by state).
    public bool SpawningEnabled { get; set; } = true;

    private IFloorTracker tracker;
    private readonly List<Vector2> lastPositions = new List<Vector2>();
    private readonly List<float> distanceSinceSpawn = new List<float>();
    private readonly List<float> nextThreshold = new List<float>();
    private List<EnergyTopicData> bag = new List<EnergyTopicData>();

    private void Awake()
    {
        tracker = trackerSource as IFloorTracker;
        RefillBag();
    }

    public void ResetSpawner()
    {
        lastPositions.Clear();
        distanceSinceSpawn.Clear();
        nextThreshold.Clear();
        SeenTypeIds.Clear();
        RefillBag();

        foreach (Transform child in floorArea)
            if (child.GetComponent<FloorIcon>() != null)
                Destroy(child.gameObject);
    }

    private void RefillBag() => bag = ShuffleBag.Shuffled(energyTypes);

    private void Update()
    {
        if (tracker == null) return;
        var positions = tracker.GetTrackedPositions();

        while (lastPositions.Count < positions.Count)
        {
            lastPositions.Add(positions[lastPositions.Count]);
            distanceSinceSpawn.Add(0f);
            nextThreshold.Add(UnityEngine.Random.Range(minSpawnDistance, maxSpawnDistance));
        }

        for (int i = 0; i < positions.Count; i++)
        {
            float moved = Vector2.Distance(lastPositions[i], positions[i]);
            distanceSinceSpawn[i] += moved;
            lastPositions[i] = positions[i];

            if (distanceSinceSpawn[i] >= nextThreshold[i])
            {
                distanceSinceSpawn[i] = 0f;
                nextThreshold[i] = UnityEngine.Random.Range(minSpawnDistance, maxSpawnDistance);
                SpawnAt(positions[i]);
            }
        }
    }

    private void SpawnAt(Vector2 floorPosMeters)
    {
        if (!SpawningEnabled) return;
        if (bag.Count == 0) RefillBag();
        var type = bag[bag.Count - 1];
        bag.RemoveAt(bag.Count - 1);
        SeenTypeIds.Add(type.topicId);

        // No visual icon is spawned under the visitor's feet anymore
        // (guests shouldn't have to step around it) - only the event fires,
        // which still drives the energy beam up to the LED.
        var localPos = FloorCoords.MetersToLocal(floorPosMeters, floorArea.rect, floorSizeMeters);
        IconSpawned?.Invoke(type, localPos);
    }
}
