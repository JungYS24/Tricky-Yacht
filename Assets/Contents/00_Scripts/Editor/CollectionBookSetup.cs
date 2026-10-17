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
    const string LockSpritePath = "Assets/Contents/06_UI/book_ui_buttons/Icon_Lock.png";
    const string BookUiFolder = "Assets/Contents/06_UI/book_ui_buttons";
    const string DropdownUiFolder = BookUiFolder + "/dropdown_ui";
    // CrossBTN, slot_dark_small_1 과 같은 PPU.
    const float BookSpritePpu = 100f;
    static readonly Vector2 ArrowButtonSize = new Vector2(64f, 64f);
    static readonly Vector2 CloseButtonSize = new Vector2(96f, 64f);

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
    // 오른쪽 페이지 안쪽. 양옆 28px를 빼 아이콘이 양피지 안에 남게 한다.
    const float RightDetailWidth = 620f;
    const float RightDetailHeight = 700f;
    const float DetailPadding = 8f;
    const float FigureSpacing = 12f;
    const float FigureCellMax = 96f;

    // 시너지 행은 왼쪽 페이지에 피규어, 오른쪽 페이지에 설명이 오도록 펼침면 전체에 축소 배치한다.
    const float SynergyPageScale = 0.76f;
    const float SynergyDescriptionLeft = 1010f;

    static readonly Color Ink = new Color(0.29f, 0.18f, 0.1f, 1f);
    static readonly Color DimColor = new Color(0f, 0f, 0f, 0.65f);

    // 책갈피 스프라이트(300x250)는 리본이 x 5%~92.7%, y 36%~64%만 차지한다.
    static readonly Vector2 TabSize = new Vector2(200f, 193f);
    const float TabCenterX = -846f;
    static readonly float[] TabCenterY = { 330f, 262f, 194f };
    static readonly Vector4 TabRaycastPadding = new Vector4(10f, 69.5f, 14.6f, 69.5f);
    // 책 이미지는 책갈피 위에 그려지지만, 리본이 튀어나온 왼쪽만 클릭을 통과시킨다.
    // 탭 클릭 오른쪽 끝은 -779.6, 양피지 왼쪽은 -765이므로 레이캐스트는 -768부터 시작한다.
    const float BookRaycastLeftInset = 232f;
    static readonly Vector2 TabLabelOffsetMin = new Vector2(55f, 69.5f);
    static readonly Vector2 TabLabelOffsetMax = new Vector2(-59f, -69.5f);
    static readonly Vector3 TabScale = new Vector3(0.9f, 1f, 1f);
    static readonly Vector3 TabLabelScale = new Vector3(1.075f, 1f, 1f);

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
            ConfigureBookSprites();
            ConfigureSlotPanel();
            ConfigureDropdownSprites();
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
            SetupSynergyList(root, displayer, so, reverseLabel);
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

        // 책갈피는 사용자가 책 위로 올려 둔 순서를 유지한다. 책, 페이지, 닫기, 탭.
        SetParent(bookImage, bookRoot);
        Stretch((RectTransform)bookImage);
        SetSibling(bookImage, 0);
        // 순서를 바꾸면 책갈피가 책 위로 올라온다. 그리기 순서는 두고 겹치는 왼쪽만 클릭에서 뺀다.
        Image bookGraphic = bookImage.GetComponent<Image>();
        Vector4 bookPadding = new Vector4(BookRaycastLeftInset, 0f, 0f, 0f);
        if (bookGraphic.raycastPadding != bookPadding) bookGraphic.raycastPadding = bookPadding;

        RectTransform figurePage = EnsureContainer(bookRoot, "Page_Figure");
        SetSibling(figurePage, 1);
        SetActive(figurePage.gameObject, true);
        RectTransform synergyPage = EnsureContainer(bookRoot, "Page_Synergy");
        SetSibling(synergyPage, 2);

        SetParent(closeButton, bookRoot);
        SetCenterRect((RectTransform)closeButton, new Vector2(797f, 403f), CloseButtonSize);
        SetSibling(closeButton, 3);
        SetParent(tabGroup, bookRoot);
        Stretch((RectTransform)tabGroup);
        SetSibling(tabGroup, 4);
        ApplySpriteSwap(closeButton.GetComponent<Button>(), LoadBookSprite("Btn_Close"), LoadBookSprite("Btn_Close_Pressed"), null);

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
            SetCenterRect((RectTransform)tab, new Vector2(TabCenterX, TabCenterY[i]), TabSize, TabScale);

            Button button = tab.GetComponent<Button>();
            Image image = tab.GetComponent<Image>();
            image.raycastPadding = TabRaycastPadding;
            image.preserveAspect = false;

            TextMeshProUGUI label = tab.GetComponentInChildren<TextMeshProUGUI>(true);
            // 라벨은 LocalizationManager.GetUi로 넣으므로 패키지 로컬라이저는 끈다.
            LocalizeStringEvent localizer = label.GetComponent<LocalizeStringEvent>();
            if (localizer != null) localizer.enabled = false;
            SetAnchors(label.rectTransform, Vector2.zero, Vector2.one, TabLabelOffsetMin, TabLabelOffsetMax, TabLabelScale);
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

        Sprite arrowLeft = LoadBookSprite("Btn_Arrow_L");
        Sprite arrowLeftPressed = LoadBookSprite("Btn_Arrow_L_Pressed");
        Sprite arrowLeftDisabled = LoadBookSprite("Btn_Arrow_L_Disabled");
        Sprite arrowRight = LoadBookSprite("Btn_Arrow_R");
        Sprite arrowRightPressed = LoadBookSprite("Btn_Arrow_R_Pressed");
        Sprite arrowRightDisabled = LoadBookSprite("Btn_Arrow_R_Disabled");
        if (arrowLeft == null || arrowRight == null) throw new InvalidOperationException("도감 화살표 스프라이트를 찾지 못했습니다.");
        Vector2 arrowSize = ArrowButtonSize;

        // 헤더: 제목(왼쪽) + 수집률(오른쪽), 그 아래 ◀ 바이옴 ▶
        float headerY = 360f;
        TextMeshProUGUI title = EnsureText(textTemplate, page, "TitleText", new Vector2(LeftPageCenterX, headerY), new Vector2(LeftPageWidth, 56f), 38f, HorizontalAlignmentOptions.Left);
        title.text = "피규어 도감";
        TextMeshProUGUI progress = EnsureText(textTemplate, page, "ProgressText", new Vector2(LeftPageCenterX, headerY), new Vector2(LeftPageWidth, 56f), 30f, HorizontalAlignmentOptions.Right);
        progress.text = "0 / 0";

        // 제목(y 360, 높이 56)과 화살표가 맞닿아 있어서 바이옴 줄만 16 내린다. 제목·수집률은 그대로.
        // 가장 긴 이름: 한국어 스페셜·서커스·하늘섬(3자), 영어 Snowfield·Skyisland(9자), 일본어 ジャングル(6자).
        // 패널 폭 220은 그 이름을 폰트 28 안에 두고, 바이옴을 넘겨도 크기가 바뀌지 않는다.
        float biomeY = 286f;
        const float biomePlateWidth = 220f;
        float biomePlateHeight = arrowSize.y;
        const float biomeGap = 8f;
        float biomeArrowX = biomePlateWidth * 0.5f + biomeGap + arrowSize.x * 0.5f;
        Sprite biomePlateSprite = LoadBookSprite("Plate_Biome");
        if (biomePlateSprite == null) throw new InvalidOperationException("Plate_Biome 스프라이트를 찾지 못했습니다.");
        Image biomePlate = EnsureImage(page, "BiomePlate", biomePlateSprite, new Vector2(LeftPageCenterX, biomeY), new Vector2(biomePlateWidth, biomePlateHeight), Color.white);
        if (biomePlate.type != Image.Type.Sliced) biomePlate.type = Image.Type.Sliced;
        biomePlate.raycastTarget = false;
        if (Mathf.Abs(biomePlate.pixelsPerUnitMultiplier - 1f) > 0.001f) biomePlate.pixelsPerUnitMultiplier = 1f;
        TextMeshProUGUI biomeName = EnsureText(textTemplate, page, "BiomeNameText", new Vector2(LeftPageCenterX, biomeY), new Vector2(biomePlateWidth - 32f, biomePlateHeight - 16f), 28f, HorizontalAlignmentOptions.Center);
        biomeName.text = "전체";
        biomeName.fontStyle = FontStyles.Bold;
        biomeName.enableAutoSizing = true;
        biomeName.fontSizeMin = 18f;
        biomeName.fontSizeMax = 28f;
        biomeName.color = new Color(246f / 255f, 228f / 255f, 194f / 255f, 1f);
        biomeName.raycastTarget = false;
        int biomeTextIndex = biomeName.transform.GetSiblingIndex();
        if (biomePlate.transform.GetSiblingIndex() != biomeTextIndex - 1)
            SetSibling(biomePlate.transform, biomeTextIndex);
        Button prevBiome = EnsureArrowButton(closeButton, page, "PrevBiomeButton", arrowLeft, new Vector2(LeftPageCenterX - biomeArrowX, biomeY), arrowSize);
        Button nextBiome = EnsureArrowButton(closeButton, page, "NextBiomeButton", arrowRight, new Vector2(LeftPageCenterX + biomeArrowX, biomeY), arrowSize);
        ApplySpriteSwap(prevBiome, arrowLeft, arrowLeftPressed, null);
        ApplySpriteSwap(nextBiome, arrowRight, arrowRightPressed, null);

        // 그리드: 5열 x 3행 = 15칸, 스크롤 대신 페이지 넘김.
        // 바이옴 화살표 아래끝(254)과 그리드 위끝(254)이 맞닿아서 그리드도 16 내린다. 3행 높이 520은 유지한다.
        SetParent(scrollView, page);
        const float gridY = -22f;
        const float gridHeight = 520f;
        SetCenterRect((RectTransform)scrollView, new Vector2(LeftPageCenterX, gridY), new Vector2(LeftPageWidth, gridHeight));
        ScrollRect scrollRect = scrollView.GetComponent<ScrollRect>();
        if (scrollRect != null) scrollRect.enabled = false;
        DeactivateIfFound(scrollView, "Scrollbar Horizontal");
        DeactivateIfFound(scrollView, "Scrollbar Vertical");

        RectTransform viewport = (RectTransform)Require(scrollView, "Viewport");
        Stretch(viewport);
        RectTransform content = (RectTransform)Require(viewport, "Content");
        // 그리드 내용 높이는 하이어라키 값을 유지한다. 스크롤은 꺼져 있어서 뷰포트가 칸을 보여 준다.
        Vector2 contentSize = content.anchorMin.y > 0.5f ? content.sizeDelta : new Vector2(0f, gridHeight);
        SetRect(content, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, contentSize, content.localScale);
        GridLayoutGroup grid = content.GetComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(128f, 164f);
        grid.spacing = new Vector2(11f, 14f);
        grid.padding = new RectOffset(0, 0, 0, 0);
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment = TextAnchor.UpperCenter;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 5;

        // 페이지 넘김. 그리드 아래끝(-282)과 화살표가 겹치지 않게 8 더 내린다. 화살표 아래끝 -358, 양피지 아래 -409.
        float pagerY = -326f;
        TextMeshProUGUI pageText = EnsureText(textTemplate, page, "PageText", new Vector2(LeftPageCenterX, pagerY), new Vector2(160f, 52f), 28f, HorizontalAlignmentOptions.Center);
        pageText.text = "1 / 1";
        Button prevPage = EnsureArrowButton(closeButton, page, "PrevPageButton", arrowLeft, new Vector2(LeftPageCenterX - 110f, pagerY), arrowSize);
        Button nextPage = EnsureArrowButton(closeButton, page, "NextPageButton", arrowRight, new Vector2(LeftPageCenterX + 110f, pagerY), arrowSize);
        ApplySpriteSwap(prevPage, arrowLeft, arrowLeftPressed, arrowLeftDisabled);
        ApplySpriteSwap(nextPage, arrowRight, arrowRightPressed, arrowRightDisabled);

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
        SetActive(iconFrame.gameObject, false);
        DeactivateIfFound(detail, "LeftButton");
        DeactivateIfFound(detail, "RightButton");

        // 이름 72칸의 아래끝에서 12px 띄우고, 그 아래에 아이콘 박스를 둔다.
        // 100px 그림은 3배(300), 85px(끈끈이 공)는 3배(255), 바나나는 박스 안에 맞춘다.
        const float nameY = 308f;
        const float nameHeight = 72f;
        float iconBox = FigureDetailPanel.BookFigureIconBox;
        float iconY = nameY - nameHeight * 0.5f - 12f - iconBox * 0.5f;

        Image figureImage = panel.figureImage;
        figureImage.preserveAspect = true;
        figureImage.raycastTarget = false;
        SetCenterRect(figureImage.rectTransform, new Vector2(0f, iconY), new Vector2(iconBox, iconBox));

        TextMeshProUGUI nameText = panel.nameText;
        SetCenterRect(nameText.rectTransform, new Vector2(0f, nameY), new Vector2(620f, nameHeight));
        StyleText(nameText, 42f, HorizontalAlignmentOptions.Center, VerticalAlignmentOptions.Middle);
        nameText.fontStyle = FontStyles.Bold;
        nameText.enableAutoSizing = true;
        nameText.fontSizeMin = 24f;
        nameText.fontSizeMax = 42f;
        nameText.textWrappingMode = TextWrappingModes.NoWrap;
        nameText.overflowMode = TextOverflowModes.Ellipsis;
        nameText.transform.SetAsLastSibling();

        // 아이콘이 커진 만큼 효과 설명·획득 바이옴·구분선을 아래로 민다.
        const float descHeight = 56f;
        const float descWidth = 580f;
        float descTop = iconY - iconBox * 0.5f - 12f;
        TextMeshProUGUI descText = panel.descText;
        // 위쪽을 고정하고 내용만큼 아래로 늘어나게 피벗을 둔다. 짧은 글은 56을 유지한다.
        SetRect(descText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f),
            new Vector2(0f, descTop), new Vector2(descWidth, descHeight), Vector3.one);
        StyleText(descText, 24f, HorizontalAlignmentOptions.Center, VerticalAlignmentOptions.Top);
        descText.textWrappingMode = TextWrappingModes.Normal;
        descText.overflowMode = TextOverflowModes.Overflow;
        descText.enableAutoSizing = false;
        descText.fontSizeMin = 20f;
        descText.fontSizeMax = 24f;
        ContentSizeFitter descFitter = GetOrAdd<ContentSizeFitter>(descText.gameObject);
        if (descFitter.horizontalFit != ContentSizeFitter.FitMode.Unconstrained) descFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        if (descFitter.verticalFit != ContentSizeFitter.FitMode.PreferredSize) descFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        LayoutElement descElement = GetOrAdd<LayoutElement>(descText.gameObject);
        if (!Near(descElement.minHeight, descHeight)) descElement.minHeight = descHeight;
        if (descElement.preferredHeight >= 0f) descElement.preferredHeight = -1f;

        const float locationHeight = 32f;
        float locationY = descTop - descHeight - 4f - locationHeight * 0.5f;
        TextMeshProUGUI locationText = panel.locationText;
        SetCenterRect(locationText.rectTransform, new Vector2(0f, locationY), new Vector2(580f, locationHeight));
        StyleText(locationText, 22f, HorizontalAlignmentOptions.Center, VerticalAlignmentOptions.Middle);
        locationText.color = new Color(0.45f, 0.32f, 0.2f, 1f);
        locationText.textWrappingMode = TextWrappingModes.Normal;

        float dividerY = locationY - locationHeight * 0.5f - 8f;
        SetupDetailExtras(detail, panel, nameText, iconY, dividerY);
        Debug.Log("[CollectionBook] figure detail nameY=" + nameY + " iconY=" + iconY + " iconBox=" + iconBox
            + " descTop=" + descTop + " locationY=" + locationY + " dividerY=" + dividerY);
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
            new Vector2(width, PageHeight), Vector3.one);
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

    static bool Near(float a, float b)
    {
        return Mathf.Abs(a - b) < 0.01f;
    }

    static bool Near(Vector2 a, Vector2 b)
    {
        return Near(a.x, b.x) && Near(a.y, b.y);
    }

    static bool Near(Vector3 a, Vector3 b)
    {
        return Near(a.x, b.x) && Near(a.y, b.y) && Near(a.z, b.z);
    }

    // 같은 값을 다시 쓰면 부동소수 오차로 씬 diff가 생기므로 다를 때만 쓴다.
    static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta, Vector3 scale)
    {
        if (!Near(rect.anchorMin, anchorMin)) rect.anchorMin = anchorMin;
        if (!Near(rect.anchorMax, anchorMax)) rect.anchorMax = anchorMax;
        if (!Near(rect.pivot, pivot)) rect.pivot = pivot;
        if (!Near(rect.localScale, scale)) rect.localScale = scale;
        if (rect.localRotation != Quaternion.identity) rect.localRotation = Quaternion.identity;
        if (!Near(rect.anchoredPosition, anchoredPosition)) rect.anchoredPosition = anchoredPosition;
        if (!Near(rect.localPosition.z, 0f)) rect.anchoredPosition3D = new Vector3(anchoredPosition.x, anchoredPosition.y, 0f);
        if (!Near(rect.sizeDelta, sizeDelta)) rect.sizeDelta = sizeDelta;
    }

    static void SetCenterRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        SetCenterRect(rect, position, size, Vector3.one);
    }

    static void SetCenterRect(RectTransform rect, Vector2 position, Vector2 size, Vector3 scale)
    {
        Vector2 center = new Vector2(0.5f, 0.5f);
        SetRect(rect, center, center, center, position, size, scale);
    }

    static void SetAnchors(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
    {
        SetAnchors(rect, anchorMin, anchorMax, Vector2.zero, Vector2.zero);
    }

    // 피벗이 가운데일 때 offsetMin/Max를 위치·크기로 바꿔 쓴다. offsetMin/Max 세터는 오차가 생겨서 쓰지 않는다.
    static void SetAnchors(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        SetAnchors(rect, anchorMin, anchorMax, offsetMin, offsetMax, Vector3.one);
    }

    static void SetAnchors(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Vector3 scale)
    {
        Vector2 center = new Vector2(0.5f, 0.5f);
        SetRect(rect, anchorMin, anchorMax, center, (offsetMin + offsetMax) * 0.5f, offsetMax - offsetMin, scale);
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

    static void ConfigureBookSprites()
    {
        foreach (string file in System.IO.Directory.GetFiles(BookUiFolder, "*.png"))
        {
            string path = file.Replace('\\', '/');
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;

            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            Vector4 border = Vector4.zero;
            if (name == "Badge_Count") border = new Vector4(12f, 12f, 12f, 12f);
            else if (name == "Plate_Biome") border = new Vector4(16f, 16f, 16f, 16f);
            else if (name.StartsWith("SynergyRow")) border = new Vector4(82f, 10f, 14f, 10f);

            bool changed = false;
            if (importer.textureType != TextureImporterType.Sprite) { importer.textureType = TextureImporterType.Sprite; changed = true; }
            if (importer.spriteImportMode != SpriteImportMode.Single) { importer.spriteImportMode = SpriteImportMode.Single; changed = true; }
            if (importer.mipmapEnabled) { importer.mipmapEnabled = false; changed = true; }
            if (importer.filterMode != FilterMode.Point) { importer.filterMode = FilterMode.Point; changed = true; }
            if (importer.textureCompression != TextureImporterCompression.Uncompressed) { importer.textureCompression = TextureImporterCompression.Uncompressed; changed = true; }
            if (Mathf.Abs(importer.spritePixelsPerUnit - BookSpritePpu) > 0.01f) { importer.spritePixelsPerUnit = BookSpritePpu; changed = true; }
            if (importer.spriteBorder != border) { importer.spriteBorder = border; changed = true; }
            if (changed) importer.SaveAndReimport();
        }
        Debug.Log("[CollectionBook] book sprites PPU=" + BookSpritePpu + " (CrossBTN, slot_dark_small_1)");
    }

    // 172x162 직사각형. 테두리 약 20px라 칸에 늘릴 때 9-slice로 쓴다.
    static void ConfigureSlotPanel()
    {
        TextureImporter importer = AssetImporter.GetAtPath(IconFrameSpritePath) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("slot_dark_small_1 임포터를 찾지 못했습니다.");
        Vector4 border = new Vector4(20f, 20f, 20f, 20f);
        bool changed = false;
        if (importer.textureType != TextureImporterType.Sprite) { importer.textureType = TextureImporterType.Sprite; changed = true; }
        if (importer.spriteImportMode != SpriteImportMode.Single) { importer.spriteImportMode = SpriteImportMode.Single; changed = true; }
        if (importer.mipmapEnabled) { importer.mipmapEnabled = false; changed = true; }
        if (importer.filterMode != FilterMode.Point) { importer.filterMode = FilterMode.Point; changed = true; }
        if (importer.textureCompression != TextureImporterCompression.Uncompressed) { importer.textureCompression = TextureImporterCompression.Uncompressed; changed = true; }
        if (importer.crunchedCompression) { importer.crunchedCompression = false; changed = true; }
        if (importer.spriteBorder != border) { importer.spriteBorder = border; changed = true; }
        if (changed) importer.SaveAndReimport();
        Debug.Log("[CollectionBook] slot_dark_small_1 172x162 border=20 Point uncompressed");
    }

    static void ConfigureDropdownSprites()
    {
        foreach (string file in System.IO.Directory.GetFiles(DropdownUiFolder, "*.png"))
        {
            string path = file.Replace('\\', '/');
            int assets = path.IndexOf("Assets/", StringComparison.Ordinal);
            if (assets >= 0) path = path.Substring(assets);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            Vector4 border = Vector4.zero;
            if (name == "Dropdown_Field" || name == "Dropdown_Field_Pressed" || name == "Dropdown_List")
                border = new Vector4(12f, 12f, 12f, 12f);
            else if (name == "Dropdown_Item_Highlight")
                border = new Vector4(8f, 8f, 8f, 8f);

            bool changed = false;
            if (importer.textureType != TextureImporterType.Sprite) { importer.textureType = TextureImporterType.Sprite; changed = true; }
            if (importer.spriteImportMode != SpriteImportMode.Single) { importer.spriteImportMode = SpriteImportMode.Single; changed = true; }
            if (importer.mipmapEnabled) { importer.mipmapEnabled = false; changed = true; }
            if (importer.filterMode != FilterMode.Point) { importer.filterMode = FilterMode.Point; changed = true; }
            if (importer.textureCompression != TextureImporterCompression.Uncompressed) { importer.textureCompression = TextureImporterCompression.Uncompressed; changed = true; }
            if (importer.crunchedCompression) { importer.crunchedCompression = false; changed = true; }
            if (Mathf.Abs(importer.spritePixelsPerUnit - BookSpritePpu) > 0.01f) { importer.spritePixelsPerUnit = BookSpritePpu; changed = true; }
            if (importer.spriteBorder != border) { importer.spriteBorder = border; changed = true; }
            if (changed) importer.SaveAndReimport();
        }
        Debug.Log("[CollectionBook] dropdown sprites Point uncompressed PPU=" + BookSpritePpu);
    }

    static Sprite LoadBookSprite(string name)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>(BookUiFolder + "/" + name + ".png");
    }

    static void ApplySpriteSwap(Button button, Sprite normal, Sprite pressed, Sprite disabled)
    {
        if (button == null || normal == null) return;
        Image image = button.GetComponent<Image>();
        if (image.sprite != normal) image.sprite = normal;
        if (image.type != Image.Type.Simple) image.type = Image.Type.Simple;
        if (button.transition != Selectable.Transition.SpriteSwap) button.transition = Selectable.Transition.SpriteSwap;
        SpriteState state = button.spriteState;
        if (state.highlightedSprite == normal && state.pressedSprite == pressed && state.selectedSprite == normal && state.disabledSprite == disabled)
            return;
        state.highlightedSprite = normal;
        state.pressedSprite = pressed;
        state.selectedSprite = normal;
        state.disabledSprite = disabled;
        button.spriteState = state;
    }

    static void SetupDetailExtras(Transform detail, FigureDetailPanel panel, TextMeshProUGUI template, float iconY, float dividerY)
    {
        Sprite knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        Sprite white = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
        Sprite arrowLeft = LoadBookSprite("Btn_Arrow_L");
        Sprite arrowLeftPressed = LoadBookSprite("Btn_Arrow_L_Pressed");
        Sprite arrowRight = LoadBookSprite("Btn_Arrow_R");
        Sprite arrowRightPressed = LoadBookSprite("Btn_Arrow_R_Pressed");
        Sprite badge = LoadBookSprite("Badge_Count");

        float iconBox = FigureDetailPanel.BookFigureIconBox;
        float shadowWidth = iconBox * 0.72f;
        Image figureShadow = EnsureImage(detail, "FigureShadow", knob, new Vector2(0f, iconY - iconBox * 0.42f), new Vector2(shadowWidth, shadowWidth * 0.22f), new Color(0f, 0f, 0f, 0.28f));
        int figureIndex = panel.figureImage.transform.GetSiblingIndex();
        if (figureShadow.transform.GetSiblingIndex() != figureIndex - 1)
            SetSibling(figureShadow.transform, figureIndex);

        Image divider = EnsureImage(detail, "Divider", white, new Vector2(0f, dividerY), new Vector2(560f, 3f), new Color(Ink.r, Ink.g, Ink.b, 0.35f));
        divider.raycastTarget = false;

        // 드롭 몬스터: 라벨·이름·출현 바이옴을 4px 간격으로 붙인다.
        // 굵은 글꼴 줄 높이(포인트 30에서 43.4)가 칸보다 크면 말줄임이 한 줄을 통째로 지운다. 이름 칸은 줄 높이보다 크게 둔다.
        // 몬스터 128px를 2배(256)로 키우면 양피지 아래(-409)를 넘는다. 페이지 안에 남는 정수 배는 1배다.
        const float labelHeight = 28f;
        const float dropNameHeight = 40f;
        const float biomeHeight = 28f;
        const float textGap = 4f;
        const float monsterGap = 6f;
        float monsterBox = FigureDetailPanel.BookMonsterIconBox;
        float labelY = dividerY - 8f - labelHeight * 0.5f;
        float dropNameY = labelY - labelHeight * 0.5f - textGap - dropNameHeight * 0.5f;
        float biomeTextY = dropNameY - dropNameHeight * 0.5f - textGap - biomeHeight * 0.5f;
        float monsterY = biomeTextY - biomeHeight * 0.5f - monsterGap - monsterBox * 0.5f;
        float contentTop = labelY + labelHeight * 0.5f;
        float contentBottom = monsterY - monsterBox * 0.5f;
        float columnY = (contentTop + contentBottom) * 0.5f;
        float columnHeight = contentTop - contentBottom + 8f;
        const float columnWidth = 300f;

        Transform dropRoot = EnsureChild(detail, "DropMonsterRoot");
        SetCenterRect((RectTransform)dropRoot, new Vector2(-160f, columnY), new Vector2(columnWidth, columnHeight));
        float dropLocal = -columnY;
        TextMeshProUGUI dropLabel = EnsureText(template.transform, dropRoot, "Label", new Vector2(0f, labelY + dropLocal), new Vector2(260f, labelHeight), 18f, HorizontalAlignmentOptions.Center);
        dropLabel.color = new Color(0.45f, 0.32f, 0.2f, 1f);
        // 하이어라키에서 지운 몬스터 이름·출현 바이옴은 다시 만들지 않는다.
        // 스프라이트 배율 1.5와 위치는 사용자가 맞춘 값을 유지하고, 미리 넣어 둔 스프라이트는 지우지 않는다.
        Transform monsterTransform = FindDirect(dropRoot, "MonsterImage");
        if (monsterTransform == null) monsterTransform = NewImage(dropRoot, "MonsterImage");
        Image monsterImage = monsterTransform.GetComponent<Image>();
        monsterImage.preserveAspect = true;
        monsterImage.raycastTarget = false;
        if (monsterImage.color != Color.white) monsterImage.color = Color.white;
        SetCenterRect(monsterImage.rectTransform, new Vector2(0f, -9.3f), new Vector2(monsterBox, monsterBox), new Vector3(1.5f, 1.5f, 1.5f));
        // 피규어 아이콘 그림자와 같은 옅은 타원. 보이는 폭의 약 70%, 발밑, 스프라이트보다 뒤.
        float monsterVisual = monsterBox * 1.5f;
        float monsterShadowWidth = monsterVisual * 0.72f;
        Image monsterShadow = EnsureImage(dropRoot, "MonsterShadow", knob,
            new Vector2(0f, -9.3f - monsterVisual * 0.42f),
            new Vector2(monsterShadowWidth, monsterShadowWidth * 0.22f),
            new Color(0f, 0f, 0f, 0.28f));
        int monsterIndex = monsterImage.transform.GetSiblingIndex();
        if (monsterShadow.transform.GetSiblingIndex() != monsterIndex - 1)
            SetSibling(monsterShadow.transform, monsterIndex);
        float arrowX = monsterBox * 0.5f + 16f + ArrowButtonSize.x * 0.5f;
        Button dropPrev = EnsureBookArrow(dropRoot, "Prev", arrowLeft, new Vector2(-arrowX, monsterY + dropLocal));
        Button dropNext = EnsureBookArrow(dropRoot, "Next", arrowRight, new Vector2(arrowX, monsterY + dropLocal));
        ApplySpriteSwap(dropPrev, arrowLeft, arrowLeftPressed, null);
        ApplySpriteSwap(dropNext, arrowRight, arrowRightPressed, null);

        Transform synergyRoot = EnsureChild(detail, "RelatedSynergyRoot");
        SetCenterRect((RectTransform)synergyRoot, new Vector2(160f, columnY), new Vector2(columnWidth, columnHeight));
        TextMeshProUGUI synergyLabel = EnsureText(template.transform, synergyRoot, "Label", new Vector2(0f, labelY + dropLocal), new Vector2(260f, labelHeight), 18f, HorizontalAlignmentOptions.Center);
        synergyLabel.color = new Color(0.45f, 0.32f, 0.2f, 1f);
        RectTransform content = (RectTransform)EnsureChild(synergyRoot, "Content");
        // 제목 아래 8px에서 시작한다. 가로 2칸(96+12)이 페이지 안에 들어가고, 넘치면 런타임에서 줄을 바꾼다.
        float cardTop = labelY + dropLocal - labelHeight * 0.5f - 8f;
        SetRect(content, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f),
            new Vector2(0f, cardTop), new Vector2(276f, FigureCellMax * 2f + FigureSpacing), Vector3.one);
        HorizontalLayoutGroup oldRow = content.GetComponent<HorizontalLayoutGroup>();
        if (oldRow != null) Object.DestroyImmediate(oldRow);
        GridLayoutGroup oldGrid = content.GetComponent<GridLayoutGroup>();
        if (oldGrid != null) Object.DestroyImmediate(oldGrid);
        GameObject card = EnsureSynergyCard(synergyRoot, template, badge);
        SetActive(card, false);
        Debug.Log("[CollectionBook] drop columnY=" + columnY.ToString("0.##") + " labelY=" + (labelY + dropLocal).ToString("0.##")
            + " nameY=" + (dropNameY + dropLocal).ToString("0.##") + " biomeY=" + (biomeTextY + dropLocal).ToString("0.##")
            + " monsterY=" + (monsterY + dropLocal).ToString("0.##") + " monsterBottomBook=" + (monsterY - monsterBox * 0.5f + PageCenterY).ToString("0.##"));

        panel.figureShadow = figureShadow;
        panel.dropMonsterRoot = dropRoot.gameObject;
        panel.dropMonsterLabel = dropLabel;
        if (panel.dropMonsterName != null) panel.dropMonsterName = null;
        if (panel.dropMonsterBiome != null) panel.dropMonsterBiome = null;
        panel.dropMonsterImage = monsterImage;
        panel.dropMonsterShadow = monsterShadow;
        panel.dropMonsterPrev = dropPrev;
        panel.dropMonsterNext = dropNext;
        panel.relatedSynergyRoot = synergyRoot.gameObject;
        panel.relatedSynergyLabel = synergyLabel;
        panel.relatedSynergyContent = content;
        panel.synergyCardTemplate = card;
        panel.countBadgeSprite = badge;
        EditorUtility.SetDirty(panel);
    }

    static GameObject EnsureSynergyCard(Transform parent, TextMeshProUGUI template, Sprite badgeSprite)
    {
        Transform found = FindDirect(parent, "SynergyCardTemplate");
        if (found == null)
        {
            GameObject go = new GameObject("SynergyCardTemplate", typeof(RectTransform), typeof(Image), typeof(Button));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            found = go.transform;
            NewImage(found, "Icon");
            Transform badgeRoot = NewImage(found, "Badge");
            TextMeshProUGUI badgeText = Object.Instantiate(template, badgeRoot);
            badgeText.name = "Text";
            TextMeshProUGUI name = Object.Instantiate(template, found);
            name.name = "Name";
        }

        RectTransform rect = (RectTransform)found;
        if (rect.sizeDelta != new Vector2(FigureCellMax, FigureCellMax)) rect.sizeDelta = new Vector2(FigureCellMax, FigureCellMax);
        Image plate = found.GetComponent<Image>();
        Sprite slotSprite = LoadSprite(IconFrameSpritePath, null);
        if (slotSprite == null) throw new InvalidOperationException("slot_dark_small_1 스프라이트를 찾지 못했습니다.");
        ApplyImage(plate, slotSprite, Image.Type.Sliced, false, Color.white);
        if (Mathf.Abs(plate.pixelsPerUnitMultiplier - 1f) > 0.001f) plate.pixelsPerUnitMultiplier = 1f;
        plate.raycastTarget = true;
        Button button = found.GetComponent<Button>();
        button.targetGraphic = plate;
        if (button.transition != Selectable.Transition.None) button.transition = Selectable.Transition.None;

        Image icon = found.Find("Icon").GetComponent<Image>();
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        if (icon.color != Color.white) icon.color = Color.white;
        FitIconBox(icon.rectTransform);

        // 배지 원본은 68x36, 테두리 12. 52x28로 줄이면 가운데가 찌그러져 깨져 보인다.
        Image badge = found.Find("Badge").GetComponent<Image>();
        badge.sprite = badgeSprite;
        badge.type = Image.Type.Sliced;
        badge.color = Color.white;
        badge.raycastTarget = false;
        if (Mathf.Abs(badge.pixelsPerUnitMultiplier - 1f) > 0.001f) badge.pixelsPerUnitMultiplier = 1f;
        SetRect(badge.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0.5f),
            new Vector2(-18f, 8f), new Vector2(68f, 36f), Vector3.one);
        TextMeshProUGUI count = badge.GetComponentInChildren<TextMeshProUGUI>(true);
        SetAnchors(count.rectTransform, Vector2.zero, Vector2.one);
        count.fontSize = 16f;
        count.fontStyle = FontStyles.Bold;
        count.alignment = TextAlignmentOptions.Center;
        count.color = new Color(0.9647f, 0.9176f, 0.8157f, 1f);
        count.raycastTarget = false;
        count.textWrappingMode = TextWrappingModes.NoWrap;
        count.overflowMode = TextOverflowModes.Overflow;

        // 이름 칸의 말줄임(…)이 화면의 •••로 보였다. 시너지 이름은 표시하지 않는다.
        TextMeshProUGUI cardName = found.Find("Name").GetComponent<TextMeshProUGUI>();
        if (cardName.gameObject.activeSelf) cardName.gameObject.SetActive(false);
        return found.gameObject;
    }

    static void SetupSynergyList(GameObject root, SynergyWikiDisplayer displayer, SerializedObject so, TextMeshProUGUI textTemplate)
    {
        Sprite normal = LoadBookSprite("SynergyRow_Normal");
        Sprite selected = LoadBookSprite("SynergyRow_Selected");
        RectTransform scroll = (RectTransform)Require(root.transform, "Scroll View");
        ScrollRect scrollRect = scroll.GetComponent<ScrollRect>();

        // 시너지 페이지는 책 중앙에서 (14.5, 14)에 있다. 피규어 제목(책 -378, 360)과 같은 높이.
        float offsetX = (SpreadLeft + SpreadRight) * 0.5f;
        float headerY = 360f - PageCenterY;
        float left0 = LeftPageCenterX - LeftPageWidth * 0.5f - offsetX;
        float left1 = LeftPageCenterX + LeftPageWidth * 0.5f - offsetX;
        float pageLeft = -(SpreadRight - SpreadLeft) * 0.5f;
        float pageBottom = -PageHeight * 0.5f;
        float pageWidth = SpreadRight - SpreadLeft;
        const float inset = 16f;
        float scrollTop = headerY - 44f;
        SetAnchors(scroll,
            new Vector2((left0 + inset - pageLeft) / pageWidth, 24f / PageHeight),
            new Vector2((left1 - inset - pageLeft) / pageWidth, (scrollTop - pageBottom) / PageHeight));

        TextMeshProUGUI listTitle = EnsureText(textTemplate.transform, root.transform, "ListTitle",
            new Vector2(left0 + inset + 100f, headerY), new Vector2(200f, 56f), 38f, HorizontalAlignmentOptions.Left);
        listTitle.text = "시너지";

        RectTransform toggle = (RectTransform)Require(root.transform, "Toggle");
        Vector2 toggleSize = toggle.sizeDelta.x > 1f ? toggle.sizeDelta : new Vector2(150f, 50f);
        float toggleX = left1 - inset - toggleSize.x * 0.5f;
        SetCenterRect(toggle, new Vector2(toggleX, headerY), toggleSize);
        RectTransform order = (RectTransform)Require(root.transform, "Order Option");
        Vector2 orderSize = new Vector2(220f, 44f);
        float orderX = toggleX - toggleSize.x * 0.5f - 12f - orderSize.x * 0.5f;
        SetCenterRect(order, new Vector2(orderX, headerY), orderSize);
        SetLastSibling(listTitle.transform);
        SetLastSibling(order);
        SetLastSibling(toggle);

        RectTransform content = (RectTransform)Require(root.transform, "Scroll View/Viewport/Content");
        GridLayoutGroup contentGrid = content.GetComponent<GridLayoutGroup>();
        if (contentGrid != null && contentGrid.enabled) contentGrid.enabled = false;
        VerticalLayoutGroup layout = GetOrAdd<VerticalLayoutGroup>(content.gameObject);
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        if (content.pivot != new Vector2(0.5f, 1f)) content.pivot = new Vector2(0.5f, 1f);
        if (content.anchorMin != new Vector2(0f, 1f)) content.anchorMin = new Vector2(0f, 1f);
        if (content.anchorMax != new Vector2(1f, 1f)) content.anchorMax = new Vector2(1f, 1f);
        ContentSizeFitter fitter = GetOrAdd<ContentSizeFitter>(content.gameObject);
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        Transform template = EnsureSynergyRow(root.transform, textTemplate, normal);
        Transform detail = EnsureChild(root.transform, "SynergyDetail");
        float detailX = RightPageCenterX - offsetX;
        SetCenterRect((RectTransform)detail, new Vector2(detailX, 0f), new Vector2(RightDetailWidth, RightDetailHeight));
        VerticalLayoutGroup column = GetOrAdd<VerticalLayoutGroup>(detail.gameObject);
        if (column.padding.left != (int)DetailPadding || column.padding.right != (int)DetailPadding || column.padding.top != 12 || column.padding.bottom != 12)
            column.padding = new RectOffset((int)DetailPadding, (int)DetailPadding, 12, 12);
        column.spacing = 12f;
        column.childAlignment = TextAnchor.UpperCenter;
        column.childControlWidth = true;
        column.childControlHeight = true;
        column.childForceExpandWidth = true;
        column.childForceExpandHeight = false;

        Transform header = EnsureChild(detail, "Header");
        LayoutElement headerElement = GetOrAdd<LayoutElement>(header.gameObject);
        headerElement.preferredHeight = 88f;
        headerElement.flexibleWidth = 1f;
        HorizontalLayoutGroup headerRow = GetOrAdd<HorizontalLayoutGroup>(header.gameObject);
        headerRow.spacing = 16f;
        headerRow.childAlignment = TextAnchor.MiddleCenter;
        headerRow.childControlWidth = false;
        headerRow.childControlHeight = false;
        headerRow.childForceExpandWidth = false;
        headerRow.childForceExpandHeight = false;
        Transform existingIcon = FindDirect(detail, "Icon");
        if (existingIcon != null) SetParent(existingIcon, header);
        Transform existingName = FindDirect(detail, "Name");
        if (existingName != null) SetParent(existingName, header);

        Image detailIcon = EnsureImage(header, "Icon", null, Vector2.zero, new Vector2(76f, 76f), Color.white);
        detailIcon.preserveAspect = true;
        TextMeshProUGUI detailName = EnsureText(textTemplate.transform, header, "Name", Vector2.zero, new Vector2(360f, 64f), 48f, HorizontalAlignmentOptions.Center);
        detailName.fontStyle = FontStyles.Bold;
        detailName.enableAutoSizing = true;
        detailName.fontSizeMin = 28f;
        detailName.fontSizeMax = 48f;
        detailName.textWrappingMode = TextWrappingModes.NoWrap;
        detailName.overflowMode = TextOverflowModes.Ellipsis;

        float innerWidth = RightDetailWidth - DetailPadding * 2f;
        TextMeshProUGUI detailDescription = EnsureText(textTemplate.transform, detail, "Description", Vector2.zero, new Vector2(innerWidth, 96f), 24f, HorizontalAlignmentOptions.Left);
        detailDescription.verticalAlignment = VerticalAlignmentOptions.Top;
        detailDescription.textWrappingMode = TextWrappingModes.Normal;
        detailDescription.overflowMode = TextOverflowModes.Overflow;
        // 헤더-설명 간격(12)은 유지하고, 설명과 칸 사이에만 32px를 더한다.
        // 사이 간격 = 12 + 스페이서 20 + 12 = 44.
        Transform spacer = EnsureChild(detail, "FigureGap");
        LayoutElement gap = GetOrAdd<LayoutElement>(spacer.gameObject);
        gap.minHeight = 20f;
        gap.preferredHeight = 20f;
        gap.flexibleHeight = 0f;
        gap.flexibleWidth = 1f;

        Transform figures = EnsureChild(detail, "Figures");
        LayoutElement figuresElement = GetOrAdd<LayoutElement>(figures.gameObject);
        figuresElement.preferredHeight = FigureCellMax * 2f + FigureSpacing;
        figuresElement.flexibleWidth = 1f;
        // 레이아웃 그룹은 한 오브젝트에 둘을 붙이면 새 컴포넌트가 바로 빠진다.
        HorizontalLayoutGroup oldRow = figures.GetComponent<HorizontalLayoutGroup>();
        if (oldRow != null) Object.DestroyImmediate(oldRow);
        GridLayoutGroup figureGrid = GetOrAdd<GridLayoutGroup>(figures.gameObject);
        if (figureGrid == null) throw new InvalidOperationException("피규어 칸 GridLayoutGroup을 만들지 못했습니다.");
        figureGrid.cellSize = new Vector2(FigureCellMax, FigureCellMax);
        figureGrid.spacing = new Vector2(FigureSpacing, FigureSpacing);
        figureGrid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        figureGrid.startAxis = GridLayoutGroup.Axis.Horizontal;
        figureGrid.childAlignment = TextAnchor.UpperCenter;
        figureGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        figureGrid.constraintCount = 5;
        Sprite slotSprite = LoadSprite(IconFrameSpritePath, null);
        if (slotSprite == null) throw new InvalidOperationException("slot_dark_small_1 스프라이트를 찾지 못했습니다.");
        GameObject slotTemplate = EnsureFigureSlot(detail, slotSprite);

        int maxFigures = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:SynergyData", new[] { SynergyDataFolder }))
        {
            SynergyData data = AssetDatabase.LoadAssetAtPath<SynergyData>(AssetDatabase.GUIDToAssetPath(guid));
            if (data == null || data.RequiredFigures == null) continue;
            maxFigures = Mathf.Max(maxFigures, data.RequiredFigures.Count);
        }

        float cellAtMax = Mathf.Min(FigureCellMax, (innerWidth - FigureSpacing * (maxFigures - 1)) / Mathf.Max(1, maxFigures));
        so.FindProperty("rowTemplate").objectReferenceValue = template.gameObject;
        so.FindProperty("rowNormalSprite").objectReferenceValue = normal;
        so.FindProperty("rowSelectedSprite").objectReferenceValue = selected;
        so.FindProperty("listScroll").objectReferenceValue = scrollRect;
        so.FindProperty("listTitle").objectReferenceValue = listTitle;
        so.FindProperty("detailName").objectReferenceValue = detailName;
        so.FindProperty("detailDescription").objectReferenceValue = detailDescription;
        so.FindProperty("detailIcon").objectReferenceValue = detailIcon;
        so.FindProperty("detailFigureRoot").objectReferenceValue = figures;
        so.FindProperty("detailFigureTemplate").objectReferenceValue = slotTemplate;
        so.FindProperty("figureAreaWidth").floatValue = innerWidth;
        SetSibling(header, 0);
        SetSibling(detailDescription.transform, 1);
        SetSibling(spacer, 2);
        SetSibling(figures, 3);
        SetLastSibling(slotTemplate.transform);
        StyleSynergyControls(order, toggle, textTemplate, template.Find("Name").GetComponent<TextMeshProUGUI>());
        Debug.Log("[CollectionBook] synergy figures max=" + maxFigures + " cellAtMax=" + cellAtMax.ToString("0.##") + " inner=" + innerWidth + " descToFigures=44");
    }

    static readonly Color DropdownInk = new Color(70f / 255f, 40f / 255f, 26f / 255f, 1f);

    // 정렬 드롭다운과 역순 토글에 dropdown_ui 스프라이트를 붙인다. 본체 위치·크기는 바꾸지 않는다.
    static void StyleSynergyControls(RectTransform order, RectTransform toggle, TextMeshProUGUI reverseLabel, TextMeshProUGUI rowFont)
    {
        Sprite field = LoadDropdownSprite("Dropdown_Field");
        Sprite pressed = LoadDropdownSprite("Dropdown_Field_Pressed");
        Sprite arrowSprite = LoadDropdownSprite("Dropdown_Arrow");
        Sprite list = LoadDropdownSprite("Dropdown_List");
        Sprite highlight = LoadDropdownSprite("Dropdown_Item_Highlight");
        Sprite check = LoadDropdownSprite("Dropdown_Check");
        Sprite toggleBox = LoadDropdownSprite("Toggle_Box");
        Sprite toggleCheck = LoadDropdownSprite("Toggle_Check");

        Image fieldImage = order.GetComponent<Image>();
        ApplyImage(fieldImage, field, Image.Type.Sliced, false, Color.white);
        if (Mathf.Abs(fieldImage.pixelsPerUnitMultiplier - 1f) > 0.001f) fieldImage.pixelsPerUnitMultiplier = 1f;
        TMP_Dropdown dropdown = order.GetComponent<TMP_Dropdown>();
        if (dropdown.targetGraphic != fieldImage) dropdown.targetGraphic = fieldImage;
        ApplySpriteSwap(dropdown, field, pressed);

        RectTransform arrow = (RectTransform)Require(order, "Arrow");
        Image arrowImage = arrow.GetComponent<Image>();
        ApplyImage(arrowImage, arrowSprite, Image.Type.Simple, true, Color.white);
        SetRect(arrow, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-6f, 0f), new Vector2(36f, 28f), Vector3.one);

        Transform template = Require(order, "Template");
        Image listImage = template.GetComponent<Image>();
        ApplyImage(listImage, list, Image.Type.Sliced, false, Color.white);
        if (Mathf.Abs(listImage.pixelsPerUnitMultiplier - 1f) > 0.001f) listImage.pixelsPerUnitMultiplier = 1f;
        RectTransform templateRect = (RectTransform)template;
        // 내용보다 짧게 시작하면 TMP가 늘리지 않고 아래 항목을 자른다. 길면 내용 높이에 맞춰 줄어든다.
        if (Mathf.Abs(templateRect.sizeDelta.y - 220f) > 0.01f)
            templateRect.sizeDelta = new Vector2(templateRect.sizeDelta.x, 220f);
        // 뷰포트를 줄이면 TMP가 목록 높이를 내용에 맞춘 뒤 마스크가 마지막 항목을 자른다.
        RectTransform viewport = (RectTransform)Require(template, "Viewport");
        SetAnchors(viewport, Vector2.zero, Vector2.one);
        ScrollRect listScroll = template.GetComponent<ScrollRect>();
        if (listScroll.verticalScrollbarVisibility != ScrollRect.ScrollbarVisibility.AutoHide)
            listScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        Transform scrollbar = template.Find("Scrollbar");
        if (scrollbar != null) SetActive(scrollbar.gameObject, false);

        Canvas listCanvas = GetOrAdd<Canvas>(template.gameObject);
        if (!listCanvas.overrideSorting) listCanvas.overrideSorting = true;
        if (listCanvas.sortingOrder != 500) listCanvas.sortingOrder = 500;
        AdditionalCanvasShaderChannels channels = AdditionalCanvasShaderChannels.TexCoord1
            | AdditionalCanvasShaderChannels.Normal | AdditionalCanvasShaderChannels.Tangent;
        if ((listCanvas.additionalShaderChannels & channels) != channels)
            listCanvas.additionalShaderChannels |= channels;
        GetOrAdd<GraphicRaycaster>(template.gameObject);
        SetActive(template.gameObject, false);

        // 내용이 항목보다 짧으면 TMP가 마지막 줄을 목록 아래로 밀어 글자가 잘린다.
        const float itemHeight = 44f;
        const float listPad = 8f;
        RectTransform content = (RectTransform)Require(template, "Viewport/Content");
        RectTransform item = (RectTransform)Require(content, "Item");
        SetRect(item, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -listPad), new Vector2(0f, itemHeight), Vector3.one);
        SetRect(content, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            Vector2.zero, new Vector2(0f, itemHeight + listPad * 2f), Vector3.one);
        Image itemBackground = Require(item, "Item Background").GetComponent<Image>();
        ApplyImage(itemBackground, highlight, Image.Type.Sliced, false, Color.white);
        if (Mathf.Abs(itemBackground.pixelsPerUnitMultiplier - 1f) > 0.001f) itemBackground.pixelsPerUnitMultiplier = 1f;
        Toggle itemToggle = item.GetComponent<Toggle>();
        if (itemToggle.targetGraphic != itemBackground) itemToggle.targetGraphic = itemBackground;
        if (itemToggle.transition != Selectable.Transition.ColorTint) itemToggle.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = itemToggle.colors;
        Color hidden = new Color(1f, 1f, 1f, 0f);
        if (colors.normalColor != hidden || colors.highlightedColor != Color.white || colors.pressedColor != Color.white || colors.selectedColor != Color.white)
        {
            colors.normalColor = hidden;
            colors.highlightedColor = Color.white;
            colors.pressedColor = Color.white;
            colors.selectedColor = Color.white;
            colors.disabledColor = hidden;
            colors.colorMultiplier = 1f;
            itemToggle.colors = colors;
        }
        RectTransform itemCheck = (RectTransform)Require(item, "Item Checkmark");
        ApplyImage(itemCheck.GetComponent<Image>(), check, Image.Type.Simple, true, Color.white);
        SetRect(itemCheck, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(22f, 0f), new Vector2(32f, 32f), Vector3.one);
        SetAnchors(((RectTransform)Require(item, "Item Label")), Vector2.zero, Vector2.one, new Vector2(42f, 1f), new Vector2(-8f, -2f));

        // 화살표 36px가 오른쪽을 차지하므로 글자 영역을 그 안쪽으로 두고, 칸 높이(44)에 맞게 26으로 키운다.
        TextMeshProUGUI caption = dropdown.captionText as TextMeshProUGUI;
        TextMeshProUGUI itemLabel = dropdown.itemText as TextMeshProUGUI;
        if (caption == null || itemLabel == null) throw new InvalidOperationException("드롭다운 글자가 TextMeshProUGUI가 아닙니다.");
        // 늘어나는 앵커의 음수 크기는 페이지가 꺼진 동안 너비가 0이 되어 글자가 안 그려진다.
        SetRect(caption.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(14f, 0f), new Vector2(156f, 36f), Vector3.one);
        ApplyDropdownLabel(caption, rowFont, 22f);
        if (caption.overflowMode != TextOverflowModes.Overflow) caption.overflowMode = TextOverflowModes.Overflow;
        ApplyDropdownLabel(itemLabel, rowFont, 22f);
        if (itemLabel.overflowMode != TextOverflowModes.Overflow) itemLabel.overflowMode = TextOverflowModes.Overflow;
        ApplyDropdownLabel(reverseLabel, rowFont, reverseLabel.fontSize);

        Image box = Require(toggle, "Background").GetComponent<Image>();
        ApplyImage(box, toggleBox, Image.Type.Simple, true, Color.white);
        RectTransform boxRect = box.rectTransform;
        if (boxRect.sizeDelta != new Vector2(36f, 36f)) boxRect.sizeDelta = new Vector2(36f, 36f);
        Image mark = Require(toggle, "Background/Checkmark").GetComponent<Image>();
        ApplyImage(mark, toggleCheck, Image.Type.Simple, true, Color.white);
    }

    static void ApplyDropdownLabel(TextMeshProUGUI text, TextMeshProUGUI fontSource, float fontSize)
    {
        if (text.font != fontSource.font) text.font = fontSource.font;
        if (fontSource.fontSharedMaterial != null && text.fontSharedMaterial != fontSource.fontSharedMaterial)
            text.fontSharedMaterial = fontSource.fontSharedMaterial;
        if (text.enableAutoSizing) text.enableAutoSizing = false;
        if (Mathf.Abs(text.fontSize - fontSize) > 0.01f) text.fontSize = fontSize;
        if (text.color != DropdownInk) text.color = DropdownInk;
        if (text.horizontalAlignment != HorizontalAlignmentOptions.Left) text.horizontalAlignment = HorizontalAlignmentOptions.Left;
        if (text.verticalAlignment != VerticalAlignmentOptions.Middle) text.verticalAlignment = VerticalAlignmentOptions.Middle;
        if (text.textWrappingMode != TextWrappingModes.NoWrap) text.textWrappingMode = TextWrappingModes.NoWrap;
        if (text.overflowMode != TextOverflowModes.Ellipsis) text.overflowMode = TextOverflowModes.Ellipsis;
        if (text.raycastTarget) text.raycastTarget = false;
    }

    static void ApplyImage(Image image, Sprite sprite, Image.Type type, bool preserveAspect, Color color)
    {
        if (image.sprite != sprite) image.sprite = sprite;
        if (image.type != type) image.type = type;
        if (image.preserveAspect != preserveAspect) image.preserveAspect = preserveAspect;
        if (image.color != color) image.color = color;
    }

    static void ApplySpriteSwap(Selectable selectable, Sprite normal, Sprite pressed)
    {
        if (selectable.transition != Selectable.Transition.SpriteSwap) selectable.transition = Selectable.Transition.SpriteSwap;
        SpriteState state = selectable.spriteState;
        if (state.highlightedSprite == normal && state.pressedSprite == pressed && state.selectedSprite == normal && state.disabledSprite == normal)
            return;
        state.highlightedSprite = normal;
        state.pressedSprite = pressed;
        state.selectedSprite = normal;
        state.disabledSprite = normal;
        selectable.spriteState = state;
    }

    static Sprite LoadDropdownSprite(string name)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(DropdownUiFolder + "/" + name + ".png");
        if (sprite == null) throw new InvalidOperationException(name + " 스프라이트를 찾지 못했습니다.");
        return sprite;
    }

    // 칸의 75%. preserveAspect가 어떤 스프라이트든 이 박스 안에 넣는다.
    static void FitIconBox(RectTransform rect)
    {
        SetRect(rect, new Vector2(0.125f, 0.125f), new Vector2(0.875f, 0.875f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, Vector3.one);
    }

    static GameObject EnsureFigureSlot(Transform detail, Sprite slotSprite)
    {
        Transform slot = FindDirect(detail, "FigureSlotTemplate");
        if (slot == null)
        {
            GameObject go = new GameObject("FigureSlotTemplate", typeof(RectTransform), typeof(LayoutElement));
            go.layer = detail.gameObject.layer;
            go.transform.SetParent(detail, false);
            slot = go.transform;
            NewImage(slot, "Plate");
            NewImage(slot, "Icon");
            NewImage(slot, "Silhouette");
        }

        SetActive(slot.gameObject, false);
        RectTransform rect = (RectTransform)slot;
        if (rect.sizeDelta != new Vector2(FigureCellMax, FigureCellMax)) rect.sizeDelta = new Vector2(FigureCellMax, FigureCellMax);
        Image plate = slot.Find("Plate").GetComponent<Image>();
        if (plate.sprite != slotSprite) plate.sprite = slotSprite;
        if (plate.type != Image.Type.Sliced) plate.type = Image.Type.Sliced;
        if (plate.color != Color.white) plate.color = Color.white;
        plate.raycastTarget = false;
        if (Mathf.Abs(plate.pixelsPerUnitMultiplier - 1f) > 0.001f) plate.pixelsPerUnitMultiplier = 1f;
        Stretch(plate.rectTransform);

        Image icon = slot.Find("Icon").GetComponent<Image>();
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        FitIconBox(icon.rectTransform);
        Image silhouette = slot.Find("Silhouette").GetComponent<Image>();
        silhouette.color = new Color(0f, 0f, 0f, 0.9019608f);
        silhouette.preserveAspect = true;
        silhouette.raycastTarget = false;
        FitIconBox(silhouette.rectTransform);
        SetActive(silhouette.gameObject, false);
        return slot.gameObject;
    }

    static Transform EnsureSynergyRow(Transform parent, TextMeshProUGUI template, Sprite normal)
    {
        Transform row = FindDirect(parent, "SynergyRowTemplate");
        if (row == null)
        {
            GameObject go = new GameObject("SynergyRowTemplate", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            row = go.transform;
            NewImage(row, "Icon");
            TextMeshProUGUI name = Object.Instantiate(template, row);
            name.name = "Name";
        }

        SetActive(row.gameObject, false);
        LayoutElement element = row.GetComponent<LayoutElement>();
        element.preferredHeight = 96f;
        element.flexibleWidth = 1f;
        Image background = row.GetComponent<Image>();
        background.sprite = normal;
        background.type = Image.Type.Sliced;
        background.color = Color.white;
        if (Mathf.Abs(background.pixelsPerUnitMultiplier - 1f) > 0.001f) background.pixelsPerUnitMultiplier = 1f;
        Button button = row.GetComponent<Button>();
        button.targetGraphic = background;
        if (button.transition != Selectable.Transition.None) button.transition = Selectable.Transition.None;

        Image icon = row.Find("Icon").GetComponent<Image>();
        RectTransform iconRect = icon.rectTransform;
        iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0f, 0.5f);
        if (iconRect.anchoredPosition != new Vector2(12f, 0f)) iconRect.anchoredPosition = new Vector2(12f, 0f);
        if (iconRect.sizeDelta != new Vector2(72f, 72f)) iconRect.sizeDelta = new Vector2(72f, 72f);
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        TextMeshProUGUI label = row.Find("Name").GetComponent<TextMeshProUGUI>();
        SetAnchors(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(96f, 8f), new Vector2(-128f, -8f));
        StyleText(label, 32f, HorizontalAlignmentOptions.Left, VerticalAlignmentOptions.Middle);
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;

        Transform progressTransform = FindDirect(row, "Progress");
        TextMeshProUGUI progress = progressTransform != null
            ? progressTransform.GetComponent<TextMeshProUGUI>()
            : Object.Instantiate(template, row);
        progress.name = "Progress";
        SetRect(progress.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(-16f, 0f), new Vector2(104f, 48f), Vector3.one);
        StyleText(progress, 28f, HorizontalAlignmentOptions.Right, VerticalAlignmentOptions.Middle);
        progress.textWrappingMode = TextWrappingModes.NoWrap;
        progress.overflowMode = TextOverflowModes.Ellipsis;
        if (progress.text != "0 / 0") progress.text = "0 / 0";
        return row;
    }

    static Button EnsureBookArrow(Transform parent, string name, Sprite sprite, Vector2 position)
    {
        Transform found = FindDirect(parent, name);
        if (found == null)
        {
            found = NewImage(parent, name);
            found.gameObject.AddComponent<Button>();
        }
        SetCenterRect((RectTransform)found, position, ArrowButtonSize);
        Image image = found.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = true;
        image.color = Color.white;
        Button button = found.GetComponent<Button>();
        button.targetGraphic = image;
        return button;
    }

    static Image EnsureImage(Transform parent, string name, Sprite sprite, Vector2 position, Vector2 size, Color color)
    {
        Transform found = FindDirect(parent, name);
        if (found == null) found = NewImage(parent, name);
        Image image = found.GetComponent<Image>();
        if (image.sprite != sprite) image.sprite = sprite;
        if (image.color != color) image.color = color;
        image.raycastTarget = false;
        SetCenterRect(image.rectTransform, position, size);
        return image;
    }

    static Transform NewImage(Transform parent, string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    static Transform EnsureChild(Transform parent, string name)
    {
        Transform found = FindDirect(parent, name);
        if (found != null) return found;
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        return go.transform;
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
