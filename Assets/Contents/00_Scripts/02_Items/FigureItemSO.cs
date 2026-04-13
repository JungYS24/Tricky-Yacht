using UnityEngine;

[CreateAssetMenu(fileName = "NewFigure", menuName = "Shop/Items/Figure")]
public class FigureItemSO : BaseItemDataSO
{
    [Header("--- 피규어 전용 스펙 ---")]
    public bool isPermanent = true;
    public int requiredSlots = 1;

    public int stageClearGoldBonus = 0;
    public override void ApplyItemEffect(DiceManager diceManager)
    {
        Debug.Log($"{itemName} 피규어 배치! 슬롯 {requiredSlots} 소모");
    }
}