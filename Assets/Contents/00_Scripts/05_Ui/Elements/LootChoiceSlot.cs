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

    [Header("등급 스킨 (비어 있으면 바꾸지 않음)")]
    public LootPanelSkin panelSkin;
    public Image cardImage;

    // 그림 무대의 아이콘 박스. 64px는 2배, 128px는 1배, 그보다 큰 그림(코인 주머니)은 이 박스 안에 맞춘다.
    public const float IconBox = 160f;
    const float HoverLift = 8f;
    const float HoverDuration = 0.1f;

    // 고를 수 없는 카드(스낵칸 가득)의 어둡기
    static readonly Color DisabledTint = new Color(0.45f, 0.45f, 0.45f, 1f);
    const float LegendPop = 1.06f;
    const float LegendPopDuration = 0.15f;

    private BaseItemDataSO currentLootData;
    private RewardRow currentReward;
    private bool selectable = true;
    private LootSelectionPanel parentPanel;
    private float restY;
    private Button selfButton;
    private bool colorsCaptured;
    private Color cardColor = Color.white;
    private Color iconColor = Color.white;

    public RewardRow CurrentReward => currentReward;
    public bool Selectable => selectable;

    private void Awake()
    {

        selfButton = GetComponent<Button>();
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
        if (cardBody != null)
        {
            cardBody.DOKill();
            cardBody.localScale = Vector3.one;
        }
    }

    public void Setup(BaseItemDataSO data, LootSelectionPanel panel)
    {
        currentLootData = data;
        currentReward = null;
        parentPanel = panel;
        SetSelectable(true);

        // 아이템 정보 채우기
        lootIcon.sprite = data.icon;
        lootIcon.enabled = true;
        FigureDetailPanel.FitPopupStage(lootIcon, iconStage, iconShadow, IconBox);
        lootNameText.text = LocalizationManager.GetItemDisplayName(data);
        if (lootDescText != null)
        {
            lootDescText.text = LocalizationManager.GetItemDescription(data);
        }
        ApplyGradeSkin(data.grade);
    }

    // 루트 테이블 보상 카드. 아이콘 → 이름 → 구분선 → 설명(기존 카드 그대로, 등급 배너 없음).
    public void Setup(RewardRow row, Sprite icon, string name, string desc, bool selectable, LootSelectionPanel panel)
    {
        currentLootData = null;
        currentReward = row;
        parentPanel = panel;

        lootIcon.sprite = icon;
        lootIcon.enabled = icon != null;
        FigureDetailPanel.FitPopupStage(lootIcon, iconStage, iconShadow, IconBox);
        FitRewardIcon(lootIcon, IconBox);
        lootNameText.text = name;
        if (lootDescText != null) lootDescText.text = desc;
        ApplyGradeSkin(row != null ? row.ItemGrade : ItemGrade.Normal);
        SetSelectable(selectable);

        if (row != null && row.ItemGrade == ItemGrade.Legendary) PlayLegendPop();
    }

    // 큰 그림(보상 아이콘 256px, 리뉴얼 스낵 256px)은 박스에 맞춰 0.625배처럼 어중간하게 줄이지 않고
    // 1/2, 1/4 처럼 정확히 나눠 떨어지게 줄인다(256 → 128). 박스보다 작은 그림은 기존처럼 정수 배로 키운다.
    public static void FitRewardIcon(Image image, float box)
    {
        if (image == null || image.sprite == null) return;
        float longest = Mathf.Max(image.sprite.rect.width, image.sprite.rect.height);
        if (longest <= box) return; // FitPopupStage가 이미 정수 배로 맞췄다.
        float scale = 1f;
        while (longest * scale > box && scale > 1f / 64f) scale *= 0.5f;
        image.preserveAspect = true;
        image.rectTransform.sizeDelta = image.sprite.rect.size * scale;
    }

    void SetSelectable(bool value)
    {
        selectable = value;
        if (selfButton == null) selfButton = GetComponent<Button>();
        if (selfButton != null) selfButton.interactable = value;
        if (cardImage == null && cardBody != null) cardImage = cardBody.GetComponent<Image>();
        if (!colorsCaptured)
        {
            colorsCaptured = true;
            cardColor = cardImage != null ? cardImage.color : Color.white;
            iconColor = lootIcon != null ? lootIcon.color : Color.white;
        }
        if (cardImage != null) cardImage.color = value ? cardColor : cardColor * DisabledTint;
        if (lootIcon != null) lootIcon.color = value ? iconColor : iconColor * DisabledTint;
        if (!value) SetHover(false, true);
    }

    void PlayLegendPop()
    {
        if (cardBody == null) return;
        cardBody.DOKill();
        cardBody.localScale = Vector3.one * LegendPop;
        cardBody.DOScale(1f, LegendPopDuration).SetEase(Ease.OutQuad).SetUpdate(true).SetLink(gameObject);
    }

    // 등급에 맞춰 카드 바탕과 이름 색을 바꾼다.
    private void ApplyGradeSkin(ItemGrade grade)
    {
        if (panelSkin == null) return;
        if (cardImage == null && cardBody != null) cardImage = cardBody.GetComponent<Image>();
        Sprite panel = panelSkin.PanelFor(grade);
        if (cardImage != null && panel != null) cardImage.sprite = panel;
        if (lootNameText != null) lootNameText.color = panelSkin.NameColorFor(grade);
        if (lootDescText != null) lootDescText.color = panelSkin.descColor;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!selectable) return;
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
        cardBody.localScale = Vector3.one; // 레전드 등장 연출 중이었으면 원래 크기로
        float y = on ? restY + HoverLift : restY;
        if (instant)
            cardBody.anchoredPosition = new Vector2(cardBody.anchoredPosition.x, y);
        else
            cardBody.DOAnchorPosY(y, HoverDuration).SetEase(Ease.OutQuad).SetUpdate(true).SetLink(gameObject);
    }

    private void OnClicked()
    {
        // 부모 패널에게 내가 선택되었다고 알림
        if (parentPanel == null || !selectable) return;
        if (currentReward != null)
            parentPanel.OnRewardSelected(this);
        else if (currentLootData != null)
            parentPanel.OnLootSelected(currentLootData);
    }

}
