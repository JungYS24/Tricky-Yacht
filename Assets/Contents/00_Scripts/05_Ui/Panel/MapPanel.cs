using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Serialization;
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
    // 노드 설명 패널(Node_Des). 예전 필드 이름 legend의 씬 연결을 유지한다.
    [FormerlySerializedAs("legend")]
    public RectTransform nodeDes;
    public Button journeyRollButton;
    public Image journeyResultImage;
    // 여정 주사위 보드 이미지(Journey_Result_Slot 배경). 빌드에서는 여기 연결한 값만 쓴다.
    public Sprite journeyBoardSprite;

    const string PlayerMarkerKey = "Icon_Map_Player";

    // 여정 주사위 굴림 결과가 적용된 직후(튜토리얼 안내용)
    public static event System.Action<JourneyRollResult> JourneyRolled;

    Sprite dotSprite;
    DiceManager boundDice;
    Coroutine journeyRollRoutine;
    bool journeyRolling;
    Tween resultPopupTween;
    GameObject resultPopupObject;

    // 칸 배치 간격(px)
    const float ColSpacing = 168f;
    const float RowSpacing = 128f;
    const float LeftPad = 280f;
    const float RightPad = 80f;
    const float BottomPad = 52f;
    const float TopPad = 80f;

    // 점선: 선 하나를 이미지 하나로 그리고 점 무늬를 반복(Tiled)해 깐다.
    const float DotSpacing = 18f;
    const float DotSize = 8f;
    static readonly Color DotColor = new Color(0.85f, 0.9f, 0.95f, 0.85f);

    // 지금 그려 둔 지도. 같은 지도면 점선과 칸 버튼을 다시 만들지 않고 칸 상태만 바꾼다.
    MapData builtMap;
    RectTransform builtLineLayer;
    RectTransform builtNodeLayer;
    readonly Dictionary<int, Vector2> nodePositions = new Dictionary<int, Vector2>();
    readonly Dictionary<int, MapNodeView> nodeViews = new Dictionary<int, MapNodeView>();
    readonly HashSet<int> selectableNodes = new HashSet<int>();

    public void Hide()
    {
        StopJourneyRoll();
        gameObject.SetActive(false);
    }

    public void Show(DiceManager diceManager)
    {
        StopJourneyRoll();
        boundDice = diceManager;
        gameObject.SetActive(true);
        // 굴리기 버튼은 전투의 Reroll_BTN을 복제해 쓴다.
        Button combatRoll = diceManager != null && diceManager.ui != null ? diceManager.ui.rollButton : null;
        MapUiHierarchy.EnsureMap(this, combatRoll);
        Rebuild(diceManager);
    }

    public void OnJourneyRoll()
    {
        if (journeyRolling || boundDice == null || !boundDice.CanRollJourney()) return;
        KillResultPopup(); // 팝업이 떠 있는 동안 다시 굴리면 이전 트윈은 끄고 새로 띄운다
        journeyRolling = true;
        if (journeyRollButton != null) journeyRollButton.interactable = false;
        journeyRollRoutine = StartCoroutine(PlayJourneyRoll());
    }

    void Rebuild(DiceManager diceManager)
    {
        if (lineLayer == null || nodeLayer == null || content == null) return;

        MapData map = diceManager.stageProgression.currentMap;
        MapTableData table = diceManager.GetMapTable();
        if (map == null || table == null)
        {
            ClearMap();
            return;
        }

        MapConfig cfg = table.Config;
        FitScrollToBottom();
        float width = LeftPad + RightPad + Mathf.Max(0, cfg.GridWidth - 1) * ColSpacing;
        float height = BottomPad + TopPad + cfg.MaxPathLength * RowSpacing;
        content.sizeDelta = new Vector2(width, height);
        FitLayer(lineLayer, content.sizeDelta);
        FitLayer(nodeLayer, content.sizeDelta);

        // 점선과 칸 버튼은 지도가 바뀔 때만 새로 만든다. 같은 지도면 칸 상태(갈 수 있음·현재·방문)만 바꾼다.
        if (NeedsBuild(map))
            BuildMap(map);
        RefreshNodes(diceManager, map, table);

        RefreshNodeDes(table);
        PlaceNodeDes();
        RefreshJourneyChrome(diceManager);
        Canvas.ForceUpdateCanvases();
        FocusOnPlayer(diceManager, nodePositions);
    }

    bool NeedsBuild(MapData map)
    {
        if (map != builtMap || lineLayer != builtLineLayer || nodeLayer != builtNodeLayer) return true;
        if (nodeViews.Count != map.nodes.Count) return true;
        // 다른 곳에서 칸 버튼이 지워졌으면 다시 만든다.
        foreach (KeyValuePair<int, MapNodeView> pair in nodeViews)
        {
            if (pair.Value == null) return true;
        }
        return false;
    }

    void ClearMap()
    {
        ClearChildren(lineLayer);
        ClearChildren(nodeLayer);
        nodeViews.Clear();
        nodePositions.Clear();
        builtMap = null;
    }

    // 지도 한 장의 칸 위치, 점선, 칸 버튼을 만든다. 칸 위치의 흔들림은 시드로 정해져 매번 같다.
    void BuildMap(MapData map)
    {
        ClearMap();
        builtMap = map;
        builtLineLayer = lineLayer;
        builtNodeLayer = nodeLayer;

        for (int i = 0; i < map.nodes.Count; i++)
        {
            MapNode node = map.nodes[i];
            var jitter = new System.Random(map.seed + node.id * 17);
            float jx = ((float)jitter.NextDouble() * 2f - 1f) * 16f;
            float jy = ((float)jitter.NextDouble() * 2f - 1f) * 10f;
            nodePositions[node.id] = new Vector2(LeftPad + node.col * ColSpacing + jx, BottomPad + node.row * RowSpacing + jy);
        }

        for (int i = 0; i < map.nodes.Count; i++)
        {
            MapNode node = map.nodes[i];
            for (int c = 0; c < node.children.Count; c++)
            {
                int childId = node.children[c];
                if (!nodePositions.ContainsKey(childId)) continue;
                DrawDottedLine(nodePositions[node.id], nodePositions[childId]);
            }
        }

        for (int i = 0; i < map.nodes.Count; i++)
        {
            MapNode node = map.nodes[i];
            nodeViews[node.id] = CreateNode(nodePositions[node.id]);
        }
    }

    MapNodeView CreateNode(Vector2 position)
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
        return view;
    }

    // 칸 버튼마다 그림·갈 수 있음·현재 칸·방문 표시를 다시 입힌다. 갈 수 있는 칸은 한 번의 탐색으로 모은다.
    void RefreshNodes(DiceManager diceManager, MapData map, MapTableData table)
    {
        StageManager stage = diceManager.stageProgression;
        diceManager.MapFlow.CollectSelectableNodes(selectableNodes);
        for (int i = 0; i < map.nodes.Count; i++)
        {
            MapNode node = map.nodes[i];
            if (!nodeViews.TryGetValue(node.id, out MapNodeView view) || view == null) continue;

            MapNodeTypeRow rule = table.GetNodeRule(node.type);
            string iconKey = rule != null ? rule.Icon_Key : null;
            Color tint = Color.white;
            Sprite sprite = null;
            if (iconSet != null)
                sprite = iconSet.GetSprite(iconKey, out tint);
            bool selectable = selectableNodes.Contains(node.id);
            bool current = node.id == stage.currentNodeId;
            bool visited = ListHas(stage.visitedNodeIds, node.id) || ListHas(stage.passedNodeIds, node.id);
            view.Bind(diceManager, node.id, sprite, tint, selectable, current, visited);
            if (current && iconSet != null)
                view.SetCurrentMarker(iconSet.GetSprite(PlayerMarkerKey, out _));
            // Bind가 크기를 1로 되돌리므로 보스 칸 크기는 그 뒤에 다시 키운다.
            if (node.type == MapNodeType.Boss)
                view.transform.localScale = new Vector3(2.8f, 2.8f, 1f);
        }
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
        MapUiHierarchy.Stretch(ringRect);
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

    void RefreshNodeDes(MapTableData table)
    {
        if (nodeDes == null || table == null) return;
        for (int i = 0; i < MapLegend.Retired.Length; i++)
        {
            Transform retired = nodeDes.Find(MapLegend.Retired[i]);
            if (retired != null) retired.gameObject.SetActive(false);
        }
        for (int i = 0; i < MapLegend.Names.Length; i++)
        {
            Transform item = nodeDes.Find(MapLegend.Names[i]);
            if (item == null) continue;
            item.gameObject.SetActive(true);
            MapNodeTypeRow rule = table.GetNodeRule(MapLegend.Types[i]);
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
            if (label != null) label.text = table.GetNodeName(MapLegend.Types[i]);
        }
    }

    void FocusOnPlayer(DiceManager diceManager, Dictionary<int, Vector2> positions)
    {
        if (scrollRect == null || content == null) return;
        float targetY = 0f;
        bool found = false;
        int currentId = diceManager.stageProgression.currentNodeId;
        if (currentId >= 0 && positions.ContainsKey(currentId))
        {
            targetY = positions[currentId].y;
            found = true;
        }
        else
        {
            MapData map = diceManager.stageProgression.currentMap;
            for (int i = 0; i < map.nodes.Count; i++)
            {
                MapNode node = map.nodes[i];
                if (node.row != 0 || !positions.ContainsKey(node.id)) continue;
                if (!found || positions[node.id].y < targetY)
                {
                    targetY = positions[node.id].y;
                    found = true;
                }
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

    // 노드 설명 패널: 오른쪽 위에 두고, 항목은 MapLegend 순서대로 위에서 아래로 쌓는다.
    // 패널 위치는 설정 버튼(Setting_BTN) 바로 아래로 MapUiHierarchy가 맞춘다.
    void PlaceNodeDes()
    {
        if (nodeDes == null) return;
        nodeDes.anchorMin = new Vector2(1f, 1f);
        nodeDes.anchorMax = new Vector2(1f, 1f);
        nodeDes.pivot = new Vector2(1f, 1f);
        const float rowStep = 42f;
        int shown = 0;
        for (int i = 0; i < MapLegend.Names.Length; i++)
        {
            Transform item = nodeDes.Find(MapLegend.Names[i]);
            if (item == null) continue;
            item.gameObject.SetActive(true);
            RectTransform rect = item as RectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(-16f, 40f);
            rect.anchoredPosition = new Vector2(8f, -(8f + shown * rowStep));
            shown++;
        }
        nodeDes.sizeDelta = new Vector2(240f, 16f + shown * rowStep);
        MapUiHierarchy.PlaceNodeDesBelowSetting(nodeDes);
    }

    void RefreshJourneyChrome(DiceManager diceManager)
    {
        if (journeyRollButton != null)
        {
            Text label = journeyRollButton.GetComponentInChildren<Text>();
            if (label != null)
                label.text = LocalizationManager.GetUi("UI_MAP_JOURNEY_ROLL", "굴리기");
            journeyRollButton.interactable = !journeyRolling && diceManager.CanRollJourney();
        }
        if (journeyRolling) return;
        int pending = diceManager.stageProgression.pendingJourneyMove;
        if (pending > 0)
        {
            // 대기 중인 이동 칸 수는 면 번호(1~3)와 같으므로 그 면 스프라이트를 다시 보여 준다.
            Sprite pendingFace = diceManager.GetJourneyFaceSprite(diceManager.GetJourneyDiceData(), pending - 1);
            SetJourneyResult(pendingFace);
        }
        else
            SetJourneyResult(null);
    }

    // 결과 슬롯: 보드 위에 굴린 면 스프라이트만 크게 보여 준다. 스프라이트가 없으면 면 이미지를 끈다.
    void SetJourneyResult(Sprite face)
    {
        if (journeyResultImage == null) return;
        journeyResultImage.sprite = face;
        journeyResultImage.enabled = face != null;
    }

    IEnumerator PlayJourneyRoll()
    {
        journeyRolling = true;
        if (journeyRollButton != null) journeyRollButton.interactable = false;
        Sfx.Play(SfxId.Journey_Roll);
        MapTableData table = boundDice != null ? boundDice.GetMapTable() : null;
        float seconds = table != null ? Mathf.Max(0.05f, table.Config.JourneyRollSeconds) : 0.5f;
        float elapsed = 0f;
        // 굴리는 동안 여정 주사위의 면 스프라이트를 빠르게 바꿔 보여 준다.
        const float faceSwapInterval = 0.06f;
        float swapTimer = faceSwapInterval;
        while (elapsed < seconds)
        {
            swapTimer += Time.unscaledDeltaTime;
            if (boundDice != null && swapTimer >= faceSwapInterval)
            {
                swapTimer = 0f;
                SetJourneyResult(boundDice.RandomJourneyFaceSprite());
            }
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        bool move = false;
        int applied = 0;
        bool flyGold = false;
        JourneyRollResult result = default;
        if (boundDice != null)
        {
            result = boundDice.RollJourneyFace();
            // 코인이면 화면 골드 숫자는 코인 날아가기 연출이 올린다(연출을 쓸 수 있을 때만, 실제 골드는 바로 오른다).
            flyGold = result.outcome == JourneyOutcome.Gold && CoinFlyFeedback.CanPlay(boundDice.shopManager);
            move = boundDice.ApplyJourneyResult(result, out applied, flyGold);
        }
        journeyRolling = false;
        journeyRollRoutine = null;
        if (boundDice == null) yield break;

        SetJourneyResult(result.faceSprite);
        PlayJourneyOutcomeFeedback(result, applied, flyGold);
        JourneyRolled?.Invoke(result);
        if (move)
            Rebuild(boundDice);
        else if (journeyRollButton != null)
            journeyRollButton.interactable = boundDice.CanRollJourney();
    }

    void StopJourneyRoll()
    {
        if (journeyRollRoutine != null)
            StopCoroutine(journeyRollRoutine);
        journeyRollRoutine = null;
        journeyRolling = false;
        KillResultPopup();
    }

    // 해골·하트·코인 결과 연출. 이동 눈은 연출 없이 둔다. 숫자는 실제로 적용된 값이다.
    void PlayJourneyOutcomeFeedback(JourneyRollResult result, int applied, bool flyGold)
    {
        switch (result.outcome)
        {
            case JourneyOutcome.Damage:
                // 해골: 검정 글씨에 흰 아웃라인. 화면 붉어짐과 피격음은 DiceManager가 이미 재생했다.
                if (applied > 0) ShowResultPopup("-" + applied, Color.black, new Color(1f, 1f, 1f, 0.95f));
                break;
            case JourneyOutcome.Heal:
                if (applied > 0) ShowResultPopup("+" + applied, new Color(0.95f, 0.2f, 0.2f, 1f), new Color(0.1f, 0.03f, 0.03f, 0.95f));
                break;
            case JourneyOutcome.Gold:
                if (applied > 0)
                {
                    ShowResultPopup("+" + applied, new Color(1f, 0.86f, 0.15f, 1f), new Color(0.15f, 0.1f, 0f, 0.95f));
                    // 코인 아이콘이 주사위 면 위치에서 튀어나와 골드 패널로 날아간다(눈 값만큼).
                    if (flyGold && journeyResultImage != null)
                        CoinFlyFeedback.Play(journeyResultImage.transform.position, Mathf.Max(1, result.pips), applied);
                    else if (flyGold)
                        CoinFlyFeedback.RefreshCounter(boundDice.shopManager);
                }
                else if (flyGold)
                    CoinFlyFeedback.RefreshCounter(boundDice.shopManager);
                break;
        }
    }

    // 주사위 면 바로 위에 숫자를 띄운다. 0.35초 동안 0.6→1.15→1 크기로 커지며 60px 위로 올라가고, 0.25초 페이드아웃 뒤 지운다.
    void ShowResultPopup(string message, Color textColor, Color outlineColor)
    {
        KillResultPopup();
        if (journeyResultImage == null) return;

        Transform slot = journeyResultImage.transform.parent;
        var go = new GameObject("Journey_Result_Popup", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(slot, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(260f, 80f);
        RectTransform faceRect = journeyResultImage.rectTransform;
        float startY = faceRect.anchoredPosition.y + faceRect.sizeDelta.y * 0.5f + 36f;
        rect.anchoredPosition = new Vector2(0f, startY);
        rect.localScale = Vector3.one * 0.6f;

        Text text = go.GetComponent<Text>();
        text.font = MapUiHierarchy.BuiltinFont();
        text.fontSize = 56;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        text.color = textColor;
        text.text = message;

        // 보드 위에서도 읽히도록 어두운(또는 밝은) 아웃라인을 넣는다. 글자 알파를 따라 같이 사라진다.
        Outline outline = go.AddComponent<Outline>();
        outline.effectColor = outlineColor;
        outline.effectDistance = new Vector2(3f, -3f);

        resultPopupObject = go;
        resultPopupTween = DOTween.Sequence().SetUpdate(true).SetLink(go);
        var seq = (Sequence)resultPopupTween;
        seq.Append(rect.DOScale(1.15f, 0.2f).SetEase(Ease.OutQuad));
        seq.Append(rect.DOScale(1f, 0.15f).SetEase(Ease.InOutQuad));
        seq.Insert(0f, rect.DOAnchorPosY(startY + 60f, 0.35f).SetEase(Ease.OutQuad));
        seq.Append(text.DOFade(0f, 0.25f));
        seq.OnComplete(() =>
        {
            if (go != null) Destroy(go);
            if (resultPopupObject == go) resultPopupObject = null;
            resultPopupTween = null;
        });
    }

    void KillResultPopup()
    {
        if (resultPopupTween != null && resultPopupTween.IsActive()) resultPopupTween.Kill();
        resultPopupTween = null;
        if (resultPopupObject != null) Destroy(resultPopupObject);
        resultPopupObject = null;
    }

    void OnDisable()
    {
        KillResultPopup();
    }

    // 실행 중에 만든 점 무늬 텍스처는 씬이 바뀌어도 자동으로 지워지지 않을 수 있어 직접 지운다.
    void OnDestroy()
    {
        if (dotSprite == null) return;
        Texture2D texture = dotSprite.texture;
        Destroy(dotSprite);
        if (texture != null) Destroy(texture);
        dotSprite = null;
    }

    static bool ListHas(List<int> ids, int id)
    {
        return ids != null && ids.Contains(id);
    }

    // 두 칸 사이 점선. 예전에는 점마다 오브젝트를 만들었지만, 이제 선 하나를 이미지 하나로 두고
    // 점 하나가 들어간 18px 무늬를 반복해 깐다(Tiled). 점 크기·색은 예전과 같고 간격만 18px로 고정된다.
    void DrawDottedLine(Vector2 from, Vector2 to)
    {
        Vector2 delta = to - from;
        float distance = delta.magnitude;
        if (distance < 1f) return;
        if (dotSprite == null) dotSprite = MakeDotTile(lineLayer);

        var go = new GameObject("Line", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(lineLayer, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = from;
        rect.sizeDelta = new Vector2(distance, DotSize);
        rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        var image = go.GetComponent<Image>();
        image.sprite = dotSprite;
        image.type = Image.Type.Tiled;
        image.color = DotColor;
        image.raycastTarget = false;
    }

    // 가로 18px × 세로 8px 무늬 한 칸. 가운데에 예전과 같은 지름 8px 점이 하나 있다.
    // 반복(Repeat)으로 두면 Tiled 이미지가 선 하나를 사각형 하나로 그린다.
    static Sprite MakeDotTile(Transform layer)
    {
        int width = Mathf.RoundToInt(DotSpacing);
        int height = Mathf.RoundToInt(DotSize);
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Repeat;
        var pixels = new Color32[width * height];
        float centerX = width * 0.5f - 0.5f;
        float centerY = height * 0.5f - 0.5f;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float dx = x - centerX;
                float dy = y - centerY;
                pixels[y * width + x] = dx * dx + dy * dy <= 10f ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply();
        // 캔버스 기준 PPU에 맞추면 무늬 한 칸이 화면에서 정확히 18×8로 깔린다.
        Canvas canvas = layer != null ? layer.GetComponentInParent<Canvas>() : null;
        float ppu = canvas != null ? canvas.referencePixelsPerUnit : 100f;
        return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), ppu, 0, SpriteMeshType.FullRect);
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

}
