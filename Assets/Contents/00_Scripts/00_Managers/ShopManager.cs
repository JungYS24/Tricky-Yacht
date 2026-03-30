using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;

public class ShopManager : MonoBehaviour
{
    [Header("참조 설정")]
    public DiceManager diceManager;
    public List<ShopItemDataSO> allItemsPool;
    public ShopSlot[] shopSlots;
    public GameObject shopUI; // 상점 패널(SHOP)을 담는 변수

    [Header("설명창(Tooltip) UI")]
    public GameObject tooltipPanel;    // 설명창 부모 오브젝트
    public RectTransform tooltipRect;  // 툴팁의 위치 조정을 위한 RectTransform
    public TextMeshProUGUI descText;   // 아이템 설명 텍스트

    public int currentGold = 3000;

    private bool isTooltipActive = false; // 툴팁 활성화 상태

    private void Awake()
    {
        // 인스펙터에서 연결 안 했을 경우를 대비해 자동으로 가져오기
        if (tooltipRect == null && tooltipPanel != null)
            tooltipRect = tooltipPanel.GetComponent<RectTransform>();

        HideTooltip(); // 시작할 때 무조건 끄기
    }


    private void Start()
    {
        if (diceManager != null && diceManager.ui != null)
        {
            diceManager.ui.UpdateGoldUI(currentGold);
        }
    }

    private void Update()
    {
        
    }



    public void OpenShop()
    {
        if (shopUI != null) shopUI.SetActive(true);

        // 상점 열릴 때 현재 골드 표시 업데이트
        if (diceManager != null && diceManager.ui != null)
        {
            diceManager.ui.UpdateGoldUI(currentGold);
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

    // 매개변수에서 위치값(slotPos) 제거함
    public void ShowTooltip(string desc, RectTransform slotRect)
    {
        descText.text = desc;
        tooltipPanel.SetActive(true);

        // 툴팁의 피벗(기준점)을 왼쪽 중앙으로 설정함.
        tooltipRect.pivot = new Vector2(0f, 0.5f);

        // 툴팁의 위치를 현재 마우스가 올라간 슬롯의 중앙 위치로 1차로 맞춤
        tooltipRect.position = slotRect.position;

        // 슬롯 위치에서 오른쪽(X)과 아래쪽(Y)으로 살짝 밀어서 골드 텍스트 옆에 오게 만듬
        //  120f 와 -50f 숫자를 유니티에서 플레이해보며 입맛에 맞게 조절
        tooltipRect.localPosition += new Vector3(120f, -50f, 0f);
    }
    public void HideTooltip()
    {
        tooltipPanel.SetActive(false);
        isTooltipActive = false;
    }

    public void PurchaseItem(ShopItemDataSO item)
    {
        if (currentGold >= item.price)
        {
            currentGold -= item.price;
            //Debug.Log($"{item.itemName} 구매! 남은 골드: {currentGold}");
            if (diceManager != null && diceManager.ui != null)
            {
                diceManager.ui.UpdateGoldUI(currentGold);
            }

            // TODO: 실제 효과 적용 로직 추가 지점
        }
    }

    public void CloseShop()
    {
        if (shopUI != null) shopUI.SetActive(false);
        if (diceManager != null) diceManager.NextStage();
    }

    

}