using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;
using TMPro;

// 도감 "티켓" 탭. 왼쪽 페이지에 티켓 8장 목록(시너지 목록과 같은 줄), 오른쪽 페이지에 고른 티켓 상세.
// 탭을 열면 방금 얻은 티켓이 선택된다(얻은 티켓이 없으면 맨 위 티켓).
public class TicketBookPage : MonoBehaviour
{
    [Header("티켓 순서 (위에서부터)")]
    public List<TicketItemSO> tickets = new List<TicketItemSO>();

    [Header("왼쪽 목록")]
    public TicketBookSlot slotPrefab;
    public Transform listRoot;
    public TextMeshProUGUI titleText;

    [Header("오른쪽 상세")]
    public Image detailIcon;
    public TextMeshProUGUI detailLevelText;
    public TextMeshProUGUI detailName;
    [FormerlySerializedAs("currentText")] public TextMeshProUGUI multiplierText; // 배수 1.32배 → 1.45배 (다음 강화)
    public TextMeshProUGUI exampleLabel;
    public Image[] exampleDice = new Image[5];
    public TextMeshProUGUI howToText;

    [Header("족보 예시 주사위 (눈 1~6 이미지, 족보에 쓰인 주사위 / 남는 주사위 색)")]
    public Sprite[] diceFaceSprites = new Sprite[6];
    public Color handDiceColor = Color.white;
    public Color restDiceColor = new Color(0.7f, 0.7f, 0.7f, 0.45f);

    [Header("글자 색 (티켓을 1장 이상 먹으면 숫자와 현재 배수 수치만 강화 색으로 바뀜)")]
    public Color normalColor = new Color(0.29f, 0.18f, 0.1f, 1f);
    public Color upgradedColor = new Color(0.78f, 0.47f, 0f, 1f);

    private readonly List<TicketBookSlot> slots = new List<TicketBookSlot>();

    public void Refresh()
    {
        EnsureSlots();

        for (int i = 0; i < slots.Count; i++)
        {
            TicketItemSO ticket = tickets[i];
            slots[i].gameObject.SetActive(ticket != null);
            if (ticket == null) continue;
            slots[i].Setup(ticket, GetLevel(ticket), GetMultiplier(ticket), Select);
        }

        if (titleText != null)
            titleText.text = LocalizationManager.GetUi("UI_TICKET_BOOK_TITLE", "티켓 도감");

        Select(GetDefaultTicket());
    }

    public void Select(TicketItemSO ticket)
    {
        foreach (var slot in slots)
            slot.SetSelected(slot.Ticket != null && slot.Ticket == ticket);
        ShowDetail(ticket);
    }

    private void EnsureSlots()
    {
        if (slotPrefab == null || listRoot == null) return;

        while (slots.Count < tickets.Count)
            slots.Add(Instantiate(slotPrefab, listRoot));
    }

    // 방금 얻은 티켓(보유 목록의 마지막) → 없으면 맨 위 티켓
    private TicketItemSO GetDefaultTicket()
    {
        InventoryManager inventory = InventoryManager.Instance;
        if (inventory != null)
        {
            for (int i = inventory.ownedTickets.Count - 1; i >= 0; i--)
            {
                TicketItemSO owned = inventory.ownedTickets[i];
                if (owned == null) continue;
                TicketItemSO entry = tickets.Find(t => t != null && t.targetHand == owned.targetHand);
                if (entry != null) return entry;
            }
        }
        return tickets.Find(t => t != null);
    }

    private void ShowDetail(TicketItemSO ticket)
    {
        if (ticket == null) return;

        int level = GetLevel(ticket);
        float multiplier = GetMultiplier(ticket);
        bool upgraded = level > 1;

        if (detailIcon != null) detailIcon.sprite = ticket.icon;
        if (detailLevelText != null)
        {
            detailLevelText.text = level.ToString();
            detailLevelText.color = upgraded ? upgradedColor : normalColor;
        }
        if (detailName != null) detailName.text = LocalizationManager.GetItemDisplayName(ticket);
        if (multiplierText != null)
        {
            string current = TicketBookSlot.FormatMultiplier(multiplier);
            if (upgraded) current = TicketBookSlot.Paint(current, upgradedColor);
            multiplierText.text = LocalizationManager.GetUi("UI_TICKET_BOOK_MULT", "배수 {0}배 → {1}배 (다음 강화)",
                current, TicketBookSlot.FormatMultiplier(multiplier * ticket.upgradeMultiplier));
            multiplierText.color = normalColor;
        }
        if (exampleLabel != null) exampleLabel.text = LocalizationManager.GetUi("UI_TICKET_BOOK_EXAMPLE", "족보 예시");
        if (howToText != null)
            howToText.text = LocalizationManager.GetUi("UI_TICKET_BOOK_HOWTO", "티켓팩을 열면 티켓 3장 중 1장을 골라 얻습니다.");

        ShowExampleDice(ticket);
    }

    // 족보에 쓰인 주사위는 그대로, 남는 주사위는 흐리게 (결산 강조와 같은 기준). 하이 카드처럼 쓰인 주사위가 없으면 전부 그대로.
    private void ShowExampleDice(TicketItemSO ticket)
    {
        int[] values = ticket.exampleDice;
        bool hasExample = values != null && values.Length == exampleDice.Length;
        HandRank rank = TicketBookSlot.ToHandRank(ticket.targetHand);

        bool anyUsed = false;
        for (int i = 0; hasExample && i < values.Length; i++)
            anyUsed |= HandRankUtil.IsUsedInHand(rank, values, i);

        for (int i = 0; i < exampleDice.Length; i++)
        {
            Image die = exampleDice[i];
            if (die == null) continue;

            int value = hasExample ? values[i] : 0;
            bool show = value >= 1 && value <= diceFaceSprites.Length && diceFaceSprites[value - 1] != null;
            die.gameObject.SetActive(show);
            if (!show) continue;

            die.sprite = diceFaceSprites[value - 1];
            die.color = !anyUsed || HandRankUtil.IsUsedInHand(rank, values, i) ? handDiceColor : restDiceColor;
        }

        if (exampleLabel != null) exampleLabel.gameObject.SetActive(hasExample);
    }

    private static int GetLevel(TicketItemSO ticket)
    {
        InventoryManager inventory = InventoryManager.Instance;
        return (inventory != null ? inventory.GetTicketCount(ticket.targetHand) : 0) + 1;
    }

    private static float GetMultiplier(TicketItemSO ticket)
    {
        DiceManager diceManager = DiceManager.Instance;
        return diceManager != null ? diceManager.GetHandMultiplier(ticket.targetHand) : 1f;
    }
}
