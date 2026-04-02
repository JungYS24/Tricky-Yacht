using UnityEngine;

[CreateAssetMenu(fileName = "NewShaker", menuName = "Shop/Items/Shaker")]
public class ShakerItemSO : BaseItemDataSO
{
    [Header("--- ½¦ÀÌÄ¿ Àü¿ë ½ºÆå ---")]
    public ItemGrade grade;
    public ShakerClass shakerClass;

    public override void ApplyItemEffect(DiceManager diceManager)
    {
        Debug.Log($"{itemName} ÀåÂø! ½¦ÀÌÄ¿ Å¬·¡½º: {shakerClass}");
    }
}