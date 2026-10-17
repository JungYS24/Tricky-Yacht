#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// 도감 패널(CollectionBookPanel)을 책 레이아웃으로 정리하고 시너지 도감을 책 페이지로 붙인다.
/// 이름으로 찾아서 옮기고 복제하므로 여러 번 실행해도 결과가 같다.
/// 메뉴: Tools/UI/Setup CollectionBook
/// </summary>
public static class CollectionBookSetup
{
    const string ScenePath = "Assets/Contents/01_Scenes/MainScene_Map.unity";
    const string SynergyScenePath = "Assets/Contents/01_Scenes/UIDesign/SynergyWiki.unity";
    const string PrefabFolder = "Assets/Contents/03_Prefabs/UI/CollectionBook";
    const string SynergyPagePrefabPath = PrefabFolder + "/SynergyWikiPage.prefab";
    const string SynergyElementPrefabPath = PrefabFolder + "/SynergyWikiElement_Book.prefab";
    const string ElementPrefabPath = "Assets/Contents/03_Prefabs/UIElements/FigureList/Element.prefab";
    const string SlotPrefabPath = "Assets/Contents/03_Prefabs/CollectionSlotPrefab/CollectionSlotPrefab.prefab";
    const string SynergyDataFolder = "Assets/Contents/05_DataSO/Synergy";
    const string UiCsvPath = "Assets/Contents/11_Localization/Source/localization_csv/UI.csv";
    const string ArrowSpritePath = "Assets/Contents/06_UI/Sprites/Button/CrossBTN.png";
    const string IconFrameSpritePath = "Assets/Contents/06_UI/Sprites/score_panel_sprites/slot_dark_small_1.png";
    const string LockSpritePath = "Assets/Contents/06_UI/Sprites/Icon/자물쇠-removebg-preview.png";

    const int PanelSortingOrder = 400;

    // 책 루트(UI_Book 2000x1000) 기준 좌표. UI_Book.png의 양피지 영역을 재서 정했다.
    // 왼쪽 페이지 x -765~9, 접힘선 9~28, 오른쪽 페이지 28~794, 세로 -409~437
    static readonly Vector2 BookRootPosition = new Vector2(-44f, 0f);
    static readonly Vector2 BookSize = new Vector2(2000f, 1000f);
    const float LeftPageCenterX = -378f;
    const float LeftPageWidth = 684f;
    const float RightPageCenterX = 411f;
    const float RightPageWidth = 676f;
    const float PageCenterY = 14f;
    const float PageHeight = 756f;
    const float SpreadLeft = -720f;
    const float SpreadRight = 749f;

    // 시너지 행은 왼쪽 페이지에 피규어, 오른쪽 페이지에 설명이 오도록 펼침면 전체에 축소 배치한다.
    const float SynergyPageScale = 0.76f;
    const float SynergyDescriptionLeft = 1010f;

    static readonly Color Ink = new Color(0.29f, 0.18f, 0.1f, 1f);
    static readonly Color DimColor = new Color(0f, 0f, 0f, 0.65f);

    // 책갈피 스프라이트(300x250)는 리본이 x 5%~92.7%, y 36%~64%만 차지한다.
    static readonly Vector2 TabSize = new Vector2(200f, 193f);
    const float TabCenterX = -865f;
    static readonly float[] TabCenterY = { 330f, 262f, 194f };
    static readonly Vector4 TabRaycastPadding = new Vector4(10f, 69.5f, 14.6f, 69.5f);
    // 책 이미지는 책갈피 위에 그려지지만, 리본이 튀어나온 왼쪽만 클릭을 통과시킨다.
    // 탭 클릭 오른쪽 끝은 -779.6, 양피지 왼쪽은 -765이므로 레이캐스트는 -768부터 시작한다.
    const float BookRaycastLeftInset = 232f;
    static readonly Vector2 TabLabelOffsetMin = new Vector2(34f, 69.5f);
    static readonly Vector2 TabLabelOffsetMax = new Vector2(-80f, -69.5f);

