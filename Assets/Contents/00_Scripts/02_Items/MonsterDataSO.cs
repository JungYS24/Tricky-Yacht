using UnityEngine;

[CreateAssetMenu(fileName = "NewMonsterData", menuName = "ScriptableObjects/MonsterData")]
public class MonsterDataSO : ScriptableObject
{
    [Header("몬스터 기본 정보")]
    public string monsterName = "몬스터 이름";
    public Sprite monsterSprite;

    [Header("전리품 (박제) 설정")]
    public FigureItemSO dropFigureData;

    [Range(0f, 1f)] public float dropRate = 0.5f;
}