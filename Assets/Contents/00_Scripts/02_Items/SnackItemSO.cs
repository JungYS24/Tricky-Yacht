using UnityEngine;

[CreateAssetMenu(fileName = "NewSnack", menuName = "Shop/Items/Snack")]
public class SnackItemSO : BaseItemDataSO
{
    [Header("--- ½º³¼ Àü¿ë ½ºÆå ---")]
    public int instantBonusChips = 0;
    public int tempRerollAdd = 0;
    public bool ignoreDebuff = false;

    public override void ApplyItemEffect(DiceManager2 diceManager)
    {
        if (diceManager != null)
        {
            diceManager.maxRerolls += tempRerollAdd;
        }
        Debug.Log($"{itemName} ¼·Ãë! ¸®·Ñ {tempRerollAdd}È¸ Áõ°¡");
    }
}