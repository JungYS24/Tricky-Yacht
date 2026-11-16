using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 전투씬(MainScene_Map)과 같은 카메라·스폰 위치·스케일로 몬스터 해상도를 확인하는 테스트용 컴포넌트.
/// MonsterResolutionTest 씬의 몬스터 슬롯에 붙어 있습니다.
///
/// 왼쪽 목록: 검색창 / 바이옴 탭 / 해상도 필터 / 몬스터 클릭으로 선택
///  (클릭 대상을 "확인"·"비교" 중에서 고를 수 있습니다)
///
/// 단축키 (검색창에 입력 중일 때는 동작하지 않음, Esc 로 입력 해제)
///  ← / →      : 현재 목록에서 이전 / 다음 몬스터
///  Space      : 애니메이션 일시정지 / 재생
///  , / .      : 일시정지 중 이전 / 다음 프레임
///  R          : 비교용 몬스터 표시 켜기 / 끄기
///  [ / ]      : 현재 목록에서 비교용 몬스터 변경
///  B          : 칸(셀) 테두리 표시 켜기 / 끄기
///  Tab        : 목록 패널 접기 / 펴기
///  H          : 정보 패널 숨기기 / 보이기
///
/// 에디터에서 실행하면 Assets/Contents/05_DataSO/MonstersSO 아래의 MonsterDataSO 를 자동으로 다시 읽어서
/// 새로 추가하거나 교체한 몬스터도 바로 목록에 나옵니다.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Animator))]
public class MonsterResolutionTester : MonoBehaviour
{
    [Serializable]
    public class MonsterEntry
    {
        public MonsterDataSO data;
        public string biome;
    }

    [Header("몬스터 목록 (에디터 실행 시 자동 갱신)")]
    public string monsterFolder = "Assets/Contents/05_DataSO/MonstersSO";
    public bool autoRefreshInEditor = true;
    public List<MonsterEntry> entries = new List<MonsterEntry>();

    [Header("비교용 몬스터")]
    public SpriteRenderer referenceRenderer;
    public Animator referenceAnimator;
    public bool showReference = true;

    [Header("전투씬 Enemy 와 같은 발밑 그림자")]
    public bool groundShadow = true;

    [Header("표시")]
    public bool showCellBounds = true;
    public bool showInfo = true;
    public bool showList = true;

    const string AllTab = "전체";
    static readonly string[] ResFilters = { "전체", "기존 (<256)", "리워크 (256+)" };
    static readonly string[] ClickTargets = { "확인 대상", "비교 대상" };

    private SpriteRenderer sr;
    private Animator anim;
    private GroundShadow shadow;
    private GroundShadow referenceShadow;
    private LineRenderer cellLine;
    private LineRenderer referenceCellLine;

    private int current = -1;     // entries 인덱스
    private int reference = -1;   // entries 인덱스
    private bool paused;

    private string search = "";
    private string[] tabs = { AllTab };
    private int tab;
    private int resFilter;
    private int clickTarget;
    private List<int> filtered = new List<int>();
    private string filterKey;
    private Vector2 listScroll;
    private GUIStyle boxStyle, labelStyle, itemStyle, itemSelStyle, itemRefStyle, smallStyle;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        anim = GetComponent<Animator>();
#if UNITY_EDITOR
        if (autoRefreshInEditor) RefreshFromProject();
#endif
        entries.RemoveAll(e => e == null || e.data == null);
        tabs = new[] { AllTab }.Concat(entries.Select(e => e.biome).Distinct()).ToArray();

