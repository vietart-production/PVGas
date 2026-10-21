using System.Collections.Generic;
using UnityEngine;

// One persistent, always-on neon strand spanning both Khu 2 screens: a chain
// of waypoints on the floor, then a chain on the LED, phase-matched across
// the physical seam (pathStartDistance) exactly like the real floor->flame
// interaction beam - so when the two screens sit stacked in 2D, it reads as
// one continuous line instead of two unrelated streaks. Each consecutive
// pair of waypoints S-bends on its own, so more waypoints = more bends.
// Deliberately just one strand: more would compete with the spawned
// icons/beams for attention.
// Edit the waypoints with the AmbientEnergyLinkEditor scene-view handles
// (drag existing points; use the Inspector's "Add bend point" to insert more).
public class AmbientEnergyLink : MonoBehaviour
{
    [SerializeField] private RectTransform floorArea;
    [SerializeField] private RectTransform ledCanvas;

    // Ascending Y, local to their own RectTransform. At least 2 each.
    [SerializeField]
    private List<Vector2> floorWaypoints = new List<Vector2> { new Vector2(0f, -850f), new Vector2(0f, 960f) };
    [SerializeField]
    private List<Vector2> ledWaypoints = new List<Vector2> { new Vector2(0f, -666f), new Vector2(0f, -117f) };

    [SerializeField] private Color color = new Color(0f, 0.62f, 0.34f, 0.75f);
    [SerializeField] private float growDuration = 1.8f;
    [SerializeField] private float seed = 23f;
    [SerializeField] private float width = FlowBeam.BandHalf; // ribbon half-width; same on every segment so it matches at every join
    [SerializeField] private float waveAmp = FlowBeam.WaveAmp; // sideways meander amplitude
    [SerializeField] private float glowWidth = 1f; // per-filament glow/blur size - independent of width (bundle spread)
    [SerializeField] private float intensity = 1f; // brightness

    private readonly List<FlowBeam> beams = new List<FlowBeam>();

    public RectTransform FloorArea => floorArea;
    public RectTransform LedCanvas => ledCanvas;
    public List<Vector2> FloorWaypoints => floorWaypoints;
    public List<Vector2> LedWaypoints => ledWaypoints;

    private void OnEnable() => BuildAll();

    // Called by AmbientEnergyLinkEditor after any field/waypoint edit, so
    // dragging a point in the Scene view (or tweaking Width/Intensity/etc.
    // in the Inspector) rebuilds the strand immediately instead of only on
    // the next OnEnable.
    public void Rebuild()
    {
        DisposeBeams();
        BuildAll();
    }

    private void BuildAll()
    {
        if (floorArea == null || ledCanvas == null) return;

        float distance = BuildChain(floorArea, floorWaypoints, 0f, siblingBase: 0);

        var ledBackground = ledCanvas.Find("Background");
        int ledSiblingBase = ledBackground != null ? ledBackground.GetSiblingIndex() + 1 : 0;
        BuildChain(ledCanvas, ledWaypoints, distance, siblingBase: ledSiblingBase);
    }

    // Chains the waypoints into consecutive FlowBeam segments, each one a
    // single S-bend from one point to the next; pathStartDistance carries
    // over between segments (and across the floor->LED call) so the noise
    // never visibly jumps at a join.
    private float BuildChain(RectTransform area, List<Vector2> waypoints, float startDistance, int siblingBase)
    {
        var pts = new List<Vector2>(waypoints);
        pts.Sort((a, b) => a.y.CompareTo(b.y));

        float distance = startDistance;
        for (int i = 0; i < pts.Count - 1; i++)
        {
            Vector2 from = pts[i];
            Vector2 to = pts[i + 1];
            var beam = FlowBeam.Create(area, from, to, color, growDuration, seed, distance,
                pinchStart: false, pinchEnd: false, siblingIndex: siblingBase + i, onArrive: null,
                bandHalf: width, waveAmp: waveAmp, glowWidth: glowWidth, intensity: intensity);
            beams.Add(beam);
            distance += to.y - from.y;
        }
        return distance;
    }

    private void OnDisable() => DisposeBeams();

    private void DisposeBeams()
    {
        foreach (var beam in beams) beam?.Dispose();
        beams.Clear();
    }
}
