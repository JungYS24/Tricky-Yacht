using UnityEngine;
using UnityEngine.UI;

// 씬에 없는 지도 UI만 채웁니다. 이미 하이어라키에 있으면 그 오브젝트를 그대로 씁니다.
public static class MapUiHierarchy
{
    public static void EnsureMap(MapPanel panel)
    {
        if (panel == null) return;
        RectTransform root = panel.transform as RectTransform;
        Transform scroll = root.Find("MapScroll");
        bool createScroll = scroll == null;
        if (createScroll) Stretch(root);
        Image background = panel.GetComponent<Image>();
        bool newBackground = background == null;
        if (newBackground) background = panel.gameObject.AddComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0f);
        background.raycastTarget = false;
        EnsureMapDim(root);
        if (createScroll)
        {
            var scrollGo = new GameObject("MapScroll", typeof(RectTransform));
            scrollGo.transform.SetParent(root, false);
            scroll = scrollGo.transform;
            RectTransform scrollRect = scroll as RectTransform;
            Stretch(scrollRect);
            scrollRect.offsetMin = new Vector2(24f, 150f);
            scrollRect.offsetMax = new Vector2(-24f, -24f);
            Image scrollImage = scrollGo.AddComponent<Image>();
            scrollImage.color = new Color(1f, 1f, 1f, 0.02f);
            Mask mask = scrollGo.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            ScrollRect createdScroll = scrollGo.AddComponent<ScrollRect>();
            createdScroll.horizontal = false;
            createdScroll.vertical = true;
            createdScroll.movementType = ScrollRect.MovementType.Clamped;
            createdScroll.scrollSensitivity = 30f;
        }

        Transform content = scroll.Find("Content");
        if (content == null)
        {
            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(scroll, false);
            content = contentGo.transform;
            RectTransform contentRect = content as RectTransform;
            contentRect.anchorMin = Vector2.zero;
            contentRect.anchorMax = Vector2.zero;
            contentRect.pivot = Vector2.zero;
            contentRect.sizeDelta = new Vector2(900f, 1800f);
        }

        Transform lines = content.Find("Lines");
        if (lines == null)
        {
            var linesGo = new GameObject("Lines", typeof(RectTransform));
            linesGo.transform.SetParent(content, false);
            lines = linesGo.transform;
            Stretch(lines as RectTransform);
        }

        Transform nodes = content.Find("Nodes");
        if (nodes == null)
        {
            var nodesGo = new GameObject("Nodes", typeof(RectTransform));
            nodesGo.transform.SetParent(content, false);
            nodes = nodesGo.transform;
            Stretch(nodes as RectTransform);
        }

        ScrollRect scrollView = scroll.GetComponent<ScrollRect>();
        if (scrollView == null) scrollView = scroll.gameObject.AddComponent<ScrollRect>();
        scrollView.content = content as RectTransform;
        scrollView.viewport = scroll as RectTransform;

        Transform legend = root.Find("Legend");
        if (legend == null)
        {
            var legendGo = new GameObject("Legend", typeof(RectTransform));
            legendGo.transform.SetParent(root, false);
            legend = legendGo.transform;
            RectTransform legendRect = legend as RectTransform;
        legendRect.anchorMin = new Vector2(1f, 0f);
        legendRect.anchorMax = new Vector2(1f, 0f);
        legendRect.pivot = new Vector2(1f, 0f);
        legendRect.sizeDelta = new Vector2(240f, 300f);
        legendRect.anchoredPosition = new Vector2(-28f, 28f);
        }
        EnsureLegendSlots(legend);

        Transform template = root.Find("NodeTemplate");
        if (template == null)
        {
            var templateGo = new GameObject("NodeTemplate", typeof(RectTransform));
            templateGo.transform.SetParent(root, false);
            template = templateGo.transform;
            RectTransform templateRect = template as RectTransform;
            templateRect.anchorMin = new Vector2(0f, 1f);
            templateRect.anchorMax = new Vector2(0f, 1f);
            templateRect.pivot = new Vector2(0.5f, 0.5f);
            templateRect.anchoredPosition = new Vector2(80f, -80f);
            templateRect.sizeDelta = new Vector2(78f, 78f);
        }
        MapNodeView nodeTemplate = EnsureNodeView(template.gameObject);
        template.gameObject.SetActive(false);

