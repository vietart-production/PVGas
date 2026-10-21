using System;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
#if UNITY_EDITOR
using UnityEditor;
#endif

// A neon "flowing light ribbon" beam (a wavy bundle of parallel filaments):
// one UI quad drawn entirely by the PVGas/FlowRibbon shader.
// Default mode (alignToDirection: false) always runs bottom -> top in the
// canvas (the floor and the center LED are stacked vertically), optionally
// S-bending sideways - this is what the floor/LED energy beams use.
// alignToDirection: true instead rotates the quad to point straight from
// "from" to "to" at any angle (no S-bend) - for a standalone decorative
// ribbon placed anywhere in a panel (e.g. replacing a static glow-line image).
// Grows from "from" to "to" over growDuration (onArrive fires at the end),
// then persists and keeps flowing until Dispose(). Outside Play mode (e.g.
// an AmbientEnergyLink being shaped in the Editor) there is no DOTween
// ticking, so it skips straight to fully grown and force-repaints the Scene
// view every frame instead, so the shader's own time-driven flow still
// animates live while you drag points around.
[ExecuteAlways]
public class FlowBeam : MonoBehaviour
{
    // Same on both screens so the ribbon matches across the seam.
    public const float BandHalf = 46f;   // half width of the filament bundle
    public const float WaveAmp = 46f;    // sideways amplitude of the big wave
    private const float NodePad = 45f;   // extra quad at pinched ends so end glows aren't clipped

    private Material material;
    private Tween growTween;
    private float progress;

    // Geometric arrival detection (Lỗi 1): instead of only firing onArrive
    // when the fixed-duration tween completes, every frame we check whether
    // the travelling head has actually entered arrivalTarget's own rect (its
    // "raycast/object area") and fire as soon as it does - this matches the
    // shader's head overshoot (hp = progress*1.12, see FlowRibbon.shader)
    // instead of lagging behind it. The tween's OnComplete remains as a
    // fallback so beams with no arrivalTarget (e.g. the ambient column) keep
    // their old time-based behavior untouched.
    private RectTransform canvasAreaRef;
    private RectTransform arrivalTarget;
    private Vector2 fromLocal, toLocal;
    private Action onArriveCallback;
    private bool arrived;

