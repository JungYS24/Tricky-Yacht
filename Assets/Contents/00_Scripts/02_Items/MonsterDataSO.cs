using UnityEngine;

[CreateAssetMenu(fileName = "NewMonsterData", menuName = "ScriptableObjects/MonsterData")]
public class MonsterDataSO : ScriptableObject
{
    [Header("몬스터 기본 정보")]
    public string monsterName = "몬스터 이름";
    public Sprite monsterSprite; // (애니메이션이 없는 정지 몬스터를 위한 예비용)

    //몬스터의 애니메이션을 통제하는 파일
    public RuntimeAnimatorController animatorController;

    [Header("전투 스탯")]
    public int baseHP = 40;
    public AnimationCurve hpScalingCurve = AnimationCurve.Linear(1, 1, 50, 10);

    [Header("전리품 (박제) 설정")]
    public FigureItemSO dropFigureData;
    [Range(0f, 1f)] public float dropRate = 0.5f;
}