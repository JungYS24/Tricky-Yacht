using UnityEngine;
using System.Collections.Generic;

// 전투에 낼 몬스터 고르기와 능력치 계산 규칙. 몬스터 자체(체력·피격·공격·연출)는 Enemy가 맡는다.
// 밸런스 수치를 볼 때 이 파일을 보면 된다. Enemy.Initialize가 예전과 같은 순서로 부르므로 무작위 선택 순서도 같다.
public static class MonsterSpawnRules
{
    // 이번 전투에 낼 몬스터. 튜토리얼 첫 전투 → 차원문 등 강제 몬스터 → 보스 칸의 보스 → 엘리트·일반 순으로 정한다.
    // 강제 몬스터(pendingForcedMonster)는 여기서 읽고 비운다. 고를 몬스터가 없으면 null.
    public static MonsterDataSO PickMonster(DiceManager dice, BiomeDataSO currentBiome)
    {
        MonsterDataSO nextMonsterData = null;
        MonsterDataSO forcedMonster = dice != null ? dice.pendingForcedMonster : null;
        if (dice != null) dice.pendingForcedMonster = null;

        if (TutorialMode.Active && TutorialMode.WeakestMonster(currentBiome) != null)
        {
            // 튜토리얼 모드 첫 전투는 가장 약한 몬스터로 고정합니다.
            nextMonsterData = TutorialMode.WeakestMonster(currentBiome);
        }
        else if (forcedMonster != null)
        {
            // 차원문 등, 바이옴 목록보다 먼저 강제 몬스터를 씁니다. 보스 몬스터면 bossAbility도 같이 적용됩니다.
            nextMonsterData = forcedMonster;
        }
        else if (currentBiome != null)
        {
            // 지도의 보스 칸에서 시작한 전투만 보스전입니다(층 번호와는 상관없음).
            bool bossStage = dice != null && dice.mapBossBattle;
            if (bossStage && currentBiome.bossMonster != null)
            {
                nextMonsterData = currentBiome.bossMonster;
            }
            //그 외의 일반 스테이지는 리스트에서 등급에 맞는 몬스터를 랜덤 출현
            else if (currentBiome.biomeMonsters != null && currentBiome.biomeMonsters.Count > 0)
            {
                bool elite = dice != null && dice.isEliteBattle;
                nextMonsterData = PickBiomeMonster(currentBiome.biomeMonsters, elite);
            }
        }
        return nextMonsterData;
    }

    // 몬스터 데이터의 기본 체력·공격력에 바이옴 성장, 눈먼 점술가 패널티, 튜토리얼 체력 상한을 적용한다.
    public static void ScaleStats(DiceManager dice, int currentStage, ref int finalMaxHP, ref int finalAttack)
    {
        // 바이옴 단계 + 바이옴 안 전투 수로 성장 (BalanceConfig.useLegacyGrowth 면 예전 스테이지 공식)
        // 튜토리얼 모드는 예전 공식을 그대로 쓴다.
        BalanceConfig cfg = BalanceConfig.Current;
        bool legacy = cfg.useLegacyGrowth || TutorialMode.Active || dice == null;
        int biomeTier = dice != null ? dice.CurrentBiomeTier : 0;
        int nodeInBiome = dice != null ? dice.CombatsClearedInBiome : 0;
        if (legacy)
        {
            finalMaxHP = Mathf.RoundToInt(finalMaxHP * Mathf.Pow(cfg.legacyHpGrowth, currentStage - 1));
            finalAttack += currentStage / 2;
        }
        else
        {
            finalMaxHP = cfg.ScaleHp(finalMaxHP, biomeTier, nodeInBiome, currentStage);
            finalAttack = cfg.ScaleAtk(finalAttack, biomeTier, nodeInBiome, currentStage);
        }

        //(눈먼 점술가 패널티 적용)
        if (DiceManager.Instance != null && DiceManager.Instance.isNextEnemyHPBoosted)
        {
            finalMaxHP *= 2; // 적 체력을 2배(100% 증가)로 만듬
            DiceManager.Instance.isNextEnemyHPBoosted = false; // 적용했으니 스위치를 다시 끔
        }

        // 튜토리얼 모드에서는 한 턴에 잡힐 정도로 체력을 낮춥니다.
        if (TutorialMode.Active) finalMaxHP = Mathf.Min(finalMaxHP, TutorialMode.MonsterHp);
    }

    // 지도의 엘리트 노드는 엘리트 등급만, 일반 전투는 일반 등급만 뽑는다.
    // 바이옴 목록에 해당 등급이 없으면 엘리트는 체력이 가장 높은 몬스터, 일반은 목록 전체에서 뽑는다.
    static MonsterDataSO PickBiomeMonster(List<MonsterDataSO> monsters, bool elite)
    {
        MonsterGrade wanted = elite ? MonsterGrade.Elite : MonsterGrade.Normal;
        var pool = new List<MonsterDataSO>();
        foreach (MonsterDataSO m in monsters)
            if (m != null && m.monsterGrade == wanted) pool.Add(m);
        if (pool.Count > 0) return pool[UnityEngine.Random.Range(0, pool.Count)];

        if (elite)
        {
            MonsterDataSO strongest = null;
            foreach (MonsterDataSO m in monsters)
                if (m != null && (strongest == null || m.maxHp > strongest.maxHp)) strongest = m;
            if (strongest != null) return strongest;
        }
        return monsters[UnityEngine.Random.Range(0, monsters.Count)];
    }

    // 지도 노드가 보스·엘리트면 그 등급을, 아니면(일반·차원문·조우) 몬스터 데이터의 등급을 쓴다.
    public static MonsterGrade ResolveGrade(MonsterDataSO data)
    {
        DiceManager dice = DiceManager.Instance;
        if (dice != null)
        {
            MapNode node = dice.stageProgression.FindNode(dice.stageProgression.currentNodeId);
            if (node != null && node.type == MapNodeType.Boss) return MonsterGrade.Boss;
            if (node != null && node.type == MapNodeType.Elite) return MonsterGrade.Elite;
        }
        return data != null ? data.monsterGrade : MonsterGrade.Normal;
    }
}
