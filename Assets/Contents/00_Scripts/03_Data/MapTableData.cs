using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

public enum MapNodeType
{
    Enemy,
    Boss,
    Encounter,
    Shop,
    Opel,
    Rest
}

// Map_Table.json 한 줄을 그대로 받는 그릇입니다. Value는 문자열이므로 타입은 따로 해석합니다.
[Serializable]
public class MapConfigEntry
{
    public string Key;
    public string Value;
    public string Value_Type;
    public string Desc_KR;
}

[Serializable]
public class MapNodeTypeRow
{
    public string Node_ID;
    public string Name_Key;
    public string Desc_Key;
    public string Name_KR;
    public string Desc_KR;
    public string Icon_Key;
    public bool Is_Facility;
    public int Min_Row;
    public int Min_Per_Map;
    public int Max_Per_Map;
}

[Serializable]
public class BiomeWeightRow
{
    public string Biome;
    public int Enemy_W;
    public int Encounter_W;
    public int Shop_W;
    public int Opel_W;
    public int Rest_W;
}

[Serializable]
public class MapTableJson
{
    public List<MapConfigEntry> mapConfig = new List<MapConfigEntry>();
    public List<MapNodeTypeRow> nodeTypes = new List<MapNodeTypeRow>();
    public List<BiomeWeightRow> biomeWeights = new List<BiomeWeightRow>();
}

// 시트 값을 코드에서 쓰기 좋게 풀어 둔 전역 설정입니다.
public class MapConfig
{
    public int MinEnemyCount = 8;
    public int MaxPathLength = 14;
    public int GridWidth = 5;
    public int LaneCount = 4;
    public float SkipChance = 0.15f;
    public float DetourEnemyRatio = 0.25f;
    public int MaxGenAttempts = 100;
    public int RestHealPercent = 30;
    public bool PostBattleShop;
    public int BiomeCountToVoid = 10;
}

public class MapTableData
{
    public MapConfig Config = new MapConfig();
    public List<MapNodeTypeRow> NodeTypes = new List<MapNodeTypeRow>();
    public List<BiomeWeightRow> BiomeWeights = new List<BiomeWeightRow>();

    public static MapTableData Parse(string json)
    {
        var data = new MapTableData();
        if (string.IsNullOrEmpty(json))
        {
            Debug.LogWarning("[Map] Map_Table.json이 비어 있습니다. 기본값을 사용합니다.");
            return data;
        }

        MapTableJson raw = JsonUtility.FromJson<MapTableJson>(json);
        if (raw == null)
        {
            Debug.LogWarning("[Map] Map_Table.json을 읽지 못했습니다. 기본값을 사용합니다.");
            return data;
        }

        if (raw.nodeTypes != null) data.NodeTypes = raw.nodeTypes;
        if (raw.biomeWeights != null) data.BiomeWeights = raw.biomeWeights;
        data.ApplyConfig(raw.mapConfig);
        return data;
    }

