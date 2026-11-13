using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 덱에 들어 있는 디버프 주사위를 종류별 아이콘 + 개수로 보여 주는 HUD.
// 오른쪽 위 피규어 칸과 대칭으로 왼쪽 위에 가로로 놓이며, 칸 크기·간격·배율·높이를 피규어 칸에서 그대로 읽어 똑같이 맞춥니다.
// (전투 화면: 나무 패널 오른쪽, 피규어 줄과 같은 높이 / 지도 화면: 체력 칸 아래)
// 피규어처럼 틀 없이 주사위 그림만 두고, 뒤쪽 보랏빛이 숨 쉬듯 밝아졌다 어두워져 디버프임을 나타냅니다.
// 이번 전투 한정 디버프에는 모래시계가 붙고, 누르면 디버프 설명 패널이 열립니다. DiceManager가 실행 중에 만듭니다.
public sealed class DebuffTrackerHud : MonoBehaviour
{
    public static DebuffTrackerHud Instance { get; private set; }

    // 영구 디버프를 먼저, 이번 전투 한정을 뒤에 둡니다.
    static readonly DebuffDiceType[] Order =
    {
        DebuffDiceType.Fake, DebuffDiceType.Spine, DebuffDiceType.Curse, DebuffDiceType.Thief, DebuffDiceType.Shackle
    };

    static readonly string[] InnerPanelNames = { "CoinPanel", "HeartPanel", "Drop_Rate_Panel", "Stage_Panel", "slot_counter_chip (1)" };

    sealed class Icon
    {
        public RectTransform root;
        public Image glow;
        public Image die;
        public TextMeshProUGUI count;
        public int shown;
    }

    DiceManager manager;
    RectTransform rect;
    readonly Dictionary<DebuffDiceType, Icon> icons = new Dictionary<DebuffDiceType, Icon>();
    readonly Dictionary<DebuffDiceType, int> counts = new Dictionary<DebuffDiceType, int>();

    // 피규어 칸 기준값(없으면 프로필 값). Place에서 매번 다시 읽습니다.
    float cellSize = 70f;
    float spacing = 10f;

    static DebuffVisualProfile Profile => DebuffFeedback.Profile;

    public static DebuffTrackerHud Create(DiceManager owner)
    {
        if (Instance != null) return Instance;
        var go = new GameObject("Debuff Tracker", typeof(RectTransform));
        var hud = go.AddComponent<DebuffTrackerHud>();
        hud.manager = owner;
        hud.rect = (RectTransform)go.transform;
        hud.rect.pivot = new Vector2(0f, 0.5f);
        hud.rect.sizeDelta = Vector2.zero;
        Instance = hud;
        return hud;
    }

