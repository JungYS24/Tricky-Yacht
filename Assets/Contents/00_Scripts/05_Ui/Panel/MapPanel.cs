using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 세로로 스크롤하는 분기 맵입니다. 노드와 점선은 열·층 좌표에 다시 그립니다.
public class MapPanel : MonoBehaviour
{
    public MapNodeView nodePrefab;
    public MapNodeView nodeTemplate;
    public MapIconSet iconSet;
    public ScrollRect scrollRect;
    public RectTransform content;
    public RectTransform lineLayer;
    public RectTransform nodeLayer;
    public RectTransform legend;

    Sprite dotSprite;

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public void Show(DiceManager diceManager)
    {
        gameObject.SetActive(true);
        MapUiHierarchy.EnsureMap(this);
        Rebuild(diceManager);
    }

    void Rebuild(DiceManager diceManager)
    {
        if (lineLayer == null || nodeLayer == null || content == null) return;
        ClearChildren(lineLayer);
        ClearChildren(nodeLayer);

        MapData map = diceManager.stageProgression.currentMap;
        MapTableData table = diceManager.GetMapTable();
        if (map == null || table == null) return;

        MapConfig cfg = table.Config;
        const float colSpacing = 168f;
        const float rowSpacing = 128f;
        const float leftPad = 280f;
        const float rightPad = 80f;
        const float bottomPad = 52f;
        const float topPad = 80f;
        FitScrollToBottom();
        float width = leftPad + rightPad + Mathf.Max(0, cfg.GridWidth - 1) * colSpacing;
        float height = bottomPad + topPad + cfg.MaxPathLength * rowSpacing;
        content.sizeDelta = new Vector2(width, height);
        FitLayer(lineLayer, content.sizeDelta);
        FitLayer(nodeLayer, content.sizeDelta);

        var positions = new Dictionary<int, Vector2>(map.nodes.Count);
        for (int i = 0; i < map.nodes.Count; i++)
        {
            MapNode node = map.nodes[i];
            var jitter = new System.Random(map.seed + node.id * 17);
            float jx = ((float)jitter.NextDouble() * 2f - 1f) * 16f;
            float jy = ((float)jitter.NextDouble() * 2f - 1f) * 10f;
            positions[node.id] = new Vector2(leftPad + node.col * colSpacing + jx, bottomPad + node.row * rowSpacing + jy);
        }

        for (int i = 0; i < map.nodes.Count; i++)
        {
            MapNode node = map.nodes[i];
            for (int c = 0; c < node.children.Count; c++)
            {
                int childId = node.children[c];
                if (!positions.ContainsKey(childId)) continue;
                DrawDottedLine(positions[node.id], positions[childId]);
            }
        }

        for (int i = 0; i < map.nodes.Count; i++)
        {
            MapNode node = map.nodes[i];
            CreateNode(diceManager, table, node, positions[node.id]);
        }

        RefreshLegend(table);
        PlaceLegend();
        Canvas.ForceUpdateCanvases();
        FocusOnSelectable(diceManager, positions);
    }

    void CreateNode(DiceManager diceManager, MapTableData table, MapNode node, Vector2 position)
    {
        MapNodeView source = nodeTemplate != null ? nodeTemplate : nodePrefab;
        MapNodeView view = source != null
            ? Instantiate(source, nodeLayer)
            : CreateRuntimeNode();
        view.gameObject.SetActive(true);
        var rect = view.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 0f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(78f, 78f);
        rect.anchoredPosition = position;

        MapNodeTypeRow rule = table.GetNodeRule(node.type);
        string iconKey = rule != null ? rule.Icon_Key : null;
        Color tint = Color.white;
        Sprite sprite = null;
        if (iconSet != null)
            sprite = iconSet.GetSprite(iconKey, out tint);
        bool selectable = diceManager.IsMapNodeSelectable(node.id);
        bool current = node.id == diceManager.stageProgression.currentNodeId;
        bool visited = diceManager.stageProgression.visitedNodeIds != null && diceManager.stageProgression.visitedNodeIds.Contains(node.id);
        view.Bind(diceManager, node.id, sprite, tint, selectable, current, visited);
        if (node.type == MapNodeType.Boss)
            view.transform.localScale = new Vector3(2.8f, 2.8f, 1f);
    }

    MapNodeView CreateRuntimeNode()
    {
        var go = new GameObject("MapNode", typeof(RectTransform), typeof(Image), typeof(Button), typeof(MapNodeView));
        go.transform.SetParent(nodeLayer, false);
        var icon = go.GetComponent<Image>();
        icon.color = Color.white;
        var button = go.GetComponent<Button>();
        button.targetGraphic = icon;

        var ringGo = new GameObject("Ring", typeof(RectTransform), typeof(Image));
        ringGo.transform.SetParent(go.transform, false);
        var ringRect = ringGo.GetComponent<RectTransform>();
        Stretch(ringRect);
        ringRect.offsetMin = new Vector2(-8f, -8f);
        ringRect.offsetMax = new Vector2(8f, 8f);
        var ring = ringGo.GetComponent<Image>();
        ring.raycastTarget = false;
        ring.color = new Color(1f, 0.92f, 0.4f, 0.9f);
        ring.transform.SetAsFirstSibling();

        var view = go.GetComponent<MapNodeView>();
        view.button = button;
        view.icon = icon;
        view.ring = ring;
        return view;
    }

