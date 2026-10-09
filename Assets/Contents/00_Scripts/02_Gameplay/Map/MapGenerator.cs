using System;
using System.Collections.Generic;
using UnityEngine;

public class MapNode
{
    public int id;
    public int row;
    public int col;
    public MapNodeType type;
    public bool isShortcut;
    public bool isShopRow;
    public List<int> children = new List<int>();
    public List<int> parents = new List<int>();
}

public class MapData
{
    public List<MapNode> nodes = new List<MapNode>();
    public int bossId;
    public int seed;
}

public class MapGenerateResult
{
    public MapData map;
    public int attemptsUsed;
    public bool usedFallback;
}

// MonoBehaviour 없이 시드만으로 맵을 만듭니다. 같은 시드는 항상 같은 맵입니다.
public static class MapGenerator
{
    struct Edge
    {
        public int r1, c1, r2, c2;
        public Edge(int r1, int c1, int r2, int c2)
        {
            this.r1 = r1;
            this.c1 = c1;
            this.r2 = r2;
            this.c2 = c2;
        }
    }

    struct Reach
    {
        public int enemies;
        public int prevId;
        public int prevLen;
    }

    public static MapData Generate(int runSeed, int biomeIndex, BiomeType biome, MapTableData table)
    {
        return GenerateDetailed(runSeed, biomeIndex, biome, table).map;
    }

    public static MapGenerateResult GenerateDetailed(int runSeed, int biomeIndex, BiomeType biome, MapTableData table)
    {
        var result = new MapGenerateResult();
        if (table == null) table = new MapTableData();
        MapConfig cfg = table.Config;
        int seed = runSeed * 31 + biomeIndex;
        int attempts = Mathf.Max(1, cfg.MaxGenAttempts);

        for (int attempt = 0; attempt < attempts; attempt++)
        {
            var rng = new System.Random(seed + attempt);
            if (TryBuild(rng, biome, table, seed + attempt, out MapData map))
            {
                result.map = map;
                result.attemptsUsed = attempt + 1;
                result.usedFallback = false;
                return result;
            }
        }

        Debug.LogWarning($"[Map] 맵 생성을 {attempts}번 시도했지만 실패했습니다. 지름길만 있는 안전 맵을 사용합니다. runSeed={runSeed}, biome={biome}");
        result.map = BuildSafetyMap(seed, table);
        result.attemptsUsed = attempts;
        result.usedFallback = true;
        return result;
    }

    public static bool SameLayout(MapData a, MapData b)
    {
        if (a == null || b == null || a.nodes.Count != b.nodes.Count || a.bossId != b.bossId)
            return false;
        for (int i = 0; i < a.nodes.Count; i++)
        {
            MapNode x = a.nodes[i];
            MapNode y = b.nodes[i];
            if (x.id != y.id || x.row != y.row || x.col != y.col || x.type != y.type || x.isShortcut != y.isShortcut)
                return false;
            if (x.children.Count != y.children.Count) return false;
            for (int c = 0; c < x.children.Count; c++)
            {
                if (x.children[c] != y.children[c]) return false;
            }
        }
        return true;
    }

    static bool TryBuild(System.Random rng, BiomeType biome, MapTableData table, int mapSeed, out MapData map)
    {
        map = null;
        MapConfig cfg = table.Config;
        if (cfg.GridWidth < 1 || cfg.MaxPathLength < 2 || cfg.LaneCount < 1 || cfg.MinEnemyCount < 2)
            return false;

        int lastRow = cfg.MaxPathLength - 1;
        var nodes = new List<MapNode>();
        var lookup = new Dictionary<int, MapNode>();
        var edges = new List<Edge>();

        for (int lane = 0; lane < cfg.LaneCount; lane++)
        {
            bool shortcut = lane == 0;
            List<int> forcedRows = null;
            if (shortcut)
            {
                forcedRows = BuildShortcutRows(cfg, rng);
                if (forcedRows == null) return false;
            }

            int col = StartColumn(cfg, lane);
            MapNode current = GetOrCreate(nodes, lookup, cfg, 0, col, shortcut);
            if (shortcut)
            {
                for (int i = 1; i < forcedRows.Count; i++)
                {
                    if (!TryStep(rng, nodes, lookup, edges, cfg, ref current, forcedRows[i], true))
                        return false;
                }
            }
            else
            {
                while (current.row < lastRow)
                {
                    int dy = NextLaneStep(rng, cfg, current.row, lastRow);
                    if (!TryStep(rng, nodes, lookup, edges, cfg, ref current, current.row + dy, false))
                        return false;
                }
            }
        }

        int bossCol = cfg.GridWidth / 2;
        MapNode boss = GetOrCreate(nodes, lookup, cfg, cfg.MaxPathLength, bossCol, false);
        boss.type = MapNodeType.Boss;
        for (int i = 0; i < nodes.Count; i++)
        {
            MapNode node = nodes[i];
            if (node.row != lastRow || node.id == boss.id) continue;
            if (!node.children.Contains(boss.id))
                AddEdge(edges, node, boss);
        }

        if (!AssignTypes(rng, nodes, biome, table))
            return false;
        if (!RepairDetours(nodes, table))
            return false;
        if (!Validate(nodes, boss.id, table))
            return false;

        map = new MapData { nodes = nodes, bossId = boss.id, seed = mapSeed };
        return true;
    }

