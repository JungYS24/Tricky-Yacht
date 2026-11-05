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
    [Tooltip("바이옴 보스를 잡았을 때 최대 체력의 몇 %를 회복할지")]
    [Range(0, 100)] public int biomeClearHealPercent = 100;

    [Header("데모")]
    [Tooltip("이 수만큼 바이옴 보스를 잡으면 데모 클리어로 끝난다. 0 이면 제한 없음(정식판)")]
    public int demoBiomeLimit = 0;

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
    // 바이옴 단계(0~5)별 배율. 6번째(5)는 비차원.
    public float[] tierHp = { 1.28f, 1.35f, 1.55f, 1.75f, 1.95f, 1.95f };
    public float[] tierAtk = { 1.2f, 1.2f, 1.25f, 1.35f, 1.45f, 1.4f };
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
    [Tooltip("엘리트 처치 골드 전체에 곱하는 배율")]
    public float eliteGoldMultiplier = 2f;
    [Tooltip("보스 처치 골드 전체(바이옴 클리어 보상 포함)에 곱하는 배율")]
    public float bossGoldMultiplier = 2f;
    public int journeyGoldPerPip = 6;
    [Tooltip("바이옴 단계별 처치 골드 배율")]
    public float[] tierGold = { 1.0f, 1.2f, 1.4f, 1.6f, 1.8f, 2.0f };

    [Header("상점")]
    [Tooltip("상점에 들어갈 때 진열 칸 하나를 골라 이 배율로 할인한다. 1 이면 세일 없음")]
    [Range(0f, 1f)] public float shopSaleMultiplier = 0.5f;
    public int rerollBaseCost = 50;
    [Tooltip("한 상점에서 리롤할 때마다 오르는 비용")]
    public int rerollCostStep = 25;
    [Range(0f, 1f)] public float sellRatio = 0.4f;
    [Tooltip("바이옴 단계별 상점 진열 가격 배율")]
    public float[] tierShopPrice = { 1.0f, 1.1f, 1.2f, 1.3f, 1.4f, 1.5f };

    [Header("디버프 주사위 (엘리트·차원문 전투마다 들어오는 개수)")]
    [Tooltip("바이옴 단계별 최소 개수")]
    public int[] tierDebuffMin = { 0, 1, 1, 2, 2, 2 };
    [Tooltip("바이옴 단계별 최대 개수")]
    public int[] tierDebuffMax = { 0, 1, 2, 2, 3, 3 };
    [Tooltip("켜면 차원문 디버프(가시·저주)가 덱에 영구히 남는다. 끄면 그 전투가 끝날 때 사라진다")]
    public bool portalDebuffPermanent = false;

    [Header("최종보스 악마주사위")]
    [Tooltip("2페이즈(원페어·투페어 피해 무효) 시작 체력 비율")]
    [Range(0f, 1f)] public float devilPhase2HpRatio = 0.6f;
    [Tooltip("3페이즈(공격 패턴 굴림) 시작 체력 비율")]
    [Range(0f, 1f)] public float devilPhase3HpRatio = 0.25f;

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

    public float TierGold(int tier) => Tier(tierGold, tier);
    public float TierShopPrice(int tier) => Tier(tierShopPrice, tier);

    // 바이옴 단계별 디버프 주사위 개수를 [최소, 최대] 안에서 뽑는다.
    public int RollDebuffCount(int tier)
    {
        int min = TierInt(tierDebuffMin, tier), max = Mathf.Max(min, TierInt(tierDebuffMax, tier));
        return Random.Range(min, max + 1);
    }

    static int TierInt(int[] table, int tier)
    {
        if (table == null || table.Length == 0) return 0;
        return Mathf.Max(0, table[Mathf.Clamp(tier, 0, table.Length - 1)]);
    }

    public float GoldMultiplierFor(MonsterGrade grade)
    {
        switch (grade)
        {
            case MonsterGrade.Boss: return bossGoldMultiplier;
            case MonsterGrade.Elite: return eliteGoldMultiplier;
            default: return 1f;
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
