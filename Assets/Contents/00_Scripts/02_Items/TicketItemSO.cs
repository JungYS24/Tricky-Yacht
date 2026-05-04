using UnityEngine;

// 족보 종류를 선택할 수 있게 만들어줍니다.
public enum HandType
{
    HighCard, OnePair, TwoPair, Triple, FullHouse, FourOfAKind, Straight, Yacht
}

[CreateAssetMenu(fileName = "NewTicketItem", menuName = "Shop/Items/TicketItem")]
public class TicketItemSO : BaseItemDataSO
{
    [Header("업그레이드할 족보")]
    public HandType targetHand;

    [Header("배수 증가량 (기본 1.1배)")]
    public float upgradeMultiplier = 1.1f; // 기존 배수에 곱해집니다.

    public override void ApplyItemEffect(DiceManager diceManager)
    {
        if (diceManager != null)
        {
            // DiceManager에 족보와 증가량을 전달하여 업그레이드 시킵니다.
            diceManager.UpgradeHand(targetHand, upgradeMultiplier);
            Debug.Log($"티켓 적용: {targetHand}의 배수가 {upgradeMultiplier}배 증가했습니다!");
        }
    }
}