using UnityEngine;
using UnityEngine.UI;

// a graphic's colour going from one colour to another and back, like the hints' pulse (the guide's example pictures)
public class GuidePulse : MonoBehaviour
{
    public Graphic target;
    public Color from = Color.white, to = Color.white;
    public float seconds = 1.6f;

    void Update()
    {
        if (target == null) return;
        target.color = Color.Lerp(from, to, 0.5f - 0.5f * Mathf.Cos(Time.unscaledTime * 2f * Mathf.PI / seconds));
    }
}
