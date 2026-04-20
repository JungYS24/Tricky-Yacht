using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

// 마우스 오버 이벤트를 받기 위해 IPointerEnterHandler, IPointerExitHandler 인터페이스 추가
public class InventorySlot : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
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
        if (isEmpty) return;

        // 좌클릭: 스낵 먹기
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            if (!ShopManager.IsShopOpen && currentItem is SnackItemSO snack)
            {
                // 현재 먹으려는 스낵이 페퍼민트인데, 이미 DiceManager에서 효과가 활성 상태라면
                if (snack.snackType == SnackType.Peppermint && manager.diceManager.isPeppermintActive)
                {
                    Debug.Log("이미 페퍼민트 효과가 활성화되어 있어 다시 먹을 수 없습니다!");
                    return; // 여기서 함수를 종료하면 아래의 snack.ApplyItemEffect와 ClearSlot이 실행되지 않습니다.
                }

                snack.ApplyItemEffect(manager.diceManager);
                ClearSlot(); // 효과가 적용된 후에만 슬롯을 비웁니다.
                manager.HideSellPopup();
                manager.HideTooltip(); // 아이템을 먹어서 사라졌으니 툴팁도 닫아줍니다.
            }
        }
        // 우클릭: 피규어 판매 팝업 띄우기
        else if (eventData.button == PointerEventData.InputButton.Right)
        {
            if (currentItem is FigureItemSO)
            {
                manager.ShowSellPopup(this);
            }
        }
    }
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!isEmpty && currentItem != null)
        {
            // 인벤토리 매니저에게 내 위치(RectTransform)와 설명을 전달하여 툴팁 띄우기
            manager.ShowTooltip(currentItem.description, GetComponent<RectTransform>());
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        manager.HideTooltip();
    }
}