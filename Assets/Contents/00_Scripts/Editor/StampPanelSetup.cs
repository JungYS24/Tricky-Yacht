#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// MainScene_Map에 스탬프 선택창과 상점 스탬프 4종을 연결한다.
// 코팅 선택창을 복제해 이름과 스크립트만 스탬프용으로 바꾸고 ShopManager에 연결한다.
// 이미 연결돼 있으면 다시 만들지 않으므로 여러 번 눌러도 된다. 끝나면 씬만 저장하면 된다.
public static class StampPanelSetup
{
    const string ScenePath = "Assets/Contents/01_Scenes/MainScene_Map.unity";
    const string StampFolder = "Assets/Contents/10_Resources/Data/DiceData/Stamp";
    static readonly string[] StampAssets = { "Heart", "Clover", "Diamond", "Spade" };

    [MenuItem("Tools/Shop/Setup Stamp Panel")]
    public static void SetupFromMenu()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        ShopManager shop = FindInScene<ShopManager>(scene);
        if (shop == null)
        {
            Debug.LogError("[Stamp Setup] MainScene_Map에서 ShopManager를 찾지 못했습니다.");
            return;
        }

        Undo.RecordObject(shop, "Setup Stamp Panel");
        bool created = EnsurePanel(shop);
        int added = AddStampItems(shop);

        EditorUtility.SetDirty(shop);
        EditorSceneManager.MarkSceneDirty(scene);
        if (shop.stampSelectionPanel != null) Selection.activeGameObject = shop.stampSelectionPanel.gameObject;
        Debug.Log($"[Stamp Setup] 완료: 스탬프 선택창 {(created ? "새로 만듦" : "이미 연결돼 있음")}, 상점 목록에 스탬프 {added}개 추가. 씬을 저장해 주세요.");
    }

    static bool EnsurePanel(ShopManager shop)
    {
        if (shop.stampSelectionPanel != null) return false;

        CoatingSelectionPanel source = shop.coatingSelectionPanel;
        if (source == null)
        {
            Debug.LogError("[Stamp Setup] ShopManager에 코팅 선택창이 연결돼 있지 않아 복제할 수 없습니다.");
            return false;
        }

        // 복제하면 패널 안을 가리키던 참조(panelRoot, slotParent)는 복제본 쪽으로 따라온다.
        GameObject copy = Object.Instantiate(source.gameObject, source.transform.parent);
        copy.name = "StampSelectionPanel";
        copy.transform.SetSiblingIndex(source.transform.GetSiblingIndex() + 1);
        copy.SetActive(false);
        Undo.RegisterCreatedObjectUndo(copy, "Create Stamp Panel");

        CoatingSelectionPanel coating = copy.GetComponent<CoatingSelectionPanel>();
        GameObject panelRoot = coating.panelRoot;
        Transform slotParent = coating.slotParent;
        GameObject slotPrefab = coating.diceSlotPrefab;
        Object.DestroyImmediate(coating);

        StampSelectionPanel stamp = copy.AddComponent<StampSelectionPanel>();
        stamp.panelRoot = panelRoot;
        stamp.slotParent = slotParent;
        stamp.diceSlotPrefab = slotPrefab;
        shop.stampSelectionPanel = stamp;
        return true;
    }

    static int AddStampItems(ShopManager shop)
    {
        if (shop.allItemsPool == null) shop.allItemsPool = new List<BaseItemDataSO>();
        int added = 0;
        foreach (string assetName in StampAssets)
        {
            string path = $"{StampFolder}/{assetName}.asset";
            StampItemSO item = AssetDatabase.LoadAssetAtPath<StampItemSO>(path);
            if (item == null)
            {
                Debug.LogWarning($"[Stamp Setup] 스탬프 에셋을 찾지 못했습니다: {path}");
                continue;
            }
            if (shop.allItemsPool.Contains(item)) continue;
            shop.allItemsPool.Add(item);
            added++;
        }
        return added;
    }

    static T FindInScene<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T found = root.GetComponentInChildren<T>(true);
            if (found != null) return found;
        }
        return null;
    }
}
#endif
