using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

// A brief "stamp" at the spawn point: pops in, holds for a beat, then
// shrinks + fades out and destroys itself - it no longer lingers on the
// floor. The icon's own color is forced into one of the 3 brand colors
// (PVGas/LumaRecolor, luma-based so the illustration's shape/shading still
// reads) instead of the topic's individual color.
public class FloorIcon : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text label;

    public EnergyTopicData Topic { get; private set; }

    private Material recolorMaterial;

    public void Bind(EnergyTopicData topic)
    {
        Topic = topic;
        if (iconImage != null)
        {
            iconImage.sprite = topic.icon;
            var brandColor = BrandPalette.For(topic.topicId);
            if (topic.icon != null)
            {
                recolorMaterial = new Material(Shader.Find("PVGas/LumaRecolor"));
                recolorMaterial.SetColor("_Color", brandColor);
                recolorMaterial.SetFloat("_Flat", BrandPalette.IsWhite(brandColor) ? 1f : 0f);
                iconImage.material = recolorMaterial;
                iconImage.color = Color.white;
            }
            else
            {
                iconImage.color = brandColor;
            }
        }
        if (label != null) label.text = topic.displayName;

        PlayStampFx();
    }

    private void PlayStampFx()
    {
        transform.DOKill();
        transform.localScale = Vector3.one * 0.4f;
        if (iconImage != null) iconImage.color = new Color(iconImage.color.r, iconImage.color.g, iconImage.color.b, 0f);

        var seq = DOTween.Sequence();
        seq.Append(transform.DOScale(1.2f, 0.22f).SetEase(Ease.OutBack));
        if (iconImage != null) seq.Join(iconImage.DOFade(1f, 0.18f));
        seq.AppendInterval(0.25f);
        seq.Append(transform.DOScale(0.7f, 0.5f).SetEase(Ease.InQuad));
        if (iconImage != null) seq.Join(iconImage.DOFade(0f, 0.45f));
        seq.OnComplete(() => Destroy(gameObject));
    }

    private void OnDestroy()
    {
        transform.DOKill();
        if (recolorMaterial != null) Destroy(recolorMaterial);
    }
}
