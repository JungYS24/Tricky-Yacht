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
            // 골드 추가
            diceManager.shopManager.currentGold += goldAmount;

            // UI 업데이트
            if (diceManager.ui != null)
            {
                diceManager.ui.UpdateGoldUI(diceManager.shopManager.currentGold);
            }

            // 카운팅 연출 (GoldCounter가 존재한다면 실행)
            if (GoldCounter.Instance != null)
            {
                GoldCounter.Instance.SetGold(diceManager.shopManager.currentGold);
            }

            Debug.Log($"코인 전리품 획득! {goldAmount} G 추가됨.");
        }
    }
}