    void OnEnable() => DiceManager.OnDeckUpdateNeeded += Refresh;
    void OnDisable() => DiceManager.OnDeckUpdateNeeded -= Refresh;

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // 화면에 맞춰 붙일 자리를 다시 잡습니다. travel이 true면 지도 화면 배치입니다.
    public void Place(bool travel)
    {
        if (manager == null) return;
        RectTransform score = manager.HudRect("ScorePanel");
        RectTransform figures = manager.HudRect("FigureSlotArea");
        RectTransform anchorRef = figures != null ? figures : score;
        Transform parent = anchorRef != null ? anchorRef.parent : null;
        if (parent == null) return;
        rect.SetParent(parent, false);
        // 피규어 칸 바로 뒤에 두어, 상점·설정 같은 창이 뜨면 피규어 칸처럼 그 아래로 가려집니다.
        rect.SetSiblingIndex(Mathf.Min(parent.childCount - 1, anchorRef.GetSiblingIndex() + 1));
        MatchFigureSlots(figures);

        var p = Profile;
        float halfHeight = cellSize * rect.localScale.y * 0.5f;
        Vector3 local;
        RectTransform heart = manager.HudRect("HeartPanel");
        if (travel && heart != null)
        {
            Vector2 offset = p != null ? p.trackerMapOffset : new Vector2(6f, -16f);
            local = parent.InverseTransformPoint(Corner(heart, 0)) + (Vector3)offset + Vector3.down * halfHeight;
        }
        else
        {
            // 나무 패널 안쪽 칸들(코인·체력·확률·스테이지 칸)의 오른쪽 끝에서 패널 테두리만큼 띄운 자리.
            // 굴리기·끝내기 버튼도 같은 패널의 자식이라 패널 자식 전체가 아니라 안쪽 칸만 기준으로 삼습니다.
            float right = float.MinValue;
            foreach (string name in InnerPanelNames)
            {
                RectTransform inner = manager.HudRect(name);
                if (inner != null && inner.gameObject.activeInHierarchy)
                    right = Mathf.Max(right, parent.InverseTransformPoint(Corner(inner, 2)).x);
            }
            if (right == float.MinValue) return;
            Vector2 offset = p != null ? p.trackerCombatOffset : new Vector2(90f, -24f);
            // 높이는 오른쪽 위 피규어 줄과 같게 맞춥니다. 피규어 칸을 못 찾으면 화면 위에서 offset.y만큼 내립니다.
            float y = figures != null
                ? figures.localPosition.y
                : ((RectTransform)parent).rect.yMax + offset.y - halfHeight;
            local = new Vector3(right + offset.x, y, 0f);
        }
        rect.localPosition = local;
        Refresh();
    }

    // 피규어 칸 묶음의 칸 크기·간격·배율을 그대로 가져와 같은 크기로 보이게 합니다.
    void MatchFigureSlots(RectTransform figures)
    {
        var grid = figures != null ? figures.GetComponent<GridLayoutGroup>() : null;
        var p = Profile;
        cellSize = grid != null ? grid.cellSize.x : (p != null ? p.trackerIconSize : 70f);
        cellSize *= p != null ? p.trackerScale : 1f; // 1이면 피규어 칸과 같은 크기
        spacing = grid != null ? grid.spacing.x : 10f;
        rect.localScale = figures != null ? figures.localScale : Vector3.one;
        foreach (var icon in icons.Values) ResizeIcon(icon);
        Layout();
    }

    static Vector3 Corner(RectTransform target, int index)
    {
        var corners = new Vector3[4];
        target.GetWorldCorners(corners);
        return corners[index];
    }

    // 덱의 디버프 주사위를 다시 세어 아이콘을 맞춥니다. 새로 생긴 아이콘은 툭 튀어나옵니다.
    public void Refresh()
    {
        if (manager == null || manager.masterDeck == null) return;
        counts.Clear();
        foreach (var die in manager.masterDeck)
        {
            if (die == null || !die.IsDebuff) continue;
            counts.TryGetValue(die.debuffType, out int n);
            counts[die.debuffType] = n + 1;
        }
        foreach (var type in Order)
        {
            counts.TryGetValue(type, out int n);
            SetCount(type, n, false);
        }
        Layout();
    }

    // 등장 연출에서 디버프가 덱에 스며드는 순간 호출합니다. 실제 덱에 들어가기 전이라 표시만 하나 올립니다.
    public static void Bump(DebuffDiceType type)
    {
        if (Instance == null || type == DebuffDiceType.None) return;
        int next = Instance.icons.TryGetValue(type, out Icon icon) ? icon.shown + 1 : 1;
        Instance.SetCount(type, next, true);
        Instance.Layout();
    }

    // 엘리트 전투 종료 시 사라지는 연출의 시작 위치로 씁니다.
    public static Transform IconTransform(DebuffDiceType type)
    {
        if (Instance == null || !Instance.isActiveAndEnabled) return null;
        return Instance.icons.TryGetValue(type, out Icon icon) && icon.root != null ? icon.root : null;
    }

