using System.Collections.Generic;

// 지도 데이터 형식: 칸 종류, 칸 하나, 지도 한 장.
// 지도 한 장은 MapGenerator가 시드로 만들고, 진행 상황(현재 칸·방문 칸)은 StageManager가 들고 있다.

public enum MapNodeType
{
    Enemy,
    Elite,
    Boss,
    Encounter,
    ShopNyaon,
    ShopRonan,
    ShopOpel,
    Portal,
    Rest
}

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
