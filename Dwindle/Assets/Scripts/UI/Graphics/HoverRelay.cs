using System;
using UnityEngine;
using UnityEngine.EventSystems;

// reports when the mouse goes over or leaves this UI element (added from code, e.g. to the doors)
public class HoverRelay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public Action onEnter, onExit;

    public void OnPointerEnter(PointerEventData eventData) => onEnter?.Invoke();
    public void OnPointerExit(PointerEventData eventData) => onExit?.Invoke();
}
