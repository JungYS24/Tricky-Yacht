using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CollectionSlot : MonoBehaviour
{
    public Image figureIcon;
    public TextMeshProUGUI nameText;
    public Button slotButton;

    [Header("미해금 상태 처리용")]
    public Sprite unknownQuestionSprite;
    public Color lockedColor = new Color(0f, 0f, 0f, 0.7f); // 실루엣 효과용 까만색

    private FigureItemSO currentFigure;
    private CollectionBookManager manager;
    private bool isUnlocked;
    private bool isEncountered;

    public void Setup(FigureItemSO figureData, bool unlocked, bool encountered, CollectionBookManager mgr)
    {
        currentFigure = figureData;
        isUnlocked = unlocked;
        isEncountered = encountered;
        manager = mgr;

        slotButton.onClick.RemoveAllListeners();
        slotButton.onClick.AddListener(OnSlotClicked);

        if (isUnlocked)
        {
            figureIcon.sprite = figureData.icon;
            figureIcon.color = Color.white;
            nameText.text = figureData.itemName;
            nameText.color = Color.white;
        }
        else if (isEncountered)
        {
            figureIcon.sprite = figureData.icon;
            figureIcon.color = Color.gray;
            nameText.text = figureData.itemName;
            nameText.color = Color.green;
        }
        else
        {
            figureIcon.sprite = unknownQuestionSprite != null ? unknownQuestionSprite : figureData.icon;
            figureIcon.color = unknownQuestionSprite != null ? Color.white : lockedColor;
            nameText.text = "???";
            nameText.color = new Color(0.4f, 0.4f, 0.4f, 1f);
        }
    }

    private void OnSlotClicked()
    {
        if (manager != null)
        {
            manager.OpenFigureDetail(currentFigure);
        }
    }
}