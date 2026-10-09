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
        var typeNames = new[] { "적", "보스", "조우", "상점", "오펠", "휴식" };

        foreach (BiomeWeightRow weight in table.BiomeWeights)
        {
            if (weight == null || !System.Enum.TryParse(weight.Biome, out BiomeType biome))
                continue;

            int runs = 200;
            int success = 0;
            int attemptSum = 0;
            var typeSum = new int[6];
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
            float facility = (typeSum[2] + typeSum[3] + typeSum[4] + typeSum[5]) / (float)runs;
            report.AppendLine(
                $"{weight.Biome}: 성공 {success}/{runs} ({success * 100f / runs:0.#}%), 평균 시도 {attemptSum / (float)runs:0.0}, 시설 {facility:0.0}, 최단 적 {shortestEnemy}, 최장 적 {longestEnemy} | " +
                $"{typeNames[0]} {typeSum[0] / (float)runs:0.0}, {typeNames[2]} {typeSum[2] / (float)runs:0.0}, {typeNames[3]} {typeSum[3] / (float)runs:0.0}, {typeNames[4]} {typeSum[4] / (float)runs:0.0}, {typeNames[5]} {typeSum[5] / (float)runs:0.0}");
        }

        report.AppendLine($"전체 성공 {totalSuccess}/{totalRuns} ({(totalRuns == 0 ? 0f : totalSuccess * 100f / totalRuns):0.#}%), 평균 시도 {(totalRuns == 0 ? 0f : totalAttempts / (float)totalRuns):0.0}");
        report.AppendLine(deterministic ? "결정성 검사: 통과" : "결정성 검사: 실패");
        Debug.Log(report.ToString());
    }

    static void Accumulate(MapData map, int[] typeSum, ref int shortestEnemy, ref int longestEnemy)
    {
        if (map == null) return;
        var counts = new int[6];
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
                minEnemy[index] = maxEnemy[index] = node.type == MapNodeType.Enemy ? 1 : 0;
            }
            if (minLen[index] == int.MaxValue) continue;

            for (int c = 0; c < node.children.Count; c++)
            {
                int child = node.children[c];
                if (child < 0 || child >= count || map.nodes[child].type == MapNodeType.Boss) continue;
                bool shopRow = map.nodes[child].isShopRow;
                int add = !shopRow && map.nodes[child].type == MapNodeType.Enemy ? 1 : 0;
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
}
#endif
