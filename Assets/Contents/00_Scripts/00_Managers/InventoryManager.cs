using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance { get; private set; }

    [Header("참조")]
    public DiceManager diceManager;

    [Header("슬롯 배열")]
    public InventorySlot[] figureSlots;
    public InventorySlot[] snackSlots;

    [Header("인벤토리 용량 제한")]
    public int maxFigureSlots = 6; // 피규어는 기존처럼 배열 크기만큼 허용
    public int maxSnackSlots = 5;   // 스낵칸 최대 5개로 제한

    [Header("판매 팝업 UI")]
    public GameObject sellPopupRoot;
    public GameObject sellPopupPanel;     // 실제 그래픽이 있는 팝업창 (마우스 따라다닐 부분)
    public Button sellButton;             // 판매 확인 버튼
    public Button backgroundCloseButton;  // 팝업 뒤에 깔린 투명한 전체화면 닫기 버튼
    public TextMeshProUGUI sellPriceText;

    // [추가] 툴팁 UI
    [Header("설명창(Tooltip) UI")]
    public GameObject tooltipPanel;
    public RectTransform tooltipRect;
    public TextMeshProUGUI descText;

    private InventorySlot targetSellSlot;

    private void Awake()
    {
        Instance = this;

        foreach (var slot in figureSlots) slot.Initialize(this);
        foreach (var slot in snackSlots) slot.Initialize(this);

        if (sellButton != null) sellButton.onClick.AddListener(SellTargetItem);

        // 취소 버튼 대신 투명한 배경을 누르면 팝업이 닫히도록 연결
        if (backgroundCloseButton != null) backgroundCloseButton.onClick.AddListener(HideSellPopup);

        // 툴팁 RectTransform 자동 연결
        if (tooltipRect == null && tooltipPanel != null)
            tooltipRect = tooltipPanel.GetComponent<RectTransform>();

        HideSellPopup();
        HideTooltip(); // 시작할 때 툴팁 숨기기
    }

    public void ClearAllSlots()
    {
        foreach (var slot in figureSlots) slot.ClearSlot();
        foreach (var slot in snackSlots) slot.ClearSlot();
        Debug.Log("인벤토리의 모든 아이템이 초기화되었습니다.");
    }

    public bool AddItem(BaseItemDataSO item)
    {
        if (item is FigureItemSO) return PlaceIntoEmptySlot(item, figureSlots, maxFigureSlots);
        else if (item is SnackItemSO) return PlaceIntoEmptySlot(item, snackSlots, maxSnackSlots);
        return false;
    }

    private bool PlaceIntoEmptySlot(BaseItemDataSO item, InventorySlot[] slots, int maxLimit)
    {
        // 슬롯 배열의 실제 길이와 기획상 최대 길이 중 더 작은 값을 기준으로 삼습니다.
        int limit = Mathf.Min(slots.Length, maxLimit);

        for (int i = 0; i < limit; i++)
        {
            if (slots[i].isEmpty)
            {
                slots[i].SetItem(item);
                return true;
            }
        }
        return false;
    }

    public void ShowSellPopup(InventorySlot slot)
    {
        targetSellSlot = slot;

        int sellPrice = Mathf.FloorToInt(slot.currentItem.price * 0.5f);
        if (sellPriceText != null) sellPriceText.text = $"판매: {sellPrice} G";

        if (sellPopupRoot != null)
        {
            // 전체 팝업 루트를 켭니다 (투명 배경 활성화)
            sellPopupRoot.SetActive(true);

            // 실제 내용물이 있는 작은 팝업창만 마우스(슬롯) 위치 근처로 이동시킵니다
            if (sellPopupPanel != null)
            {
                sellPopupPanel.transform.position = slot.transform.position;
                sellPopupPanel.transform.localPosition += new Vector3(0f, 100f, 0f);

                Vector3 localPos = sellPopupPanel.transform.localPosition;
                localPos.z = 0f;
                sellPopupPanel.transform.localPosition = localPos;
            }
        }
    }

    public void HideSellPopup()
    {
        if (sellPopupRoot != null) sellPopupRoot.SetActive(false);
        targetSellSlot = null;
    }

    private void SellTargetItem()
    {
        if (targetSellSlot == null || targetSellSlot.isEmpty) return;

        int sellPrice = Mathf.FloorToInt(targetSellSlot.currentItem.price * 0.5f);

        if (diceManager != null && diceManager.shopManager != null)
        {
            diceManager.shopManager.currentGold += sellPrice;
            diceManager.ui?.UpdateGoldUI(diceManager.shopManager.currentGold);
        }

        Debug.Log($"피규어 [{targetSellSlot.currentItem.itemName}] 판매 완료! +{sellPrice} G");

        targetSellSlot.ClearSlot();
        HideSellPopup();
        HideTooltip(); //판매 후 툴팁 가리기
    }

    //피규어 슬롯에 빈자리가 있는지 확인
    public bool HasEmptyFigureSlot()
    {
        int limit = Mathf.Min(figureSlots.Length, maxFigureSlots);
        for (int i = 0; i < limit; i++)
        {
            if (figureSlots[i].isEmpty) return true;
        }
        return false;
    }

    //보유 중인 피규어들의 클리어 보너스 골드 총합 계산
    public int ApplyAllFigurePassives(DiceManager diceManager, ShopManager shopManager)
    {
        int totalGoldBonus = 0;
        foreach (var slot in figureSlots)
        {
            if (!slot.isEmpty && slot.currentItem is FigureItemSO figure)
            {
                figure.ApplyPassiveEffect(diceManager, shopManager);
                if (figure.abilityType == FigureAbility.GoldBonus)
                    totalGoldBonus += figure.abilityValue;
            }
        }
        return totalGoldBonus;
    }

    public bool HasActiveFigureAbility(FigureAbility abilityType)
    {
        int limit = Mathf.Min(figureSlots.Length, maxFigureSlots);
        for (int i = 0; i < limit; i++)
        {
            if (!figureSlots[i].isEmpty && figureSlots[i].currentItem is FigureItemSO figure)
            {
                if (figure.abilityType == abilityType) return true;
            }
        }
        return false;
    }

    // 툴팁 표시 함수
    public void ShowTooltip(string desc, RectTransform slotRect)
    {
        if (descText == null || tooltipPanel == null) return;

        descText.text = desc;
        tooltipPanel.SetActive(true);
        // 툴팁 패널을 계층 구조의 맨 아래로 보내서 화면상 가장 앞에 오게 합니다.
        tooltipRect.SetAsLastSibling();

        // 툴팁 위치를 슬롯 근처로 조정 (상점과 동일한 방식)
        tooltipRect.pivot = new Vector2(0f, 0.5f);
        tooltipRect.position = slotRect.position;
        // x, y 값을 조절하여 마우스/슬롯을 가리지 않게 오프셋 부여
        tooltipRect.localPosition += new Vector3(0f, -50f, 0f);
    }

    //툴팁 숨김 함수
    public void HideTooltip()
    {
        if (tooltipPanel != null) tooltipPanel.SetActive(false);
    }
}