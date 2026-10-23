using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ToastView : MonoBehaviour
{
    [SerializeField] CanvasGroup canvasGroup;
    [SerializeField] Image background;
    [SerializeField] TMP_Text label;

    [Header("Timing")]
    [SerializeField] float fadeSeconds = 0.18f;
    [SerializeField] float holdSeconds = 1.35f;

    [Header("Colors")]
    [SerializeField] Color normalColor =
        new Color(0.19f, 0.21f, 0.27f, 0.96f);

    readonly Queue<ToastMessage> messages = new();
    Coroutine showing;

    struct ToastMessage
    {
        public string text;
        public Color color;
    }

    void Awake()
    {
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    public void Show(string message)
    {
        Show(message, normalColor);
    }

    public void Show(string message, Color color)
    {
        messages.Enqueue(new ToastMessage
        {
            text = message,
            color = color
        });

        if (showing == null)
            showing = StartCoroutine(ShowMessages());
    }

    IEnumerator ShowMessages()
    {
        while (messages.Count > 0)
        {
            ToastMessage message = messages.Dequeue();

            label.text = message.text;
            background.color = normalColor;

            yield return Fade(0f, 1f);
            yield return new WaitForSecondsRealtime(holdSeconds);
            yield return Fade(1f, 0f);
        }

        showing = null;
    }

    IEnumerator Fade(float from, float to)
    {
        if (fadeSeconds <= 0f)
        {
            canvasGroup.alpha = to;
            yield break;
        }

        for (float elapsed = 0f;
             elapsed < fadeSeconds;
             elapsed += Time.unscaledDeltaTime)
        {
            canvasGroup.alpha =
                Mathf.Lerp(from, to, elapsed / fadeSeconds);

            yield return null;
        }

        canvasGroup.alpha = to;
    }

        void OnDisable()
    {
        Clear();
    }

    public void Clear()
    {
        messages.Clear();

        if (showing != null)
            StopCoroutine(showing);

        showing = null;
        canvasGroup.alpha = 0f;
    }
}