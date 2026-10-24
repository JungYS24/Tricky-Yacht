using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 티켓 도감 왼쪽 목록 한 줄 (시너지 목록과 같은 줄 판): 티켓 이미지, 이미지 오른쪽 아래 숫자(기본 1, 티켓 1장마다 +1), 현재 배수 설명
public class TicketBookSlot : MonoBehaviour
{
    public Image background;
    public Button button;
    public Image ticketIcon;
    public TextMeshProUGUI levelText;
    public TextMeshProUGUI descText;

    [Header("줄 판 (시너지 목록과 같은 이미지)")]
    public Sprite normalSprite;
    public Sprite selectedSprite;

    [Header("글자 색 (티켓을 1장 이상 먹으면 숫자와 배수 수치만 강화 색으로 바뀜)")]
    public Color normalLevelColor = new Color(0.29f, 0.18f, 0.1f, 1f);
    public Color normalDescColor = new Color(0.29f, 0.18f, 0.1f, 1f);
    public Color upgradedColor = new Color(0.78f, 0.47f, 0f, 1f);

    public TicketItemSO Ticket { get; private set; }

    public void Setup(TicketItemSO ticket, int level, float multiplier, Action<TicketItemSO> onClick)
    {
        Ticket = ticket;
        bool upgraded = level > 1;

        ticketIcon.sprite = ticket.icon;

        levelText.text = level.ToString();
        levelText.color = upgraded ? upgradedColor : normalLevelColor;

        string multText = FormatMultiplier(multiplier);
        if (upgraded) multText = Paint(multText, upgradedColor);
        descText.text = LocalizationManager.GetUi("UI_TICKET_BOOK_DESC", "{0} 달성 시 {1}배", HandName(ticket.targetHand), multText);
        descText.color = normalDescColor;

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            if (onClick != null) button.onClick.AddListener(() => onClick(ticket));
        }
    }

    public void SetSelected(bool selected)
    {
        if (background != null && normalSprite != null)
            background.sprite = selected && selectedSprite != null ? selectedSprite : normalSprite;
    }

    public static string HandName(HandType handType)
    {
        return LocalizationManager.GetHandDisplayName(ToHandRank(handType));
    }

    public static string FormatMultiplier(float multiplier)
    {
        return multiplier.ToString("0.0#", CultureInfo.InvariantCulture);
    }

    // 문장 중 수치만 색을 바꾼다 (TMP 리치 텍스트)
    public static string Paint(string text, Color color)
    {
        return $"<color=#{ColorUtility.ToHtmlStringRGBA(color)}>{text}</color>";
    }

    public static HandRank ToHandRank(HandType handType)
    {
        switch (handType)
        {
            case HandType.OnePair: return HandRank.OnePair;
            case HandType.TwoPair: return HandRank.TwoPair;
            case HandType.Triple: return HandRank.Triple;
            case HandType.FullHouse: return HandRank.FullHouse;
            case HandType.FourOfAKind: return HandRank.FourOfAKind;
            case HandType.Straight: return HandRank.Straight;
            case HandType.Yacht: return HandRank.Yacht;
            default: return HandRank.HighCard;
        }
    }
}
