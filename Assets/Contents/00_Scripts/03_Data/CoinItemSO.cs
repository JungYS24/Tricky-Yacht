using UnityEngine;

[CreateAssetMenu(fileName = "NewCoinItem", menuName = "Shop/Items/CoinItem")]
public class CoinItemSO : BaseItemDataSO
{
    [Header("지급할 골드량")]
    public int goldAmount = 300;

    public override void ApplyItemEffect(DiceManager diceManager)
    {
        if (diceManager != null && diceManager.shopManager != null)
        {
            // 골드 추가: 에메랄드 보너스, UI 및 카운팅 연출까지 함께 처리
            int grantedGold = diceManager.shopManager.GrantGold(goldAmount);

            Debug.Log($"코인 전리품 획득! {grantedGold} G 추가됨.");
        }
    }
}