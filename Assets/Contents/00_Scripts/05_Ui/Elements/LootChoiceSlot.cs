using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

// 전리품 선택 버튼 하나하나에 붙을 스크립트
public class LootChoiceSlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI 연결 (슬롯 내부)")]
    public Image lootIcon;
    public TextMeshProUGUI lootNameText;
    public TextMeshProUGUI lootDescText;

    [Header("나무 카드 구성 (비어 있으면 기존 배치)")]
    public RectTransform cardBody;
    public GameObject highlight;
    public RectTransform iconStage;
    public RectTransform iconShadow;

    // 그림 무대의 아이콘 박스. 64px는 2배, 128px는 1배, 그보다 큰 그림(코인 주머니)은 이 박스 안에 맞춘다.
    public const float IconBox = 160f;
    const float HoverLift = 8f;
    const float HoverDuration = 0.1f;

    private BaseItemDataSO currentLootData;
    private LootSelectionPanel parentPanel;
    private float restY;

    private void Awake()
    {

        Button selfButton = GetComponent<Button>();
        if (selfButton != null)
        {
            selfButton.onClick.RemoveAllListeners();
            selfButton.onClick.AddListener(OnClicked);
        }
        if (cardBody != null) restY = cardBody.anchoredPosition.y;
    }

    private void OnDisable()
    {
        SetHover(false, true);
    }

    public void Setup(BaseItemDataSO data, LootSelectionPanel panel)
    {
        currentLootData = data;
        parentPanel = panel;

        // 아이템 정보 채우기
        lootIcon.sprite = data.icon;
        FigureDetailPanel.FitPopupStage(lootIcon, iconStage, iconShadow, IconBox);
        lootNameText.text = LocalizationManager.GetItemDisplayName(data);
        if (lootDescText != null)
        {
            lootDescText.text = LocalizationManager.GetItemDescription(data);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        SetHover(true, false);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        SetHover(false, false);
    }

    // 마우스를 올리면 금색 테두리를 켜고 카드를 살짝 띄운다. 클릭 판정 칸(슬롯 자신)은 움직이지 않는다.
    private void SetHover(bool on, bool instant)
    {
        if (highlight != null) highlight.SetActive(on);
        if (cardBody == null) return;
        cardBody.DOKill();
        float y = on ? restY + HoverLift : restY;
        if (instant)
            cardBody.anchoredPosition = new Vector2(cardBody.anchoredPosition.x, y);
        else
            cardBody.DOAnchorPosY(y, HoverDuration).SetEase(Ease.OutQuad).SetUpdate(true).SetLink(gameObject);
    }

    private void OnClicked()
    {
        // 부모 패널에게 내가 선택되었다고 알림
        if (parentPanel != null && currentLootData != null)
        {
            parentPanel.OnLootSelected(currentLootData);
        }
    }

}
