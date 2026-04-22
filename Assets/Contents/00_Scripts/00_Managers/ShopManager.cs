using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class ShopManager : MonoBehaviour
{
    public static bool IsShopOpen { get; private set; } = false;

    [Header("참조 설정")]
    public DiceManager diceManager;
    public List<BaseItemDataSO> allItemsPool;
    public ShopSlot[] shopSlots;
    public GameObject shopUI;

    [Header("설명창(Tooltip) UI")]
    public GameObject tooltipPanel;
    public RectTransform tooltipRect;
    public TextMeshProUGUI descText;

    [Header("리롤 및 재화 설정")]
    public int currentGold = 3000;
    public Button shopRerollButton;
    public TextMeshProUGUI rerollCostText;
    public int rerollCost = 100;

    [Header("상점 제어 버튼")]
    public Button nextStageButton;

    [Header("티켓 시스템 UI")]
    public GameObject ticketSelectionPanel;
    public Button[] ticketChoiceButtons;

    [Header("코팅 선택 UI")]
    public CoatingSelectionPanel coatingSelectionPanel;

    private void Awake()
    {
        if (tooltipRect == null && tooltipPanel != null)
            tooltipRect = tooltipPanel.GetComponent<RectTransform>();

        HideTooltip();

        if (shopRerollButton != null)
            shopRerollButton.onClick.AddListener(RerollShop);

        if (rerollCostText != null)
            rerollCostText.text = "리롤 : "+rerollCost + " G";

        if (nextStageButton != null)
            nextStageButton.onClick.AddListener(CloseShopAndGoNext);

        if (ticketSelectionPanel != null)
            ticketSelectionPanel.SetActive(false);


        IsShopOpen = false;
    }

    private void Start()
    {
        if (diceManager?.ui != null) diceManager.ui.UpdateGoldUI(currentGold);
    }

    public void OpenShop()
    {
        IsShopOpen = true;
        if (shopUI != null) shopUI.SetActive(true);
        if (diceManager?.ui != null) diceManager.ui.UpdateGoldUI(currentGold);

        RefreshShop(false);
    }

    public void RefreshShop(bool isReroll)
    {
        List<BaseItemDataSO> shuffled = new List<BaseItemDataSO>(allItemsPool);
        for (int i = 0; i < shuffled.Count; i++)
        {
            int rnd = Random.Range(i, shuffled.Count);
            (shuffled[i], shuffled[rnd]) = (shuffled[rnd], shuffled[i]);
        }

        int dataIndex = 0;
        for (int i = 0; i < shopSlots.Length; i++)
        {
            if (isReroll && shopSlots[i].isPurchased) continue;

            if (dataIndex < shuffled.Count)
            {
                shopSlots[i].SetupSlot(shuffled[dataIndex], this);
                dataIndex++;
            }
        }
    }

    public void RerollShop()
    {
        if (coatingSelectionPanel != null && coatingSelectionPanel.gameObject.activeSelf) return;//코팅 선택 중이면 작동 불가

        if (currentGold >= rerollCost)
        {
            currentGold -= rerollCost;
            if (diceManager?.ui != null) diceManager.ui.UpdateGoldUI(currentGold);
            RefreshShop(true);
        }
    }

    public void CloseShopAndGoNext()
    {
        // 코팅 선택 중이면 다음 스테이지 넘어가기 불가
        if (coatingSelectionPanel != null && coatingSelectionPanel.gameObject.activeSelf) return;

        IsShopOpen = false;
        if (shopUI != null) shopUI.SetActive(false);

        if (diceManager != null) diceManager.NextStage();
    }

    public bool PurchaseItem(BaseItemDataSO item)
    {
        // 코팅 선택 중이면 리롤 불가
        if (coatingSelectionPanel != null && coatingSelectionPanel.gameObject.activeSelf) return false;

        if (currentGold >= item.price)
        {
            if (item is FigureItemSO || item is SnackItemSO)
            {
                if (InventoryManager.Instance.AddItem(item))
                {
                    currentGold -= item.price;
                    if (diceManager?.ui != null) diceManager.ui.UpdateGoldUI(currentGold);
                    return true;
                }
                else
                {
                    Debug.Log("인벤토리가 꽉 차서 구매할 수 없습니다!");
                    return false;
                }
            }
            else
            {

                item.ApplyItemEffect(diceManager);

                currentGold -= item.price;
                if (diceManager?.ui != null) diceManager.ui.UpdateGoldUI(currentGold);
                return true;
            }
        }

        Debug.Log("골드가 부족합니다.");
        return false;
    }

    public void ShowTooltip(string desc, RectTransform slotRect)
    {
        descText.text = desc;
        tooltipPanel.SetActive(true);
        tooltipRect.pivot = new Vector2(0f, 0.5f);

        tooltipRect.position = slotRect.position;
        tooltipRect.localPosition += new Vector3(120f, -50f, 0f);
    }
    public void ShowTicketSelection()
    {
        if (ticketSelectionPanel != null)
        {
            // 선택창을 켭니다.
            ticketSelectionPanel.SetActive(true);

            // 3개의 버튼에 '클릭 시 닫히는 기능(OnTicketSelected)'을 연결
            foreach (Button btn in ticketChoiceButtons)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => OnTicketSelected(btn));
            }
        }
    }

    private void OnTicketSelected(Button selectedButton)
    {
        if (ticketSelectionPanel != null)
            ticketSelectionPanel.SetActive(false);
    }

    public void ShowCoatingSelection(DiceType type, float mult, Color color)
    {
        if (coatingSelectionPanel != null && diceManager != null)
        {
            coatingSelectionPanel.OpenSelection(diceManager, type, mult, color);
        }
        else
        {
            Debug.LogWarning("CoatingSelectionPanel 또는 DiceManager 연결이 누락되었습니다.");
        }
    }


    public void HideTooltip() => tooltipPanel.SetActive(false);
}