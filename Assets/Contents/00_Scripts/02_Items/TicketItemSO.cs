using UnityEngine;

[CreateAssetMenu(fileName = "NewTicketItem", menuName = "Shop/Items/TicketItem")]
public class TicketItemSO : BaseItemDataSO
{
    public override void ApplyItemEffect(DiceManager diceManager)
    {
        if (diceManager.shopManager != null)
        {
            diceManager.shopManager.ShowTicketSelection();
        }
    }
}