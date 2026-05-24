using UnityEngine;

// PlayBonus 삭제됨
public enum FigureAbility
{
    GoldBonus, RerollBonus,
    PrismDamageBonus,     // 유니콘: 프리즘 코팅 개수 비례 대미지 배수 추가
    CherryChipBonus,      // 달마: 소모한 체리 개수 비례 칩 추가
    YachtGoldBonus,       // 복고양이: 요트(파이브 카드) 달성 시 코인 획득
    ThreeDiceRerollBonus  // 클락판다: 3눈금이 3개 이상일 때 리롤 +1회
}

[CreateAssetMenu(fileName = "NewFigure", menuName = "Shop/Items/Figure")]
public class FigureItemSO : BaseItemDataSO
{
    [Header("--- 피규어 전용 스펙 ---")]
    public bool isPermanent = true;
    public int requiredSlots = 1;

    [Header("--- 피규어 패시브 능력 ---")]
    public FigureAbility abilityType;
    public int abilityValue;

    public override void ApplyItemEffect(DiceManager diceManager)
    {
        Debug.Log($"{itemName} 피규어 배치! 슬롯 {requiredSlots} 소모");
    }

    public void ApplyPassiveEffect(DiceManager diceManager, ShopManager shopManager)
    {
        switch (abilityType)
        {
            case FigureAbility.GoldBonus:
                break;
            case FigureAbility.RerollBonus:
                diceManager.maxRerolls += abilityValue;
                break;
        }
    }
}