    static int StartColumn(MapConfig cfg, int lane)
    {
        if (cfg.StartColumns == null || cfg.StartColumns.Length == 0)
            return Mathf.Clamp(cfg.GridWidth / 2, 0, Mathf.Max(0, cfg.GridWidth - 1));
        int col = cfg.StartColumns[lane % cfg.StartColumns.Length];
        return Mathf.Clamp(col, 0, Mathf.Max(0, cfg.GridWidth - 1));
    }

    static int NextLaneStep(System.Random rng, MapConfig cfg, int row, int lastRow)
    {
        if (row + 2 > lastRow || rng.NextDouble() >= cfg.SkipChance)
            return 1;
        int skipped = row + 2;
        if (row < cfg.ShopRow && skipped > cfg.ShopRow)
            return 1;
        return 2;
    }

    static List<int> BuildShortcutRows(MapConfig cfg, System.Random rng)
    {
        int last = cfg.MaxPathLength - 1;
        int shop = cfg.ShopRow;
        int maxSkip = Math.Max(1, cfg.ShortcutMaxSkip);
        if (shop <= 0 || shop >= last) return null;

        for (int attempt = 0; attempt < 200; attempt++)
        {
            int before = 1 + rng.Next(7);
            int after = cfg.MinEnemyCount - before;
            if (after < 1) continue;

            List<int> below = PickRows(rng, 0, shop - 1, before, 0);
            List<int> above = PickRows(rng, shop + 1, last, after, last);
            if (below == null || above == null) continue;

            var rows = new List<int>(below.Count + above.Count + 1);
            rows.AddRange(below);
            rows.Add(shop);
            rows.AddRange(above);
            if (GapsFit(rows, maxSkip)) return rows;
        }
        return null;
    }

    static List<int> PickRows(System.Random rng, int lo, int hi, int count, int required)
    {
        if (count < 1 || hi < lo || count > hi - lo + 1) return null;
        var chosen = new List<int>();
        if (required >= lo && required <= hi) chosen.Add(required);
        var pool = new List<int>();
        for (int row = lo; row <= hi; row++)
            if (row != required) pool.Add(row);
        Shuffle(pool, rng);
        for (int i = 0; chosen.Count < count && i < pool.Count; i++)
            chosen.Add(pool[i]);
        if (chosen.Count != count) return null;
        chosen.Sort();
        return chosen;
    }

    static bool GapsFit(List<int> rows, int maxSkip)
    {
        for (int i = 1; i < rows.Count; i++)
        {
            int gap = rows[i] - rows[i - 1];
            if (gap < 1 || gap > maxSkip) return false;
        }
        return true;
    }

    static bool TryStep(System.Random rng, List<MapNode> nodes, Dictionary<int, MapNode> lookup, List<Edge> edges, MapConfig cfg, ref MapNode current, int nextRow, bool shortcut)
    {
        var candidates = new List<int>(3);
        for (int delta = -1; delta <= 1; delta++)
        {
            int nextCol = current.col + delta;
            if (nextCol >= 0 && nextCol < cfg.GridWidth)
                candidates.Add(nextCol);
        }
        Shuffle(candidates, rng);

        for (int i = 0; i < candidates.Count; i++)
        {
            int nextCol = candidates[i];
            bool exists = EdgeExists(edges, current.row, current.col, nextRow, nextCol);
            if (!exists && Crosses(edges, current.row, current.col, nextRow, nextCol))
                continue;

            MapNode next = GetOrCreate(nodes, lookup, cfg, nextRow, nextCol, shortcut);
            if (!exists)
                AddEdge(edges, current, next);
            current = next;
            return true;
        }
        return false;
    }

