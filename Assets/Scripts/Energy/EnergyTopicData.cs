using UnityEngine;

[CreateAssetMenu(fileName = "EnergyTopicData", menuName = "PVGas/Energy Topic Data")]
public class EnergyTopicData : ScriptableObject
{
    public string topicId;
    public string displayName;
    public Color color = Color.white;
    public Sprite icon;

    [Tooltip("Degrees around the center LED flame, 0 = top, clockwise.")]
    public float slotAngleDegrees;
}
