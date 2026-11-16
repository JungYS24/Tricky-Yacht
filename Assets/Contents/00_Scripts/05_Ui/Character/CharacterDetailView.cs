using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 상세 화면(ref_03~07). 캐릭터마다 패널을 복제하지 않고 이 한 패널을 다시 채운다.
public class CharacterDetailView : MonoBehaviour
{
    [Header("왼쪽")]
    [SerializeField] private TextMeshProUGUI headerText;
    [SerializeField] private CharacterSelectFigure figure;
    [SerializeField] private Image figureGlow;
    [SerializeField, Range(0f, 1f)] private float glowAlpha = 0.22f;
    [SerializeField] private TextMeshProUGUI baseHpLabel;
    [SerializeField] private TextMeshProUGUI baseHpValue;
    [SerializeField] private TextMeshProUGUI baseGoldLabel;
    [SerializeField] private TextMeshProUGUI baseGoldValue;
    [SerializeField] private TextMeshProUGUI playLabel;
    [SerializeField] private TextMeshProUGUI playValue;

    [Header("오른쪽")]
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI subtitleText;
    [SerializeField] private TextMeshProUGUI summaryText;
    [SerializeField] private TextMeshProUGUI deckLabel;
    [SerializeField] private RectTransform deckRow;
    [Tooltip("배경 Image + 자식 아이콘 Image 한 칸. 첫 칸(여정 주사위)만 배경을 켠다.")]
    [SerializeField] private RectTransform deckSlotTemplate;
    [SerializeField] private Color journeySlotColor = new Color32(0x3A, 0x30, 0x2A, 0xFF);
    [SerializeField] private TextMeshProUGUI uniqueLabel;
    [SerializeField] private RectTransform uniqueRoot;
    [SerializeField] private CharacterSelectInfoCardView uniqueTemplate;
    [SerializeField] private TextMeshProUGUI figureLabel;
    [SerializeField] private CharacterSelectInfoCardView figureA;
    [SerializeField] private CharacterSelectInfoCardView figureB;

    [Header("우측 하단")]
    [SerializeField] private Button startButton;
    [SerializeField] private TextMeshProUGUI startText;
    [SerializeField] private GameObject lockBox;
    [SerializeField] private TextMeshProUGUI lockHeaderText;
    [SerializeField] private TextMeshProUGUI lockConditionText;

    [Header("이동")]
    [SerializeField] private Button backButton;
    [SerializeField] private TextMeshProUGUI backText;
    [SerializeField] private Button previousButton;
    [SerializeField] private Button nextButton;

    [Tooltip("이전/다음 캐릭터로 넘길 때 짧게 움직이는 영역")]
    [SerializeField] private RectTransform content;

    public Button StartButton => startButton;
    public Button BackButton => backButton;
    public Button PreviousButton => previousButton;
    public Button NextButton => nextButton;
    public RectTransform Content => content;

    public void Bind(CharacterSelectEntry entry, int index, CharacterSelectCatalog catalog)
    {
        CharacterData data = entry.character;
        bool unlocked = data == null || data.IsUnlocked;
        Color accent = entry.accent;
        string accentHex = ColorUtility.ToHtmlStringRGB(accent);

        // 01은 목록 화면 번호이므로 캐릭터는 02부터 센다(레퍼런스와 같은 번호).
        if (headerText != null) headerText.text = $"<color=#{accentHex}>{index + 2:00}</color>   {entry.englishName.Get()}";

        if (figure != null) figure.Show(entry, !unlocked, catalog);
        if (figureGlow != null)
        {
            Color glow = accent;
            glow.a = glowAlpha;
            figureGlow.color = glow;
        }

        SetText(baseHpLabel, CharacterSelectLabels.BaseHp.Get());
        SetText(baseHpValue, data != null ? data.InitialHP.ToString() : "-");
        SetText(baseGoldLabel, CharacterSelectLabels.BaseGold.Get());
        SetText(baseGoldValue, CharacterSelectInteraction.StartingGold.ToString());
        SetText(playLabel, CharacterSelectLabels.Play.Get());
        SetText(playValue, entry.playTag.Get(), accent);

        SetText(nameText, LocalizationManager.GetCharacterName(data));
        SetText(subtitleText, entry.subtitle.Get(), accent);
        SetText(summaryText, entry.summary.Get());

        List<DiceData1> deck = catalog.BuildStartingDeck();
        SetText(deckLabel, CharacterSelectLabels.Format(CharacterSelectLabels.StartDeck, deck.Count));
        BindDeck(deck, catalog);

        SetText(uniqueLabel, CharacterSelectLabels.UniqueDice.Get());
        BindUniqueDice(entry, accent);

        SetText(figureLabel, CharacterSelectLabels.StartFigure.Get());
        if (figureA != null) figureA.Bind(entry.figureA, accent, "A");
        if (figureB != null) figureB.Bind(entry.figureB, accent, "B");

        if (startButton != null)
        {
            startButton.gameObject.SetActive(unlocked);
            startButton.interactable = unlocked;
            if (startButton.targetGraphic != null) startButton.targetGraphic.color = accent;
        }
        SetText(startText, CharacterSelectLabels.Start.Get());

        if (lockBox != null) lockBox.SetActive(!unlocked);
        SetText(lockHeaderText, CharacterSelectLabels.UnlockHeader.Get());
        SetText(lockConditionText, entry.unlockCondition.Get());

        SetText(backText, CharacterSelectLabels.Back.Get());
    }

    private void BindDeck(List<DiceData1> deck, CharacterSelectCatalog catalog)
    {
        if (deckRow == null || deckSlotTemplate == null)
            return;

        List<RectTransform> slots = CharacterSelectUi.EnsureItems(deckRow, deckSlotTemplate, deck.Count, "Slot_");
        for (int i = 0; i < slots.Count; i++)
        {
            Image background = slots[i].GetComponent<Image>();
            if (background != null)
                background.color = deck[i].isJourneyDice ? journeySlotColor : Color.clear;

            Image icon = slots[i].childCount > 0 ? slots[i].GetChild(0).GetComponent<Image>() : null;
            if (icon != null)
            {
                Sprite sprite = catalog.DieSprite(deck[i]);
                icon.sprite = sprite;
                icon.enabled = sprite != null;
            }
        }
    }

    private void BindUniqueDice(CharacterSelectEntry entry, Color accent)
    {
        if (uniqueRoot == null || uniqueTemplate == null)
            return;

        int count = entry.uniqueDice != null ? entry.uniqueDice.Count : 0;
        List<RectTransform> cards = CharacterSelectUi.EnsureItems(uniqueRoot, (RectTransform)uniqueTemplate.transform, count, "UniqueDice_");
        for (int i = 0; i < cards.Count; i++)
            cards[i].GetComponent<CharacterSelectInfoCardView>().Bind(entry.uniqueDice[i], accent, null);
    }

    private static void SetText(TextMeshProUGUI label, string value)
    {
        if (label != null) label.text = value;
    }

    private static void SetText(TextMeshProUGUI label, string value, Color color)
    {
        if (label == null) return;
        label.text = value;
        label.color = color;
    }
}