    static MapNode GetOrCreate(List<MapNode> nodes, Dictionary<int, MapNode> lookup, MapConfig cfg, int row, int col, bool shortcut)
    {
        int key = row * 1000 + col;
        if (lookup.TryGetValue(key, out MapNode found))
        {
            if (shortcut) found.isShortcut = true;
            found.isShopRow = found.row == cfg.ShopRow;
            return found;
        }

        bool shopRow = row == cfg.ShopRow;
        var node = new MapNode
        {
            id = nodes.Count,
            row = row,
            col = col,
            type = row == cfg.MaxPathLength ? MapNodeType.Boss : (shopRow ? MapNodeType.Shop : MapNodeType.Enemy),
            isShortcut = shortcut,
            isShopRow = shopRow
        };
        nodes.Add(node);
        lookup[key] = node;
        return node;
    }

    static void AddEdge(List<Edge> edges, MapNode from, MapNode to)
    {
        edges.Add(new Edge(from.row, from.col, to.row, to.col));
        if (!from.children.Contains(to.id)) from.children.Add(to.id);
        if (!to.parents.Contains(from.id)) to.parents.Add(from.id);
    }

    static bool EdgeExists(List<Edge> edges, int r1, int c1, int r2, int c2)
    {
        for (int i = 0; i < edges.Count; i++)
        {
            Edge edge = edges[i];
            if (edge.r1 == r1 && edge.c1 == c1 && edge.r2 == r2 && edge.c2 == c2)
                return true;
        }
        return false;
    }

    // 겹치는 층 구간에서 열 차이가 부호를 바꾸면 교차입니다. 끝점만 닿는 경우는 허용합니다.
    static bool Crosses(List<Edge> edges, int r1, int c1, int r2, int c2)
    {
        for (int i = 0; i < edges.Count; i++)
        {
            Edge other = edges[i];
            int lo = Math.Max(Math.Min(r1, r2), Math.Min(other.r1, other.r2));
            int hi = Math.Min(Math.Max(r1, r2), Math.Max(other.r1, other.r2));
            if (hi <= lo) continue;

            float s1 = ColumnAt(r1, c1, r2, c2, lo) - ColumnAt(other.r1, other.c1, other.r2, other.c2, lo);
            float s2 = ColumnAt(r1, c1, r2, c2, hi) - ColumnAt(other.r1, other.c1, other.r2, other.c2, hi);
            if (s1 * s2 < 0f) return true;
            if (Math.Abs(s1) < 0.0001f && Math.Abs(s2) < 0.0001f) return true;
        }
        return false;
    }

    static float ColumnAt(int r1, int c1, int r2, int c2, int row)
    {
        float span = r2 - r1;
        if (Math.Abs(span) < 0.0001f) return c1;
        float t = (row - r1) / span;
        return c1 + (c2 - c1) * t;
    }

    static bool AssignTypes(System.Random rng, List<MapNode> nodes, BiomeType biome, MapTableData table)
    {
        var order = new List<int>(nodes.Count);
        for (int i = 0; i < nodes.Count; i++) order.Add(i);
        order.Sort((a, b) =>
        {
            int row = nodes[a].row.CompareTo(nodes[b].row);
            return row != 0 ? row : nodes[a].col.CompareTo(nodes[b].col);
        });

        var counts = new int[6];
        var pool = new List<MapNodeType>(5);
        var weights = new List<int>(5);

        for (int i = 0; i < order.Count; i++)
        {
            MapNode node = nodes[order[i]];
            if (node.type == MapNodeType.Boss)
            {
                counts[(int)MapNodeType.Boss]++;
                continue;
            }
            if (node.isShopRow)
            {
                node.type = MapNodeType.Shop;
                continue;
            }
            if (node.row == 0 || node.isShortcut)
            {
                node.type = MapNodeType.Enemy;
                counts[(int)MapNodeType.Enemy]++;
                continue;
            }

            pool.Clear();
            weights.Clear();
            AddCandidate(pool, weights, nodes, node, MapNodeType.Enemy, counts, biome, table);
            AddCandidate(pool, weights, nodes, node, MapNodeType.Encounter, counts, biome, table);
            AddCandidate(pool, weights, nodes, node, MapNodeType.Shop, counts, biome, table);
            AddCandidate(pool, weights, nodes, node, MapNodeType.Rest, counts, biome, table);

            MapNodeType picked = PickWeighted(rng, pool, weights);
            node.type = picked;
            counts[(int)picked]++;
        }
        return true;
    }

