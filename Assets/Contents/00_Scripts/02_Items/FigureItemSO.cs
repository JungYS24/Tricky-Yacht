using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewFigure", menuName = "Shop/Items/Figure")]
public class FigureItemSO : BaseItemDataSO
{
    [Header("--- 피규어 전용 스펙 ---")]
    public bool isPermanent = true;
    public int requiredSlots = 1;

    [Header("--- 피규어 노드 데이터 ---")]
    // 에디터에서 원인과 보상을 자유롭게 조립할 수 있는 리스트[cite: 1]
    public List<FigureNode> figureNodes = new List<FigureNode>();

    public override void ApplyItemEffect(DiceManager diceManager)
    {
        Debug.Log($"{itemName} 피규어 획득! 슬롯 {requiredSlots} 소모");
    }
}