        // Enemy.Awake 와 같은 값 (Monster_Slot 프리팹 기준)
        if (groundShadow) shadow = GroundShadow.Attach(sr, 0.44f, 0.06f, 0.016f, 0.47f);
        if (groundShadow && referenceRenderer != null) referenceShadow = GroundShadow.Attach(referenceRenderer, 0.44f, 0.06f, 0.016f, 0.47f);
        cellLine = CreateCellLine(transform, new Color(0f, 1f, 1f, 0.8f));
        if (referenceRenderer != null) referenceCellLine = CreateCellLine(referenceRenderer.transform, new Color(1f, 0.8f, 0f, 0.8f));
    }

    void Start()
    {
        if (entries.Count == 0) return;
        Select(0);
        // 기본 비교 대상: 첫 번째 기존 해상도(<256) 몬스터
        int r = entries.FindIndex(e => CellSize(e) < 256);
        SelectReference(r >= 0 ? r : 0);
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;
        if (GUIUtility.keyboardControl != 0) return; // 검색창 입력 중

        if (kb.rightArrowKey.wasPressedThisFrame) StepInList(ref current, 1, Select);
        if (kb.leftArrowKey.wasPressedThisFrame) StepInList(ref current, -1, Select);
        if (kb.rightBracketKey.wasPressedThisFrame) StepInList(ref reference, 1, SelectReference);
        if (kb.leftBracketKey.wasPressedThisFrame) StepInList(ref reference, -1, SelectReference);
        if (kb.rKey.wasPressedThisFrame) { showReference = !showReference; SelectReference(reference); }
        if (kb.bKey.wasPressedThisFrame) showCellBounds = !showCellBounds;
        if (kb.hKey.wasPressedThisFrame) showInfo = !showInfo;
        if (kb.tabKey.wasPressedThisFrame) showList = !showList;
        if (kb.spaceKey.wasPressedThisFrame)
        {
            paused = !paused;
            SyncSpeed();
        }
        if (paused && kb.periodKey.wasPressedThisFrame) StepFrame(1);
        if (paused && kb.commaKey.wasPressedThisFrame) StepFrame(-1);
    }

    void LateUpdate()
    {
        UpdateCellLine(cellLine, sr, showCellBounds);
        UpdateCellLine(referenceCellLine, referenceRenderer, showCellBounds && showReference);
    }

    // ───────────────────────── 목록 / 필터 ─────────────────────────

#if UNITY_EDITOR
    [ContextMenu("몬스터 목록 갱신")]
    public void RefreshFromProject()
    {
        string root = monsterFolder.TrimEnd('/');
        entries = UnityEditor.AssetDatabase.FindAssets("t:MonsterDataSO", new[] { root })
            .Select(UnityEditor.AssetDatabase.GUIDToAssetPath)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .Select(p =>
            {
                string rel = p.Substring(root.Length + 1);
                int slash = rel.IndexOf('/');
                return new MonsterEntry
                {
                    data = UnityEditor.AssetDatabase.LoadAssetAtPath<MonsterDataSO>(p),
                    biome = slash > 0 ? rel.Substring(0, slash) : "기타",
                };
            })
            .Where(e => e.data != null)
            .ToList();
    }