    static void AddCandidate(List<MapNodeType> pool, List<int> weights, List<MapNode> nodes, MapNode node, MapNodeType type, int[] counts, BiomeType biome, MapTableData table)
    {
        if (type != MapNodeType.Enemy)
        {
            MapNodeTypeRow rule = table.GetNodeRule(type);
            if (rule == null) return;
            if (node.row < rule.Min_Row) return;
            if (counts[(int)type] >= rule.Max_Per_Map) return;
            if (rule.Is_Facility && ParentHasType(nodes, node, type)) return;
        }

        int weight = table.GetWeight(biome, type);
        if (type == MapNodeType.Shop)
            weight += table.GetWeight(biome, MapNodeType.Opel);
        if (type == MapNodeType.Enemy && weight <= 0) weight = 1;
        if (weight <= 0) return;
        pool.Add(type);
        weights.Add(weight);
    }

    static bool ParentHasType(List<MapNode> nodes, MapNode node, MapNodeType type)
    {
        for (int i = 0; i < node.parents.Count; i++)
        {
            int parentId = node.parents[i];
            if (parentId >= 0 && parentId < nodes.Count && nodes[parentId].type == type)
                return true;
        }
        return false;
    }

    static MapNodeType PickWeighted(System.Random rng, List<MapNodeType> pool, List<int> weights)
    {
        int sum = 0;
        for (int i = 0; i < weights.Count; i++) sum += weights[i];
        if (sum <= 0 || pool.Count == 0) return MapNodeType.Enemy;

        int roll = rng.Next(sum);
        int acc = 0;
        for (int i = 0; i < pool.Count; i++)
        {
            acc += weights[i];
            if (roll < acc) return pool[i];
        }
        return MapNodeType.Enemy;
    }

    static bool RepairDetours(List<MapNode> nodes, MapTableData table)
    {
        MapConfig cfg = table.Config;
        int guard = nodes.Count + 1;
        for (int n = 0; n < guard; n++)
        {
            if (!TryFindViolation(nodes, cfg, out int endId, out int len))
                return true;
            if (!FlipHighestFacility(nodes, table, endId, len))
                return false;
        }
        return false;
    }

