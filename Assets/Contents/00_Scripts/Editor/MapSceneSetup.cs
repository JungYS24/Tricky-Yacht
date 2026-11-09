#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// MainScene_Map에 지도 화면 루트와 참조를 붙입니다.
// 메뉴(Tools/Map/Setup MainScene_Map)를 직접 눌렀을 때만 실행합니다.
// 예전에는 컴파일할 때마다 지도 오브젝트가 없으면 자동으로 씬을 고치고 저장했지만, 모르는 사이 씬이 바뀌지 않도록 자동 실행은 뺐습니다.
public static class MapSceneSetup
{
    const string ScenePath = "Assets/Contents/01_Scenes/MainScene_Map.unity";
    const string TablePath = "Assets/Contents/10_Resources/Data/Map_Table.json";
    const string IconPath = "Assets/Contents/10_Resources/Data/MapIconSet.asset";
    const string PrefabPath = "Assets/Contents/03_Prefabs/Map/MapNodeView.prefab";

    [MenuItem("Tools/Map/Setup MainScene_Map")]
    public static void SetupMainSceneMap()
    {
        MapIconSet icons = LoadOrCreateIcons();
        MapNodeView prefab = LoadOrCreatePrefab();

        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        DiceManager dice = Object.FindFirstObjectByType<DiceManager>();
        if (dice == null)
        {
            Debug.LogError("[Map] MainScene_Map에서 DiceManager를 찾지 못했습니다.");
            return;
        }

        Canvas canvas = FindMainCanvas();
        if (canvas == null)
        {
            Debug.LogError("[Map] Canvas를 찾지 못했습니다.");
            return;
        }

        MapPanel mapPanel = FindOrCreate<MapPanel>(canvas.transform, "MapRoot");
        RestPanel restPanel = FindOrCreate<RestPanel>(canvas.transform, "RestPanel");
        MapUiHierarchy.Stretch(mapPanel.GetComponent<RectTransform>());
        MapUiHierarchy.Stretch(restPanel.GetComponent<RectTransform>());
        mapPanel.gameObject.SetActive(false);
        restPanel.gameObject.SetActive(false);

        MapUiHierarchy.EnsureMap(mapPanel);
        MapUiHierarchy.EnsureRest(restPanel);
        mapPanel.iconSet = icons;
        mapPanel.nodePrefab = prefab;
        EditorUtility.SetDirty(mapPanel);
        EditorUtility.SetDirty(restPanel);

        dice.mapTableAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(TablePath);
        dice.mapPanel = mapPanel;
        dice.restPanel = restPanel;
        dice.mapIconSet = icons;
        EditorUtility.SetDirty(dice);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[Map] MainScene_Map 하이어라키에 MapScroll, Node_Des, RestPanel을 배치했습니다.");
    }

    public static void BatchRun()
    {
        SetupMainSceneMap();
        MapSimulator.Simulate();
    }

    static MapIconSet LoadOrCreateIcons()
    {
        MapIconSet icons = AssetDatabase.LoadAssetAtPath<MapIconSet>(IconPath);
        if (icons != null) return icons;
        icons = ScriptableObject.CreateInstance<MapIconSet>();
        icons.FillDefaultColors();
        AssetDatabase.CreateAsset(icons, IconPath);
        return icons;
    }

    static MapNodeView LoadOrCreatePrefab()
    {
        MapNodeView prefab = AssetDatabase.LoadAssetAtPath<MapNodeView>(PrefabPath);
        if (prefab != null) return prefab;

        if (!AssetDatabase.IsValidFolder("Assets/Contents/03_Prefabs/Map"))
            AssetDatabase.CreateFolder("Assets/Contents/03_Prefabs", "Map");

        var temp = new GameObject("MapNodeView", typeof(RectTransform), typeof(Image), typeof(Button), typeof(MapNodeView));
        var view = temp.GetComponent<MapNodeView>();
        view.icon = temp.GetComponent<Image>();
        view.button = temp.GetComponent<Button>();
        view.button.targetGraphic = view.icon;

        var ring = new GameObject("Ring", typeof(RectTransform), typeof(Image));
        ring.transform.SetParent(temp.transform, false);
        var ringRect = ring.GetComponent<RectTransform>();
        ringRect.anchorMin = Vector2.zero;
        ringRect.anchorMax = Vector2.one;
        ringRect.offsetMin = new Vector2(-8f, -8f);
        ringRect.offsetMax = new Vector2(8f, 8f);
        ringRect.SetAsFirstSibling();
        view.ring = ring.GetComponent<Image>();
        view.ring.raycastTarget = false;

        var rect = temp.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(78f, 78f);

        prefab = PrefabUtility.SaveAsPrefabAsset(temp, PrefabPath).GetComponent<MapNodeView>();
        Object.DestroyImmediate(temp);
        return prefab;
    }

    static T FindOrCreate<T>(Transform parent, string name) where T : Component
    {
        Transform found = parent.Find(name);
        if (found != null)
        {
            T existing = found.GetComponent<T>();
            if (existing != null) return existing;
        }

        var go = new GameObject(name, typeof(RectTransform), typeof(T));
        go.transform.SetParent(parent, false);
        return go.GetComponent<T>();
    }

    static Canvas FindMainCanvas()
    {
        Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        for (int i = 0; i < canvases.Length; i++)
        {
            if (canvases[i] != null && canvases[i].gameObject.name == "Canvas")
                return canvases[i];
        }
        return canvases.Length > 0 ? canvases[0] : null;
    }
}
#endif
