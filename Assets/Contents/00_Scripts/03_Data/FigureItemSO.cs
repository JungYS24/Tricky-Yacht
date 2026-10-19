using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewFigure", menuName = "Shop/Items/Figure")]
public class FigureItemSO : BaseItemDataSO
{
    [Header("--- 피규어 분류 (엑셀 기준) ---")]
    public FigureCategory category = FigureCategory.None;
    public int requiredSlots = 1;

    [Header("--- 도감(Collection) 정보 ---")]
    public List<BiomeType> sourceBiomes = new List<BiomeType> { BiomeType.Forest };

    [Header("--- 피규어 노드 데이터 ---")]
    public List<FigureNode> figureNodes = new List<FigureNode>();

    public override void ApplyItemEffect(DiceManager diceManager)
    {
        Debug.Log($"[{category}] {itemName} 피규어 인벤토리에 추가됨!");
    }
}