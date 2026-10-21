using UnityEngine;

[CreateAssetMenu(fileName = "TopicData", menuName = "PVGas/Topic Data")]
public class TopicData : ScriptableObject
{
    public string topicId;
    public string displayName;
    public Sprite icon;
    public string subtitle;
    public Sprite headerImage;
    public string[] stats;
    public ContentCard[] cards;
}

[System.Serializable]
public class ContentCard
{
    public Sprite image;
    public string title;
    [TextArea] public string body;
}
