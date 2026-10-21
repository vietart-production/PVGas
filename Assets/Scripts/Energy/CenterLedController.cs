using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

// The flame starts fully gray (PVGas/FlameFill material). Each energy pulse
// that arrives from the floor reveals one more 1/8 slice of its real color,
// counted from the bottom up - no separate icon grid anymore, the flame
// itself is the progress display.
public class CenterLedController : MonoBehaviour
{
    [SerializeField] private RectTransform canvasRect; // CenterLedCanvas's own RectTransform
    [SerializeField] private RectTransform flame;
    [SerializeField] private CanvasGroup finaleGroup; // placeholder hook - the graphics team's own finale visual goes here later
    [SerializeField] private int totalSteps = 8;
    [SerializeField] private float fillStepDuration = 0.15f; // flame color reveal ramp - kept short so it feels instant right as the beam arrives

    [Header("Ambient Column (decorative, always on)")]
    [SerializeField] private Color ambientColumnColor = new Color(0f, 0.62f, 0.34f); // matches the bkg's dominant green
    [SerializeField] private float ambientColumnWidth = 60f;
    [SerializeField] private float ambientColumnWaveAmp = 18f;
    [SerializeField] private float ambientColumnIntensity = 1f; // gamma-lift exponent (FlowRibbon.shader) - 1 = unchanged; the old 0.6 was tuned for the pre-gamma formula and reads as much too dim under this one
    [SerializeField] private float ambientColumnSpinSpeed = 0.15f; // rotations/sec around the vertical axis
    // Manual placement tweak (set by hand in the editor to angle the column
    // to match the bkg art) - overrides the auto flame->roof vertical layout.
    [SerializeField] private Vector3 ambientColumnLocalPosition = new Vector3(4f, 280f, 0f);
    [SerializeField] private Quaternion ambientColumnWorldRotation = new Quaternion(0f, 0f, 0.4464300870895386f, 0.8948185443878174f);
    [SerializeField] private Vector3 ambientColumnLocalScale = Vector3.one;
    [SerializeField] private Vector2 ambientColumnSizeDelta = new Vector2(595.82f, 1130.69f);

    [Header("Finale (8 icons pop out, orbit, retract into flame, thank you)")]
    [SerializeField] private EnergyTopicData[] finaleTopics; // the 8 showcased types, same assets as FloorIconSpawner
    [SerializeField] private float finaleIconSize = 150f;
    [SerializeField] private float finaleIconRadius = 220f;
    [SerializeField] private float finaleEntryStagger = 0.7f; // delay between each icon popping out
    [SerializeField] private float finaleEntryDuration = 0.55f; // pop + fly-to-slot duration per icon
    [SerializeField] private float finaleOrbitDegPerSec = 12f; // slow continuous spin while waiting for the rest
    [SerializeField] private float finaleRetractDuration = 0.6f; // all icons fly back + shrink into the flame
    [SerializeField] private float finaleFadeDuration = 0.6f; // flame fading out after the icons are sucked in
    [SerializeField] private float finaleTextFadeDuration = 0.8f; // thank-you image fade-in
    [SerializeField] private float finaleTextFadeOutDuration = 0.8f; // thank-you image fade-out before the cycle resets
    [SerializeField] private float finaleIconGlowIntensity = 2.2f; // overexposes the icon toward white, like the beam/burst's own emission look

    public float FinaleTextFadeOutDuration => finaleTextFadeOutDuration;
    public RectTransform Flame => flame;
    public int TotalSteps => totalSteps;

    private Image flameImage;
    private Material flameFillMaterial;
    private Tween fillTween;
    private int fillSteps;
    private int beamSiblingIndex; // just above Background, below the flame
    private FlowBeam ambientColumnBeam; // static decorative beam, flame -> bkg roof, never disposed

    private Coroutine finaleCoroutine;
    private readonly List<GameObject> finaleIconObjects = new List<GameObject>();
    private readonly List<Material> finaleIconMaterials = new List<Material>();

    private void Awake() => EnsureInitialized();

