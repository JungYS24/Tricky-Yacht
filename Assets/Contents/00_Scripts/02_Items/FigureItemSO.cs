using UnityEngine;

// 피규어가 가질 수 있는 패시브 능력 종류
public enum FigureAbility { GoldBonus, RerollBonus, PlayBonus }

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

    // 스테이지 클리어 시 실행될 패시브 효과
    public void ApplyPassiveEffect(DiceManager diceManager, ShopManager shopManager)
    {
        switch (abilityType)
        {
            case FigureAbility.GoldBonus:
                // 골드는 리턴값으로 합산하기 위해 여기선 로그만 남기거나 
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