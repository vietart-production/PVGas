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
    // LiDAR/OSC track ids are not stable across occlusion/reacquisition - a
    // person can keep their id reassigned while standing still near the
    // tracker's own match-radius boundary (AugmentaOscInteraction.README.md,
    // "Track matching"). Without this, that read as a brand new track and
    // silently dropped all walked-distance progress. A lost id reappearing
    // this close, this soon, is instead treated as the same person continuing.
    [SerializeField] private float reattachRadius = 1.5f;
    [SerializeField] private float reattachWindow = 0.5f;

    // Vector2 payload is the icon's local anchoredPosition inside floorArea
    // (pixel space, not meters) - what the cross-canvas beam needs.
    public event Action<EnergyTopicData, Vector2> IconSpawned;
    public HashSet<string> SeenTypeIds { get; } = new HashSet<string>();

    // FloorExperienceManager keeps this in lockstep with its own state, so
    // an icon only ever appears when the beam side is also ready to fire -
    // otherwise a footstep during Waiting/Finale would spawn a "phantom"
    // icon with no beam (the listener silently discards it by state).
    public bool SpawningEnabled { get; set; } = true;

    // Per-person walked-distance state, keyed by the tracker's own track id -
    // not by array index, which breaks the moment a person leaves/arrives
    // mid-stream (every later index shifts and inherits a stranger's
    // accumulated distance) and is trivially gamed by the mouse prototype
    // (release + click elsewhere reads as the same person at a new array
    // slot 0, "moving" there instantly).
    private class TrackState
    {
        public Vector2 lastPos;
        public float distanceSinceSpawn;
        public float nextThreshold;
    }

    private class Orphan
    {
        public TrackState state;
        public float droppedAt;
    }

    private IFloorTracker tracker;
    private readonly Dictionary<int, TrackState> tracks = new Dictionary<int, TrackState>();
    private readonly List<int> staleIds = new List<int>();
    private readonly List<Orphan> orphans = new List<Orphan>();
    private List<EnergyTopicData> bag = new List<EnergyTopicData>();

    private void Awake()
    {
        tracker = trackerSource as IFloorTracker;
        RefillBag();
    }

    public void ResetSpawner()
    {
        tracks.Clear();
        orphans.Clear();
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

        staleIds.Clear();
        staleIds.AddRange(tracks.Keys);

        for (int i = 0; i < positions.Count; i++)
        {
            var p = positions[i];
            staleIds.Remove(p.id);

            if (!tracks.TryGetValue(p.id, out var st))
            {
                // First sighting of this id - try to pick up a just-lost
                // track's progress instead of assuming a brand new person.
                st = TakeOrphanNear(p.pos);
                if (st != null) st.lastPos = p.pos; // snap to here - don't count the id-swap gap itself as "moved"
                else st = new TrackState { lastPos = p.pos, nextThreshold = UnityEngine.Random.Range(minSpawnDistance, maxSpawnDistance) };
                tracks[p.id] = st;
                continue; // don't count a "move" yet (there's nothing newer to compare against)
            }

            float moved = Vector2.Distance(st.lastPos, p.pos);
            st.distanceSinceSpawn += moved;
            st.lastPos = p.pos;

            if (st.distanceSinceSpawn >= st.nextThreshold)
            {
                st.distanceSinceSpawn = 0f;
                st.nextThreshold = UnityEngine.Random.Range(minSpawnDistance, maxSpawnDistance);
                SpawnAt(p.pos);
            }
        }

        foreach (var id in staleIds)
        {
            orphans.Add(new Orphan { state = tracks[id], droppedAt = Time.time });
            tracks.Remove(id);
        }
        orphans.RemoveAll(o => Time.time - o.droppedAt > reattachWindow);
    }

    private TrackState TakeOrphanNear(Vector2 pos)
    {
        for (int i = 0; i < orphans.Count; i++)
        {
            if (Vector2.Distance(orphans[i].state.lastPos, pos) <= reattachRadius)
            {
                var st = orphans[i].state;
                orphans.RemoveAt(i);
                return st;
            }
        }
        return null;
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
