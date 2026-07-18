using UnityEngine;

//보스 능력을 정의하는 Enum 추가 (앞으로 다른 능력도 여기에 추가하면 됨)
public enum BossAbilityType
{
    None,       // 일반 몬스터 또는 능력 없음
    FakeDice    // 가짜 주사위 변이 능력
}

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

    // 보스 능력 변수 추가
    [Header("Boss Ability (보스 전용 능력)")]
    public BossAbilityType bossAbility = BossAbilityType.None;
}