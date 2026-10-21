using System.Collections;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    [SerializeField] private CanvasGroup homePanel;
    [SerializeField] private CanvasGroup detailPanel;
    [SerializeField] private DetailPanelController detailController;

    [SerializeField] private CanvasGroup projectionHomePanel;
    [SerializeField] private CanvasGroup projectionDetailPanel;
    [SerializeField] private DetailPanelController projectionController;

    [SerializeField] private float fadeDuration = 0.25f;
    [SerializeField] private ResolumeLayerFader resolumeFader;
    [SerializeField] private KioskStateBroadcaster broadcaster;

    private Coroutine fadeRoutine;

    public void Configure(CanvasGroup home, CanvasGroup detail, DetailPanelController controller)
    {
        homePanel = home;
        detailPanel = detail;
        detailController = controller;
    }

    // Optional mirror output (e.g. the big-screen projection feed) - same
    // state as the main UI, just without the touch-only Back/Home buttons.
    public void ConfigureProjection(CanvasGroup home, CanvasGroup detail, DetailPanelController controller)
    {
        projectionHomePanel = home;
        projectionDetailPanel = detail;
        projectionController = controller;
    }

    private void Start()
    {
        SetImmediate(homePanel, true);
        SetImmediate(detailPanel, false);
        SetImmediate(projectionHomePanel, true);
        SetImmediate(projectionDetailPanel, false);
    }

    public void ConfigureResolume(ResolumeLayerFader fader)
    {
        resolumeFader = fader;
    }

    public void ConfigureBroadcaster(KioskStateBroadcaster value)
    {
        broadcaster = value;
    }

    public void ShowHome()
    {
        SwitchTo(detailPanel, homePanel, projectionDetailPanel, projectionHomePanel);
        if (resolumeFader != null) resolumeFader.FadeOut();
        if (broadcaster != null) broadcaster.BroadcastHome();
    }

    public void ShowDetail(TopicData topic)
    {
        detailController.Bind(topic);
        if (projectionController != null) projectionController.Bind(topic);
        SwitchTo(homePanel, detailPanel, projectionHomePanel, projectionDetailPanel);
        if (resolumeFader != null) resolumeFader.FadeIn();
        if (broadcaster != null) broadcaster.BroadcastTopic(topic.topicId);
    }

    private void SwitchTo(CanvasGroup from, CanvasGroup to, CanvasGroup fromMirror, CanvasGroup toMirror)
    {
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(Fade(from, to, fromMirror, toMirror));
    }

    private IEnumerator Fade(CanvasGroup from, CanvasGroup to, CanvasGroup fromMirror, CanvasGroup toMirror)
    {
        PrepareFadeIn(from, false);
        PrepareFadeIn(to, true);
        PrepareFadeIn(fromMirror, false);
        PrepareFadeIn(toMirror, true);

        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            float a = Mathf.Clamp01(t / fadeDuration);
            SetAlpha(from, 1f - a);
            SetAlpha(to, a);
            SetAlpha(fromMirror, 1f - a);
            SetAlpha(toMirror, a);
            yield return null;
        }

        FinishFadeOut(from);
        FinishFadeIn(to);
        FinishFadeOut(fromMirror);
        FinishFadeIn(toMirror);
    }

    private void PrepareFadeIn(CanvasGroup group, bool isTarget)
    {
        if (group == null) return;
        if (isTarget) group.gameObject.SetActive(true);
        group.interactable = false;
        group.blocksRaycasts = false;
    }

    private void SetAlpha(CanvasGroup group, float a)
    {
        if (group == null) return;
        group.alpha = a;
    }

    private void FinishFadeOut(CanvasGroup group)
    {
        if (group == null) return;
        group.alpha = 0f;
        group.gameObject.SetActive(false);
    }

    private void FinishFadeIn(CanvasGroup group)
    {
        if (group == null) return;
        group.alpha = 1f;
        group.interactable = true;
        group.blocksRaycasts = true;
    }

    private void SetImmediate(CanvasGroup group, bool visible)
    {
        if (group == null) return;
        group.gameObject.SetActive(visible);
        group.alpha = visible ? 1f : 0f;
        group.interactable = visible;
        group.blocksRaycasts = visible;
    }
}
