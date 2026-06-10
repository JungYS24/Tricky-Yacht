using UnityEngine;

[CreateAssetMenu(fileName = "NewDiceDestroyItem", menuName = "Shop/Items/DiceDestroy")]
public class DiceDestroyItemSO : BaseItemDataSO
{
    public override void ApplyItemEffect(DiceManager diceManager)
    {
        if (diceManager != null && diceManager.shopManager != null)
        {
            // 상점 매니저에게 주사위 파괴 UI를 열어달라고 요청
            diceManager.shopManager.ShowDiceDestructionSelection();
        }
    }
}