using UnityEngine;

// 상점 진열용 '티켓 봉투' 아이템
[CreateAssetMenu(fileName = "NewTicketPack", menuName = "Shop/Items/TicketPack")]
public class TicketPackSO : BaseItemDataSO
{
    public override void ApplyItemEffect(DiceManager diceManager)
    {
        if (diceManager != null && diceManager.shopManager != null)
        {
            // 상점에서 이걸 구매하면 배수를 올리지 않고, 티켓 선택 패널을 엽니다!
            diceManager.shopManager.ShowTicketSelection();
        }
    }
}