using UnityEngine;

[CreateAssetMenu(fileName = "NewDiceItem", menuName = "Shop/Items/DiceItem")]
public class DiceItemSO : BaseItemDataSO
{
    [Header("주사위 구성 (6개 면)")]
    public int[] customFaces = new int[6] { 1, 2, 3, 4, 5, 6 };

    public override void ApplyItemEffect(DiceManager diceManager)
    {
        // 상점에서 구매 시 masterDeck에 새 주사위 추가
        DiceData1 newDice = new DiceData1(itemName, customFaces);
        diceManager.masterDeck.Add(newDice);

        Debug.Log($"{itemName}이(가) 덱에 추가되었습니다! 총 주사위 수: {diceManager.masterDeck.Count}");
    }
}