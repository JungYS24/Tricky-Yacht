using UnityEngine;

public enum MonsterGrade { Normal, Elite, Boss }

// 밸런스 수치를 한 곳에 모은 설정. Assets/Resources/BalanceConfig.asset 을 읽는다.
// 몬스터 개별 수치·상점 가격은 기존 JSON 테이블에 두고, 여기에는 공식 계수와 공통 값만 둔다.
[CreateAssetMenu(fileName = "BalanceConfig", menuName = "ScriptableObjects/BalanceConfig")]
public class BalanceConfig : ScriptableObject
{
    const string ResourcePath = "BalanceConfig";

    [Header("플레이어")]
    public int playerMaxHP = 80;
    public int startGold = 100;
    public int maxRerolls = 2;
    [Range(0, 100)] public int restHealPercent = 25;

    [Header("몬스터 공격 턴")]
    public int normalAttackTurn = 3;
    public int eliteAttackTurn = 2;
    public int bossAttackTurn = 2;
    [Tooltip("보스 체력이 이 비율 이하가 되면 2페이즈")]
    [Range(0f, 1f)] public float bossPhase2HpRatio = 0.5f;
    public int bossPhase2AttackTurn = 2;

    [Header("몬스터 성장 (바이옴 단계 + 바이옴 안 전투 수)")]
    [Tooltip("켜면 예전 공식(체력 x1.05^(stage-1), 공격력 +stage/2)을 쓴다")]
    public bool useLegacyGrowth = false;
    public float[] tierHp = { 1.0f, 1.0f, 2.4f, 3.4f };
    public float[] tierAtk = { 1.0f, 1.0f, 1.9f, 2.5f };
    [Tooltip("바이옴 안에서 이미 치른 전투 1회당 체력 증가율")]
    public float hpPerNodeInBiome = 0.032f;
    [Tooltip("바이옴 안 전투 이 횟수마다 공격력 +1")]
    public int nodesPerAtkStep = 6;
    public float legacyHpGrowth = 1.05f;

    [Header("골드")]
    [Tooltip("몬스터 dropGold 에 곱하는 배율")]
    public float monsterGoldMultiplier = 1.2f;
    [Tooltip("보스 처치 시 몬스터 골드와 별도로 주는 바이옴 클리어 보상")]
    public int biomeClearBonusGold = 180;
    public int journeyGoldPerPip = 6;

    [Header("상점")]
    public int rerollBaseCost = 50;
    [Tooltip("한 상점에서 리롤할 때마다 오르는 비용")]
    public int rerollCostStep = 25;
    [Range(0f, 1f)] public float sellRatio = 0.4f;

    [Header("피규어 드롭률 (페퍼민트 없이 처치했을 때)")]
    [Range(0f, 1f)] public float normalDropRate = 0.12f;
    [Range(0f, 1f)] public float eliteDropRate = 0.35f;
    [Range(0f, 1f)] public float bossDropRate = 1f;

    static BalanceConfig current;

    // 에셋이 없으면 코드 기본값으로 만든 임시 설정을 쓴다.
    public static BalanceConfig Current
    {
        get
        {
            if (current == null)
            {
                current = Resources.Load<BalanceConfig>(ResourcePath);
                if (current == null)
                {
                    Debug.LogWarning("[Balance] Resources/BalanceConfig.asset 이 없어 기본값을 사용합니다.");
                    current = CreateInstance<BalanceConfig>();
                }
            }
            return current;
        }
    }

    // 시뮬레이터처럼 다른 값으로 돌려 볼 때만 쓴다. null 이면 에셋을 다시 읽는다.
    public static void OverrideForTesting(BalanceConfig config) => current = config;

    public int AttackTurnFor(MonsterGrade grade)
    {
        switch (grade)
        {
            case MonsterGrade.Boss: return Mathf.Max(1, bossAttackTurn);
            case MonsterGrade.Elite: return Mathf.Max(1, eliteAttackTurn);
            default: return Mathf.Max(1, normalAttackTurn);
        }
    }

    public float DropRateFor(MonsterGrade grade)
    {
        switch (grade)
        {
            case MonsterGrade.Boss: return bossDropRate;
            case MonsterGrade.Elite: return eliteDropRate;
            default: return normalDropRate;
        }
    }

    static float Tier(float[] table, int tier)
    {
        if (table == null || table.Length == 0) return 1f;
        return table[Mathf.Clamp(tier, 0, table.Length - 1)];
    }

    // biomeTier: 몇 번째 바이옴인지(0부터). nodeInBiome: 이 바이옴에서 이미 치른 전투 수.
    public int ScaleHp(int baseHp, int biomeTier, int nodeInBiome, int legacyStage)
    {
        if (useLegacyGrowth)
            return Mathf.RoundToInt(baseHp * Mathf.Pow(legacyHpGrowth, legacyStage - 1));
        return Mathf.RoundToInt(baseHp * Tier(tierHp, biomeTier) * (1f + hpPerNodeInBiome * Mathf.Max(0, nodeInBiome)));
    }

    public int ScaleAtk(int baseAtk, int biomeTier, int nodeInBiome, int legacyStage)
    {
        if (useLegacyGrowth)
            return baseAtk + legacyStage / 2;
        int step = nodesPerAtkStep > 0 ? Mathf.Max(0, nodeInBiome) / nodesPerAtkStep : 0;
        return Mathf.RoundToInt(baseAtk * Tier(tierAtk, biomeTier)) + step;
    }
}
