using UnityEngine;
using UnityEngine.UI;

// Holds references to a built icon item's pieces so editor tooling can wire
// them up right after construction (home row icons + detail left-nav icons).
public class IconItemRefs : MonoBehaviour
{
    public Button button;
    public TopicButton topicButton;
}
