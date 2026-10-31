using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// a supply slot: a button with a tile icon. click to pick it, or drag it onto a square
public class TileCard : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public Image background;
    public Button button;
    public RectTransform iconRoot;
    public TMP_Text label;
    [Range(0f, 1f)] public float hoverLighten = 0.22f; // how much lighter the background gets under the mouse, when it can be clicked

    TileView icon;
    TileType type;
    Color tileColor;
    Image iconBack;      // the light square behind the tile (the Icon object's image)
    Color iconBackColor;
    Color backgroundColor; // the color without the hover
    bool hovered;

    void Awake()
    {
        // the hover is drawn here (only when the card can be clicked); the prefab's Button has no tints of its own
        backgroundColor = background.color;
    }

    public void SetTile(TileView tilePrefab, TileType type, Color color)
    {
        if (icon == null) icon = Instantiate(tilePrefab, iconRoot);
        this.type = type;
        tileColor = color;
        icon.Show(type, color);
    }

    // the background color the card has when the mouse isn't over it
    public void SetBackground(Color color)
    {
        backgroundColor = color;
        ApplyBackground();
    }

    void ApplyBackground() =>
        background.color = hovered && button.interactable ? Color.Lerp(backgroundColor, Color.white, hoverLighten) : backgroundColor;

    public void OnPointerEnter(PointerEventData eventData) { hovered = true; ApplyBackground(); }
    public void OnPointerExit(PointerEventData eventData) { hovered = false; ApplyBackground(); }

    // set by HudView: a drag from this card (GameManager decides whether it may start)
    public System.Action<TileType, PointerEventData> onDragStart;
    public System.Action<PointerEventData> onDrag, onDragEnd;

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left) onDragStart?.Invoke(type, eventData);
    }

    public void OnDrag(PointerEventData eventData) => onDrag?.Invoke(eventData);
    public void OnEndDrag(PointerEventData eventData) => onDragEnd?.Invoke(eventData);

    // a tile type you have none of can't be clicked
    public void SetAvailable(bool available)
    {
        button.interactable = available;
        ApplyBackground(); // the hover goes away when the last one is used
    }

    // an empty tile, dimmed: its square and its path in these colors (still readable, but quiet)
    public void ShowDimmed(bool dimmed, Color backColor, Color pathColor)
    {
        if (iconBack == null)
        {
            iconBack = iconRoot.GetComponent<Image>();
            if (iconBack != null) iconBackColor = iconBack.color;
        }
        if (iconBack != null) iconBack.color = dimmed ? backColor : iconBackColor;
        if (icon != null) icon.Show(type, dimmed ? pathColor : tileColor);
    }
}
