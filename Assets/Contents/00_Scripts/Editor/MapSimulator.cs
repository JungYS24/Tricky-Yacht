#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;

// 바이옴마다 시드를 돌려 생성기가 기획 수치 안에 들어오는지 확인합니다.
public static class MapSimulator
{
    const string TablePath = "Assets/Contents/10_Resources/Data/Map_Table.json";

    [MenuItem("Tools/Map/Simulate")]
    public static void Simulate()
    {
        TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(TablePath);
        if (asset == null)
        {
            Debug.LogError("[Map Simulate] Map_Table.json을 찾지 못했습니다.");
            return;
        }

        MapTableData table = MapTableData.Parse(asset.text);
        var report = new StringBuilder();
        report.AppendLine("[Map Simulate] 바이옴 16종 × 시드 200");

        int totalRuns = 0;
        int totalSuccess = 0;
        int totalAttempts = 0;
        bool deterministic = true;
        var typeNames = new[] { "적", "엘리트", "보스", "조우", "냐온", "로넌", "오펠", "차원문", "휴식" };

        foreach (BiomeWeightRow weight in table.BiomeWeights)
        {
            if (weight == null || !System.Enum.TryParse(weight.Biome, out BiomeType biome))
                continue;

            int runs = 200;
            int success = 0;
            int attemptSum = 0;
            var typeSum = new int[typeNames.Length];
            int shortestEnemy = int.MaxValue;
            int longestEnemy = 0;

            for (int seed = 1; seed <= runs; seed++)
            {
                MapGenerateResult first = MapGenerator.GenerateDetailed(seed, 0, biome, table);
                MapGenerateResult second = MapGenerator.GenerateDetailed(seed, 0, biome, table);
                if (!MapGenerator.SameLayout(first.map, second.map))
                    deterministic = false;

                totalRuns++;
                attemptSum += first.attemptsUsed;
                if (!first.usedFallback) success++;

                Accumulate(first.map, typeSum, ref shortestEnemy, ref longestEnemy);
            }

            totalSuccess += success;
            totalAttempts += attemptSum;
            float facility = FacilityCount(table, typeSum) / (float)runs;
            report.AppendLine(
                $"{weight.Biome}: 성공 {success}/{runs} ({success * 100f / runs:0.#}%), 평균 시도 {attemptSum / (float)runs:0.0}, 시설 {facility:0.0}, 최단 전투 {shortestEnemy}, 최장 전투 {longestEnemy} | " +
                $"{typeNames[0]} {Avg(typeSum, 0, runs)}, {typeNames[1]} {Avg(typeSum, 1, runs)}, {typeNames[3]} {Avg(typeSum, 3, runs)}, " +
                $"{typeNames[4]} {Avg(typeSum, 4, runs)}, {typeNames[5]} {Avg(typeSum, 5, runs)}, {typeNames[6]} {Avg(typeSum, 6, runs)}, " +
                $"{typeNames[7]} {Avg(typeSum, 7, runs)}, {typeNames[8]} {Avg(typeSum, 8, runs)}");
        }

        report.AppendLine($"전체 성공 {totalSuccess}/{totalRuns} ({(totalRuns == 0 ? 0f : totalSuccess * 100f / totalRuns):0.#}%), 평균 시도 {(totalRuns == 0 ? 0f : totalAttempts / (float)totalRuns):0.0}");
        report.AppendLine(deterministic ? "결정성 검사: 통과" : "결정성 검사: 실패");
        Debug.Log(report.ToString());
    }

    static void Accumulate(MapData map, int[] typeSum, ref int shortestEnemy, ref int longestEnemy)
    {
        if (map == null) return;
        var counts = new int[typeSum.Length];
        for (int i = 0; i < map.nodes.Count; i++)
            counts[(int)map.nodes[i].type]++;
        for (int i = 0; i < counts.Length; i++) typeSum[i] += counts[i];

        int pathShortEnemies = int.MaxValue;
        int pathLongEnemies = 0;
        MeasurePathEnemies(map, ref pathShortEnemies, ref pathLongEnemies);
        if (pathShortEnemies != int.MaxValue) shortestEnemy = Mathf.Min(shortestEnemy, pathShortEnemies);
        longestEnemy = Mathf.Max(longestEnemy, pathLongEnemies);
    }

