using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class TicketDetailPanel : MonoBehaviour
{
    public static bool IsPanelOpen { get; private set; } = false;

    public GameObject panelRoot;
    public Image ticketIcon;
    public TextMeshProUGUI ticketNameText;
    public TextMeshProUGUI ticketDescText;

    [Header("버튼 연결")]
    public Button closeButton;
    public Button backgroundCloseButton;
    public Button leftButton;  
    public Button rightButton; 

    private List<TicketItemSO> currentTicketList;
    private int currentIndex = 0;

    private void Awake()
    {
        if (closeButton != null) closeButton.onClick.AddListener(ClosePanel);
        if (backgroundCloseButton != null) backgroundCloseButton.onClick.AddListener(ClosePanel);

        // 좌우 화살표 버튼 이벤트 연결
        if (leftButton != null) leftButton.onClick.AddListener(ShowPrevious);
        if (rightButton != null) rightButton.onClick.AddListener(ShowNext);

        IsPanelOpen = false;
    }
    public void OpenPanel(List<TicketItemSO> tickets, TicketItemSO initialTicket)
    {
        if (tickets == null || tickets.Count == 0 || initialTicket == null) return;

        currentTicketList = tickets;
        currentIndex = currentTicketList.IndexOf(initialTicket);

        UpdateTicketDisplay();

        IsPanelOpen = true;
        panelRoot.SetActive(true);
    }

    private void UpdateTicketDisplay()
    {
        if (currentIndex < 0 || currentIndex >= currentTicketList.Count) return;

        TicketItemSO ticket = currentTicketList[currentIndex];

        ticketIcon.sprite = ticket.icon;
        ticketNameText.text = ticket.itemName;
        ticketDescText.text = ticket.description;

        if (leftButton != null)
        {
            leftButton.gameObject.SetActive(true); 
            leftButton.interactable = (currentIndex > 0);
        }
        if (rightButton != null)
        {
            rightButton.gameObject.SetActive(true); 
            rightButton.interactable = (currentIndex < currentTicketList.Count - 1); 
        }
    }

    private void ShowPrevious()
    {
        if (currentIndex > 0)
        {
            currentIndex--;
            UpdateTicketDisplay();
        }
    }

    private void ShowNext()
    {
        if (currentIndex < currentTicketList.Count - 1)
        {
            currentIndex++;
            UpdateTicketDisplay();
        }
    }

    public void ClosePanel()
    {
        IsPanelOpen = false;
        panelRoot.SetActive(false);
    }
}