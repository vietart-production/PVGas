using UnityEngine;

public class TopicButton : MonoBehaviour
{
    [SerializeField] private UIManager uiManager;
    [SerializeField] private TopicData topic;

    public void Configure(UIManager manager, TopicData data)
    {
        uiManager = manager;
        topic = data;
    }

    public void Activate()
    {
        if (uiManager != null && topic != null)
            uiManager.ShowDetail(topic);
    }
}
