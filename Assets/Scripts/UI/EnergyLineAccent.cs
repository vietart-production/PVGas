using UnityEngine;

// Decorative neon ribbon accent (replaces the old scrolling-texture
// "LineGlow"), using the same PVGas/FlowRibbon shader as the Khu 2 energy
// beams. Draws a straight tilted line through "center", at "tiltDegrees" off
// horizontal, "length" long - lives for as long as this component is enabled.
public class EnergyLineAccent : MonoBehaviour
{
    [SerializeField] private RectTransform area; // parent whose local rect space center/length are expressed in
    [SerializeField] private Vector2 center = new Vector2(0f, 40f);
    [SerializeField] private float length = 2200f;
    [SerializeField] private float tiltDegrees = -8f;
    [SerializeField] private Color color = new Color(0.1f, 0.4f, 0.85f, 1f); // PV Gas brand blue
    [SerializeField] private float growDuration = 0.6f;
    [SerializeField] private float seed = 0f;

    private FlowBeam beam;

    private void OnEnable()
    {
        float rad = tiltDegrees * Mathf.Deg2Rad;
        Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        Vector2 half = dir * (length * 0.5f);

        beam = FlowBeam.Create(
            area, center - half, center + half, color, growDuration,
            seed, pathStartDistance: 0f, pinchStart: false, pinchEnd: false,
            siblingIndex: transform.GetSiblingIndex(), onArrive: null, alignToDirection: true);
    }

    private void OnDisable()
    {
        beam?.Dispose();
        beam = null;
    }
}
