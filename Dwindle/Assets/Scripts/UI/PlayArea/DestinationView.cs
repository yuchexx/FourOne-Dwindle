using UnityEngine;
using UnityEngine.UI;

// a colored ring (a circle with a background-colored hole)
// when its object arrives, the hole is hidden so the ring looks filled
public class DestinationView : MonoBehaviour
{
    public Graphic ring; // CircleGraphics
    public Graphic hole;

    public void Setup(Color color, float size)
    {
        ring.color = color;
        ((RectTransform)transform).sizeDelta = Vector2.one * size;
        SetDelivered(false);
    }

    public void SetDelivered(bool delivered) => hole.enabled = !delivered;
}
