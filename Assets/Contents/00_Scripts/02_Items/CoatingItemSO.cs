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
        if (diceManager != null)
        {
            // DiceManager에게 유형, 배율, 색상을 모두 넘겨줌
            diceManager.ApplyRandomCoating(coatingType, scoreMultiplier, coatingColor);
            Debug.Log($"{itemName} 코팅 적용 완료! 유형: {coatingType}, 색상: {coatingColor}");
        }
    }
}