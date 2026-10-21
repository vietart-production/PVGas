using UnityEngine;
using UnityEngine.UI;

// Scrolls a RawImage's UV rect to fake a "light running along the line" effect.
// Native RawImage.uvRect feature - no custom shader needed.
[RequireComponent(typeof(RawImage))]
public class ScrollingUV : MonoBehaviour
{
    [SerializeField] private Vector2 speed = new Vector2(0.4f, 0f);

    private RawImage img;
    private Rect rect;

    private void Awake()
    {
        img = GetComponent<RawImage>();
        rect = img.uvRect;
    }

    private void Update()
    {
        rect.x += speed.x * Time.deltaTime;
        rect.y += speed.y * Time.deltaTime;
        img.uvRect = rect;
    }
}
