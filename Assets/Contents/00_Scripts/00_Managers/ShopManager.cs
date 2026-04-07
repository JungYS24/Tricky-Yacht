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
        if (currentGold >= rerollCost)
        {
            currentGold -= rerollCost;
            if (diceManager?.ui != null) diceManager.ui.UpdateGoldUI(currentGold);
            RefreshShop(true);
        }
    }

    public void CloseShopAndGoNext()
    {
        IsShopOpen = false;
        if (shopUI != null) shopUI.SetActive(false);

        if (diceManager != null) diceManager.NextStage();
    }

    public bool PurchaseItem(BaseItemDataSO item)
    {
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

    public void HideTooltip() => tooltipPanel.SetActive(false);
}