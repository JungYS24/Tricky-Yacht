using UnityEngine;

[CreateAssetMenu(fileName = "NewMaxHPItem", menuName = "Shop/Items/MaxHP")]
public class MaxHPItemSO : BaseItemDataSO
{
    [Header("체력 증가량")]
    public int hpIncreaseAmount = 10;

    public override void ApplyItemEffect(DiceManager diceManager)
    {
        if (diceManager != null)
        {
            // 최대 체력 증가
            diceManager.playerMaxHP += hpIncreaseAmount;
            //현재 체력도 증가
            diceManager.currentPlayerHP += hpIncreaseAmount;

            // 바뀐 체력을 화면 UI에 즉시 반영
            diceManager.ForceUpdateUI();

            Debug.Log($"최대 체력이 {hpIncreaseAmount} 증가했습니다! (현재 최대 체력: {diceManager.playerMaxHP})");
        }
    }
}