    void ApplyConfig(List<MapConfigEntry> entries)
    {
        var seen = new HashSet<string>();
        if (entries != null)
        {
            foreach (MapConfigEntry entry in entries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.Key)) continue;
                seen.Add(entry.Key);
                ApplyOne(entry);
            }
        }

        string[] required =
        {
            "Min_Enemy_Count", "Max_Path_Length", "Grid_Width", "Lane_Count", "Skip_Chance",
            "Detour_Enemy_Ratio", "Max_Gen_Attempts", "Rest_Heal_Percent", "Post_Battle_Shop", "Biome_Count_To_Void"
        };
        foreach (string key in required)
        {
            if (!seen.Contains(key))
                Debug.LogWarning($"[Map] Map_Config에 {key}가 없어 기본값을 사용합니다.");
        }
    }

    void ApplyOne(MapConfigEntry entry)
    {
        switch (entry.Key)
        {
            case "Min_Enemy_Count": Config.MinEnemyCount = ParseInt(entry.Value, Config.MinEnemyCount); break;
            case "Max_Path_Length": Config.MaxPathLength = ParseInt(entry.Value, Config.MaxPathLength); break;
            case "Grid_Width": Config.GridWidth = ParseInt(entry.Value, Config.GridWidth); break;
            case "Lane_Count": Config.LaneCount = ParseInt(entry.Value, Config.LaneCount); break;
            case "Skip_Chance": Config.SkipChance = ParseFloat(entry.Value, Config.SkipChance); break;
            case "Detour_Enemy_Ratio": Config.DetourEnemyRatio = ParseFloat(entry.Value, Config.DetourEnemyRatio); break;
            case "Max_Gen_Attempts": Config.MaxGenAttempts = ParseInt(entry.Value, Config.MaxGenAttempts); break;
            case "Rest_Heal_Percent": Config.RestHealPercent = ParseInt(entry.Value, Config.RestHealPercent); break;
            case "Post_Battle_Shop": Config.PostBattleShop = ParseBool(entry.Value); break;
            case "Biome_Count_To_Void": Config.BiomeCountToVoid = ParseInt(entry.Value, Config.BiomeCountToVoid); break;
        }
    }

    static int ParseInt(string value, int fallback)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) ? parsed : fallback;
    }

    static float ParseFloat(string value, float fallback)
    {
        return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed) ? parsed : fallback;
    }

    static bool ParseBool(string value)
    {
        return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) || value == "1";
    }

    public BiomeWeightRow GetWeights(BiomeType biome)
    {
        string name = biome.ToString();
        for (int i = 0; i < BiomeWeights.Count; i++)
        {
            if (BiomeWeights[i] != null && BiomeWeights[i].Biome == name)
                return BiomeWeights[i];
        }
        return BiomeWeights.Count > 0 ? BiomeWeights[0] : null;
    }

    public int GetWeight(BiomeType biome, MapNodeType type)
    {
        BiomeWeightRow row = GetWeights(biome);
        if (row == null) return 0;
        switch (type)
        {
            case MapNodeType.Enemy: return row.Enemy_W;
            case MapNodeType.Encounter: return row.Encounter_W;
            case MapNodeType.Shop: return row.Shop_W;
            case MapNodeType.Opel: return row.Opel_W;
            case MapNodeType.Rest: return row.Rest_W;
            default: return 0;
        }
    }

    public MapNodeTypeRow GetNodeRule(MapNodeType type)
    {
        string id = ToNodeId(type);
        for (int i = 0; i < NodeTypes.Count; i++)
        {
            if (NodeTypes[i] != null && NodeTypes[i].Node_ID == id)
                return NodeTypes[i];
        }
        return null;
    }

    public static string ToNodeId(MapNodeType type)
    {
        switch (type)
        {
            case MapNodeType.Enemy: return "Node_Enemy";
            case MapNodeType.Boss: return "Node_Boss";
            case MapNodeType.Encounter: return "Node_Encounter";
            case MapNodeType.Shop: return "Node_Shop";
            case MapNodeType.Opel: return "Node_Opel";
            case MapNodeType.Rest: return "Node_Rest";
            default: return "Node_Enemy";
        }
    }

    public static bool TryParseNodeId(string nodeId, out MapNodeType type)
    {
        switch (nodeId)
        {
            case "Node_Enemy": type = MapNodeType.Enemy; return true;
            case "Node_Boss": type = MapNodeType.Boss; return true;
            case "Node_Encounter": type = MapNodeType.Encounter; return true;
            case "Node_Shop": type = MapNodeType.Shop; return true;
            case "Node_Opel": type = MapNodeType.Opel; return true;
            case "Node_Rest": type = MapNodeType.Rest; return true;
            default: type = MapNodeType.Enemy; return false;
        }
    }

    public string GetNodeName(MapNodeType type)
    {
        MapNodeTypeRow rule = GetNodeRule(type);
        if (rule == null) return type.ToString();
        if (LocalizationManager.TryGetLocalized(LocalizationManager.ItemTable, rule.Name_Key, out string localized))
            return localized;
        return string.IsNullOrEmpty(rule.Name_KR) ? rule.Name_Key : rule.Name_KR;
    }
}
