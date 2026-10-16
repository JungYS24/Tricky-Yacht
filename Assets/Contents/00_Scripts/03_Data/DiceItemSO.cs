using UnityEngine;

[CreateAssetMenu(fileName = "NewDiceItem", menuName = "Shop/Items/DiceItem")]
public class DiceItemSO : BaseItemDataSO
{
    [Header("주사위 구성 (6개 면)")]
    public int[] customFaces = new int[6] { 1, 2, 3, 4, 5, 6 };

    [Header("특수 주사위 능력 설정")]
    public SpecialDieEffect specialEffect = SpecialDieEffect.None;

    [Header("디버프 주사위 종류")]
    [Tooltip("None: 일반 / Fake·Spine·Curse: 영구 / Thief·Shackle: 전투 한정, 뽑는 즉시 발동")]
    public DebuffDiceType debuffType = DebuffDiceType.None;

    [Header("면별 효과 (길이 6, 비워 두면 위 능력을 모든 면에 적용)")]
    public SpecialDieEffect[] faceEffects;
    [Tooltip("덱에 항상 1개 유지되는 여정 주사위. 파괴·교체 대상에서 빠집니다.")]
    public bool isJourneyDice = false;

    [Header("커스텀 외형 설정 (삼각형 주사위 등)")]
    public Sprite customDiceShell;      // 삼각형 주사위 이미지
    public Sprite[] customFaceSprites;  // 해당 주사위 전용 눈금 이미지들 (1~6번)

    // 에셋 설정을 복사한 새 주사위 데이터를 만든다. 상점 구매·시작 덱·저장 복원이 같은 경로를 쓴다.
    public DiceData1 CreateDiceData()
    {
        DiceData1 newDice = new DiceData1(itemName, (int[])customFaces.Clone());
        newDice.customDiceShell = customDiceShell;
        newDice.customFaceSprites = customFaceSprites != null ? (Sprite[])customFaceSprites.Clone() : null;

        // 이 라인을 추가하여 인스펙터에서 고른 특수 능력을 새 주사위에 전달합니다.
        newDice.specialEffect = this.specialEffect;
        newDice.debuffType = this.debuffType;

        // 면별 효과는 에셋 배열을 공유하지 않도록 복사해서 넘깁니다.
        newDice.faceEffects = (faceEffects != null && faceEffects.Length > 0) ? (SpecialDieEffect[])faceEffects.Clone() : null;
        newDice.isJourneyDice = isJourneyDice;
        return newDice;
    }

    public override void ApplyItemEffect(DiceManager diceManager)
    {
        // 상점에서 구매 시 masterDeck에 새 주사위 추가
        DiceData1 newDice = CreateDiceData();

        diceManager.masterDeck.Add(newDice);
        Debug.Log($"{itemName}이(가) 덱에 추가되었습니다! 총 주사위 수: {diceManager.masterDeck.Count}");
    }
}
