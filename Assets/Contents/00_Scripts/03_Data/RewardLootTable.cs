using System;
using System.Collections.Generic;
using UnityEngine;

// 전리품 칸 종류. 1번 칸 성장, 2번 칸 스낵, 3번 칸 주사위.
public enum RewardSlotType { Growth, Snack, Dice }

// Reward_Loot_Table.json 한 줄. 필드 이름이 JSON 키와 같아야 JsonUtility가 읽는다.
[Serializable]
public class RewardRow
{
    public string Reward_ID;
    public string Group_ID;
    public string Slot_Type;
    public string Grade;
    public int Phase;
    public bool Enabled;
    public string Name_Key;
    public string Desc_Key;
    public string Name_KR;
    public string Desc_KR;
    public string Name_EN;
    public string Desc_EN;
    public string Effect_Type;
    public string Param_Str;
    public float Value_1;
    public float Value_2;
    public string Impl_Status;
    public string Icon_Hint;

    [NonSerialized] public RewardSlotType SlotType;
    [NonSerialized] public ItemGrade ItemGrade;
    [NonSerialized] public bool Valid;
}

[Serializable]
public class RewardGradeWeightRow
{
    public int Biome_Tier;
    public float Normal;
    public float Rare;
    public float Epic;
    public float Legendary;

    public float WeightOf(ItemGrade grade)
    {
        switch (grade)
        {
            case ItemGrade.Normal: return Normal;
            case ItemGrade.Rare: return Rare;
            case ItemGrade.Epic: return Epic;
            case ItemGrade.Legendary: return Legendary;
            default: return 0f;
        }
    }
}

[Serializable]
public class RewardGradeWeights
{
    public List<RewardGradeWeightRow> standard = new List<RewardGradeWeightRow>();
    public List<RewardGradeWeightRow> elite = new List<RewardGradeWeightRow>();
    public List<RewardGradeWeightRow> boss = new List<RewardGradeWeightRow>();
}

[Serializable]
public class RewardRuleRow
{
    public string Rule_Key;
    public string Value;
    public string Description;
}

[Serializable]
class RewardLootFile
{
    public string version;
    public List<RewardRow> lootTable;
    public RewardGradeWeights gradeWeight;
    public List<RewardRuleRow> rules;
}

// 전리품 루트 테이블. ShopStockTable·MapTableData처럼 TextAsset + JsonUtility로 읽는다.
public class RewardLootTable
{
    public const int DefaultLegendaryPity = 15;

    public string Version;
    public bool Loaded;
    public List<RewardRow> Rows = new List<RewardRow>();
    public RewardGradeWeights Weights = new RewardGradeWeights();
    public int LegendaryPityScreens = DefaultLegendaryPity;
    public readonly List<string> SnackFillExclude = new List<string>();
    readonly Dictionary<string, RewardRow> byId = new Dictionary<string, RewardRow>();

    public static RewardLootTable Parse(string json)
    {
        var table = new RewardLootTable();
        if (string.IsNullOrEmpty(json)) return table;

        RewardLootFile raw;
        try
        {
            raw = JsonUtility.FromJson<RewardLootFile>(json);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Reward Loot] 표를 읽지 못했습니다: {e.Message}");
            return table;
        }
        if (raw == null || raw.lootTable == null || raw.gradeWeight == null) return table;

        table.Version = raw.version;
        table.Weights = raw.gradeWeight;
        if (table.Weights.standard == null) table.Weights.standard = new List<RewardGradeWeightRow>();
        if (table.Weights.elite == null) table.Weights.elite = new List<RewardGradeWeightRow>();
        if (table.Weights.boss == null) table.Weights.boss = new List<RewardGradeWeightRow>();
        table.Weights.standard.Sort((a, b) => a.Biome_Tier.CompareTo(b.Biome_Tier));
        table.Weights.elite.Sort((a, b) => a.Biome_Tier.CompareTo(b.Biome_Tier));
        table.Weights.boss.Sort((a, b) => a.Biome_Tier.CompareTo(b.Biome_Tier));

        if (raw.rules != null)
        {
            foreach (var rule in raw.rules)
            {
                if (rule == null) continue;
                if (rule.Rule_Key == "Legendary_Pity_Screens" && int.TryParse(rule.Value, out int pity) && pity > 0)
                    table.LegendaryPityScreens = pity;
                else if (rule.Rule_Key == "Snack_Fill_Exclude" && !string.IsNullOrEmpty(rule.Value))
                    table.SnackFillExclude.AddRange(rule.Value.Split('|'));
            }
        }
        if (table.SnackFillExclude.Count == 0) table.SnackFillExclude.Add(nameof(SnackType.Peppermint));

        foreach (var row in raw.lootTable)
        {
            if (row == null || string.IsNullOrEmpty(row.Reward_ID)) continue;
            row.Valid = TryParseSlot(row.Slot_Type, out row.SlotType) & TryParseGrade(row.Grade, out row.ItemGrade);
            if (!row.Valid)
                Debug.LogWarning($"[Reward Loot] {row.Reward_ID}: Slot_Type({row.Slot_Type}) 또는 Grade({row.Grade})를 알 수 없어 추첨에서 뺍니다.");
            table.Rows.Add(row);
            table.byId[row.Reward_ID] = row;
        }

        table.Loaded = table.Rows.Count > 0 && table.Weights.standard.Count > 0;
        return table;
    }

    public RewardRow Find(string rewardId)
    {
        if (string.IsNullOrEmpty(rewardId)) return null;
        byId.TryGetValue(rewardId, out RewardRow row);
        return row;
    }

    // 추첨에 쓸 수 있는 줄(Enabled, 형식 정상).
    public static bool IsRollable(RewardRow row) => row != null && row.Valid && row.Enabled;

    public List<RewardGradeWeightRow> WeightsFor(MonsterGrade monster)
    {
        switch (monster)
        {
            case MonsterGrade.Elite: return Weights.elite.Count > 0 ? Weights.elite : Weights.standard;
            case MonsterGrade.Boss: return Weights.boss.Count > 0 ? Weights.boss : Weights.standard;
            default: return Weights.standard;
        }
    }

    // 바이옴 단계에 맞는 줄. 표보다 크면 마지막 줄, 0보다 작으면 첫 줄.
    public RewardGradeWeightRow WeightRowFor(MonsterGrade monster, int biomeTier)
    {
        List<RewardGradeWeightRow> list = WeightsFor(monster);
        if (list == null || list.Count == 0) return null;
        RewardGradeWeightRow pick = list[0];
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].Biome_Tier <= biomeTier) pick = list[i];
        }
        return pick;
    }

    public static bool TryParseSlot(string value, out RewardSlotType slot)
    {
        return Enum.TryParse(value, true, out slot) && Enum.IsDefined(typeof(RewardSlotType), slot);
    }

    public static bool TryParseGrade(string value, out ItemGrade grade)
    {
        grade = ItemGrade.Normal;
        if (string.IsNullOrEmpty(value)) return false;
        switch (value.Trim().ToLowerInvariant())
        {
            case "normal": grade = ItemGrade.Normal; return true;
            case "rare": grade = ItemGrade.Rare; return true;
            case "epic": grade = ItemGrade.Epic; return true;
            case "legendary":
            case "legend": grade = ItemGrade.Legendary; return true;
            default: return false;
        }
    }
}
