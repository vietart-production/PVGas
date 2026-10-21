using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

// A single expanding ring of light (PVGas/RadialPulseGlow), the same color
// as the beam it accompanies, that flashes outward once right where the
// beam spawns, then fades softly away - a one-shot burst, not a looping
// aura, and not a hard cut at the end.
public static class SpawnBurst
{
    public static void Create(RectTransform area, Vector2 position, Color color, float radius,
        float expandDuration, float fadeDuration, int siblingIndex, float intensity = 3.2f)
    {
        var go = new GameObject("SpawnBurst", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(area, false);
        rt.anchorMin = rt.anchorMax = area.pivot;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = position;
        rt.sizeDelta = new Vector2(radius * 2f, radius * 2f);
        rt.SetSiblingIndex(Mathf.Clamp(siblingIndex, 0, area.childCount - 1));

        var mat = new Material(Shader.Find("PVGas/RadialPulseGlow"));
        mat.SetColor("_Color", color);
        mat.SetFloat("_RingCount", 1f);
        mat.SetFloat("_Softness", 0.8f); // gentler fade toward the edge
        mat.SetFloat("_RingSharpness", 2f); // wider, softer band instead of a thin crisp line
        // The single ring travels from the center (dist 0.5) to the edge
        // (dist 1.0) over "expandDuration" seconds - see RadialPulseGlow
        // .shader's phase math (dist*_RingCount - t*_Speed).
        mat.SetFloat("_Speed", 0.5f / Mathf.Max(expandDuration, 0.01f));
        mat.SetFloat("_StartTime", Time.time);
        mat.SetFloat("_Intensity", intensity);

        var img = go.GetComponent<Image>();
        img.material = mat;
        img.color = Color.white;
        img.raycastTarget = false;

        DOTween.Sequence()
            .AppendInterval(expandDuration)
            .Append(img.DOFade(0f, fadeDuration))
            .OnComplete(() =>
            {
                if (mat != null) Object.Destroy(mat);
                if (go != null) Object.Destroy(go);
            });
    }
}
