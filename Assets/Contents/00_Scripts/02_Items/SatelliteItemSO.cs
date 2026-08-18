using UnityEngine;

[CreateAssetMenu(fileName = "NewSatellite", menuName = "Shop/Items/Satellite")]
public class SatelliteItemSO : BaseItemDataSO
{
    [Header("--- 위성 전용 스펙 ---")]
    public SatelliteType satelliteType = SatelliteType.Mercury;

    public override void ApplyItemEffect(DiceManager diceManager)
    {
        if (diceManager != null && diceManager.shopManager != null)
        {
            // ShopManager에게 위성 선택 UI를 열어달라고 요청
            diceManager.shopManager.ShowSatelliteSelection(satelliteType);
        }
    }
}