    static bool TryFindViolation(List<MapNode> nodes, MapConfig cfg, out int endId, out int len)
    {
        endId = -1;
        len = 0;
        Reach[][] best = BuildDp(nodes);
        int lastRow = cfg.MaxPathLength - 1;
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i].row != lastRow) continue;
            Reach[] row = best[i];
            for (int pathLen = 1; pathLen < row.Length; pathLen++)
            {
                if (row[pathLen].enemies < 0) continue;
                int required = RequiredEnemies(pathLen, cfg);
                if (row[pathLen].enemies < required)
                {
                    endId = i;
                    len = pathLen;
                    return true;
                }
            }
        }
        return false;
    }

    static bool FlipHighestFacility(List<MapNode> nodes, MapTableData table, int endId, int len)
    {
        Reach[][] best = BuildDp(nodes);
        int id = endId;
        int pathLen = len;
        while (id >= 0 && pathLen >= 1)
        {
            MapNode node = nodes[id];
            if (node.isShopRow)
            {
                Reach skipped = best[id][pathLen];
                id = skipped.prevId;
                pathLen = skipped.prevLen;
                continue;
            }
            MapNodeTypeRow rule = table.GetNodeRule(node.type);
            if (rule != null && rule.Is_Facility)
            {
                node.type = MapNodeType.Enemy;
                return true;
            }
            Reach reach = best[id][pathLen];
            id = reach.prevId;
            pathLen = reach.prevLen;
        }
        return false;
    }

    static Reach[][] BuildDp(List<MapNode> nodes)
    {
        int maxLen = 1;
        for (int i = 0; i < nodes.Count; i++)
            maxLen = Math.Max(maxLen, nodes[i].row + 1);
        maxLen = Math.Max(maxLen, nodes.Count);

        var best = new Reach[nodes.Count][];
        for (int i = 0; i < nodes.Count; i++)
        {
            best[i] = new Reach[maxLen + 2];
            for (int len = 0; len < best[i].Length; len++)
                best[i][len].enemies = -1;
        }

        var order = new List<int>(nodes.Count);
        for (int i = 0; i < nodes.Count; i++) order.Add(i);
        order.Sort((a, b) => nodes[a].row.CompareTo(nodes[b].row));

        for (int o = 0; o < order.Count; o++)
        {
            int index = order[o];
            MapNode node = nodes[index];
            if (node.type == MapNodeType.Boss) continue;

            if (node.row == 0)
            {
                best[index][1].enemies = node.type == MapNodeType.Enemy ? 1 : 0;
                best[index][1].prevId = -1;
                best[index][1].prevLen = 0;
            }

            Reach[] from = best[index];
            for (int pathLen = 1; pathLen < from.Length; pathLen++)
            {
                if (from[pathLen].enemies < 0) continue;
                for (int c = 0; c < node.children.Count; c++)
                {
                    int childId = node.children[c];
                    if (childId < 0 || childId >= nodes.Count) continue;
                    MapNode child = nodes[childId];
                    if (child.type == MapNodeType.Boss) continue;
                    int nextLen = child.isShopRow ? pathLen : pathLen + 1;
                    if (nextLen >= best[childId].Length) continue;
                    int enemies = from[pathLen].enemies;
                    if (!child.isShopRow && child.type == MapNodeType.Enemy)
                        enemies++;
                    if (best[childId][nextLen].enemies < 0 || enemies < best[childId][nextLen].enemies)
                    {
                        best[childId][nextLen].enemies = enemies;
                        best[childId][nextLen].prevId = index;
                        best[childId][nextLen].prevLen = pathLen;
                    }
                }
            }
        }
        return best;
    }

    static int RequiredEnemies(int pathLength, MapConfig cfg)
    {
        double extra = (pathLength - cfg.MinEnemyCount) * (double)cfg.DetourEnemyRatio - 1e-6;
        int rounded = (int)Math.Ceiling(extra);
        return cfg.MinEnemyCount + rounded;
    }

    static bool Validate(List<MapNode> nodes, int bossId, MapTableData table)
    {
        MapConfig cfg = table.Config;
        var counts = new int[6];
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i].isShopRow) continue;
            counts[(int)nodes[i].type]++;
        }

        for (int t = 0; t < 6; t++)
        {
            MapNodeTypeRow rule = table.GetNodeRule((MapNodeType)t);
            if (rule == null) continue;
            if ((MapNodeType)t == MapNodeType.Opel) continue;
            if (counts[t] < rule.Min_Per_Map) return false;
        }

        Reach[][] best = BuildDp(nodes);
        int lastRow = cfg.MaxPathLength - 1;
        int shortest = int.MaxValue;
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i].row != lastRow) continue;
            for (int pathLen = 1; pathLen < best[i].Length; pathLen++)
            {
                if (best[i][pathLen].enemies >= 0)
                    shortest = Math.Min(shortest, pathLen);
            }
        }
        return shortest == cfg.MinEnemyCount && bossId >= 0;
    }

    static MapData BuildSafetyMap(int mapSeed, MapTableData table)
    {
        MapConfig cfg = table.Config;
        var nodes = new List<MapNode>();
        int col = Mathf.Max(0, cfg.GridWidth / 2);
        int last = cfg.MaxPathLength - 1;
        int shop = Mathf.Clamp(cfg.ShopRow, 1, Mathf.Max(1, last - 1));
        int maxSkip = Mathf.Max(1, cfg.ShortcutMaxSkip);
        var rows = new List<int> { 0 };
        int row = 0;
        while (row < last)
        {
            int step = Mathf.Min(maxSkip, last - row);
            if (row < shop && row + step > shop)
                step = shop - row;
            row += step;
            rows.Add(row);
        }

        MapNode previous = null;
        for (int i = 0; i < rows.Count; i++)
        {
            var node = new MapNode
            {
                id = nodes.Count,
                row = rows[i],
                col = col,
                type = rows[i] == cfg.ShopRow ? MapNodeType.Shop : MapNodeType.Enemy,
                isShortcut = true,
                isShopRow = rows[i] == cfg.ShopRow
            };
            nodes.Add(node);
            if (previous != null)
            {
                previous.children.Add(node.id);
                node.parents.Add(previous.id);
            }
            previous = node;
        }

        var boss = new MapNode
        {
            id = nodes.Count,
            row = cfg.MaxPathLength,
            col = col,
            type = MapNodeType.Boss
        };
        nodes.Add(boss);
        if (previous != null)
        {
            previous.children.Add(boss.id);
            boss.parents.Add(previous.id);
        }

        return new MapData { nodes = nodes, bossId = boss.id, seed = mapSeed };
    }

    static void Shuffle<T>(IList<T> list, System.Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            T temp = list[i];
            list[i] = list[j];
            list[j] = temp;
        }
    }
}
