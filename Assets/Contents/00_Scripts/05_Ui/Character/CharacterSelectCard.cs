using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 목록 화면의 캐릭터 카드 한 장: 색 줄 → IDLE → 이름 → 플레이 태그 → 체력·골드 → 덱 주사위 아이콘 ×개수.
public class CharacterSelectCard : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image topLine;
    [SerializeField] private Image background;
    [SerializeField] private Image border;
    [SerializeField] private CharacterSelectFigure figure;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI tagText;
    [SerializeField] private TextMeshProUGUI statsText;
    [SerializeField] private RectTransform diceRow;
    [Tooltip("주사위 아이콘 + ×개수 한 묶음. 개수만큼 복제해서 쓴다.")]
    [SerializeField] private RectTransform diceTemplate;

    [Header("강조")]
    [SerializeField] private Color backgroundColor = new Color32(0x22, 0x1B, 0x18, 0xFF);
    [SerializeField, Range(0f, 1f)] private float hoverAlpha = 0.55f;
    [SerializeField, Range(0f, 1f)] private float borderAlpha = 0.7f;

    public event Action<CharacterSelectCard> Clicked;

    public int Index { get; private set; }

    private Color accent = Color.white;
    private bool selected;
    private bool hovered;

    public void Bind(int index, CharacterSelectEntry entry, CharacterSelectCatalog catalog, List<CharacterSelectCatalog.DeckGroup> dice, int gold, string hpLabel, string goldLabel, string countFormat)
    {
        Index = index;
        accent = entry.accent;
        CharacterData data = entry.character;
        bool locked = data != null && !data.IsUnlocked;

        if (topLine != null) topLine.color = accent;
        if (figure != null) figure.Show(entry, locked, catalog);
        if (nameText != null) nameText.text = LocalizationManager.GetCharacterName(data);
        if (tagText != null)
        {
            tagText.text = entry.playTag.Get();
            tagText.color = accent;
        }
        if (statsText != null)
            statsText.text = $"<color=#9A9088>{hpLabel}</color> {(data != null ? data.InitialHP : 0)}      <color=#9A9088>{goldLabel}</color> {gold}";

        BindDice(dice, countFormat);
        Refresh();
    }

    public void SetSelected(bool value)
    {
        selected = value;
        Refresh();
    }

    public void OnPointerClick(PointerEventData eventData) => Clicked?.Invoke(this);

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovered = true;
        Refresh();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
        Refresh();
    }

    private void Refresh()
    {
        if (background != null)
        {
            Color c = backgroundColor;
            c.a = selected ? 1f : hovered ? hoverAlpha : 0f;
            background.color = c;
        }

        if (border != null)
        {
            Color c = accent;
            c.a = selected ? borderAlpha : 0f;
            border.color = c;
        }
    }

    private void BindDice(List<CharacterSelectCatalog.DeckGroup> dice, string countFormat)
    {
        if (diceRow == null || diceTemplate == null)
            return;

        List<RectTransform> items = CharacterSelectUi.EnsureItems(diceRow, diceTemplate, dice.Count, "Dice_");
        for (int i = 0; i < dice.Count; i++)
        {
            RectTransform item = items[i];
            Image icon = item.GetComponentInChildren<Image>(true);
            if (icon != null)
            {
                icon.sprite = dice[i].sprite;
                icon.enabled = dice[i].sprite != null;
            }

            TextMeshProUGUI count = item.GetComponentInChildren<TextMeshProUGUI>(true);
            if (count != null)
                count.text = string.Format(countFormat, dice[i].count);
        }
    }
}
