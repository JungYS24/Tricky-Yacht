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
            if (string.IsNullOrEmpty(row.item_ID))
                continue;

            FigureItemSO asset = FindAsset(assets, row);
            if (asset == null)
            {
                missing++;
                Debug.LogWarning($"[Studio 10&6] SO 없음, 건너뜀: {row.item_ID} ({row.item_Name_KR})");
                continue;
            }

            asset.Item_ID = row.item_ID;
            if (!string.IsNullOrEmpty(row.item_Name_KR))
                asset.itemName = row.item_Name_KR;
            asset.price = row.price;
            if (!string.IsNullOrEmpty(row.effect_Summary_KR))
                asset.description = row.effect_Summary_KR;

            ApplyBiomes(asset, row);
            EditorUtility.SetDirty(asset);
            updated++;
        }
        // JSON에 들어 있는 피규어 능력 노드를 SO에 적용
        ApplyFigureNodes(assets, file.items);

        AssetDatabase.SaveAssets();

        Debug.Log($"[Studio 10&6] 피규어 ID/이름/가격/설명/바이옴 동기화 완료. 갱신 {updated}개, SO 없음 {missing}개.");
    }

    // 동일한 피규어의 JSON 행들을 모아 노드 목록으로 변환
    private static void ApplyFigureNodes(
        FigureItemSO[] assets,
        FigureJsonRow[] rows)
    {
        var groups = new Dictionary<string, List<FigureJsonRow>>();

        foreach (FigureJsonRow row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.item_ID))
                continue;

            // 능력 데이터가 없는 기존 기본정보 행은 건너뜀
            if (string.IsNullOrWhiteSpace(row.triggerType) &&
                string.IsNullOrWhiteSpace(row.effectType))
            {
                continue;
            }

            if (!groups.TryGetValue(row.item_ID, out var group))
            {
                group = new List<FigureJsonRow>();
                groups.Add(row.item_ID, group);
            }

            group.Add(row);
        }

        int updated = 0;

        foreach (var pair in groups)
        {
            List<FigureJsonRow> figureRows = pair.Value;
            FigureItemSO asset = FindAsset(assets, figureRows[0]);

            if (asset == null)
            {
                Debug.LogWarning(
                    $"[피규어 능력] SO를 찾지 못함: {pair.Key}");
                continue;
            }

            try
            {
                // 전부 검증한 다음 교체하므로 오류가 나면 기존 노드는 유지
                List<FigureNode> nodes = BuildFigureNodes(figureRows);

                Undo.RecordObject(asset, "피규어 능력 동기화");
                asset.figureNodes = nodes;
                EditorUtility.SetDirty(asset);
                updated++;
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"[피규어 능력] {pair.Key} 적용 실패. " +
                    $"기존 노드 유지: {exception.Message}");
            }
        }

        Debug.Log($"[피규어 능력] 노드 동기화 완료: {updated}개");
    }

    // 번호 순서대로 노드를 만들고 각 노드에 효과 추가
    private static List<FigureNode> BuildFigureNodes(
        List<FigureJsonRow> rows)
    {
        rows.Sort((a, b) =>
        {
            int nodeOrder = a.nodeIndex.CompareTo(b.nodeIndex);
            return nodeOrder != 0
                ? nodeOrder
                : a.effectIndex.CompareTo(b.effectIndex);
        });

        var nodes = new List<FigureNode>();
        FigureNode currentNode = null;
        int currentNodeIndex = -1;

        foreach (FigureJsonRow row in rows)
        {
            FigureTriggerType trigger =
                ParseFigureEnum<FigureTriggerType>(row.triggerType);

            FigureEffectType effect =
                ParseFigureEnum<FigureEffectType>(row.effectType);

            EffectCalcType calculation =
                ParseFigureEnum<EffectCalcType>(row.calcType);

            if (trigger == FigureTriggerType.None ||
                effect == FigureEffectType.None)
            {
                throw new Exception("발동 조건 또는 효과가 None입니다.");
            }

            if (row.requiredKills < 0 ||
                !IsFinite(row.healthThresholdPercent) ||
                row.healthThresholdPercent < 0f ||
                row.healthThresholdPercent > 100f ||
                !IsFinite(row.probability) ||
                row.probability < 0f ||
                row.probability > 100f ||
                !IsFinite(row.effectValue) ||
                !IsFinite(row.secondaryEffectValue))
            {
                throw new Exception(
                    $"노드 {row.nodeIndex}, 효과 {row.effectIndex}: 수치 오류");
            }

            if (row.nodeIndex != currentNodeIndex)
            {
                // 노드 번호는 0, 1, 2... 순서
                if (row.nodeIndex != nodes.Count)
                {
                    throw new Exception(
                        $"nodeIndex는 0부터 연속이어야 합니다: {row.nodeIndex}");
                }

                currentNode = new FigureNode
                {
                    triggerType = trigger,
                    oncePerStage = row.oncePerStage,
                    requiredKills = row.requiredKills,
                    healthThresholdPercent = row.healthThresholdPercent,
                    effects = new List<FigureEffectNode>()
                };

                nodes.Add(currentNode);
                currentNodeIndex = row.nodeIndex;
            }
            else
            {
                // 같은 노드에 속한 행들은 동일한 조건이어야 함
                if (currentNode.triggerType != trigger ||
                    currentNode.oncePerStage != row.oncePerStage ||
                    currentNode.requiredKills != row.requiredKills ||
                    currentNode.healthThresholdPercent != row.healthThresholdPercent)
                {
                    throw new Exception(
                        $"nodeIndex {row.nodeIndex}의 발동 조건이 서로 다릅니다.");
                }
            }

            // 효과 번호도 각 노드 안에서 0, 1, 2... 순서
            if (row.effectIndex != currentNode.effects.Count)
            {
                throw new Exception(
                    $"노드 {row.nodeIndex}: effectIndex가 중복되거나 " +
                    $"순서가 빠졌습니다: {row.effectIndex}");
            }

            // 이번에 만든 능력 시트의 optionalItem은 모두 빈칸(null)
            // 값이 있는데 조용히 무시하지 않도록 오류 처리
            if (!string.IsNullOrWhiteSpace(row.optionalItem))
            {
                throw new Exception(
                    $"optionalItem '{row.optionalItem}'의 아이템 참조 연결이 필요합니다.");
            }

            currentNode.effects.Add(new FigureEffectNode
            {
                effectType = effect,
                calcType = calculation,
                effectValue = row.effectValue,
                probability = row.probability,
                optionalItem = null,
                secondaryEffectValue = row.secondaryEffectValue
            });
        }

        return nodes;
    }

    // JSON의 타입 이름을 실제 enum으로 변환
    private static T ParseFigureEnum<T>(string raw)
        where T : struct, Enum
    {
        // 숫자 enum 대신 엑셀에 적힌 정확한 이름을 사용
        string token = raw?.Trim();

        if (string.IsNullOrEmpty(token) ||
            !Enum.IsDefined(typeof(T), token) ||
            !Enum.TryParse(token, out T result))
        {
            throw new Exception(
                $"{typeof(T).Name}에 없는 타입: '{raw}'");
        }

        return result;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
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
            if (asset.Item_ID == row.item_ID)
                return asset;
        }

        foreach (FigureItemSO asset in assets)
        {
            if (!string.IsNullOrEmpty(row.item_Name_KR) && asset.itemName == row.item_Name_KR)
                return asset;
        }

        string compactIcon = CompactFigId(row.icon);
        string compactId = CompactFigId(row.item_ID);
        foreach (FigureItemSO asset in assets)
        {
            string compactName = CompactFigId(asset.name);
            if (asset.name == row.item_ID || asset.name == row.icon)
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

        TryAddBiome(asset.sourceBiomes, row.source_Biome_1);
        TryAddBiome(asset.sourceBiomes, row.source_Biome_2);
        TryAddBiome(asset.sourceBiomes, row.source_Biome_3);

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

        if (token.Equals("Shop", System.StringComparison.OrdinalIgnoreCase))
            token = nameof(BiomeType.Special);
        else if (token.Equals("SkyIsland", System.StringComparison.OrdinalIgnoreCase))
            token = nameof(BiomeType.Skyisland);

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
        public string item_ID;
        public string icon;
        public string nameKey;
        public string descKey;
        public string item_Name_KR;
        public int price;
        public string effect_Summary_KR;
        public string source_Biome_1;
        public string source_Biome_2;
        public string source_Biome_3;

        // 엑셀 행을 노드와 효과 목록으로 묶기 위한 번호
        public int nodeIndex;
        public int effectIndex;

        // 노드의 발동 조건
        public string triggerType;
        public bool oncePerStage;
        public int requiredKills;
        public float healthThresholdPercent;

        // 노드 안의 효과
        public string effectType;
        public string calcType;
        public float effectValue;
        public float probability;
        public string optionalItem;
        public float secondaryEffectValue;
    }
}
#endif