    void RefreshLegend(MapTableData table)
    {
        if (legend == null || table == null) return;
        MapNodeType[] types =
        {
            MapNodeType.Enemy, MapNodeType.Boss, MapNodeType.Encounter,
            MapNodeType.Shop, MapNodeType.Opel, MapNodeType.Rest
        };
        string[] names = { "Enemy", "Boss", "Encounter", "Shop", "Opel", "Rest" };
        for (int i = 0; i < names.Length; i++)
        {
            Transform item = legend.Find(names[i]);
            if (item == null) continue;
            MapNodeTypeRow rule = table.GetNodeRule(types[i]);
            string iconKey = rule != null ? rule.Icon_Key : null;
            Sprite sprite = iconSet != null ? iconSet.GetSprite(iconKey, out _) : null;
            Image icon = item.Find("Icon") != null ? item.Find("Icon").GetComponent<Image>() : null;
            if (icon != null && sprite != null)
            {
                icon.sprite = sprite;
                icon.color = Color.white;
                icon.preserveAspect = true;
            }
            Text label = item.Find("Label") != null ? item.Find("Label").GetComponent<Text>() : null;
            if (label != null) label.text = table.GetNodeName(types[i]);
            item.gameObject.SetActive(names[i] != "Opel");
        }
    }

    void FocusOnSelectable(DiceManager diceManager, Dictionary<int, Vector2> positions)
    {
        if (scrollRect == null || content == null) return;
        float targetY = 0f;
        bool found = false;
        MapData map = diceManager.stageProgression.currentMap;
        for (int i = 0; i < map.nodes.Count; i++)
        {
            int id = map.nodes[i].id;
            if (!diceManager.IsMapNodeSelectable(id) || !positions.ContainsKey(id)) continue;
            if (!found || positions[id].y < targetY)
            {
                targetY = positions[id].y;
                found = true;
            }
        }
        if (!found)
        {
            scrollRect.verticalNormalizedPosition = 0f;
            return;
        }

        RectTransform viewport = scrollRect.viewport != null ? scrollRect.viewport : scrollRect.transform as RectTransform;
        float viewHeight = viewport != null ? viewport.rect.height : 0f;
        float scrollable = content.rect.height - viewHeight;
        if (scrollable <= 1f)
        {
            scrollRect.verticalNormalizedPosition = 0f;
            return;
        }
        float bottom = Mathf.Max(0f, targetY - 100f);
        scrollRect.verticalNormalizedPosition = Mathf.Clamp01(bottom / scrollable);
    }

    void FitScrollToBottom()
    {
        if (scrollRect == null) return;
        RectTransform viewport = scrollRect.transform as RectTransform;
        if (viewport == null) return;
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.pivot = new Vector2(0.5f, 0.5f);
        Vector2 min = viewport.offsetMin;
        Vector2 max = viewport.offsetMax;
        min.x = 205f;
        max.x = -157f;
        viewport.offsetMin = min;
        viewport.offsetMax = max;
    }

    void PlaceLegend()
    {
        if (legend == null) return;
        legend.anchorMin = new Vector2(1f, 0f);
        legend.anchorMax = new Vector2(1f, 0f);
        legend.pivot = new Vector2(1f, 0f);
        legend.sizeDelta = new Vector2(240f, 300f);
        legend.anchoredPosition = new Vector2(-28f, 28f);

        string[] names = { "Enemy", "Boss", "Encounter", "Shop", "Opel", "Rest" };
        int shown = 0;
        for (int i = 0; i < names.Length; i++)
        {
            Transform item = legend.Find(names[i]);
            if (item == null) continue;
            bool visible = names[i] != "Opel";
            item.gameObject.SetActive(visible);
            if (!visible) continue;
            RectTransform rect = item as RectTransform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.sizeDelta = new Vector2(-16f, 48f);
            rect.anchoredPosition = new Vector2(8f, 8f + shown * 52f);
            shown++;
        }
    }

    void DrawDottedLine(Vector2 from, Vector2 to)
    {
        if (dotSprite == null) dotSprite = MakeDot();
        Vector2 delta = to - from;
        float distance = delta.magnitude;
        int dots = Mathf.Max(2, Mathf.FloorToInt(distance / 18f));
        for (int i = 0; i <= dots; i++)
        {
            float t = i / (float)dots;
            var go = new GameObject("Dot", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(lineLayer, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.sizeDelta = new Vector2(8f, 8f);
            rect.anchoredPosition = Vector2.Lerp(from, to, t);
            var image = go.GetComponent<Image>();
            image.sprite = dotSprite;
            image.color = new Color(0.85f, 0.9f, 0.95f, 0.85f);
            image.raycastTarget = false;
        }
    }

    static Sprite MakeDot()
    {
        const int size = 8;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - 3.5f;
                float dy = y - 3.5f;
                texture.SetPixel(x, y, dx * dx + dy * dy <= 10f ? Color.white : new Color(0f, 0f, 0f, 0f));
            }
        }
        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
    }

    static void ClearChildren(Transform parent)
    {
        if (parent == null) return;
        for (int i = parent.childCount - 1; i >= 0; i--)
            Destroy(parent.GetChild(i).gameObject);
    }

    static void FitLayer(RectTransform layer, Vector2 size)
    {
        layer.anchorMin = Vector2.zero;
        layer.anchorMax = Vector2.zero;
        layer.pivot = Vector2.zero;
        layer.anchoredPosition = Vector2.zero;
        layer.sizeDelta = size;
        layer.localScale = Vector3.one;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    public static Font BuiltinFont()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return font;
    }
}