        panel.scrollRect = scrollView;
        panel.content = content as RectTransform;
        panel.lineLayer = lines as RectTransform;
        panel.nodeLayer = nodes as RectTransform;
        panel.legend = legend as RectTransform;
        panel.nodeTemplate = nodeTemplate;
    }

    public static void EnsureRest(RestPanel panel)
    {
        if (panel == null) return;
        Transform box = panel.transform.Find("Box");
        bool created = box == null;
        if (created)
        {
            Stretch(panel.transform as RectTransform);
            Image dim = GetOrAdd<Image>(panel.gameObject);
            dim.color = new Color(0.04f, 0.05f, 0.08f, 0.82f);
            var boxGo = new GameObject("Box", typeof(RectTransform), typeof(Image));
            boxGo.transform.SetParent(panel.transform, false);
            box = boxGo.transform;
            RectTransform boxRect = box as RectTransform;
            boxRect.sizeDelta = new Vector2(640f, 420f);
            boxRect.anchorMin = boxRect.anchorMax = new Vector2(0.5f, 0.5f);
            boxGo.GetComponent<Image>().color = new Color(0.12f, 0.14f, 0.18f, 1f);
        }

        panel.titleText = EnsureLabel(box, "Title", "휴식", 36, new Vector2(0f, 140f), new Vector2(520f, 70f), created);
        panel.descText = EnsureLabel(box, "Description", "체력을 회복하거나 주사위를 파괴합니다.", 22, new Vector2(0f, 70f), new Vector2(520f, 80f), created);
        panel.healButton = EnsureButton(box, "HealButton", "체력 회복", new Vector2(0f, -20f), created);
        panel.destroyButton = EnsureButton(box, "DestroyButton", "주사위 파괴", new Vector2(0f, -110f), created);
    }

    public static void EnsureOpel(OpelPanel panel)
    {
        if (panel == null) return;
        Transform box = panel.transform.Find("Box");
        bool created = box == null;
        if (created)
        {
            Stretch(panel.transform as RectTransform);
            Image dim = GetOrAdd<Image>(panel.gameObject);
            dim.color = new Color(0.04f, 0.06f, 0.1f, 0.82f);
            var boxGo = new GameObject("Box", typeof(RectTransform), typeof(Image));
            boxGo.transform.SetParent(panel.transform, false);
            box = boxGo.transform;
            RectTransform boxRect = box as RectTransform;
            boxRect.sizeDelta = new Vector2(720f, 520f);
            boxRect.anchorMin = boxRect.anchorMax = new Vector2(0.5f, 0.5f);
            boxGo.GetComponent<Image>().color = new Color(0.12f, 0.14f, 0.2f, 1f);
        }

        panel.titleText = EnsureLabel(box, "Title", "오펠", 36, new Vector2(0f, 190f), new Vector2(600f, 70f), created);
        panel.jupiterButton = EnsureButton(box, "JupiterButton", "목성", new Vector2(0f, 80f), created);
        panel.marsButton = EnsureButton(box, "MarsButton", "화성", new Vector2(0f, 0f), created);
        panel.mercuryButton = EnsureButton(box, "MercuryButton", "수성", new Vector2(0f, -80f), created);
        panel.venusButton = EnsureButton(box, "VenusButton", "금성", new Vector2(0f, -160f), created);
    }

    static void EnsureMapDim(RectTransform root)
    {
        Transform dim = root.Find("MapDim");
        if (dim == null)
        {
            var dimGo = new GameObject("MapDim", typeof(RectTransform), typeof(Image));
            dimGo.transform.SetParent(root, false);
            dim = dimGo.transform;
            RectTransform dimRect = dim as RectTransform;
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.offsetMin = Vector2.zero;
            dimRect.offsetMax = Vector2.zero;
            Image image = dimGo.GetComponent<Image>();
            image.raycastTarget = false;
        }
        Image dimImage = dim.GetComponent<Image>();
        if (dimImage != null)
        {
            dimImage.color = new Color(0f, 0f, 0f, 0.52f);
            dimImage.raycastTarget = false;
        }
        dim.SetAsFirstSibling();
    }

    static void EnsureLegendSlots(Transform legend)
    {
        string[] names = { "Enemy", "Boss", "Encounter", "Shop", "Opel", "Rest" };
        string[] labels = { "적", "보스", "조우자", "상인", "오펠", "휴식" };
        for (int i = 0; i < names.Length; i++)
        {
            Transform item = legend.Find(names[i]);
            bool created = item == null;
            if (created)
            {
                var itemGo = new GameObject(names[i], typeof(RectTransform));
                itemGo.transform.SetParent(legend, false);
                item = itemGo.transform;
            }
            RectTransform rect = item as RectTransform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.sizeDelta = new Vector2(-16f, 48f);
            rect.anchoredPosition = new Vector2(8f, 8f + i * 52f);

            if (item.Find("Icon") == null)
            {
                var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                iconGo.transform.SetParent(item, false);
                RectTransform iconRect = iconGo.GetComponent<RectTransform>();
                iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, 0.5f);
                iconRect.sizeDelta = new Vector2(36f, 36f);
                iconRect.anchoredPosition = new Vector2(18f, 0f);
            }

            if (item.Find("Label") == null)
            {
                Text label = EnsureLabel(item, "Label", labels[i], 22, new Vector2(120f, 0f), new Vector2(180f, 40f), true);
                label.alignment = TextAnchor.MiddleLeft;
            }
        }
    }

    static MapNodeView EnsureNodeView(GameObject target)
    {
        RectTransform rect = target.GetComponent<RectTransform>();
        if (rect == null) rect = target.AddComponent<RectTransform>();
        Image icon = GetOrAdd<Image>(target);
        icon.color = Color.white;
        Button button = GetOrAdd<Button>(target);
        button.targetGraphic = icon;

        Transform ring = target.transform.Find("Ring");
        bool newRing = ring == null;
        if (newRing)
        {
            var ringGo = new GameObject("Ring", typeof(RectTransform));
            ringGo.transform.SetParent(target.transform, false);
            ring = ringGo.transform;
            RectTransform ringRect = ring as RectTransform;
            ringRect.anchorMin = Vector2.zero;
            ringRect.anchorMax = Vector2.one;
            ringRect.offsetMin = new Vector2(-8f, -8f);
            ringRect.offsetMax = new Vector2(8f, 8f);
            ring.SetAsFirstSibling();
        }
        Image ringImage = GetOrAdd<Image>(ring.gameObject);
        if (newRing)
        {
            ringImage.raycastTarget = false;
            ringImage.color = new Color(1f, 0.92f, 0.4f, 0.9f);
        }

        MapNodeView view = target.GetComponent<MapNodeView>();
        if (view == null) view = target.AddComponent<MapNodeView>();
        view.button = button;
        view.icon = icon;
        view.ring = ringImage;
        return view;
    }

    static Text EnsureLabel(Transform parent, string name, string message, int size, Vector2 position, Vector2 area, bool place)
    {
        Transform found = parent.Find(name);
        bool created = found == null;
        if (created)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            found = go.transform;
        }
        Text text = GetOrAdd<Text>(found.gameObject);
        if (created || place)
        {
            RectTransform rect = found as RectTransform;
            rect.sizeDelta = area;
            rect.anchoredPosition = position;
            text.font = MapPanel.BuiltinFont();
            text.fontSize = size;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
        }
        if (string.IsNullOrEmpty(text.text))
            text.text = message;
        return text;
    }

    static Button EnsureButton(Transform parent, string name, string label, Vector2 position, bool place)
    {
        Transform found = parent.Find(name);
        bool created = found == null;
        if (created)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            found = go.transform;
        }
        Image image = GetOrAdd<Image>(found.gameObject);
        Button button = GetOrAdd<Button>(found.gameObject);
        if (created || place)
        {
            RectTransform rect = found as RectTransform;
            rect.sizeDelta = new Vector2(460f, 64f);
            rect.anchoredPosition = position;
            image.color = new Color(0.22f, 0.28f, 0.36f, 1f);
        }
        button.targetGraphic = image;
        Text text = EnsureLabel(found, "Text", label, 26, Vector2.zero, new Vector2(460f, 64f), created || place);
        if (created || place)
        {
            RectTransform textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            textRect.anchoredPosition = Vector2.zero;
        }
        return button;
    }

    static T GetOrAdd<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        if (component == null) component = target.AddComponent<T>();
        return component;
    }

    static void Stretch(RectTransform rect)
    {
        if (rect == null) return;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }
}
