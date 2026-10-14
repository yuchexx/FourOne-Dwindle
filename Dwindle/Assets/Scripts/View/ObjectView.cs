using UnityEngine;
using UnityEngine.UI;

// a colored circle with a direction that shows which way it faces.
public class ObjectView : MonoBehaviour
{
    public Image body;
    public GameObject blockedMark; // the cross shown while the object is blocked
    public RectTransform Rect => (RectTransform)transform;

    public void Setup(Color color, float size)
    {
        body.color = color;
        Rect.sizeDelta = Vector2.one * size;
        blockedMark.SetActive(false);
    }

    // the direction points up in the prefab so rotating the whole object turns it to face d
    public void Face(Dir d) => Rect.localEulerAngles = new Vector3(0, 0, DirUtil.Angle(d));

    public void SetBlocked(bool blocked) => blockedMark.SetActive(blocked);
}