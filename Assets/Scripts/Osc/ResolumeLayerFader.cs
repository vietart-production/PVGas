using System.Collections;
using UnityEngine;

// Fades one Resolume layer's opacity via OSC. FadeIn/FadeOut are called by
// UIManager whenever the dashboard switches between Home and a Detail topic.
public class ResolumeLayerFader : MonoBehaviour
{
    [SerializeField] private int layerIndex = 2;
    [SerializeField] private float fadeDuration = 0.5f;
    [SerializeField] private string host = OscSender.DefaultHost;
    [SerializeField] private int port = OscSender.DefaultPort;

    private Coroutine fadeRoutine;
    private float currentAlpha;

    private string Address => $"/composition/layers/{layerIndex}/video/opacity";

    public void FadeIn() => StartFade(1f);
    public void FadeOut() => StartFade(0f);

    private void StartFade(float target)
    {
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(Fade(target));
    }

    private IEnumerator Fade(float target)
    {
        float start = currentAlpha;
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            currentAlpha = Mathf.Lerp(start, target, Mathf.Clamp01(t / fadeDuration));
            OscSender.Send(host, port, Address, currentAlpha);
            yield return null;
        }
        currentAlpha = target;
        OscSender.Send(host, port, Address, target);
    }
}
