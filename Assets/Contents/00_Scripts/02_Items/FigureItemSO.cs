using UnityEngine;

// [수정] 새로운 4가지 피규어 패시브 능력 추가
public enum FigureAbility
{
    GoldBonus, RerollBonus, PlayBonus,
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
    public int abilityValue; // 수치 (골드 +2, 리롤 +1 등)

    public override void ApplyItemEffect(DiceManager diceManager)
    {
        Debug.Log($"{itemName} 피규어 배치! 슬롯 {requiredSlots} 소모");
    }

    public void ApplyPassiveEffect(DiceManager diceManager, ShopManager shopManager)
    {
        switch (abilityType)
        {
            case FigureAbility.GoldBonus:
                // 즉시 추가가 필요하다면 shopManager.currentGold += abilityValue; 실행
                break;
            case FigureAbility.RerollBonus:
                diceManager.maxRerolls += abilityValue;
                break;
            case FigureAbility.PlayBonus:
                diceManager.maxPlays += abilityValue;
                break;
        }
    }
}