    // pathStartDistance: how far along the whole floor->flame path this
    // segment begins (0 for the floor segment, floor length for the LED one),
    // so the twisting filaments are phase-continuous across the screen seam.
    // siblingIndex: draw order inside canvasArea (low = behind icons).
    public static FlowBeam Create(RectTransform canvasArea, Vector2 from, Vector2 to, Color color, float growDuration,
        float seed, float pathStartDistance, bool pinchStart, bool pinchEnd, int siblingIndex, Action onArrive,
        bool alignToDirection = false, float bandHalf = BandHalf, float waveAmp = WaveAmp,
        float glowWidth = 1f, float intensity = 1f, float trailLength = 100000f, float spinSpeed = 0f,
        RectTransform arrivalTarget = null, float emissionSpread = 1f)
    {
        float padB = pinchStart ? NodePad : 0f;
        float padT = pinchEnd ? NodePad : 0f;
        float margin = bandHalf * 1.95f + waveAmp * 1.55f + 60f; // meander + drifting filaments + glow (matches the shader's early-out extent)

        float height, width, centerX, centerY, bendStart, bendEnd, rotationDeg;

        if (alignToDirection)
        {
            Vector2 delta = to - from;
            height = Mathf.Max(delta.magnitude, 1f);
            width = margin * 2f;
            Vector2 mid = (from + to) * 0.5f;
            centerX = mid.x;
            centerY = mid.y;
            bendStart = 0f;
            bendEnd = 0f;
            // Rotates local +Y (the shader's path direction) to point along delta.
            rotationDeg = Mathf.Atan2(-delta.x, delta.y) * Mathf.Rad2Deg;
        }
        else
        {
            float y0 = from.y;
            float y1 = Mathf.Max(to.y, from.y + 1f);
            height = y1 - y0;
            float xMin = Mathf.Min(from.x, to.x) - margin;
            float xMax = Mathf.Max(from.x, to.x) + margin;
            width = xMax - xMin;
            centerX = (xMin + xMax) * 0.5f;
            centerY = (y0 - padB + y1 + padT) * 0.5f;
            bendStart = from.x - centerX;
            bendEnd = to.x - centerX;
            rotationDeg = 0f;
        }

        var go = new GameObject("FlowBeam", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(canvasArea, false);
        rt.anchorMin = rt.anchorMax = canvasArea.pivot; // so anchoredPosition shares rect.xMin/xMax's origin
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(centerX, centerY);
        rt.sizeDelta = new Vector2(width, height + padB + padT);
        rt.localRotation = Quaternion.Euler(0f, 0f, rotationDeg);
        rt.SetSiblingIndex(Mathf.Clamp(siblingIndex, 0, canvasArea.childCount - 1));

        var beam = go.AddComponent<FlowBeam>();
        beam.canvasAreaRef = canvasArea;
        beam.arrivalTarget = arrivalTarget;
        beam.fromLocal = from;
        beam.toLocal = to;
        beam.onArriveCallback = onArrive;
        beam.material = new Material(Shader.Find("PVGas/FlowRibbon"));
        var m = beam.material;
        m.SetColor("_Color", color);
        m.SetFloat("_Seed", seed);
        m.SetFloat("_Width", width);
        m.SetFloat("_Height", height);
        m.SetFloat("_PadB", padB);
        m.SetFloat("_PadT", padT);
        m.SetFloat("_BandHalf", bandHalf);
        m.SetFloat("_WaveAmp", waveAmp);
        m.SetFloat("_GlowWidth", glowWidth);
        m.SetFloat("_Intensity", 1f); // gamma-lift fixed neutral - the intensity knob now drives real HDR emission below instead
        m.SetColor("_EmissionColor", color);
        m.SetFloat("_EmissionIntensity", intensity);
        m.SetFloat("_EmissionSpread", emissionSpread);
        m.SetFloat("_TrailLength", trailLength);
        m.SetFloat("_SpinSpeed", spinSpeed);
        m.SetFloat("_BendStart", bendStart);
        m.SetFloat("_BendEnd", bendEnd);
        m.SetFloat("_GStart", pathStartDistance);
        m.SetFloat("_PinchStart", pinchStart ? 1f : 0f);
        m.SetFloat("_PinchEnd", pinchEnd ? 1f : 0f);
        m.SetFloat("_Progress", 0f);

        var img = go.GetComponent<Image>();
        img.material = m;
        img.color = Color.white;
        img.raycastTarget = false;

        if (Application.isPlaying)
        {
            beam.growTween = DOTween.To(() => beam.progress, x =>
                {
                    beam.progress = x;
                    m.SetFloat("_Progress", x);
                }, 1f, growDuration)
                .SetEase(Ease.OutCubic)
                .OnComplete(() =>
                {
                    // Fallback: fires the old time-based way if geometric
                    // detection (Update(), below) never caught it - no
                    // arrivalTarget given, or the path math missed it.
                    if (!beam.arrived) { beam.arrived = true; onArrive?.Invoke(); }
                });
        }
        else
        {
            // Edit mode: no tween ticks, so just show the finished shape.
            beam.progress = 1f;
            m.SetFloat("_Progress", 1f);
        }

        return beam;
    }

    private bool disposed;

    // One-shot pulses (the real floor/LED interaction beam) fade out and
    // dispose themselves right after arriving, instead of persisting.
    public void FadeOutAndDispose(float fadeDuration)
    {
        if (disposed) return;
        growTween?.Kill();
        if (!Application.isPlaying) { Dispose(); return; }

        var img = GetComponent<Image>();
        img.DOFade(0f, fadeDuration).OnComplete(Dispose);
    }

    // Instead of vanishing the instant it touches the flame, the beam
    // keeps "flowing into" it: progress keeps advancing past 1 (past the
    // literal endpoint), which - thanks to FlowRibbon.shader's existing
    // trail-fade math - makes the lit segment visibly shrink from the
    // flame end backward over "duration", timed so the point nearest the
    // flame fades first and the fade reaches the tail right as it fully
    // arrives. Also far cheaper than sitting fully-lit and static for
    // that whole time: the shader's mask<=0.001 early-out skips its
    // expensive per-filament loop for every pixel that's already faded,
    // so cost shrinks as the beam is consumed instead of staying maxed.
    public void ConsumeIntoFlame(float trailFraction, float duration)
    {
        if (disposed) return;
        growTween?.Kill();
        if (!Application.isPlaying) { Dispose(); return; }

        float target = (1f + 4f * Mathf.Max(trailFraction, 0.05f)) / 1.12f;
        growTween = DOTween.To(() => progress, x =>
            {
                progress = x;
                material.SetFloat("_Progress", x);
            }, target, duration)
            .SetEase(Ease.Linear)
            .OnComplete(Dispose);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;

        growTween?.Kill(); // a disposed beam must not fire onArrive later
        if (Application.isPlaying)
        {
            if (material != null) Destroy(material);
            Destroy(gameObject);
        }
        else
        {
            // Destroy() is a no-op (with a console error) outside Play mode.
            if (material != null) DestroyImmediate(material);
            DestroyImmediate(gameObject);
        }
    }

    private void OnDestroy() => growTween?.Kill();

    private void Update()
    {
#if UNITY_EDITOR
        // Keeps the shader's time-driven wander/flow animating live in the
        // Scene view while editing an AmbientEnergyLink - Unity only
        // repaints on its own when something interacts with the view.
        if (!Application.isPlaying) { SceneView.RepaintAll(); return; }
#endif
        if (arrived || disposed || arrivalTarget == null) return;

        // Same head-position math as the shader's hp (FlowRibbon.shader) -
        // overshoots past "to" as progress approaches 1, so this fires at
        // the moment the visible head actually reaches arrivalTarget's
        // rect instead of lagging behind it until the full-duration tween
        // completes.
        float headT = Mathf.Clamp01(progress * 1.12f);
        Vector2 headLocal = Vector2.Lerp(fromLocal, toLocal, headT);
        Vector3 world = canvasAreaRef.TransformPoint(headLocal);
        Vector3 inTarget = arrivalTarget.InverseTransformPoint(world);
        if (arrivalTarget.rect.Contains(new Vector2(inTarget.x, inTarget.y)))
        {
            arrived = true;
            onArriveCallback?.Invoke();
        }
    }
}
