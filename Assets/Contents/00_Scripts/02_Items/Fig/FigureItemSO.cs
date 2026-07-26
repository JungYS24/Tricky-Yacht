using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewFigure", menuName = "Shop/Items/Figure")]
public class FigureItemSO : BaseItemDataSO
{
    [Header("--- 피규어 고유 사양 ---")]
    public bool isPermanent = true;
    public int requiredSlots = 1;

    [Header("--- 도감(Collection) 정보 ---")]
    public BiomeType sourceBiome = BiomeType.Forest;
    public string acquisitionLocation = "숲 바이옴에서 획득 가능";

    [Header("--- 피규어 노드 데이터 ---")]
    public List<FigureNode> figureNodes = new List<FigureNode>();

    public override void ApplyItemEffect(DiceManager diceManager)
    {
        Debug.Log($"{itemName} 피규어 획득! 슬롯 {requiredSlots} 소모");
    }
}