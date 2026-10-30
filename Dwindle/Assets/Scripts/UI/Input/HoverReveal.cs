using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// shows Target while the mouse is over this object or anything inside it (the score's history, the "..." menu)
public class HoverReveal : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public GameObject target;
    [Tooltip("Seconds it stays open after the mouse leaves")]
    [Min(0f)] public float hideDelay = 0.25f;
    [Tooltip("Optional: clicking this button keeps Target open; clicking it again closes it")]
    public Button pinButton;

    float hideAt = -1f;
    bool pinned;

    void Start()
    {
        if (pinButton != null) pinButton.onClick.AddListener(() => { if (pinned) Hide(); else Pin(); });
    }

    void OnEnable() => Hide();
    void OnDisable() => Hide();

    public void OnPointerEnter(PointerEventData eventData)
    {
        hideAt = -1f;
        if (target != null) target.SetActive(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!pinned) hideAt = Time.unscaledTime + hideDelay;
    }

    void Update()
    {
        if (hideAt >= 0f && Time.unscaledTime >= hideAt) Hide();
    }

    void Pin()
    {
        pinned = true;
        hideAt = -1f;
        if (target != null) target.SetActive(true);
    }

    void Hide()
    {
        pinned = false;
        hideAt = -1f;
        if (target != null) target.SetActive(false);
    }
}