#endif

    static int CellSize(MonsterEntry e)
    {
        var s = e?.data != null ? e.data.monsterSprite : null;
        return s != null ? Mathf.RoundToInt(Mathf.Max(s.rect.width, s.rect.height)) : 0;
    }

    void RebuildFilter()
    {
        string key = search + "|" + tab + "|" + resFilter + "|" + entries.Count;
        if (key == filterKey) return;
        filterKey = key;
        filtered.Clear();
        string q = search.Trim();
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (tab > 0 && e.biome != tabs[tab]) continue;
            int cell = CellSize(e);
            if (resFilter == 1 && cell >= 256) continue;
            if (resFilter == 2 && cell < 256) continue;
            if (q.Length > 0 && !Matches(e, q)) continue;
            filtered.Add(i);
        }
    }

    static bool Matches(MonsterEntry e, string q)
    {
        bool Has(string s) => !string.IsNullOrEmpty(s) && s.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;
        return Has(e.data.name) || Has(e.data.monsterName) || Has(e.data.monsterID) || Has(e.biome);
    }

    void StepInList(ref int idx, int dir, Action<int> select)
    {
        RebuildFilter();
        var list = filtered.Count > 0 ? filtered : Enumerable.Range(0, entries.Count).ToList();
        if (list.Count == 0) return;
        int pos = list.IndexOf(idx);
        pos = pos < 0 ? (dir > 0 ? 0 : list.Count - 1) : (pos + dir + list.Count) % list.Count;
        select(list[pos]);
    }

    // ───────────────────────── 적용 ─────────────────────────

    void Select(int i)
    {
        if (i < 0 || i >= entries.Count) return;
        current = i;
        ApplyTo(entries[i].data, sr, anim);
        if (shadow != null) shadow.Refit(entries[i].data.monsterSprite);
        SyncSpeed();
    }

    void SelectReference(int i)
    {
        if (referenceRenderer == null) return;
        bool on = showReference && i >= 0 && i < entries.Count;
        referenceRenderer.gameObject.SetActive(on);
        if (i >= 0 && i < entries.Count) reference = i;
        if (!on) return;
        ApplyTo(entries[i].data, referenceRenderer, referenceAnimator);
        if (referenceShadow != null) referenceShadow.Refit(entries[i].data.monsterSprite);
        SyncSpeed();
    }

    void SyncSpeed()
    {
        float speed = paused ? 0f : 1f;
        anim.speed = speed;
        if (referenceAnimator != null) referenceAnimator.speed = speed;
    }

    // Enemy.SetupMonster 와 같은 순서: 컨트롤러가 있으면 애니메이터, 없으면 정지 스프라이트
    static void ApplyTo(MonsterDataSO data, SpriteRenderer target, Animator animator)
    {
        if (data == null) return;
        target.sprite = data.monsterSprite;
        if (animator == null) return;
        animator.runtimeAnimatorController = data.animatorController;
        if (data.animatorController != null)
        {
            animator.Rebind();
            animator.Update(0f);
        }
    }

    void StepFrame(int dir)
    {
        StepAnimator(anim, dir);
        if (referenceAnimator != null && referenceAnimator.isActiveAndEnabled) StepAnimator(referenceAnimator, dir);
    }

    static void StepAnimator(Animator a, int dir)
    {
        if (a.runtimeAnimatorController == null) return;
        var clips = a.GetCurrentAnimatorClipInfo(0);
        if (clips.Length == 0) return;
        var clip = clips[0].clip;
        var state = a.GetCurrentAnimatorStateInfo(0);
        float half = 0.5f / Mathf.Max(1f, clip.frameRate);
        // 스프라이트 키 간격을 모르면 8프레임 기준으로 한 칸씩 이동
        float stepTime = clip.length / 8f;
        float t = Mathf.Repeat(state.normalizedTime * clip.length + dir * stepTime + half, clip.length);
        a.Play(state.fullPathHash, 0, t / clip.length);
        a.Update(0f);
    }

    // ───────────────────────── 칸 테두리 ─────────────────────────

    static LineRenderer CreateCellLine(Transform parent, Color color)
    {
        var go = new GameObject("CellBounds");
        go.transform.SetParent(parent, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.loop = true;
        lr.positionCount = 4;
        lr.widthMultiplier = 0.02f;
        Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (shader != null) lr.material = new Material(shader);
        lr.startColor = lr.endColor = color;
        lr.sortingOrder = 100;
        return lr;
    }

    static void UpdateCellLine(LineRenderer lr, SpriteRenderer target, bool visible)
    {
        if (lr == null) return;
        bool on = visible && target != null && target.gameObject.activeInHierarchy && target.sprite != null;
        lr.enabled = on;
        if (!on) return;
        Bounds b = target.sprite.bounds;
        Transform t = target.transform;
        lr.SetPosition(0, t.TransformPoint(new Vector3(b.min.x, b.min.y)));
        lr.SetPosition(1, t.TransformPoint(new Vector3(b.min.x, b.max.y)));
        lr.SetPosition(2, t.TransformPoint(new Vector3(b.max.x, b.max.y)));
        lr.SetPosition(3, t.TransformPoint(new Vector3(b.max.x, b.min.y)));
    }

    // ───────────────────────── GUI ─────────────────────────

    string Describe(string label, int idx, SpriteRenderer target)
    {
        if (idx < 0 || idx >= entries.Count || target == null || target.sprite == null) return label + ": (없음)";
        var e = entries[idx];
        Sprite s = target.sprite;
        Camera cam = Camera.main;
        float worldH = s.rect.height / s.pixelsPerUnit * target.transform.lossyScale.y;
        float worldW = s.rect.width / s.pixelsPerUnit * target.transform.lossyScale.x;
        float screenPxPerUnit = cam != null && cam.orthographic ? Screen.height / (cam.orthographicSize * 2f) : 0f;
        float screenH = worldH * screenPxPerUnit;
        float texelScale = screenH / s.rect.height;
        return $"{label}: {e.data.name}  ({e.biome})\n" +
               $"  현재 프레임 {s.name}  |  시트 {s.texture.width}×{s.texture.height}\n" +
               $"  칸 {s.rect.width:0}×{s.rect.height:0}px  |  PPU {s.pixelsPerUnit:0}  |  스케일 {target.transform.lossyScale.y:0.##}\n" +
               $"  월드 크기 {worldW:0.##}×{worldH:0.##}  |  화면 {screenH:0}px (텍셀 1개 = 화면 {texelScale:0.##}px)" +
               (e.data.animatorController == null ? "\n  ※ 애니메이터 없음 (정지 스프라이트)" : "");
    }

    void InitStyles()
    {
        if (boxStyle != null) return;
        boxStyle = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 15, wordWrap = false, padding = new RectOffset(10, 10, 8, 8) };
        boxStyle.normal.textColor = Color.white;
        labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 15 };
        labelStyle.normal.textColor = Color.white;
        smallStyle = new GUIStyle(labelStyle) { fontSize = 13 };
        smallStyle.normal.textColor = new Color(0.8f, 0.8f, 0.8f);
        itemStyle = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleLeft, fontSize = 14, padding = new RectOffset(8, 8, 4, 4) };
        itemSelStyle = new GUIStyle(itemStyle);
        itemSelStyle.normal.textColor = itemSelStyle.hover.textColor = new Color(0.3f, 1f, 1f);
        itemSelStyle.fontStyle = FontStyle.Bold;
        itemRefStyle = new GUIStyle(itemStyle);
        itemRefStyle.normal.textColor = itemRefStyle.hover.textColor = new Color(1f, 0.85f, 0.2f);
        GUI.skin.textField.fontSize = 15;
        GUI.skin.button.fontSize = 14;
    }

    void OnGUI()
    {
        InitStyles();
        // 1080p 기준으로 GUI 크기를 맞춤 (Game 창이 작아도 읽을 수 있게)
        float scale = Mathf.Max(0.6f, Screen.height / 1080f);
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        float w = Screen.width / scale, h = Screen.height / scale;

        var ev = Event.current;
        if (ev.type == EventType.KeyDown && ev.keyCode == KeyCode.Escape) { GUIUtility.keyboardControl = 0; ev.Use(); }

        if (showList) DrawList(h);
        else if (GUI.Button(new Rect(10, 10, 120, 30), "목록 열기 (Tab)")) showList = true;

        if (showInfo) DrawInfo(w);
    }

    void DrawList(float h)
    {
        const float pw = 340f;
        var area = new Rect(10, 10, pw, h - 20);
        GUI.Box(area, GUIContent.none, boxStyle);
        GUILayout.BeginArea(new Rect(area.x + 8, area.y + 8, area.width - 16, area.height - 16));

        GUILayout.BeginHorizontal();
        GUILayout.Label($"몬스터 {entries.Count}종", labelStyle);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("접기 (Tab)", GUILayout.Width(90))) showList = false;
        GUILayout.EndHorizontal();

        // 검색
        GUILayout.BeginHorizontal();
        GUI.SetNextControlName("MonsterSearch");
        string newSearch = GUILayout.TextField(search, GUILayout.Height(26));
        if (GUILayout.Button("✕", GUILayout.Width(28), GUILayout.Height(26))) { newSearch = ""; GUIUtility.keyboardControl = 0; }
        GUILayout.EndHorizontal();
        if (newSearch != search) { search = newSearch; listScroll = Vector2.zero; }
        GUILayout.Label("이름·ID·바이옴으로 검색  (Enter: 첫 결과 선택, Esc: 입력 해제)", smallStyle);

        RebuildFilter();
        var ev = Event.current;
        if (ev.type == EventType.KeyDown && (ev.keyCode == KeyCode.Return || ev.keyCode == KeyCode.KeypadEnter)
            && GUI.GetNameOfFocusedControl() == "MonsterSearch" && filtered.Count > 0)
        {
            ClickEntry(filtered[0]);
            GUIUtility.keyboardControl = 0;
            ev.Use();
        }

        // 바이옴 탭
        GUILayout.Space(4);
        int newTab = GUILayout.SelectionGrid(tab, tabs.Select(TabLabel).ToArray(), 3, GUILayout.Height(26 * Mathf.Ceil(tabs.Length / 3f)));
        if (newTab != tab) { tab = newTab; listScroll = Vector2.zero; }

        // 해상도 필터
        GUILayout.Space(4);
        int newRes = GUILayout.Toolbar(resFilter, ResFilters, GUILayout.Height(24));
        if (newRes != resFilter) { resFilter = newRes; listScroll = Vector2.zero; }

        // 클릭 대상
        GUILayout.BeginHorizontal();
        GUILayout.Label("클릭하면:", labelStyle, GUILayout.Width(70));
        clickTarget = GUILayout.Toolbar(clickTarget, ClickTargets, GUILayout.Height(24));
        GUILayout.EndHorizontal();

        RebuildFilter();
        GUILayout.Label($"결과 {filtered.Count}종   (하늘색 = 확인 대상, 노란색 = 비교 대상)", smallStyle);

        listScroll = GUILayout.BeginScrollView(listScroll);
        foreach (int i in filtered)
        {
            var e = entries[i];
            int cell = CellSize(e);
            string mark = i == current ? "▶ " : i == reference && showReference ? "◆ " : "   ";
            string label = $"{mark}{e.data.name}   <{cell}px>" + (tab == 0 ? $"  · {ShortBiome(e.biome)}" : "");
            var style = i == current ? itemSelStyle : (i == reference && showReference ? itemRefStyle : itemStyle);
            if (GUILayout.Button(label, style, GUILayout.Height(26))) ClickEntry(i);
        }
        GUILayout.EndScrollView();

        GUILayout.EndArea();
    }

    void ClickEntry(int i)
    {
        if (clickTarget == 0) Select(i);
        else { showReference = true; SelectReference(i); }
    }

    string TabLabel(string biome)
    {
        if (biome == AllTab) return $"{AllTab} ({entries.Count})";
        return $"{ShortBiome(biome)} ({entries.Count(e => e.biome == biome)})";
    }

    // "01_Forest" → "Forest"
    static string ShortBiome(string biome)
    {
        int u = biome.IndexOf('_');
        return u >= 0 && u < biome.Length - 1 && char.IsDigit(biome[0]) ? biome.Substring(u + 1) : biome;
    }

    void DrawInfo(float w)
    {
        string text =
            Describe("확인 대상", current, sr) + "\n\n" +
            (showReference ? Describe("비교 대상", reference, referenceRenderer) + "\n\n" : "") +
            $"화면 {Screen.width}×{Screen.height}  |  애니메이션 {(paused ? "일시정지" : "재생")}\n" +
            "←/→ 몬스터  Space 정지  ,/. 프레임  R 비교  [/] 비교 변경\nB 칸 테두리  Tab 목록  H 정보 숨기기";
        var content = new GUIContent(text);
        Vector2 size = boxStyle.CalcSize(content);
        GUI.Box(new Rect(w - size.x - 10, 10, size.x, size.y), content, boxStyle);
    }
}
