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
    // 0보다 크면 티켓 SO의 배수 대신 이 배수를 쓴다(전리품 황금 티켓).
    private float multiplierOverride;

    public void Setup(TicketItemSO data, ShopManager manager)
    {
        Setup(data, manager, 0f);
    }

    public void Setup(TicketItemSO data, ShopManager manager, float upgradeMultiplierOverride)
    {
        currentTicketData = data;
        shopManager = manager;
        multiplierOverride = upgradeMultiplierOverride;

        ticketIcon.sprite = data.icon;
        handNameText.text = LocalizationManager.GetItemDisplayName(data);

        selectButton.onClick.RemoveAllListeners();
        selectButton.onClick.AddListener(OnClicked);
    }

    private void OnClicked()
    {
        //티켓을 골라서 창이 닫힐 때 툴팁이 화면에 남아있는 버그 방지
        shopManager.HideTooltip();

        // 선택한 티켓의 효과 적용 (배수 상승)
        if (multiplierOverride > 0f && shopManager.diceManager != null)
            shopManager.diceManager.UpgradeHand(currentTicketData.targetHand, multiplierOverride);
        else
            currentTicketData.ApplyItemEffect(shopManager.diceManager);

        //보유 티켓에 기록 (도감 티켓 탭 숫자가 올라감)
        InventoryManager.Instance.AddItem(currentTicketData);

        // 고른 티켓이 도감 버튼으로 날아가는 연출
        TicketFlyFeedback.Play(currentTicketData, ticketIcon.rectTransform);

        // 선택창 닫기
        shopManager.CloseTicketSelection();
    }

    //마우스가 버튼 위에 올라왔을 때
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (currentTicketData != null && shopManager != null)
        {
            string desc = LocalizationManager.GetItemDescription(currentTicketData);
            if (multiplierOverride > 0f)
                desc = LocalizationManager.GetSys("SYS_TICKET_CHOICE_DESC", "이 족보의 배수가 영구적으로 ×{0} 된다.", multiplierOverride.ToString("0.##"));
            shopManager.ShowTooltip(desc, GetComponent<RectTransform>());
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