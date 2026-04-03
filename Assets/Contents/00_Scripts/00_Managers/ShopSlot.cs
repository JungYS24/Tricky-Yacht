using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class ShopSlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public Image itemIcon;
    public TextMeshProUGUI priceText;
    public Button buyButton;

    private BaseItemDataSO currentData;
    private ShopManager manager;

    public bool isPurchased = false;


    public void SetupSlot(BaseItemDataSO data, ShopManager shopMgr)
    {
        currentData = data;
        manager = shopMgr;

 
        isPurchased = false;
        if (itemIcon != null)
        {
            itemIcon.sprite = data.icon;
            itemIcon.color = Color.white;
        }
        if (priceText != null) priceText.text = data.price + " G";

        buyButton.interactable = true; 
        buyButton.onClick.RemoveAllListeners();
        buyButton.onClick.AddListener(TryPurchase); 
    }

    // 버튼 클릭 시 작동하는 구매 로직
    private void TryPurchase()
    {
        if (isPurchased) return;


        if (manager.PurchaseItem(currentData))
        {
            isPurchased = true; 

            if (itemIcon != null) itemIcon.color = new Color(0.3f, 0.3f, 0.3f, 1f);

            buyButton.interactable = false; 
            if (priceText != null) priceText.text = "Sold Out"; 

            manager.HideTooltip(); 
        }
    }


    public void OnPointerEnter(PointerEventData eventData)
    {

        if (currentData != null && !isPurchased)
        {
            manager.ShowTooltip(currentData.description, GetComponent<RectTransform>());
        }
    }


    public void OnPointerExit(PointerEventData eventData)
    {
        manager.HideTooltip();
    }
}