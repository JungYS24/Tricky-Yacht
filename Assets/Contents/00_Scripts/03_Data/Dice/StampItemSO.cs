using UnityEngine;

[CreateAssetMenu(fileName = "NewStamp", menuName = "Shop/Items/Stamp")]
public class StampItemSO : BaseItemDataSO
{
    [SerializeField] private StampType type;

    // 전리품 보상(Stamp_Random)이 무작위 스탬프의 아이콘을 찾을 때 쓴다.
    public StampType StampKind => type;

    public override void ApplyItemEffect(DiceManager diceManager)
    {
        if (diceManager != null && diceManager.shopManager != null)
        {
            diceManager.shopManager.ShowStampSelection(type, icon);
        }
    }
}
