using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public static class MapLegend
{
    public static readonly string[] Names =
    {
        "Enemy", "Elite", "Boss", "Encounter", "ShopNyaon", "ShopRonan", "ShopOpel", "Portal", "Rest"
    };

    public static readonly string[] Labels =
    {
        "적", "엘리트", "보스", "조우자", "냐온", "로넌", "오펠", "차원문", "휴식"
    };

    public static readonly MapNodeType[] Types =
    {
        MapNodeType.Enemy, MapNodeType.Elite, MapNodeType.Boss, MapNodeType.Encounter,
        MapNodeType.ShopNyaon, MapNodeType.ShopRonan, MapNodeType.ShopOpel, MapNodeType.Portal, MapNodeType.Rest
    };

    public static readonly string[] Retired = { "Shop", "Opel" };
}

// 씬에 없는 지도 UI만 채웁니다. 이미 하이어라키에 있으면 그 오브젝트를 그대로 씁니다.
public static class MapUiHierarchy
{
    // rollTemplate: 전투의 Reroll_BTN. 주면 지도 굴리기 버튼을 그 모양·위치로 복제해 쓴다(에디터 세팅처럼 없으면 기본 박스).
    public static void EnsureMap(MapPanel panel, Button rollTemplate = null)
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

        // 노드 설명 패널. 씬에 예전 이름(Legend)이 남아 있으면 이름만 바꿔 그대로 쓴다.
        Transform nodeDes = root.Find("Node_Des");
        if (nodeDes == null)
        {
            Transform oldLegend = root.Find("Legend");
            if (oldLegend != null)
            {
                oldLegend.name = "Node_Des";
                nodeDes = oldLegend;
            }
        }
        if (nodeDes == null)
        {
            var nodeDesGo = new GameObject("Node_Des", typeof(RectTransform));
            nodeDesGo.transform.SetParent(root, false);
            nodeDes = nodeDesGo.transform;
            RectTransform nodeDesRect = nodeDes as RectTransform;
            nodeDesRect.anchorMin = new Vector2(1f, 1f);
            nodeDesRect.anchorMax = new Vector2(1f, 1f);
            nodeDesRect.pivot = new Vector2(1f, 1f);
            nodeDesRect.sizeDelta = new Vector2(240f, 300f);
            nodeDesRect.anchoredPosition = NodeDesFallbackPosition;
        }
        EnsureNodeDesSlots(nodeDes);
        EnsureJourneyControls(panel, root, rollTemplate);

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
        panel.nodeDes = nodeDes as RectTransform;
        panel.nodeTemplate = nodeTemplate;
    }

    public static void EnsureRest(RestPanel panel)
    {
        if (panel == null) return;
        if (panel.titleText != null && panel.healButton != null && panel.destroyButton != null)
            return;
        Debug.LogWarning("[Map] RestPanel 하이어라키가 씬에 없습니다. MainScene_Map의 배치를 확인하세요.");
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

    static void EnsureJourneyControls(MapPanel panel, RectTransform root, Button rollTemplate)
    {
        Transform slot = root.Find("Journey_Result_Slot");
        if (slot == null)
        {
            var slotGo = new GameObject("Journey_Result_Slot", typeof(RectTransform), typeof(Image));
            slotGo.transform.SetParent(root, false);
            slot = slotGo.transform;
            Image image = slotGo.GetComponent<Image>();
            image.raycastTarget = false;
        }
        RectTransform slotRect = slot as RectTransform;

        // 결과 글자는 쓰지 않는다. 씬에 남은 Text 자식은 지우고 다시 만들지 않는다.
        Transform oldResultText = slot.Find("Text");
        if (oldResultText != null) DestroyObject(oldResultText.gameObject);

        // 슬롯 배경은 여정 주사위 보드 이미지다.
        Image slotImage = GetOrAdd<Image>(slot.gameObject);
        Sprite board = panel.journeyBoardSprite;
#if UNITY_EDITOR
        // 필드가 비어 있을 때만 에디터에서 직접 찾는다(빌드에서는 인스펙터 연결 값만 쓴다).
        if (board == null) board = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(JourneyBoardEditorPath);
#endif
        slotImage.sprite = board;
        // 보드가 없으면 빈 흰 사각형이 보이지 않도록 어두운 배경으로 대신한다.
        slotImage.color = board != null ? Color.white : new Color(0.08f, 0.1f, 0.14f, 0.92f);
        slotImage.preserveAspect = true;
        slotImage.raycastTarget = false;

        Transform face = slot.Find("Face");
        if (face == null)
        {
            var faceGo = new GameObject("Face", typeof(RectTransform), typeof(Image));
            faceGo.transform.SetParent(slot, false);
            face = faceGo.transform;
        }
        // 주사위 면은 보드 가운데 초록 부분을 크게 채운다(크기는 슬롯 한 변의 62%, 슬롯 크기는 버튼 배치 뒤에 정한다).
        RectTransform faceRect = face as RectTransform;
        faceRect.anchorMin = new Vector2(0.5f, 0.5f);
        faceRect.anchorMax = new Vector2(0.5f, 0.5f);
        faceRect.pivot = new Vector2(0.5f, 0.5f);
        faceRect.anchoredPosition = Vector2.zero;
        Image faceImage = face.GetComponent<Image>();
        faceImage.preserveAspect = true;
        faceImage.raycastTarget = false;
        faceImage.enabled = faceImage.sprite != null;

        Transform button = root.Find("Journey_Roll_BTN");
        if (rollTemplate != null)
        {
            // 전투 Reroll_BTN을 그대로 복제해 같은 모양·위치에 둔다.
            button = EnsureCombatStyleRollButton(root, button, rollTemplate);
        }
        else
        {
            bool created = button == null;
            if (created)
            {
                var buttonGo = new GameObject("Journey_Roll_BTN", typeof(RectTransform));
                buttonGo.transform.SetParent(root, false);
                button = buttonGo.transform;
            }
            RectTransform buttonRect = button as RectTransform;
            buttonRect.anchorMin = new Vector2(1f, 0f);
            buttonRect.anchorMax = new Vector2(1f, 0f);
            buttonRect.pivot = new Vector2(1f, 0f);
            buttonRect.sizeDelta = new Vector2(220f, 64f);
            buttonRect.anchoredPosition = new Vector2(-28f, 28f);
            Image buttonImage = GetOrAdd<Image>(button.gameObject);
            buttonImage.color = new Color(0.22f, 0.28f, 0.36f, 1f);
            Button plainRoll = GetOrAdd<Button>(button.gameObject);
            plainRoll.targetGraphic = buttonImage;
            Text label = EnsureLabel(button, "Text", LocalizationManager.GetUi("UI_MAP_JOURNEY_ROLL", "굴리기"), 26, Vector2.zero, new Vector2(200f, 64f), true);
            RectTransform labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
        }
        Button roll = button.GetComponent<Button>();

        // 결과 슬롯은 정사각형 보드다. 한 변은 굴리기 버튼의 화면 가로, 위치는 버튼 바로 위 12px(가로 중심은 버튼과 같음).
        RectTransform buttonRectForSlot = button as RectTransform;
        Rect buttonBounds = LocalBounds(root, buttonRectForSlot);
        float side = buttonBounds.width;
        slotRect.anchorMin = new Vector2(0.5f, 0.5f);
        slotRect.anchorMax = new Vector2(0.5f, 0.5f);
        slotRect.pivot = new Vector2(0.5f, 0.5f);
        slotRect.sizeDelta = new Vector2(side, side);
        slotRect.anchoredPosition = new Vector2(
            buttonBounds.center.x - root.rect.center.x,
            buttonBounds.yMax + 12f + side * 0.5f - root.rect.center.y);
        RectTransform faceRectForSlot = face as RectTransform;
        faceRectForSlot.sizeDelta = new Vector2(side * 0.62f, side * 0.62f);

        slot.SetAsLastSibling();
        button.SetAsLastSibling();
        panel.journeyResultImage = faceImage;
        panel.journeyRollButton = roll;
        roll.onClick.RemoveAllListeners();
        roll.onClick.AddListener(panel.OnJourneyRoll);
    }

    // Setting_BTN을 못 찾았을 때 노드 설명 패널을 두는 오른쪽 위 기준 위치
    static readonly Vector2 NodeDesFallbackPosition = new Vector2(-28f, -120f);
    const float NodeDesGap = 12f;
#if UNITY_EDITOR
    const string JourneyBoardEditorPath = "Assets/Contents/06_UI/Sprites/JourneyDice_Board_512.png";
#endif

    // 노드 설명 패널을 설정 버튼(Setting_BTN) 바로 아래, 오른쪽 끝을 맞춰 둔다.
    // 설정 버튼은 HUD 쪽 오브젝트라 부모가 달라서 월드 좌표로 옮겨 계산한다.
    public static void PlaceNodeDesBelowSetting(RectTransform nodeDes)
    {
        if (nodeDes == null) return;
        RectTransform root = nodeDes.parent as RectTransform;
        nodeDes.anchorMin = new Vector2(1f, 1f);
        nodeDes.anchorMax = new Vector2(1f, 1f);
        nodeDes.pivot = new Vector2(1f, 1f);

        RectTransform setting = FindSettingButton(root);
        if (root == null || setting == null || root.lossyScale.x <= 0.0001f)
        {
            nodeDes.anchoredPosition = NodeDesFallbackPosition;
            return;
        }

        Rect bounds = LocalBounds(root, setting);
        // 오른쪽 끝은 설정 버튼의 오른쪽 끝, 위쪽 끝은 설정 버튼 아래쪽 끝에서 12px 아래.
        nodeDes.anchoredPosition = new Vector2(
            bounds.xMax - root.rect.xMax,
            bounds.yMin - NodeDesGap - root.rect.yMax);
    }

    static RectTransform FindSettingButton(RectTransform root)
    {
        if (root == null) return null;
        Canvas canvas = root.GetComponentInParent<Canvas>();
        if (canvas == null) return null;
        RectTransform[] all = canvas.rootCanvas.GetComponentsInChildren<RectTransform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i].name == "Setting_BTN") return all[i];
        }
        return null;
    }

    // target의 화면 영역을 root 로컬 좌표(root 피벗 기준)의 Rect로 돌려준다. root 스케일은 포함하지 않는다.
    static Rect LocalBounds(RectTransform root, RectTransform target)
    {
        if (root.lossyScale.x <= 0.0001f)
        {
            // 캔버스 스케일이 아직 잡히지 않았을 때를 위한 대비: 앵커 기준 값으로 근사한다.
            Vector2 size = new Vector2(target.sizeDelta.x * target.localScale.x, target.sizeDelta.y * target.localScale.y);
            Vector2 center = target.anchoredPosition + root.rect.center;
            return new Rect(center - size * 0.5f, size);
        }
        var corners = new Vector3[4];
        target.GetWorldCorners(corners);
        Vector3 min = root.InverseTransformPoint(corners[0]);
        Vector3 max = root.InverseTransformPoint(corners[2]);
        return new Rect(min.x, min.y, max.x - min.x, max.y - min.y);
    }

    // 에디터 편집 중에는 Destroy를 쓸 수 없어서 즉시 삭제로 바꿔 쓴다.
    static void DestroyObject(GameObject target)
    {
        if (Application.isPlaying) Object.Destroy(target);
        else Object.DestroyImmediate(target);
    }

    // 전투 Reroll_BTN을 지도 패널 아래에 복제한다. 이미 복제본(TMP 라벨이 있는 버튼)이면 재사용하고 위치만 다시 맞춘다.
    static Transform EnsureCombatStyleRollButton(RectTransform root, Transform existing, Button template)
    {
        // 예전 기본 박스 버튼(UI Text 라벨)은 치운다.
        if (existing != null && existing.GetComponentInChildren<TMP_Text>(true) == null)
        {
            existing.name = "Journey_Roll_BTN_Old";
            existing.gameObject.SetActive(false);
            DestroyObject(existing.gameObject);
            existing = null;
        }

        if (existing == null)
        {
            GameObject clone = Object.Instantiate(template.gameObject, root, false);
            clone.name = "Journey_Roll_BTN";
            clone.SetActive(true);
            existing = clone.transform;
        }

        // 복제본에 딸려 온 전투용 저장 이벤트(DiceManager.OnRollButtonClick)는 꺼 둔다. 지도 굴리기는 코드에서 연결한다.
        Button cloneButton = existing.GetComponent<Button>();
        for (int i = cloneButton.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
            cloneButton.onClick.SetPersistentListenerState(i, UnityEventCallState.Off);

        // 전투 Reroll_BTN과 같은 화면 위치·크기에 맞춘다(같은 캔버스 안이라 월드 좌표로 옮겨 온다).
        RectTransform rect = existing as RectTransform;
        RectTransform source = template.transform as RectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = source.pivot;
        rect.sizeDelta = source.sizeDelta;
        rect.localScale = source.localScale;
        if (root.lossyScale.x > 0.0001f)
        {
            Vector3 local = root.InverseTransformPoint(source.position);
            rect.anchoredPosition = new Vector2(local.x, local.y) - root.rect.center;
        }
        else
            rect.anchoredPosition = source.anchoredPosition;
        return existing;
    }

    static void EnsureNodeDesSlots(Transform nodeDes)
    {
        for (int i = 0; i < MapLegend.Retired.Length; i++)
        {
            Transform retired = nodeDes.Find(MapLegend.Retired[i]);
            if (retired != null) retired.gameObject.SetActive(false);
        }

        string[] names = MapLegend.Names;
        string[] labels = MapLegend.Labels;
        for (int i = 0; i < names.Length; i++)
        {
            Transform item = nodeDes.Find(names[i]);
            bool created = item == null;
            if (created)
            {
                var itemGo = new GameObject(names[i], typeof(RectTransform));
                itemGo.transform.SetParent(nodeDes, false);
                item = itemGo.transform;
            }
            // 항목은 위에서 아래로 MapLegend 순서대로 쌓는다.
            RectTransform rect = item as RectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(-16f, 48f);
            rect.anchoredPosition = new Vector2(8f, -(8f + i * 52f));

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
