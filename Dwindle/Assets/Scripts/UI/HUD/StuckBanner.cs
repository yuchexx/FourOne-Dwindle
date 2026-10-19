using System;
using UnityEngine;
using UnityEngine.UI;

// the notice when nobody left can be delivered, with Undo, Restart and End level
public class StuckBanner : MonoBehaviour
{
    [SerializeField] Button undoButton, restartButton, endButton;

    Action onUndo, onRestart, onEnd;

    // the buttons call whatever Show was given
    void Awake()
    {
        if (undoButton != null) undoButton.onClick.AddListener(() => onUndo?.Invoke());
        if (restartButton != null) restartButton.onClick.AddListener(() => onRestart?.Invoke());
        if (endButton != null) endButton.onClick.AddListener(() => onEnd?.Invoke());
    }

    public void Show(Action undo, Action restart, Action end)
    {
        onUndo = undo;
        onRestart = restart;
        onEnd = end;
        transform.SetAsLastSibling(); // drawn over the board (and under the menus, if it sits in GameScreen)
        gameObject.SetActive(true);
    }

    public void Hide() => gameObject.SetActive(false);
    public bool Showing => gameObject.activeSelf;
}
