using TMPro;
using UnityEngine;
using UnityEngine.UI;

// three stars, each with its goal written under it
public class StarRow : MonoBehaviour
{
    public Graphic[] stars;     // left to right
    public TMP_Text[] captions; // under each star

    public void Show(bool[] earned, bool purple, string[] goals)
    {
        var palette = UIPalette.Current;
        for (int i = 0; i < stars.Length; i++)
        {
            bool on = i < earned.Length && earned[i];
            stars[i].color = on ? (purple ? palette.accent : palette.star) : palette.starEmpty;
            if (stars[i] is StarGraphic sg) sg.outlineColor = Color.clear;
            if (captions != null && i < captions.Length && captions[i] != null)
                captions[i].text = i < goals.Length ? goals[i] : "";
        }
    }
}
