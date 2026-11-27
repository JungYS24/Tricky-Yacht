using System;
using System.Collections.Generic;

// 전리품 한 화면(3칸)의 추첨 결과.
public class RewardRollResult
{
    public readonly RewardRow[] Slots = new RewardRow[RewardRoller.SlotCount];
    public readonly ItemGrade[] RolledGrades = new ItemGrade[RewardRoller.SlotCount];
    public bool PityTriggered;
    public int PitySlot = -1;
    public int PityCounterAfter;
    public bool HasLegendary;
}

// 전리품 추첨 (순수 C#). 칸마다 등급을 먼저 굴리고, 그 칸·등급의 사용 가능 보상 중 균등 추첨한다.
// 시드를 넣은 System.Random을 주면 같은 결과가 나온다(검증용).
public static class RewardRoller
{
    public const int SlotCount = 3;
    static readonly RewardSlotType[] SlotOrder = { RewardSlotType.Growth, RewardSlotType.Snack, RewardSlotType.Dice };
    static readonly ItemGrade[] GradeOrder = { ItemGrade.Normal, ItemGrade.Rare, ItemGrade.Epic, ItemGrade.Legendary };

    public static RewardSlotType SlotTypeAt(int index) => SlotOrder[index];

    /// <param name="table">읽어 둔 루트 테이블</param>
    /// <param name="monster">처치한 몬스터 등급(Normal → standard, Elite → elite, Boss → boss)</param>
    /// <param name="biomeTier">DiceManager.CurrentBiomeTier</param>
    /// <param name="pityCounter">보스 화면을 뺀, 레전드가 안 나온 연속 화면 수</param>
    /// <param name="rng">System.Random (시드 주입 가능)</param>
    /// <param name="isAvailable">추가 조건(없으면 모두 통과). 스낵칸 상태는 카드 비활성으로 처리하므로 여기서 거르지 않는다.</param>
    public static RewardRollResult Roll(RewardLootTable table, MonsterGrade monster, int biomeTier, int pityCounter,
        Random rng, Func<RewardRow, bool> isAvailable = null)
    {
        var result = new RewardRollResult();
        if (table == null || !table.Loaded || rng == null)
        {
            result.PityCounterAfter = pityCounter;
            return result;
        }

        RewardGradeWeightRow weights = table.WeightRowFor(monster, biomeTier);
        bool boss = monster == MonsterGrade.Boss;

        // 레전드 천장: 보스 화면이 아니고 연속 미등장이 천장에 닿았으면 무작위 1칸을 레전드로 확정한다.
        int forcedSlot = -1;
        if (!boss && pityCounter >= table.LegendaryPityScreens)
        {
            var candidates = new List<int>();
            for (int s = 0; s < SlotCount; s++)
                if (Pool(table, SlotOrder[s], ItemGrade.Legendary, null, isAvailable).Count > 0) candidates.Add(s);
            if (candidates.Count > 0)
            {
                forcedSlot = candidates[rng.Next(candidates.Count)];
                result.PityTriggered = true;
                result.PitySlot = forcedSlot;
            }
        }

        var usedGroups = new HashSet<string>();
        for (int s = 0; s < SlotCount; s++)
        {
            ItemGrade grade = s == forcedSlot ? ItemGrade.Legendary : RollGrade(weights, rng);
            result.RolledGrades[s] = grade;

            // 풀이 비면 한 단계 낮은 등급으로 내려간다. 노멀도 비면 칸을 숨긴다(null).
            for (int g = Array.IndexOf(GradeOrder, grade); g >= 0; g--)
            {
                List<RewardRow> pool = Pool(table, SlotOrder[s], GradeOrder[g], usedGroups, isAvailable);
                if (pool.Count == 0) continue;
                RewardRow pick = pool[rng.Next(pool.Count)];
                result.Slots[s] = pick;
                if (!string.IsNullOrEmpty(pick.Group_ID)) usedGroups.Add(pick.Group_ID);
                break;
            }
        }

        for (int s = 0; s < SlotCount; s++)
            if (result.Slots[s] != null && result.Slots[s].ItemGrade == ItemGrade.Legendary) result.HasLegendary = true;

        // 레전드가 하나라도 나오면 0. 보스 화면은 세지 않는다.
        if (result.HasLegendary) result.PityCounterAfter = 0;
        else result.PityCounterAfter = boss ? pityCounter : pityCounter + 1;
        return result;
    }

    public static ItemGrade RollGrade(RewardGradeWeightRow weights, Random rng)
    {
        if (weights == null) return ItemGrade.Normal;
        float total = 0f;
        for (int i = 0; i < GradeOrder.Length; i++) total += Math.Max(0f, weights.WeightOf(GradeOrder[i]));
        if (total <= 0f) return ItemGrade.Normal;

        double roll = rng.NextDouble() * total;
        double acc = 0;
        for (int i = 0; i < GradeOrder.Length; i++)
        {
            float w = Math.Max(0f, weights.WeightOf(GradeOrder[i]));
            if (w <= 0f) continue;
            acc += w;
            if (roll < acc) return GradeOrder[i];
        }
        // 부동소수 오차로 끝까지 온 경우 가중치가 있는 가장 높은 등급
        for (int i = GradeOrder.Length - 1; i >= 0; i--)
            if (weights.WeightOf(GradeOrder[i]) > 0f) return GradeOrder[i];
        return ItemGrade.Normal;
    }

    public static List<RewardRow> Pool(RewardLootTable table, RewardSlotType slot, ItemGrade grade,
        HashSet<string> usedGroups, Func<RewardRow, bool> isAvailable)
    {
        var pool = new List<RewardRow>();
        foreach (RewardRow row in table.Rows)
        {
            if (!RewardLootTable.IsRollable(row)) continue;
            if (row.SlotType != slot || row.ItemGrade != grade) continue;
            if (usedGroups != null && !string.IsNullOrEmpty(row.Group_ID) && usedGroups.Contains(row.Group_ID)) continue;
            if (isAvailable != null && !isAvailable(row)) continue;
            pool.Add(row);
        }
        return pool;
    }
}
