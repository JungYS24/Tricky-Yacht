using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems; 

//마우스 감지
public class TicketChoiceSlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public Image ticketIcon;
    public TextMeshProUGUI handNameText;
    public Button selectButton;

    private TicketItemSO currentTicketData;
    private ShopManager shopManager;

    public void Setup(TicketItemSO data, ShopManager manager)
    {
        currentTicketData = data;
        shopManager = manager;

        ticketIcon.sprite = data.icon;
        handNameText.text = data.itemName;

        selectButton.onClick.RemoveAllListeners();
        selectButton.onClick.AddListener(OnClicked);
    }

    private void OnClicked()
    {
        //티켓을 골라서 창이 닫힐 때 툴팁이 화면에 남아있는 버그 방지
        shopManager.HideTooltip();

        // 선택한 티켓의 효과 적용 (배수 상승)
        currentTicketData.ApplyItemEffect(shopManager.diceManager);

        // 선택창 닫기
        shopManager.CloseTicketSelection();
    }

    //마우스가 버튼 위에 올라왔을 때
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (currentTicketData != null && shopManager != null)
        {
            shopManager.ShowTooltip(currentTicketData.description, GetComponent<RectTransform>());
        }
    }

    //마우스가 버튼에서 빠져나갔을 때
    public void OnPointerExit(PointerEventData eventData)
    {
        if (shopManager != null)
        {
            // 툴팁 숨기기
            shopManager.HideTooltip();
        }
    }
}