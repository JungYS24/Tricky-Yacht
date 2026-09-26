using UnityEngine;

[CreateAssetMenu(fileName = "NewStamp", menuName = "Shop/Items/Stamp")]
public class StampItemSO : BaseItemDataSO
{
    [SerializeField] private StampType type;

    [SerializeField] private Sprite sealImage;

    public override void ApplyItemEffect(DiceManager diceManager)
    {
        Debug.LogException(new System.NotImplementedException());
    }
}
