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

    [Header("이름 색 (양피지 위에서 읽히는 색 하나로 통일)")]
    public Color nameColor = new Color(0.29f, 0.18f, 0.1f, 1f);
    public Color unknownNameColor = new Color(0.5f, 0.44f, 0.37f, 1f);

    [Header("마주쳤지만 아직 해금 안 된 피규어 표식 (예전 초록 이름 대신)")]
    public GameObject encounteredMark;

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
            nameText.text = LocalizationManager.GetItemDisplayName(figureData);
            nameText.color = nameColor;
        }
        else if (isEncountered)
        {
            figureIcon.sprite = figureData.icon;
            figureIcon.color = Color.gray;
            nameText.text = LocalizationManager.GetItemDisplayName(figureData);
            nameText.color = nameColor;
        }
        else
        {
            figureIcon.sprite = unknownQuestionSprite != null ? unknownQuestionSprite : figureData.icon;
            figureIcon.color = unknownQuestionSprite != null ? Color.white : lockedColor;
            nameText.text = LocalizationManager.GetUi("UI_UNKNOWN", "???");
            nameText.color = unknownNameColor;
        }

        if (encounteredMark != null)
            encounteredMark.SetActive(isEncountered && !isUnlocked);
    }

    private void OnSlotClicked()
    {
        if (manager != null)
        {
            manager.OpenFigureDetail(currentFigure);
        }
    }
}
