using UnityEngine;

// ponytail: sine-wave pulse is a placeholder "bubble glow" look; swap for
// real glow sprite/shader once PV Gas art assets (bong bóng nước icon) arrive.
public class IconPulse : MonoBehaviour
{
    [SerializeField] private float scaleAmount = 0.06f;
    [SerializeField] private float speed = 1.5f;
    [SerializeField] private CanvasGroup glowGroup;

    private Vector3 baseScale;
    private float offset;

    private void Awake()
    {
        baseScale = transform.localScale;
        offset = Random.Range(0f, Mathf.PI * 2f);
    }

    private void Update()
    {
        float s = Mathf.Sin(Time.time * speed + offset);
        transform.localScale = baseScale * (1f + s * scaleAmount);
        if (glowGroup != null)
            glowGroup.alpha = 0.6f + s * 0.4f;
    }
}
