using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems; // 마우스 진입/퇴장 감지용
using TMPro;

public class ShopSlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public Image itemIcon;
    public TextMeshProUGUI priceText;
    public Button buyButton;

    private ShopItemDataSO currentData;
    private ShopManager manager;

    // 매니저가 슬롯을 초기화할 때 호출
    public void SetupSlot(ShopItemDataSO data, ShopManager shopMgr)
    {
        currentData = data;
        manager = shopMgr;

        if (itemIcon != null) itemIcon.sprite = data.icon;
        if (priceText != null) priceText.text = data.price + " G";

        // 버튼 클릭 이벤트 연결
        buyButton.onClick.RemoveAllListeners();
        buyButton.onClick.AddListener(() => manager.PurchaseItem(currentData));
    }

    // 마우스를 올렸을 때 (설명창 켜기)
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (currentData != null)
            manager.ShowTooltip(currentData.itemName, currentData.description, transform.position);
    }

    // 마우스를 치웠을 때 (설명창 끄기)
    public void OnPointerExit(PointerEventData eventData)
    {
        manager.HideTooltip();
    }
}