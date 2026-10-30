using UnityEngine;
using UnityEngine.EventSystems;

// shows Target while the mouse is over this object or anything inside it (the score's history, the "..." menu)
public class HoverReveal : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public GameObject target;
    [Tooltip("Seconds it stays open after the mouse leaves")]
    [Min(0f)] public float hideDelay = 0.25f;

    float hideAt = -1f;

    void OnEnable() => Hide();
    void OnDisable() => Hide();

    public void OnPointerEnter(PointerEventData eventData)
    {
        hideAt = -1f;
        if (target != null) target.SetActive(true);
    }

    public void OnPointerExit(PointerEventData eventData) => hideAt = Time.unscaledTime + hideDelay;

    void Update()
    {
        if (hideAt >= 0f && Time.unscaledTime >= hideAt) Hide();
    }

    void Hide()
    {
        hideAt = -1f;
        if (target != null) target.SetActive(false);
    }
}