    // Other scripts' Awake() (e.g. FloorExperienceManager, which resets us
    // on startup) can run before ours - Unity doesn't guarantee Awake()
    // order across components - so every public entry point calls this
    // first instead of assuming Awake() already ran.
    private void EnsureInitialized()
    {
        if (flameFillMaterial != null) return;

        flameImage = flame.GetComponent<Image>();
        flameFillMaterial = new Material(Shader.Find("PVGas/FlameFill"));
        flameImage.material = flameFillMaterial;

        var background = canvasRect.Find("Background");
        beamSiblingIndex = background != null ? background.GetSiblingIndex() + 1 : 0;

        // Static decorative column, flame -> bkg roof, rotating in place like
        // a holographic cylinder - sits right above the background and below
        // everything else (flame, icons), and is never disposed.
        Vector2 topEdge = new Vector2(flame.anchoredPosition.x, canvasRect.rect.yMax);
        ambientColumnBeam = FlowBeam.Create(canvasRect, flame.anchoredPosition, topEdge, ambientColumnColor, 0.01f,
            seed: 0f, pathStartDistance: 0f, pinchStart: true, pinchEnd: true, siblingIndex: beamSiblingIndex,
            onArrive: null, bandHalf: ambientColumnWidth, waveAmp: ambientColumnWaveAmp,
            intensity: ambientColumnIntensity, spinSpeed: ambientColumnSpinSpeed);

        var columnRect = (RectTransform)ambientColumnBeam.transform;
        columnRect.localPosition = ambientColumnLocalPosition;
        columnRect.rotation = ambientColumnWorldRotation;
        columnRect.localScale = ambientColumnLocalScale;
        columnRect.sizeDelta = ambientColumnSizeDelta;
    }

    // Called by FloorExperienceManager when a beam arrives at the flame.
    // Returns true the moment the flame just reached full (8/8) - the
    // caller uses that, not the spawn count, to decide when to start the
    // finale, so it never fires before the last beam has actually landed.
    // Edge-triggered: once already full, extra late-arriving beams (any
    // dispatched just before the cap in FloorExperienceManager kicked in)
    // are ignored instead of re-returning true and re-triggering the
    // finale on every single one of them.
    // duration <= 0 uses the default fillStepDuration ramp; callers pass
    // their own duration to spread the fill out over however long their
    // beam takes to fully soak into the flame (see FloorExperienceManager).
    public bool AdvanceFill(float duration = -1f)
    {
        EnsureInitialized();
        if (fillSteps >= totalSteps) return false;

        fillSteps++;
        float target = (float)fillSteps / totalSteps;

        // A beam's soak-in can still be mid-flight when the next one
        // arrives (especially several close together) - kill the previous
        // ramp instead of letting two tweens fight over the same property.
        fillTween?.Kill();
        fillTween = DOTween.To(() => flameFillMaterial.GetFloat("_Fill"),
            x => flameFillMaterial.SetFloat("_Fill", x), target, duration > 0f ? duration : fillStepDuration)
            .SetEase(Ease.Linear);

        return fillSteps >= totalSteps;
    }

    public void ResetFill()
    {
        EnsureInitialized();

        if (finaleCoroutine != null)
        {
            StopCoroutine(finaleCoroutine);
            finaleCoroutine = null;
        }
        foreach (var go in finaleIconObjects)
            if (go != null) Destroy(go);
        finaleIconObjects.Clear();
        foreach (var mat in finaleIconMaterials)
            if (mat != null) Destroy(mat);
        finaleIconMaterials.Clear();

        fillTween?.Kill();
        fillSteps = 0;
        flameFillMaterial.SetFloat("_Fill", 0f);
        if (flameImage != null)
        {
            flameImage.DOKill();
            var c = flameImage.color;
            c.a = 1f;
            flameImage.color = c;
        }

        if (finaleGroup != null)
        {
            finaleGroup.DOKill();
            finaleGroup.alpha = 0f;
            finaleGroup.gameObject.SetActive(false);
        }
    }

    public void ShowFinale()
    {
        EnsureInitialized();
        if (finaleCoroutine != null) StopCoroutine(finaleCoroutine);
        finaleCoroutine = StartCoroutine(FinaleSequence());
    }

    // Called by FloorExperienceManager shortly before the finale window ends
    // (FinaleTextFadeOutDuration ahead of time) so the thank-you image fades
    // smoothly out instead of being snapped away by ResetFill() - and the
    // flame crossfades back in over the same span instead of ResetFill()
    // later snapping its alpha back to 1 instantly.
    public void FadeOutFinaleText()
    {
        if (finaleGroup != null)
        {
            finaleGroup.DOKill();
            finaleGroup.DOFade(0f, finaleTextFadeOutDuration);
        }
        if (flameImage != null)
        {
            // Snap the fill back to empty (gray) *before* fading the flame
            // back in, not after - otherwise the full-color flame fades in
            // first and then visibly pops back to gray the instant
            // ResetFill() runs later. It should already read "0 energy"
            // for the whole fade-in.
            fillSteps = 0;
            flameFillMaterial.SetFloat("_Fill", 0f);
            flameImage.DOKill();
            flameImage.DOFade(1f, finaleTextFadeOutDuration);
        }
    }