    void SetCount(DebuffDiceType type, int count, bool forcePop)
    {
        icons.TryGetValue(type, out Icon icon);
        if (count <= 0)
        {
            if (icon != null)
            {
                icon.root.DOKill();
                if (icon.glow != null) icon.glow.DOKill();
                Destroy(icon.root.gameObject);
                icons.Remove(type);
            }
            return;
        }
        if (icon == null)
        {
            icon = CreateIcon(type);
            icons[type] = icon;
            forcePop = true;
        }
        bool grew = count > icon.shown;
        icon.shown = count;
        icon.count.text = count > 1 ? $"×{count}" : "";
        if (grew && forcePop)
        {
            icon.root.DOComplete();
            icon.root.localScale = Vector3.one;
            icon.root.DOPunchScale(Vector3.one * 0.3f, 0.35f, 5, 0.6f).SetUpdate(true);
        }
    }

    // 피규어 칸처럼 왼쪽부터 가로로 놓습니다.
    void Layout()
    {
        int index = 0;
        foreach (var type in Order)
        {
            if (!icons.TryGetValue(type, out Icon icon)) continue;
            icon.root.anchoredPosition = new Vector2(index * (cellSize + spacing), 0f);
            index++;
        }
    }

    Icon CreateIcon(DebuffDiceType type)
    {
        var p = Profile;
        var icon = new Icon();
        icon.root = (RectTransform)new GameObject("Debuff " + type, typeof(RectTransform)).transform;
        icon.root.SetParent(rect, false);
        icon.root.anchorMin = icon.root.anchorMax = new Vector2(0f, 0.5f);
        icon.root.pivot = new Vector2(0f, 0.5f);

        // 뒤쪽 보랏빛: 깜빡이지 않고 숨 쉬듯 천천히 밝아졌다 어두워집니다.
        if (p != null && p.roundGlow != null)
        {
            icon.glow = CreateImage("Glow", p.roundGlow, icon.root);
            Color glowColor = p.trackerGlowColor;
            icon.glow.color = new Color(glowColor.r, glowColor.g, glowColor.b, 0.35f);
            icon.glow.DOFade(0.75f, 1.4f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true).SetLink(icon.glow.gameObject);
        }

        // 피규어처럼 틀 없이 칸을 채우는 주사위 그림. 그림 자체가 눌리는 버튼입니다.
        icon.die = CreateImage("Die", DebuffFeedback.DieSprite(SourceData(type)), icon.root);
        icon.die.raycastTarget = true;
        Button button = icon.root.gameObject.AddComponent<Button>();
        button.targetGraphic = icon.die;
        DebuffDiceType opened = type;
        button.onClick.AddListener(() => OpenPanel(opened));

        icon.count = new GameObject("Count", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        RectTransform countRect = icon.count.rectTransform;
        countRect.SetParent(icon.root, false);
        countRect.anchorMin = countRect.anchorMax = new Vector2(1f, 0f);
        countRect.pivot = new Vector2(1f, 0f);
        TMP_FontAsset font = DebuffFeedback.NumberFont();
        if (font != null) icon.count.font = font;
        Material outline = DebuffFeedback.NumberMaterial(icon.count.font);
        if (outline != null) icon.count.fontSharedMaterial = outline;
        icon.count.alignment = TextAlignmentOptions.BottomRight;
        icon.count.textWrappingMode = TextWrappingModes.NoWrap;
        icon.count.color = new Color(0.95f, 0.88f, 1f);
        icon.count.raycastTarget = false;

        if (DebuffTrackerHud.IsTemporary(type) && p != null && p.hourglass != null)
        {
            // 이번 전투 한정: 오른쪽 위 모서리에 작은 모래시계
            Image hourglass = CreateImage("Hourglass", p.hourglass, icon.root);
            hourglass.rectTransform.anchorMin = hourglass.rectTransform.anchorMax = new Vector2(1f, 1f);
        }
        ResizeIcon(icon);
        return icon;
    }

    // 칸 크기에 맞춰 그림·빛·개수·모래시계 크기를 다시 잡습니다(피규어 칸 크기가 바뀌어도 따라감).
    void ResizeIcon(Icon icon)
    {
        float size = cellSize;
        icon.root.sizeDelta = new Vector2(size, size);
        if (icon.glow != null) icon.glow.rectTransform.sizeDelta = Vector2.one * size * 1.55f;
        if (icon.die != null) icon.die.rectTransform.sizeDelta = Vector2.one * size;
        if (icon.count != null)
        {
            icon.count.rectTransform.sizeDelta = new Vector2(size, size * 0.45f);
            icon.count.rectTransform.anchoredPosition = new Vector2(size * 0.08f, -size * 0.08f);
            icon.count.fontSize = size * 0.34f;
        }
        Transform glass = icon.root.Find("Hourglass");
        var p = Profile;
        if (glass != null && p != null && p.hourglass != null)
        {
            var glassRect = (RectTransform)glass;
            float width = size * 0.28f;
            glassRect.sizeDelta = new Vector2(width, width * p.hourglass.rect.height / Mathf.Max(1f, p.hourglass.rect.width));
            glassRect.anchoredPosition = new Vector2(-size * 0.02f, -size * 0.04f);
        }
    }

    static Image CreateImage(string name, Sprite sprite, Transform parent)
    {
        var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        RectTransform imageRect = image.rectTransform;
        imageRect.SetParent(parent, false);
        imageRect.anchorMin = imageRect.anchorMax = new Vector2(0.5f, 0.5f);
        imageRect.pivot = new Vector2(0.5f, 0.5f);
        imageRect.anchoredPosition = Vector2.zero;
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }

    public static bool IsTemporary(DebuffDiceType type) => type == DebuffDiceType.Thief || type == DebuffDiceType.Shackle;

    // 표시용 대표 데이터. 덱에 실제 주사위가 있으면 그 외형을, 없으면(등장 연출 중) 디버프 에셋 외형을 씁니다.
    DiceData1 SourceData(DebuffDiceType type)
    {
        if (manager != null && manager.masterDeck != null)
            foreach (var die in manager.masterDeck)
                if (die != null && die.debuffType == type) return die;
        DiceItemSO source = manager != null ? manager.FindDebuffSource(type) : null;
        return source != null ? source.CreateDiceData() : null;
    }

    void OpenPanel(DebuffDiceType selected)
    {
        if (manager == null) return;
        var entries = new List<DebuffDetailPanel.Entry>();
        int selectedIndex = 0;
        foreach (var type in Order)
        {
            if (!icons.TryGetValue(type, out Icon icon)) continue;
            DiceItemSO source = manager.FindDebuffSource(type);
            if (type == selected) selectedIndex = entries.Count;
            entries.Add(new DebuffDetailPanel.Entry
            {
                type = type,
                count = icon.shown,
                sprite = icon.die != null ? icon.die.sprite : null,
                name = source != null ? Localized(LocalizationManager.GetItemDisplayName(source), source.itemName) : type.ToString(),
                description = source != null ? Localized(LocalizationManager.GetItemDescription(source), source.description) : "",
                temporary = IsTemporary(type)
            });
        }
        DebuffDetailPanel.Open(entries, selectedIndex);
    }

    // 번역 키가 아직 없으면 번역 시스템이 "No translation found…" 문구를 돌려주므로, 그때는 에셋의 한국어 문구를 씁니다.
    // 나중에 번역 키를 추가하면 코드 수정 없이 번역 문구가 쓰입니다.
    public static string Localized(string value, string fallback)
    {
        if (string.IsNullOrEmpty(value) || value.StartsWith("No translation found", System.StringComparison.Ordinal))
            return fallback ?? string.Empty;
        return value;
    }
}
