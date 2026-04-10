using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class InventorySlot : MonoBehaviour, IPointerClickHandler
{
    public Image itemIcon;
    public bool isEmpty = true;
    public BaseItemDataSO currentItem;

    private InventoryManager manager;

    public void Initialize(InventoryManager invManager)
    {
        manager = invManager;
        ClearSlot();
    }

    public void SetItem(BaseItemDataSO item)
    {
        currentItem = item;
        isEmpty = false;
        itemIcon.sprite = item.icon;
        itemIcon.color = Color.white;
        itemIcon.gameObject.SetActive(true);
    }

    public void ClearSlot()
    {
        currentItem = null;
        isEmpty = true;
        itemIcon.sprite = null;
        itemIcon.gameObject.SetActive(false);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (isEmpty) return; // 빈 슬롯이면 무시

        // 좌클릭: 스낵 먹기
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            if (!ShopManager.IsShopOpen && currentItem is SnackItemSO snack)
            {
                snack.ApplyItemEffect(manager.diceManager);
                ClearSlot();
                manager.HideSellPopup(); // 무언가 사용하면 팝업 닫기
            }
        }
        //  우클릭: 피규어 판매 팝업 띄우기
        else if (eventData.button == PointerEventData.InputButton.Right)
        {
            if (currentItem is FigureItemSO) // 피규어만 판매 가능하게
            {
                manager.ShowSellPopup(this);
            }
        }
    }
}