    // 최단 경로의 적 수, 최장 경로의 적 수를 끝 층 기준으로 구합니다.
    static void MeasurePathEnemies(MapData map, ref int shortestEnemy, ref int longestEnemy)
    {
        int count = map.nodes.Count;
        var minLen = new int[count];
        var minEnemy = new int[count];
        var maxLen = new int[count];
        var maxEnemy = new int[count];
        for (int i = 0; i < count; i++)
        {
            minLen[i] = int.MaxValue;
            maxLen[i] = -1;
        }

        var order = new int[count];
        for (int i = 0; i < count; i++) order[i] = i;
        System.Array.Sort(order, (a, b) => map.nodes[a].row.CompareTo(map.nodes[b].row));

        for (int o = 0; o < count; o++)
        {
            int index = order[o];
            MapNode node = map.nodes[index];
            if (node.type == MapNodeType.Boss) continue;
            if (node.row == 0)
            {
                minLen[index] = maxLen[index] = 1;
                minEnemy[index] = maxEnemy[index] = MapTableData.IsCombatNode(node.type) ? 1 : 0;
            }
            if (minLen[index] == int.MaxValue) continue;

            for (int c = 0; c < node.children.Count; c++)
            {
                int child = node.children[c];
                if (child < 0 || child >= count || map.nodes[child].type == MapNodeType.Boss) continue;
                bool shopRow = map.nodes[child].isShopRow;
                int add = !shopRow && MapTableData.IsCombatNode(map.nodes[child].type) ? 1 : 0;
                int step = shopRow ? 0 : 1;
                int nextMin = minLen[index] + step;
                int nextMinEnemy = minEnemy[index] + add;
                if (nextMin < minLen[child] || (nextMin == minLen[child] && nextMinEnemy < minEnemy[child]))
                {
                    minLen[child] = nextMin;
                    minEnemy[child] = nextMinEnemy;
                }
                int nextMax = maxLen[index] + step;
                int nextMaxEnemy = maxEnemy[index] + add;
                if (nextMax > maxLen[child] || (nextMax == maxLen[child] && nextMaxEnemy > maxEnemy[child]))
                {
                    maxLen[child] = nextMax;
                    maxEnemy[child] = nextMaxEnemy;
                }
            }
        }

        int lastRow = 0;
        for (int i = 0; i < count; i++)
            if (map.nodes[i].type != MapNodeType.Boss)
                lastRow = Mathf.Max(lastRow, map.nodes[i].row);

        int bestLen = int.MaxValue;
        int worstLen = -1;
        for (int i = 0; i < count; i++)
        {
            if (map.nodes[i].row != lastRow || minLen[i] == int.MaxValue) continue;
            if (minLen[i] < bestLen)
            {
                bestLen = minLen[i];
                shortestEnemy = minEnemy[i];
            }
            else if (minLen[i] == bestLen)
            {
                shortestEnemy = Mathf.Min(shortestEnemy, minEnemy[i]);
            }
            if (maxLen[i] > worstLen)
            {
                worstLen = maxLen[i];
                longestEnemy = maxEnemy[i];
            }
            else if (maxLen[i] == worstLen)
            {
                longestEnemy = Mathf.Max(longestEnemy, maxEnemy[i]);
            }
        }
    }

    static string Avg(int[] sum, int index, int runs)
    {
        if (sum == null || index < 0 || index >= sum.Length || runs <= 0) return "0.0";
        return (sum[index] / (float)runs).ToString("0.0");
    }

    static int FacilityCount(MapTableData table, int[] typeSum)
    {
        int total = 0;
        int count = System.Enum.GetValues(typeof(MapNodeType)).Length;
        for (int i = 0; i < count && i < typeSum.Length; i++)
        {
            MapNodeTypeRow rule = table.GetNodeRule((MapNodeType)i);
            if (rule != null && rule.Is_Facility)
                total += typeSum[i];
        }
        return total;
    }
}
#endif
