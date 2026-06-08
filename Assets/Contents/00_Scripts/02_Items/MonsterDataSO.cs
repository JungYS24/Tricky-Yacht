using UnityEngine;

[CreateAssetMenu(fileName = "NewMonsterData", menuName = "ScriptableObjects/MonsterData")]
public class MonsterDataSO : ScriptableObject
{
    [Header("Monster Base Info")]
    public string monsterID;
    public string monsterName = "몬스터 이름";
    [TextArea(2, 5)] public string description;

    [Header("Visual Resources")]
    public Sprite monsterSprite;
    public RuntimeAnimatorController animatorController;

    [Header("Monster Stats")]
    public int maxHp;
    public int baseAtk;
    public float evasionRate;

    [Header("Rewards & Drops")]
    public int dropGold;
    [Range(0f, 1f)] public float dropRate = 0.5f;

    public FigureItemSO dropFigureData;
}