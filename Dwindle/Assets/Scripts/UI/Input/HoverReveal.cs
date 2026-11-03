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
    bool pinned, hovered;

    void Start()
    {
        if (pinButton != null) pinButton.onClick.AddListener(() => { if (pinned) Hide(); else Pin(); });
    }

    void OnEnable() => Hide();
    void OnDisable() { hovered = false; Hide(); }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovered = true;
        hideAt = -1f;
        if (target != null) target.SetActive(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
        if (!pinned) hideAt = Time.unscaledTime + hideDelay;
    }

    [Tooltip("Open() with the mouse elsewhere: seconds it stays before closing")]
    [Min(0f)] public float openSeconds = 2f;

    // opens Target as a hover would, without pinning: with the mouse on it, it closes when the mouse leaves; with the
    // mouse elsewhere, after Open Seconds
    public void Open()
    {
        if (!isActiveAndEnabled || target == null) return;
        target.SetActive(true);
        hovered |= MouseOver(); // it may have arrived while this was off, so no enter came
        if (!pinned) hideAt = hovered ? -1f : Time.unscaledTime + openSeconds;
    }

    // the mouse is over this object or anything inside it (Target included)
    bool MouseOver()
    {
        var events = EventSystem.current;
        var pointer = UnityEngine.InputSystem.Pointer.current;
        if (events == null || pointer == null) return false;
        var hits = new System.Collections.Generic.List<RaycastResult>();
        events.RaycastAll(new PointerEventData(events) { position = pointer.position.ReadValue() }, hits);
        return hits.Count > 0 && hits[0].gameObject.transform.IsChildOf(transform);
    }

    void Update()
    {
        if (hideAt >= 0f && Time.unscaledTime >= hideAt) Hide();
    }

    // for the tutorial (its score recap, through HudView.HoldScoreHistory): Target stays open whatever the mouse
    // does, as if pinned, until let go (which closes it)
    public void Hold(bool hold)
    {
        if (hold) Pin(); else Hide();
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
