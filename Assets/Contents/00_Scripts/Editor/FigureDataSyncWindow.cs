#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

public static class FigureDataSyncWindow
{
    private const string JsonPath = "Assets/Contents/10_Resources/Data/FigureDataList.json";
    private const string FigureFolder = "Assets/Contents/05_DataSO/FiguresSO";

    [MenuItem("Studio 10&6/피규어 데이터 동기화")]
    public static void SyncFigureData()
    {
        if (!File.Exists(JsonPath))
        {
            Debug.LogError($"[Studio 10&6] JSON 파일을 찾을 수 없습니다: {JsonPath}");
            return;
        }

        string wrapped = "{\"items\":" + File.ReadAllText(JsonPath) + "}";
        FigureJsonFile file = JsonUtility.FromJson<FigureJsonFile>(wrapped);
        if (file == null || file.items == null || file.items.Length == 0)
        {
            Debug.LogError("[Studio 10&6] FigureDataList.json을 읽지 못했습니다.");
            return;
        }

        FigureItemSO[] assets = LoadFigureAssets();
        int updated = 0;
        int missing = 0;

        foreach (FigureJsonRow row in file.items)
        {
            if (string.IsNullOrEmpty(row.Item_ID))
                continue;

            FigureItemSO asset = FindAsset(assets, row);
            if (asset == null)
            {
                missing++;
                Debug.LogWarning($"[Studio 10&6] SO 없음, 건너뜀: {row.Item_ID} ({row.Item_Name_KR})");
                continue;
            }

            asset.Item_ID = row.Item_ID;
            if (!string.IsNullOrEmpty(row.Item_Name_KR))
                asset.itemName = row.Item_Name_KR;
            asset.price = row.Price;
            if (!string.IsNullOrEmpty(row.Effect_Summary_KR))
                asset.description = row.Effect_Summary_KR;

            ApplyBiomes(asset, row);
            EditorUtility.SetDirty(asset);
            updated++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[Studio 10&6] 피규어 ID/이름/가격/설명/바이옴 동기화 완료. 갱신 {updated}개, SO 없음 {missing}개. 노드(기믹)는 건드리지 않았습니다.");
    }

    private static FigureItemSO[] LoadFigureAssets()
    {
        string[] guids = AssetDatabase.FindAssets("t:FigureItemSO", new[] { FigureFolder });
        var list = new List<FigureItemSO>(guids.Length);
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            FigureItemSO asset = AssetDatabase.LoadAssetAtPath<FigureItemSO>(path);
            if (asset != null)
                list.Add(asset);
        }

        return list.ToArray();
    }

    private static FigureItemSO FindAsset(FigureItemSO[] assets, FigureJsonRow row)
    {
        foreach (FigureItemSO asset in assets)
        {
            if (asset.Item_ID == row.Item_ID)
                return asset;
        }

        foreach (FigureItemSO asset in assets)
        {
            if (!string.IsNullOrEmpty(row.Item_Name_KR) && asset.itemName == row.Item_Name_KR)
                return asset;
        }

        string compactIcon = CompactFigId(row.Icon);
        string compactId = CompactFigId(row.Item_ID);
        foreach (FigureItemSO asset in assets)
        {
            string compactName = CompactFigId(asset.name);
            if (asset.name == row.Item_ID || asset.name == row.Icon)
                return asset;
            if (!string.IsNullOrEmpty(compactId) && compactName == compactId)
                return asset;
            if (!string.IsNullOrEmpty(compactIcon) && compactName == compactIcon)
                return asset;
        }

        return null;
    }

    private static string CompactFigId(string id)
    {
        if (string.IsNullOrEmpty(id) || !id.StartsWith("Fig_", StringComparison.Ordinal))
            return id;
        return "Fig_" + id.Substring(4).Replace("_", string.Empty);
    }

    private static void ApplyBiomes(FigureItemSO asset, FigureJsonRow row)
    {
        if (asset.sourceBiomes == null)
            asset.sourceBiomes = new List<BiomeType>();
        asset.sourceBiomes.Clear();

        TryAddBiome(asset.sourceBiomes, row.Source_Biome_1);
        TryAddBiome(asset.sourceBiomes, row.Source_Biome_2);
        TryAddBiome(asset.sourceBiomes, row.Source_Biome_3);

        if (asset.sourceBiomes.Count == 0)
            asset.sourceBiomes.Add(BiomeType.Forest);
    }

    private static void TryAddBiome(List<BiomeType> list, string raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw == "-")
            return;

        string token = raw.Trim();
        Match numbered = Regex.Match(token, @"^\d+_(.+)$");
        if (numbered.Success)
            token = numbered.Groups[1].Value;

        if (Enum.TryParse(token, true, out BiomeType biome) && !list.Contains(biome))
            list.Add(biome);
        else
            Debug.LogWarning($"[Studio 10&6] 알 수 없는 바이옴: {raw}");
    }

    [Serializable]
    private class FigureJsonFile
    {
        public FigureJsonRow[] items;
    }

    [Serializable]
    private class FigureJsonRow
    {
        public string Item_ID;
        public string Icon;
        public string Item_Name_KR;
        public int Price;
        public string Effect_Summary_KR;
        public string Source_Biome_1;
        public string Source_Biome_2;
        public string Source_Biome_3;
    }
}
#endif
