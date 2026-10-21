using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum FloorState { Waiting, Accumulating, Finale }

// A -> B -> C state machine from the KHU 2 script: waiting (idle flame),
// accumulating (footsteps spawn icons / fill slots), finale (15s showcase
// then back to waiting). Also resets to waiting after 30s with no movement.
public class FloorExperienceManager : MonoBehaviour
{
    [SerializeField] private FloorIconSpawner spawner;
    [SerializeField] private CenterLedController centerLed;
    [SerializeField] private RectTransform floorArea;
    [SerializeField] private RectTransform mergedCanvas; // shared World Space BeamLayer, seen by both the floor and LED cameras
    [SerializeField] private MonoBehaviour trackerSource; // must implement IFloorTracker
    [SerializeField] private float cycleTargetSeconds = 60f;
    [SerializeField] private float finaleDurationSeconds = 15f;
    [SerializeField] private float idleTimeoutSeconds = 30f;
    [SerializeField] private float beamDuration = 3f; // icon position all the way up to the flame, one continuous beam
    [SerializeField] private float beamWidth = 46f; // matches FlowBeam's original shared default
    [SerializeField] private float beamWaveAmp = 50f; // sideways meander amplitude - separate from width so it can be tuned on its own
    [SerializeField] private float beamTrailLength = 800f; // wind-gust tail: fades out behind the head within this many units - every unit of this stays "hot" (runs the shader's full per-filament loop) every frame, so this is a direct lag/quality trade-off, not just a look - 800 was the tested sweet spot, 1600 made even a single beam laggy
    [SerializeField] private float burstRadius = 140f; // spawn-point ring burst, same color as the beam
    [SerializeField] private float burstExpandDuration = 0.7f;
    [SerializeField] private float burstFadeDuration = 0.4f;
    [SerializeField] private float burstIntensity = 3.2f; // brightness boost so it doesn't sink into a same-hue bkg
    [SerializeField] private float beamIntensity = 4f; // gamma-lift exponent (FlowRibbon.shader) - 1 = unchanged, >1 brightens, tuned against the bkg (Lỗi 2)

    private IFloorTracker tracker;
    private float cycleTimer;
    private float idleTimer;
    private int beamsDispatched; // in-flight + arrived, this cycle - caps at centerLed.TotalSteps so late arrivals can't overfill/re-trigger the finale
    private Coroutine finaleRoutine;
    private readonly List<FlowBeam> activeFloorBeams = new List<FlowBeam>();

    public FloorState State { get; private set; } = FloorState.Waiting;

    private void Awake()
    {
        tracker = trackerSource as IFloorTracker;
        spawner.IconSpawned += OnIconSpawned;
        EnterWaiting();
    }

    private void Update()
    {
        if (State == FloorState.Waiting)
        {
            if (AnyMovement()) EnterAccumulating();
            return;
        }

        if (State != FloorState.Accumulating) return;

        cycleTimer += Time.deltaTime;
        idleTimer = AnyMovement() ? 0f : idleTimer + Time.deltaTime;

        if (idleTimer >= idleTimeoutSeconds || cycleTimer >= cycleTargetSeconds)
            EnterWaiting();
    }

    private bool AnyMovement() => tracker != null && tracker.GetTrackedPositions().Count > 0;

    // Fires a single pulse straight from the icon all the way up to the
    // flame. The beam lives in BeamLayer, a World Space canvas shared by
    // both the floor and LED cameras - each camera naturally clips to its
    // own view of the same object, so the two halves always line up at the
    // seam with no handoff needed. It's positioned closer to both cameras
    // than either screen's own content, so it always draws on top. The
    // pulse fades out and disposes itself right after arriving, and every
    // beam is forced into one of the 3 brand colors instead of the topic's
    // own color.
    private void OnIconSpawned(EnergyTopicData topic, Vector2 localPos)
    {
        if (State != FloorState.Accumulating) return;
        // Enough beams are already in flight/arrived to fill the flame -
        // block the stream so extra footsteps don't keep piling on beams
        // that would arrive after the finale already started (Lỗi 3).
        if (beamsDispatched >= centerLed.TotalSteps) return;
        beamsDispatched++;

        Vector2 iconPos = mergedCanvas.InverseTransformPoint(floorArea.TransformPoint(localPos));
        Vector2 flamePos = mergedCanvas.InverseTransformPoint(centerLed.Flame.TransformPoint(Vector3.zero));
        float seed = (topic.topicId.GetHashCode() & 0xFFFF) * 0.01f;
        Color color = BrandPalette.For(topic.topicId);

        SpawnBurst.Create(floorArea, localPos, color, burstRadius, burstExpandDuration, burstFadeDuration, 0, burstIntensity);

        // Instead of vanishing the instant the head touches the flame, the
        // beam keeps flying on into it (growTween is left running, not
        // killed) while the fill ramps up over the time it takes the
        // glowing tail to also fully catch up and soak in - "bay đến đâu
        // nạp năng lượng tới đó", finishing the 1/8 charge exactly when the
        // whole visible trail has entered, not before.
        float distance = Vector2.Distance(iconPos, flamePos);
        float travelSpeed = distance / Mathf.Max(beamDuration, 0.01f);
        float soakDuration = beamTrailLength / Mathf.Max(travelSpeed, 1f);
        float trailFraction = beamTrailLength / Mathf.Max(distance, 1f);

        FlowBeam beam = null;
        beam = FlowBeam.Create(mergedCanvas, iconPos, flamePos, color, beamDuration,
            seed, 0f, true, true, 0,
            onArrive: () =>
            {
                // Trigger the finale off the flame actually reaching full,
                // not off the spawn count - otherwise it fires the instant
                // the 8th icon appears, while that beam is still mid-flight
                // and the flame isn't actually charged yet.
                if (centerLed.AdvanceFill(soakDuration)) EnterFinale();
                beam.ConsumeIntoFlame(trailFraction, soakDuration);
            },
            bandHalf: beamWidth, waveAmp: beamWaveAmp, trailLength: beamTrailLength, intensity: beamIntensity,
            arrivalTarget: centerLed.Flame);
        activeFloorBeams.Add(beam);
    }

    private void EnterWaiting()
    {
        State = FloorState.Waiting;
        cycleTimer = 0f;
        idleTimer = 0f;
        beamsDispatched = 0;
        spawner.SpawningEnabled = false;
        spawner.ResetSpawner();
        centerLed.ResetFill();

        foreach (var beam in activeFloorBeams)
            if (beam != null) beam.Dispose();
        activeFloorBeams.Clear();
    }

    private void EnterAccumulating()
    {
        State = FloorState.Accumulating;
        cycleTimer = 0f;
        idleTimer = 0f;
        spawner.SpawningEnabled = true;
    }

    private void EnterFinale()
    {
        State = FloorState.Finale;
        spawner.SpawningEnabled = false;
        centerLed.ShowFinale();
        if (finaleRoutine != null) StopCoroutine(finaleRoutine);
        finaleRoutine = StartCoroutine(FinaleThenReset());
    }

    private IEnumerator FinaleThenReset()
    {
        float fadeOut = centerLed.FinaleTextFadeOutDuration;
        yield return new WaitForSeconds(Mathf.Max(0f, finaleDurationSeconds - fadeOut));
        centerLed.FadeOutFinaleText();
        yield return new WaitForSeconds(fadeOut);
        EnterWaiting();
    }
}
