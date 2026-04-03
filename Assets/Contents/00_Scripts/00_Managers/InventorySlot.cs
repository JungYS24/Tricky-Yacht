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
        if (ShopManager.IsShopOpen || isEmpty) return;

        if (currentItem is SnackItemSO snack)
        {
            snack.ApplyItemEffect(manager.diceManager);
            ClearSlot();
        }
    }
}