    // 8 icons pop out of the flame one by one (0.7s apart), settle into their
    // slot angle and keep orbiting (each as soon as it arrives) until all 8
    // are present, then all of them fly back and shrink into the flame at
    // once, the flame fades out, and the thank-you text takes over.
    private IEnumerator FinaleSequence()
    {
        int n = finaleTopics != null ? finaleTopics.Length : 0;
        if (n == 0) yield break;

        Vector2 center = flame.anchoredPosition;

        // One rotating pivot carries all 8 icons at once (a single looping
        // DOTween rotation) instead of recomputing sin/cos per icon every
        // frame. Each icon counter-rotates at the same speed so it stays
        // upright while being carried around by its parent.
        var pivotGO = new GameObject("FinaleOrbitPivot", typeof(RectTransform));
        var pivot = (RectTransform)pivotGO.transform;
        pivot.SetParent(canvasRect, false);
        pivot.anchorMin = pivot.anchorMax = canvasRect.pivot;
        pivot.pivot = new Vector2(0.5f, 0.5f);
        pivot.anchoredPosition = center;
        pivot.SetAsLastSibling(); // drawn above the flame, emerging "out of" it
        finaleIconObjects.Add(pivotGO);

        float orbitPeriod = finaleOrbitDegPerSec > 0f ? 360f / finaleOrbitDegPerSec : 0f;
        if (orbitPeriod > 0f)
            pivot.DOLocalRotate(new Vector3(0f, 0f, 360f), orbitPeriod, RotateMode.FastBeyond360)
                .SetEase(Ease.Linear).SetLoops(-1, LoopType.Restart);

        var icons = new RectTransform[n];
        for (int k = 0; k < n; k++)
        {
            var topic = finaleTopics[k];
            var (rt, img, mat) = CreateFinaleIcon(topic, pivot);
            icons[k] = rt;
            if (mat != null) finaleIconMaterials.Add(mat);
            finaleIconObjects.Add(rt.gameObject);

            rt.anchoredPosition = Vector2.zero;
            rt.localScale = Vector3.zero;

            if (orbitPeriod > 0f)
                rt.DOLocalRotate(new Vector3(0f, 0f, -360f), orbitPeriod, RotateMode.FastBeyond360)
                    .SetEase(Ease.Linear).SetLoops(-1, LoopType.Restart);

            Vector2 localTarget = SlotPosition(Vector2.zero, topic.slotAngleDegrees);
            var seq = DOTween.Sequence();
            seq.AppendInterval(k * finaleEntryStagger);
            seq.Append(rt.DOScale(1f, finaleEntryDuration).SetEase(Ease.OutBack));
            seq.Join(rt.DOAnchorPos(localTarget, finaleEntryDuration).SetEase(Ease.OutCubic));
        }

        yield return new WaitForSeconds((n - 1) * finaleEntryStagger + finaleEntryDuration);

        var retractSeq = DOTween.Sequence();
        foreach (var rt in icons)
        {
            retractSeq.Join(rt.DOAnchorPos(Vector2.zero, finaleRetractDuration).SetEase(Ease.InBack));
            retractSeq.Join(rt.DOScale(0f, finaleRetractDuration).SetEase(Ease.InBack));
        }
        yield return retractSeq.WaitForCompletion();

        pivot.DOKill();
        foreach (var rt in icons) rt.DOKill();

        foreach (var go in finaleIconObjects) if (go != null) Destroy(go);
        finaleIconObjects.Clear();
        foreach (var mat in finaleIconMaterials) if (mat != null) Destroy(mat);
        finaleIconMaterials.Clear();

        if (flameImage != null)
            yield return flameImage.DOFade(0f, finaleFadeDuration).WaitForCompletion();

        if (finaleGroup != null)
        {
            finaleGroup.gameObject.SetActive(true);
            finaleGroup.alpha = 0f;
            finaleGroup.DOFade(1f, finaleTextFadeDuration);
        }

        finaleCoroutine = null;
    }

    private Vector2 SlotPosition(Vector2 center, float angleDegrees)
    {
        float rad = angleDegrees * Mathf.Deg2Rad;
        return center + finaleIconRadius * new Vector2(Mathf.Sin(rad), Mathf.Cos(rad));
    }

    private (RectTransform rt, Image img, Material mat) CreateFinaleIcon(EnergyTopicData topic, RectTransform parent)
    {
        var go = new GameObject("FinaleIcon_" + topic.topicId, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = Vector2.one * finaleIconSize;

        var img = go.GetComponent<Image>();
        img.sprite = topic.icon;
        img.raycastTarget = false;

        // Forced into the 3 key-visual colors, same as the beam/floor icon
        // (BrandPalette.For), instead of each topic's own distinct color.
        Color brandColor = BrandPalette.For(topic.topicId);

        Material mat = null;
        if (topic.icon != null)
        {
            mat = new Material(Shader.Find("PVGas/LumaRecolor"));
            mat.SetColor("_Color", brandColor);
            mat.SetFloat("_Intensity", finaleIconGlowIntensity);
            mat.SetFloat("_Flat", BrandPalette.IsWhite(brandColor) ? 1f : 0f);
            img.material = mat;
            img.color = Color.white;
        }
        else
        {
            img.color = brandColor;
        }

        return (rt, img, mat);
    }
}
