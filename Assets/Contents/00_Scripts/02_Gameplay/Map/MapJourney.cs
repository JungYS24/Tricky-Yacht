using System.Collections.Generic;

// 여정 주사위 눈만큼 children을 따라갑니다. 상인 고정층과 보스는 남은 칸이 있어도 넘어가지 않습니다.
public static class MapJourney
{
    public static bool IsReachable(MapData map, int currentNodeId, int targetId, int move, bool forceStop)
    {
        if (!TryCollect(map, currentNodeId, move, forceStop, out Dictionary<int, int> distance, out _))
            return false;
        return distance.TryGetValue(targetId, out int steps) && steps >= 1 && steps <= move;
    }

    public static List<int> PassedOnWay(MapData map, int currentNodeId, int targetId, int move, bool forceStop)
    {
        var passed = new List<int>();
        if (!TryCollect(map, currentNodeId, move, forceStop, out _, out Dictionary<int, int> cameFrom))
            return passed;

        int cursor = targetId;
        int guard = 0;
        while (cameFrom.TryGetValue(cursor, out int prev) && guard++ < 128)
        {
            if (prev < 0 || prev == currentNodeId) break;
            passed.Add(prev);
            cursor = prev;
        }
        return passed;
    }

    public static bool TryCollect(MapData map, int currentNodeId, int move, bool forceStop, out Dictionary<int, int> distance, out Dictionary<int, int> cameFrom)
    {
        distance = new Dictionary<int, int>();
        cameFrom = new Dictionary<int, int>();
        if (map == null || map.nodes == null || move <= 0) return false;

        var queue = new Queue<int>();
        if (currentNodeId < 0)
        {
            for (int i = 0; i < map.nodes.Count; i++)
            {
                MapNode node = map.nodes[i];
                if (node == null || node.row != 0) continue;
                distance[node.id] = 1;
                cameFrom[node.id] = -1;
                queue.Enqueue(node.id);
            }
        }
        else
        {
            if (currentNodeId < 0 || currentNodeId >= map.nodes.Count) return false;
            distance[currentNodeId] = 0;
            queue.Enqueue(currentNodeId);
        }

        while (queue.Count > 0)
        {
            int id = queue.Dequeue();
            if (!distance.TryGetValue(id, out int steps) || steps >= move) continue;
            if (id < 0 || id >= map.nodes.Count) continue;
            MapNode node = map.nodes[id];
            if (node == null) continue;
            if (steps > 0 && forceStop && Blocks(node)) continue;

            for (int c = 0; c < node.children.Count; c++)
            {
                int child = node.children[c];
                if (child < 0 || child >= map.nodes.Count || distance.ContainsKey(child)) continue;
                distance[child] = steps + 1;
                cameFrom[child] = id;
                queue.Enqueue(child);
            }
        }
        return true;
    }

    static bool Blocks(MapNode node)
    {
        return node.isShopRow || node.type == MapNodeType.Boss;
    }
}
