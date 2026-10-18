using UnityEngine;

[CreateAssetMenu(fileName = "NewStamp", menuName = "Shop/Items/Stamp")]
public class StampItemSO : BaseItemDataSO
{
    [SerializeField] private StampType type;

    public override void ApplyItemEffect(DiceManager diceManager)
    {
        if (diceManager != null && diceManager.shopManager != null)
        {
            diceManager.shopManager.ShowStampSelection(type, icon);
        }
    }
}
