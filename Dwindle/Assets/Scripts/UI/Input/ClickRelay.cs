using System;
using UnityEngine;
using UnityEngine.EventSystems;

// reports a mouse press on this UI element
public class ClickRelay : MonoBehaviour, IPointerDownHandler
{
    public Action onDown;

    public void OnPointerDown(PointerEventData eventData) => onDown?.Invoke();
}
