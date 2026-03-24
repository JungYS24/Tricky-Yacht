using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class ShopManager : MonoBehaviour
{
    [Header("참조 설정")]
    public DiceManager diceManager;
    public List<ShopItemDataSO> allItemsPool;
    public ShopSlot[] shopSlots;
    public GameObject shopUI; //상점 패널(SHOP)을 담는 변수

    [Header("설명창(Tooltip) UI")]
    public GameObject tooltipPanel;    // 설명창 부모 오브젝트
    public TextMeshProUGUI titleText;  // 아이템 이름 텍스트
    public TextMeshProUGUI descText;   // 아이템 설명 텍스트

    public int currentGold = 3000;

    public void OpenShop()
    {
        if (shopUI != null)
        {
            shopUI.SetActive(true); //  빈 매니저가 아니라, 연결된 'SHOP' 패널을 킴
        }
        RefreshShop();
    }

    public void RefreshShop()
    {
        List<ShopItemDataSO> shuffled = new List<ShopItemDataSO>(allItemsPool);
        for (int i = 0; i < shuffled.Count; i++)
        {
            int rnd = Random.Range(i, shuffled.Count);
            ShopItemDataSO temp = shuffled[i];
            shuffled[i] = shuffled[rnd];
            shuffled[rnd] = temp;
        }

        for (int i = 0; i < shopSlots.Length; i++)
        {
            if (i < shuffled.Count)
                shopSlots[i].SetupSlot(shuffled[i], this);
        }
    }

    // 설명창 띄우기 (슬롯 위치 기준으로 좌표 조정)
    public void ShowTooltip(string name, string desc, Vector3 slotPos)
    {
        tooltipPanel.SetActive(true);
        titleText.text = name;
        descText.text = desc;
        // 슬롯의 오른쪽 상단에 위치하도록 조정 (수치는 적절히 수정하세요)
        tooltipPanel.transform.position = slotPos + new Vector3(200f, 200f, 0f);
    }

    public void HideTooltip() => tooltipPanel.SetActive(false);

    public void PurchaseItem(ShopItemDataSO item)
    {
        if (currentGold >= item.price)
        {
            currentGold -= item.price;
            Debug.Log($"{item.itemName} 구매! 남은 골드: {currentGold}");
            // 실제 효과 적용 로직 추가 지점
        }
    }

    public void CloseShop()
    {
        gameObject.SetActive(false);
        if (diceManager != null) diceManager.NextStage();
    }
}