    [MenuItem("Tools/UI/Setup CollectionBook")]
    public static void SetupFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Run(false);
    }

    public static void BatchSetup()
    {
        Run(true);
    }

    static void Run(bool fromBatch)
    {
        try
        {
            ImportUiStrings();
            SetupSlotPrefab();

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            // 씬을 열면 참조되지 않은 에셋이 언로드되므로 프리팹 작업은 연 뒤에 한다.
            GameObject elementPrefab = SetupSynergyElementPrefab();
            GameObject synergyPagePrefab = SetupSynergyPagePrefab(elementPrefab);

            SetupPanel(scene, synergyPagePrefab);
            EnsureSynergyManager(scene);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            // 꺼진 패널 아래 RectTransform은 씬을 다시 열 때 소수점이 한 번 다시 계산된다.
            // 한 번 다시 열어 저장해 두면 다음 실행에서 diff가 생기지 않는다.
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[CollectionBook] setup OK");
            ExitIfBatch(fromBatch, 0);
        }
        catch (Exception exception)
        {
            Debug.LogError("[CollectionBook] " + exception);
            ExitIfBatch(fromBatch, 1);
        }
    }

    static void ExitIfBatch(bool fromBatch, int code)
    {
        if (fromBatch || Application.isBatchMode)
            EditorApplication.Exit(code);
    }

    // ───────────────────────── 로컬라이즈 ─────────────────────────

    static void ImportUiStrings()
    {
        MethodInfo importCsv = typeof(LocalizationCsvImporter).GetMethod("ImportCsv", BindingFlags.NonPublic | BindingFlags.Static);
        if (importCsv == null)
        {
            LocalizationCsvImporter.ImportAll();
            return;
        }

        importCsv.Invoke(null, new object[] { UiCsvPath, 0, 0 });
        AssetDatabase.SaveAssets();
        Debug.Log("[CollectionBook] UI.csv 키 가져오기 완료");
    }

    // ───────────────────────── 슬롯 프리팹 ─────────────────────────

    static void SetupSlotPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(SlotPrefabPath);
        try
        {
            CollectionSlot slot = root.GetComponent<CollectionSlot>();
            if (slot == null) throw new InvalidOperationException("CollectionSlotPrefab에 CollectionSlot이 없습니다.");

            // 카드(Win_Choice_Panel) 그림칸과 이름판 비율에 맞춰 셀 크기가 바뀌어도 따라가게 한다.
            RectTransform icon = slot.figureIcon.rectTransform;
            SetAnchors(icon, new Vector2(0.15f, 0.52f), new Vector2(0.85f, 0.92f));
            slot.figureIcon.preserveAspect = true;

            TextMeshProUGUI nameText = slot.nameText;
            SetAnchors(nameText.rectTransform, new Vector2(0.1f, 0.07f), new Vector2(0.9f, 0.33f));
            nameText.fontSize = 18f;
            nameText.enableAutoSizing = true;
            nameText.fontSizeMin = 13f;
            nameText.fontSizeMax = 18f;
            nameText.textWrappingMode = TextWrappingModes.NoWrap;
            nameText.overflowMode = TextOverflowModes.Ellipsis;
            nameText.horizontalAlignment = HorizontalAlignmentOptions.Center;
            nameText.verticalAlignment = VerticalAlignmentOptions.Middle;
            nameText.color = Ink;
            nameText.raycastTarget = false;

            Transform mark = FindDirect(root.transform, "EncounteredMark");
            if (mark == null)
            {
                GameObject markGo = Object.Instantiate(slot.figureIcon.gameObject, root.transform);
                markGo.name = "EncounteredMark";
                mark = markGo.transform;
            }
            SetLastSibling(mark);
            SetRect((RectTransform)mark, Vector2.one, Vector2.one, Vector2.one, new Vector2(-8f, -8f), new Vector2(28f, 28f), Vector3.one);
            Image markImage = mark.GetComponent<Image>();
            markImage.sprite = LoadSprite(LockSpritePath, null);
            markImage.color = Color.white;
            markImage.preserveAspect = true;
            markImage.raycastTarget = false;
            SetActive(mark.gameObject, false);
            slot.encounteredMark = mark.gameObject;

            PrefabUtility.SaveAsPrefabAsset(root, SlotPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ───────────────────────── 시너지 프리팹 ─────────────────────────

    // 김병훈 Element 프리팹은 그대로 두고, 책용 배경·설명 위치만 바꾼 Variant를 쓴다.
    static GameObject SetupSynergyElementPrefab()
    {
        EnsureFolder(PrefabFolder);
        if (AssetDatabase.LoadAssetAtPath<GameObject>(SynergyElementPrefabPath) == null)
        {
            GameObject basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ElementPrefabPath);
            if (basePrefab == null) throw new InvalidOperationException("Element 프리팹을 찾지 못했습니다: " + ElementPrefabPath);
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
            instance.name = "SynergyWikiElement_Book";
            PrefabUtility.SaveAsPrefabAsset(instance, SynergyElementPrefabPath);
            Object.DestroyImmediate(instance);
        }

        GameObject root = PrefabUtility.LoadPrefabContents(SynergyElementPrefabPath);
        try
        {
            root.GetComponent<Image>().enabled = false;

            RectTransform description = (RectTransform)Require(root.transform, "Description");
            SetHorizontalOffsets(description, SynergyDescriptionLeft, 0f);

            Require(root.transform, "Description/DescriptionBackground").GetComponent<Image>().enabled = false;
            Require(root.transform, "Description/DescriptionBackground/Name").GetComponent<TextMeshProUGUI>().color = Ink;
            TextMeshProUGUI descriptionText = Require(root.transform, "Description/DescriptionBackground/Description").GetComponent<TextMeshProUGUI>();
            descriptionText.color = Ink;
            descriptionText.fontSize = 26f;

            PrefabUtility.SaveAsPrefabAsset(root, SynergyElementPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        return AssetDatabase.LoadAssetAtPath<GameObject>(SynergyElementPrefabPath);
    }

    static GameObject SetupSynergyPagePrefab(GameObject elementPrefab)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(SynergyPagePrefabPath) == null)
            CreateSynergyPagePrefabFromScene();

        GameObject root = PrefabUtility.LoadPrefabContents(SynergyPagePrefabPath);
        try
        {
            // 테스트용 회색 배경과 스크롤 배경은 끄고 양피지가 보이게 한다.
            root.GetComponent<Image>().enabled = false;
            Require(root.transform, "Scroll View").GetComponent<Image>().enabled = false;

            Image scrollbar = Require(root.transform, "Scroll View/Scrollbar Vertical").GetComponent<Image>();
            scrollbar.color = new Color(Ink.r, Ink.g, Ink.b, 0.15f);
            Require(root.transform, "Scroll View/Scrollbar Vertical/Sliding Area/Handle").GetComponent<Image>().color = new Color(Ink.r, Ink.g, Ink.b, 0.6f);

            // 접힘선에 걸리지 않게 빈 목록 안내는 왼쪽 페이지에 둔다.
            RectTransform empty = (RectTransform)Require(root.transform, "Text (TMP)");
            SetAnchors(empty, new Vector2(0f, 0f), new Vector2(0.47f, 1f));
            empty.GetComponent<TextMeshProUGUI>().color = new Color(Ink.r, Ink.g, Ink.b, 0.6f);

            TextMeshProUGUI reverseLabel = Require(root.transform, "Toggle/Label").GetComponent<TextMeshProUGUI>();
            reverseLabel.color = Ink;
            TMP_Dropdown orderDropdown = Require(root.transform, "Order Option").GetComponent<TMP_Dropdown>();
            if (orderDropdown == null) throw new InvalidOperationException("Order Option에 TMP_Dropdown이 없습니다.");

            SynergyWikiDisplayer displayer = root.GetComponent<SynergyWikiDisplayer>();
            SerializedObject so = new SerializedObject(displayer);
            so.FindProperty("componentObj").objectReferenceValue = elementPrefab;
            so.FindProperty("orderDropdown").objectReferenceValue = orderDropdown;
            so.FindProperty("reverseLabel").objectReferenceValue = reverseLabel;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, SynergyPagePrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        return AssetDatabase.LoadAssetAtPath<GameObject>(SynergyPagePrefabPath);
    }

    // SynergyWiki 씬에서 Wiki(목록 + 정렬 UI)만 복제해 프리팹으로 만든다. 씬은 저장하지 않고 닫는다.
    static void CreateSynergyPagePrefabFromScene()
    {
        Scene wikiScene = EditorSceneManager.OpenScene(SynergyScenePath, OpenSceneMode.Additive);
        GameObject clone = null;
        try
        {
            GameObject[] roots = wikiScene.GetRootGameObjects();
            SynergyWikiDisplayer source = roots
                .Select(r => r.GetComponentInChildren<SynergyWikiDisplayer>(true))
                .FirstOrDefault(d => d != null);
            GameObject canvas = roots.FirstOrDefault(r => r.name == "Canvas");
            Transform wiki = canvas != null ? canvas.transform.Find("Wiki") : null;
            if (source == null || wiki == null)
                throw new InvalidOperationException("SynergyWiki 씬에서 Wiki 또는 SynergyWikiDisplayer를 찾지 못했습니다.");

            clone = Object.Instantiate(wiki.gameObject);
            clone.name = "SynergyWikiPage";
            SynergyWikiDisplayer displayer = clone.AddComponent<SynergyWikiDisplayer>();

            SerializedObject sourceSo = new SerializedObject(source);
            SerializedObject so = new SerializedObject(displayer);
            so.FindProperty("viewport").objectReferenceValue =
                RemapInClone((Transform)sourceSo.FindProperty("viewport").objectReferenceValue, wiki, clone.transform);
            Transform emptyMessage = RemapInClone(
                ((GameObject)sourceSo.FindProperty("noSynergyMessageObj").objectReferenceValue).transform, wiki, clone.transform);
            so.FindProperty("noSynergyMessageObj").objectReferenceValue = emptyMessage.gameObject;
            so.FindProperty("componentObj").objectReferenceValue = sourceSo.FindProperty("componentObj").objectReferenceValue;
            so.FindProperty("figureFrameObj").objectReferenceValue = sourceSo.FindProperty("figureFrameObj").objectReferenceValue;
            so.ApplyModifiedPropertiesWithoutUndo();

            // 정렬 드롭다운·역순 토글 이벤트가 씬의 SynergyWikiDisplayer를 가리키므로 복제본으로 바꾼다.
            foreach (MonoBehaviour behaviour in clone.GetComponentsInChildren<MonoBehaviour>(true))
                RetargetReferences(behaviour, source, displayer);

            PrefabUtility.SaveAsPrefabAsset(clone, SynergyPagePrefabPath);
            Debug.Log("[CollectionBook] SynergyWikiPage 프리팹 생성");
        }
        finally
        {
            if (clone != null) Object.DestroyImmediate(clone);
            EditorSceneManager.CloseScene(wikiScene, true);
        }
    }

    static Transform RemapInClone(Transform target, Transform sourceRoot, Transform cloneRoot)
    {
        if (target == null) return null;
        if (target == sourceRoot) return cloneRoot;
        string path = AnimationUtility.CalculateTransformPath(target, sourceRoot);
        Transform found = cloneRoot.Find(path);
        if (found == null) throw new InvalidOperationException("복제본에서 경로를 찾지 못했습니다: " + path);
        return found;
    }

    static void RetargetReferences(Object owner, Object from, Object to)
    {
        SerializedObject so = new SerializedObject(owner);
        SerializedProperty property = so.GetIterator();
        bool changed = false;
        while (property.Next(true))
        {
            if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue == from)
            {
                property.objectReferenceValue = to;
                changed = true;
            }
        }
        if (changed) so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ───────────────────────── 도감 패널 ─────────────────────────

    static void SetupPanel(Scene scene, GameObject synergyPagePrefab)
    {
        GameObject canvasGo = scene.GetRootGameObjects().FirstOrDefault(r => r.name == "Canvas");
        if (canvasGo == null) throw new InvalidOperationException("Canvas를 찾지 못했습니다.");
        Transform panel = Require(canvasGo.transform, "CollectionBookPanel");
        CollectionBookManager manager = panel.GetComponent<CollectionBookManager>();
        if (manager == null) throw new InvalidOperationException("CollectionBookPanel에 CollectionBookManager가 없습니다.");

        // 모달 레이어: 패널 자체 캔버스로 HUD·다른 팝업(최대 300) 위에 그린다.
        Canvas canvas = GetOrAdd<Canvas>(panel.gameObject);
        SerializedObject canvasSo = new SerializedObject(canvas);
        canvasSo.FindProperty("m_OverrideSorting").boolValue = true;
        canvasSo.FindProperty("m_SortingOrder").intValue = PanelSortingOrder;
        canvasSo.ApplyModifiedPropertiesWithoutUndo();
        GetOrAdd<GraphicRaycaster>(panel.gameObject);

        Button dimButton = SetupDim(panel);
        RectTransform bookRoot = SetupBookRoot(panel);

        Transform bookImage = FindAnywhere(panel, "Backgrounf_Book");
        Transform tabGroup = FindAnywhere(panel, "TabButtonGroup");
        Transform closeButton = FindAnywhere(panel, "Close_BTN");
        if (bookImage == null || tabGroup == null || closeButton == null)
            throw new InvalidOperationException("Backgrounf_Book / TabButtonGroup / Close_BTN 중 하나를 찾지 못했습니다.");

        // 탭 위치는 고정 좌표로 잡으므로 옮기기 전에 레이아웃이 다시 계산하지 않게 끈다.
        VerticalLayoutGroup tabLayout = tabGroup.GetComponent<VerticalLayoutGroup>();
        if (tabLayout != null && tabLayout.enabled) tabLayout.enabled = false;

        // 책갈피는 책 뒤에 깔려서 가장자리 밖으로만 보이도록 책 이미지보다 먼저 둔다.
        SetParent(tabGroup, bookRoot);
        Stretch((RectTransform)tabGroup);
        SetSibling(tabGroup, 0);
        SetParent(bookImage, bookRoot);
        Stretch((RectTransform)bookImage);
        SetSibling(bookImage, 1);
        // 순서를 바꾸면 책갈피가 책 위로 올라온다. 그리기 순서는 두고 겹치는 왼쪽만 클릭에서 뺀다.
        Image bookGraphic = bookImage.GetComponent<Image>();
        Vector4 bookPadding = new Vector4(BookRaycastLeftInset, 0f, 0f, 0f);
        if (bookGraphic.raycastPadding != bookPadding) bookGraphic.raycastPadding = bookPadding;

        RectTransform figurePage = EnsureContainer(bookRoot, "Page_Figure");
        SetSibling(figurePage, 2);
        SetActive(figurePage.gameObject, true);
        RectTransform synergyPage = EnsureContainer(bookRoot, "Page_Synergy");
        SetSibling(synergyPage, 3);

        SetParent(closeButton, bookRoot);
        SetCenterRect((RectTransform)closeButton, new Vector2(797f, 403f), ((RectTransform)closeButton).sizeDelta);
        SetSibling(closeButton, 4);

        SetupTabs(tabGroup, manager);
        SetupFigurePage(panel, figurePage, closeButton, manager);
        SetupSynergyPage(synergyPage, synergyPagePrefab, manager);

        // 책 레이아웃으로 대체돼 쓰지 않는 기존 오브젝트는 지우지 않고 끈다(롤백용).
        DeactivateIfFound(panel, "BookBackground");      // 투명 이미지인데 책 위를 덮어 클릭을 막는다
        DeactivateIfFound(panel, "BiomeFilterPanelRoot"); // 바이옴 좌우 넘김으로 대체
        DeactivateIfFound(panel, "OpenFilterButton");
        DeactivateIfFound(panel, "CurrentFilterText");    // 헤더 바이옴 이름과 중복

        manager.dimButton = dimButton;
        manager.figurePageRoot = figurePage.gameObject;
        manager.synergyPageRoot = synergyPage.gameObject;
        EditorUtility.SetDirty(manager);

        Debug.Log("[CollectionBook] panel " + ChildNames(panel));
        Debug.Log("[CollectionBook] book " + ChildNames(bookRoot));
        Debug.Log("[CollectionBook] figure page " + ChildNames(figurePage));
    }

    static Button SetupDim(Transform panel)
    {
        Transform dim = FindDirect(panel, "Dim");
        if (dim == null)
        {
            Transform template = FindAnywhere(panel, "BookBackground");
            if (template == null) throw new InvalidOperationException("딤을 복제할 BookBackground를 찾지 못했습니다.");
            dim = Object.Instantiate(template.gameObject, panel).transform;
            dim.name = "Dim";
        }
        SetSibling(dim, 0);
        SetActive(dim.gameObject, true);

        // 패널이 화면보다 안쪽으로 잡혀 있어서 그만큼 바깥으로 늘려 화면 전체를 덮는다.
        RectTransform panelRect = (RectTransform)panel;
        Vector2 margin = new Vector2(40f, 40f);
        SetAnchors((RectTransform)dim, Vector2.zero, Vector2.one, -panelRect.offsetMin - margin, -panelRect.offsetMax + margin);

        Image image = dim.GetComponent<Image>();
        image.sprite = null;
        image.type = Image.Type.Simple;
        image.color = DimColor;
        image.raycastTarget = true;

        Button button = GetOrAdd<Button>(dim.gameObject);
        button.transition = Selectable.Transition.None;
        button.targetGraphic = image;
        Navigation navigation = button.navigation;
        navigation.mode = Navigation.Mode.None;
        button.navigation = navigation;
        return button;
    }

    static RectTransform SetupBookRoot(Transform panel)
    {
        RectTransform bookRoot = EnsureContainer(panel, "BookRoot");
        SetCenterRect(bookRoot, BookRootPosition, BookSize);
        SetSibling(bookRoot, 1);
        SetActive(bookRoot.gameObject, true);
        return bookRoot;
    }

    static void SetupTabs(Transform tabGroup, CollectionBookManager manager)
    {
        Transform biomeTab = Require(tabGroup, "TabButton_Biome");
        Transform figureTab = FindDirect(tabGroup, "TabButton_Figure");
        if (figureTab == null)
        {
            figureTab = Object.Instantiate(biomeTab.gameObject, tabGroup).transform;
            figureTab.name = "TabButton_Figure";
        }
        Transform synergyTab = Require(tabGroup, "TabButton_Synergy");
        Transform achievementTab = Require(tabGroup, "TabButton_Achievement");

        // 현재 바이옴 탭은 바이옴 필터와 역할이 겹쳐 끈다.
        SetActive(biomeTab.gameObject, false);

        Transform[] tabs = { figureTab, synergyTab, achievementTab };
        string[] defaultLabels = { "피규어", "시너지", "업적" };
        TextMeshProUGUI[] labels = new TextMeshProUGUI[tabs.Length];
        for (int i = 0; i < tabs.Length; i++)
        {
            Transform tab = tabs[i];
            SetActive(tab.gameObject, true);
            SetSibling(tab, i);
            SetCenterRect((RectTransform)tab, new Vector2(TabCenterX, TabCenterY[i]), TabSize);

            Button button = tab.GetComponent<Button>();
            Image image = tab.GetComponent<Image>();
            image.raycastPadding = TabRaycastPadding;
            image.preserveAspect = false;

            TextMeshProUGUI label = tab.GetComponentInChildren<TextMeshProUGUI>(true);
            // 라벨은 LocalizationManager.GetUi로 넣으므로 패키지 로컬라이저는 끈다.
            LocalizeStringEvent localizer = label.GetComponent<LocalizeStringEvent>();
            if (localizer != null) localizer.enabled = false;
            SetAnchors(label.rectTransform, Vector2.zero, Vector2.one, TabLabelOffsetMin, TabLabelOffsetMax);
            label.text = defaultLabels[i];
            label.color = Color.white;
            label.fontSize = 24f;
            label.enableAutoSizing = true;
            label.fontSizeMin = 14f;
            label.fontSizeMax = 24f;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.horizontalAlignment = HorizontalAlignmentOptions.Center;
            label.verticalAlignment = VerticalAlignmentOptions.Middle;
            label.raycastTarget = false;
            labels[i] = label;
        }

        Button figureButton = figureTab.GetComponent<Button>();
        Button synergyButton = synergyTab.GetComponent<Button>();
        Button achievementButton = achievementTab.GetComponent<Button>();
        // 업적 탭은 아직 기능이 없어 연결하지 않는다.
        ClearPersistentOnClick(achievementButton);
        EnsurePersistent(figureButton, manager.ShowFigurePage, nameof(CollectionBookManager.ShowFigurePage));
        EnsurePersistent(synergyButton, manager.ShowSynergyPage, nameof(CollectionBookManager.ShowSynergyPage));

        manager.figureTabButton = figureButton;
        manager.synergyTabButton = synergyButton;
        manager.achievementTabButton = achievementButton;
        manager.figureTabLabel = labels[0];
        manager.synergyTabLabel = labels[1];
        manager.achievementTabLabel = labels[2];

        LogPersistent(figureButton, "TabButton_Figure");
        LogPersistent(synergyButton, "TabButton_Synergy");
        LogPersistent(achievementButton, "TabButton_Achievement");
    }

    static void EnsurePersistent(Button button, UnityEngine.Events.UnityAction action, string methodName)
    {
        for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
        {
            if (button.onClick.GetPersistentMethodName(i) == methodName) return;
        }
        UnityEventTools.AddPersistentListener(button.onClick, action);
    }

    static void LogPersistent(Button button, string name)
    {
        int count = button.onClick.GetPersistentEventCount();
        var methods = new string[count];
        for (int i = 0; i < count; i++) methods[i] = button.onClick.GetPersistentMethodName(i);
        Debug.Log("[CollectionBook] " + name + " persistent=" + count + (count > 0 ? " " + string.Join(", ", methods) : ""));
    }

    static void SetupFigurePage(Transform panel, RectTransform page, Transform closeButton, CollectionBookManager manager)
    {
        Transform textTemplate = FindAnywhere(panel, "CurrentFilterText");
        // 시너지 페이지 프리팹 안에도 Scroll View가 있어서 원래 자리와 옮긴 자리만 찾는다.
        Transform scrollView = FindDirect(page, "Scroll View") ?? FindDirect(panel, "Scroll View");
        Transform detail = FindAnywhere(panel, "SlotFigureDetailPanel");
        if (textTemplate == null || scrollView == null || detail == null)
            throw new InvalidOperationException("CurrentFilterText / Scroll View / SlotFigureDetailPanel 중 하나를 찾지 못했습니다.");

        Sprite arrowLeft = LoadSprite(ArrowSpritePath, "CrossBTN_0");
        Sprite arrowRight = LoadSprite(ArrowSpritePath, "CrossBTN_1");
        if (arrowLeft == null || arrowRight == null) throw new InvalidOperationException("CrossBTN 화살표 스프라이트를 찾지 못했습니다.");
        Vector2 arrowSize = new Vector2(44f, 52f);

        // 헤더: 제목(왼쪽) + 수집률(오른쪽), 그 아래 ◀ 바이옴 ▶
        float headerY = 360f;
        TextMeshProUGUI title = EnsureText(textTemplate, page, "TitleText", new Vector2(LeftPageCenterX, headerY), new Vector2(LeftPageWidth, 56f), 38f, HorizontalAlignmentOptions.Left);
        title.text = "피규어 도감";
        TextMeshProUGUI progress = EnsureText(textTemplate, page, "ProgressText", new Vector2(LeftPageCenterX, headerY), new Vector2(LeftPageWidth, 56f), 30f, HorizontalAlignmentOptions.Right);
        progress.text = "0 / 0";

        float biomeY = 302f;
        TextMeshProUGUI biomeName = EnsureText(textTemplate, page, "BiomeNameText", new Vector2(LeftPageCenterX, biomeY), new Vector2(240f, 52f), 30f, HorizontalAlignmentOptions.Center);
        biomeName.text = "전체";
        Button prevBiome = EnsureArrowButton(closeButton, page, "PrevBiomeButton", arrowLeft, new Vector2(LeftPageCenterX - 150f, biomeY), arrowSize);
        Button nextBiome = EnsureArrowButton(closeButton, page, "NextBiomeButton", arrowRight, new Vector2(LeftPageCenterX + 150f, biomeY), arrowSize);

        // 그리드: 5열 x 3행 = 15칸, 스크롤 대신 페이지 넘김
        SetParent(scrollView, page);
        SetCenterRect((RectTransform)scrollView, new Vector2(LeftPageCenterX, -6f), new Vector2(LeftPageWidth, 520f));
        ScrollRect scrollRect = scrollView.GetComponent<ScrollRect>();
        if (scrollRect != null) scrollRect.enabled = false;
        DeactivateIfFound(scrollView, "Scrollbar Horizontal");
        DeactivateIfFound(scrollView, "Scrollbar Vertical");

        RectTransform viewport = (RectTransform)Require(scrollView, "Viewport");
        Stretch(viewport);
        RectTransform content = (RectTransform)Require(viewport, "Content");
        SetRect(content, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 520f), content.localScale);
        GridLayoutGroup grid = content.GetComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(128f, 164f);
        grid.spacing = new Vector2(11f, 14f);
        grid.padding = new RectOffset(0, 0, 0, 0);
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment = TextAnchor.UpperCenter;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 5;

        // 페이지 넘김 ◀ 1 / 8 ▶
        float pagerY = -318f;
        TextMeshProUGUI pageText = EnsureText(textTemplate, page, "PageText", new Vector2(LeftPageCenterX, pagerY), new Vector2(160f, 52f), 28f, HorizontalAlignmentOptions.Center);
        pageText.text = "1 / 1";
        Button prevPage = EnsureArrowButton(closeButton, page, "PrevPageButton", arrowLeft, new Vector2(LeftPageCenterX - 110f, pagerY), arrowSize);
        Button nextPage = EnsureArrowButton(closeButton, page, "NextPageButton", arrowRight, new Vector2(LeftPageCenterX + 110f, pagerY), arrowSize);

        SetupDetail(detail, page);

        manager.titleText = title;
        manager.progressText = progress;
        manager.biomeNameText = biomeName;
        manager.prevBiomeButton = prevBiome;
        manager.nextBiomeButton = nextBiome;
        manager.pageText = pageText;
        manager.prevPageButton = prevPage;
        manager.nextPageButton = nextPage;
        manager.slotsPerPage = 15;
    }

    // 오른쪽 페이지: 아이콘 액자 → 이름 → 효과 → 획득 바이옴. 인벤토리용 FigureDetailPanel은 건드리지 않는다.
    static void SetupDetail(Transform detail, RectTransform page)
    {
        SetParent(detail, page);
        SetLastSibling(detail);
        SetCenterRect((RectTransform)detail, new Vector2(RightPageCenterX, PageCenterY), new Vector2(RightPageWidth, PageHeight));

        // 중첩 캔버스가 정렬을 따로 잡으면 도감 캔버스(400) 아래로 깔리므로 부모를 따르게 한다.
        Canvas detailCanvas = detail.GetComponent<Canvas>();
        if (detailCanvas != null)
        {
            SerializedObject so = new SerializedObject(detailCanvas);
            so.FindProperty("m_OverrideSorting").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        FigureDetailPanel panel = detail.GetComponent<FigureDetailPanel>();
        panel.useCustomTextColors = true;
        EditorUtility.SetDirty(panel);

        Transform woodBlock = Require(detail, "Fig_Panel");
        Transform iconFrame = FindDirect(detail, "IconFrame");
        if (iconFrame == null)
        {
            iconFrame = Object.Instantiate(woodBlock.gameObject, detail).transform;
            iconFrame.name = "IconFrame";
        }
        SetActive(woodBlock.gameObject, false);
        SetActive(iconFrame.gameObject, true);
        Image frameImage = iconFrame.GetComponent<Image>();
        frameImage.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(IconFrameSpritePath);
        frameImage.color = Color.white;
        frameImage.raycastTarget = false;
        SetCenterRect((RectTransform)iconFrame, new Vector2(0f, 238f), new Vector2(236f, 222f));

        Image figureImage = panel.figureImage;
        figureImage.preserveAspect = true;
        figureImage.raycastTarget = false;
        SetCenterRect(figureImage.rectTransform, new Vector2(0f, 238f), new Vector2(170f, 170f));
        SetSibling(iconFrame, woodBlock.GetSiblingIndex() + 1);
        SetSibling(figureImage.transform, iconFrame.GetSiblingIndex() + 1);

        TextMeshProUGUI nameText = panel.nameText;
        SetCenterRect(nameText.rectTransform, new Vector2(0f, 88f), new Vector2(620f, 64f));
        StyleText(nameText, 38f, HorizontalAlignmentOptions.Center, VerticalAlignmentOptions.Middle);
        nameText.enableAutoSizing = true;
        nameText.fontSizeMin = 24f;
        nameText.fontSizeMax = 38f;
        nameText.textWrappingMode = TextWrappingModes.NoWrap;
        nameText.overflowMode = TextOverflowModes.Ellipsis;

        TextMeshProUGUI descText = panel.descText;
        SetCenterRect(descText.rectTransform, new Vector2(0f, -78f), new Vector2(600f, 240f));
        StyleText(descText, 26f, HorizontalAlignmentOptions.Left, VerticalAlignmentOptions.Top);
        descText.textWrappingMode = TextWrappingModes.Normal;
        descText.overflowMode = TextOverflowModes.Ellipsis;

        TextMeshProUGUI locationText = panel.locationText;
        SetCenterRect(locationText.rectTransform, new Vector2(0f, -262f), new Vector2(600f, 72f));
        StyleText(locationText, 24f, HorizontalAlignmentOptions.Left, VerticalAlignmentOptions.Top);
        locationText.textWrappingMode = TextWrappingModes.Normal;
    }

    static void SetupSynergyPage(RectTransform page, GameObject synergyPagePrefab, CollectionBookManager manager)
    {
        Transform instance = FindDirect(page, "SynergyWikiPage");
        if (instance == null)
        {
            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(synergyPagePrefab, page);
            go.name = "SynergyWikiPage";
            instance = go.transform;
        }

        RectTransform rect = (RectTransform)instance;
        float width = SpreadRight - SpreadLeft;
        Vector2 center = new Vector2(0.5f, 0.5f);
        SetRect(rect, center, center, center, new Vector2((SpreadLeft + SpreadRight) * 0.5f, PageCenterY),
            new Vector2(width / SynergyPageScale, PageHeight / SynergyPageScale),
            new Vector3(SynergyPageScale, SynergyPageScale, 1f));
        SetActive(instance.gameObject, true);
        SetActive(page.gameObject, false);

        manager.synergyWiki = instance.GetComponent<SynergyWikiDisplayer>();
    }

    // ───────────────────────── 시너지 매니저 ─────────────────────────

    // 시너지 데이터는 SynergyWiki 테스트 씬에만 있어서 본편 씬에도 SynergyManager를 둔다.
    static void EnsureSynergyManager(Scene scene)
    {
        GameObject go = scene.GetRootGameObjects().FirstOrDefault(r => r.GetComponent<SynergyManager>() != null);
        if (go == null)
        {
            go = new GameObject("SynergyManager");
            SceneManager.MoveGameObjectToScene(go, scene);
        }
        SynergyManager synergyManager = GetOrAdd<SynergyManager>(go);

        SynergyData[] synergies = AssetDatabase.FindAssets("t:SynergyData", new[] { SynergyDataFolder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(AssetDatabase.LoadAssetAtPath<SynergyData>)
            .Where(data => data != null)
            .ToArray();

        SerializedObject so = new SerializedObject(synergyManager);
        SerializedProperty list = so.FindProperty("synergies");
        list.arraySize = synergies.Length;
        for (int i = 0; i < synergies.Length; i++)
            list.GetArrayElementAtIndex(i).objectReferenceValue = synergies[i];
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(synergyManager);
        Debug.Log("[CollectionBook] SynergyManager synergies=" + synergies.Length);
    }

    // ───────────────────────── 공용 ─────────────────────────

    static TextMeshProUGUI EnsureText(Transform template, Transform parent, string name, Vector2 position, Vector2 size, float fontSize, HorizontalAlignmentOptions alignment)
    {
        Transform found = FindDirect(parent, name);
        if (found == null)
        {
            found = Object.Instantiate(template.gameObject, parent).transform;
            found.name = name;
        }
        SetActive(found.gameObject, true);
        TextMeshProUGUI text = found.GetComponent<TextMeshProUGUI>();
        SetCenterRect(text.rectTransform, position, size);
        StyleText(text, fontSize, alignment, VerticalAlignmentOptions.Middle);
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }

    static void StyleText(TextMeshProUGUI text, float fontSize, HorizontalAlignmentOptions horizontal, VerticalAlignmentOptions vertical)
    {
        text.fontSize = fontSize;
        text.enableAutoSizing = false;
        text.horizontalAlignment = horizontal;
        text.verticalAlignment = vertical;
        text.color = Ink;
        text.raycastTarget = false;
    }

    static Button EnsureArrowButton(Transform template, Transform parent, string name, Sprite sprite, Vector2 position, Vector2 size)
    {
        Transform found = FindDirect(parent, name);
        if (found == null)
        {
            found = Object.Instantiate(template.gameObject, parent).transform;
            found.name = name;
        }
        SetActive(found.gameObject, true);
        SetCenterRect((RectTransform)found, position, size);
        Image image = found.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        Button button = found.GetComponent<Button>();
        ClearPersistentOnClick(button);
        return button;
    }

    static void ClearPersistentOnClick(Button button)
    {
        SerializedObject so = new SerializedObject(button);
        so.FindProperty("m_OnClick.m_PersistentCalls.m_Calls").ClearArray();
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static RectTransform EnsureContainer(Transform parent, string name)
    {
        Transform found = FindDirect(parent, name);
        if (found == null)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            found = go.transform;
        }
        RectTransform rect = (RectTransform)found;
        Stretch(rect);
        return rect;
    }

    static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        string parent = System.IO.Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(folder));
    }

    static Sprite LoadSprite(string path, string spriteName)
    {
        var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>();
        return spriteName == null ? sprites.FirstOrDefault() : sprites.FirstOrDefault(s => s.name == spriteName);
    }

    static T GetOrAdd<T>(GameObject go) where T : Component
    {
        T component = go.GetComponent<T>();
        return component != null ? component : go.AddComponent<T>();
    }

    // 같은 값을 다시 쓰면 부동소수 오차로 씬 diff가 생기므로 다를 때만 쓴다.
    static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta, Vector3 scale)
    {
        if (rect.anchorMin != anchorMin) rect.anchorMin = anchorMin;
        if (rect.anchorMax != anchorMax) rect.anchorMax = anchorMax;
        if (rect.pivot != pivot) rect.pivot = pivot;
        if (rect.localScale != scale) rect.localScale = scale;
        if (rect.localRotation != Quaternion.identity) rect.localRotation = Quaternion.identity;
        if (rect.anchoredPosition != anchoredPosition) rect.anchoredPosition = anchoredPosition;
        if (rect.localPosition.z != 0f) rect.anchoredPosition3D = new Vector3(anchoredPosition.x, anchoredPosition.y, 0f);
        if (rect.sizeDelta != sizeDelta) rect.sizeDelta = sizeDelta;
    }

    static void SetCenterRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        Vector2 center = new Vector2(0.5f, 0.5f);
        SetRect(rect, center, center, center, position, size, Vector3.one);
    }

    static void SetAnchors(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
    {
        SetAnchors(rect, anchorMin, anchorMax, Vector2.zero, Vector2.zero);
    }

    // 피벗이 가운데일 때 offsetMin/Max를 위치·크기로 바꿔 쓴다. offsetMin/Max 세터는 오차가 생겨서 쓰지 않는다.
    static void SetAnchors(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        Vector2 center = new Vector2(0.5f, 0.5f);
        SetRect(rect, anchorMin, anchorMax, center, (offsetMin + offsetMax) * 0.5f, offsetMax - offsetMin, Vector3.one);
    }

    static void Stretch(RectTransform rect)
    {
        SetAnchors(rect, Vector2.zero, Vector2.one);
    }

    // 가로 앵커는 그대로 두고 offsetMin.x / offsetMax.x에 해당하는 값만 맞춘다.
    static void SetHorizontalOffsets(RectTransform rect, float offsetMinX, float offsetMaxX)
    {
        float width = offsetMaxX - offsetMinX;
        float x = offsetMinX + width * rect.pivot.x;
        Vector2 position = new Vector2(x, rect.anchoredPosition.y);
        Vector2 size = new Vector2(width, rect.sizeDelta.y);
        if (rect.anchoredPosition != position) rect.anchoredPosition = position;
        if (rect.sizeDelta != size) rect.sizeDelta = size;
    }

    static void SetParent(Transform child, Transform parent)
    {
        if (child.parent != parent) child.SetParent(parent, false);
    }

    static void SetSibling(Transform child, int index)
    {
        if (child.GetSiblingIndex() != index) child.SetSiblingIndex(index);
    }

    static void SetLastSibling(Transform child)
    {
        SetSibling(child, child.parent.childCount - 1);
    }

    static void SetActive(GameObject go, bool active)
    {
        if (go.activeSelf != active) go.SetActive(active);
    }

    // 끄는 오브젝트는 활성 상태만 바꾸고 RectTransform은 건드리지 않는다(롤백 시 원래 자리 그대로).
    static void DeactivateIfFound(Transform root, string name)
    {
        Transform found = FindAnywhere(root, name);
        if (found != null) SetActive(found.gameObject, false);
    }

    static Transform Require(Transform parent, string path)
    {
        Transform found = parent.Find(path);
        if (found == null) throw new InvalidOperationException($"'{parent.name}' 아래에서 '{path}'를 찾지 못했습니다.");
        return found;
    }

    static Transform FindDirect(Transform parent, string name)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == name) return child;
        }
        return null;
    }

    // 옮긴 뒤에도 다시 찾을 수 있게 하위 전체에서 이름으로 찾는다.
    static Transform FindAnywhere(Transform root, string name)
    {
        return root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name && t != root);
    }

    static string ChildNames(Transform parent)
    {
        var names = new System.Text.StringBuilder();
        for (int i = 0; i < parent.childCount; i++)
        {
            if (i > 0) names.Append(" > ");
            Transform child = parent.GetChild(i);
            names.Append(child.gameObject.activeSelf ? child.name : "(" + child.name + ")");
        }
        return names.ToString();
    }
}
#endif
