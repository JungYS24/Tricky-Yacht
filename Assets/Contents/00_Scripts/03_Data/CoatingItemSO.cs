using UnityEngine;

[CreateAssetMenu(fileName = "NewCoating", menuName = "Shop/Items/Coating")]
public class CoatingItemSO : BaseItemDataSO
{
    [Header("--- 코팅 전용 스펙 ---")]
    public DiceType coatingType = DiceType.Prism;
    public float scoreMultiplier = 1.0f;

    [Header("--- 코팅 시 씌워질 색상 ---")]
    public Color coatingColor = Color.yellow;

    public override void ApplyItemEffect(DiceManager diceManager)
    {
        if (diceManager != null && diceManager.shopManager != null)
        {
            // ShopManager에게 선택 UI를 열어달라고 요청
            diceManager.shopManager.ShowCoatingSelection(coatingType, scoreMultiplier, coatingColor);
        }
    }
}