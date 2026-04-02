using UnityEngine;

[CreateAssetMenu(fileName = "NewCoating", menuName = "Shop/Items/Coating")]
public class CoatingItemSO : BaseItemDataSO
{
    [Header("--- 코팅 전용 스펙 ---")]
    public float scoreMultiplier = 1f;
    public int bonusCoin = 0;
    public float targetScoreReduce = 0f;

    public override void ApplyItemEffect(DiceManager diceManager)
    {
        Debug.Log($"{itemName} 코팅 적용! 배수 {scoreMultiplier} 증가");
    }
}