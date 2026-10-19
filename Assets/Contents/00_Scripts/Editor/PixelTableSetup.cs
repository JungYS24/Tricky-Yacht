#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 지금 열려 있는 MainScene_Map의 주사위 테이블(KeepSlot/DiceTable (1)) 그림만 또렷한 새 그림으로 바꾼다.
/// 위치·크기·정렬 순서는 그대로 둔다(새 그림은 PPU를 맞춰 화면에 같은 크기로 보인다).
/// 씬을 다시 열지 않으므로 손으로 고친 내용은 그대로 남는다. 바꾼 뒤 Ctrl+S로 저장한다. Ctrl+Z로 되돌릴 수 있다.
/// 메뉴: Tools/UI/Apply Pixel Table
/// </summary>
public static class PixelTableSetup
{
    const string SpritePath = "Assets/Contents/06_UI/Sprites/DiceTable_Pixel.png";
    const string LogTag = "[PixelTable] ";

    [MenuItem("Tools/UI/Apply Pixel Table")]
    public static void Apply()
    {
        Scene scene = SceneManager.GetActiveScene();
        SpriteRenderer table = FindTable(scene);
        if (table == null) return;

        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
        if (sprite == null)
        {
            Debug.LogError(LogTag + "그림을 찾지 못했습니다: " + SpritePath);
            return;
        }

        Undo.RecordObject(table, "Apply Pixel Table");
        table.sprite = sprite;
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log(LogTag + "테이블 그림을 바꿨습니다. Ctrl+S로 씬을 저장해 주세요.");
    }

    const string OriginalPath = "Assets/Contents/06_UI/Sprites/Table_Neo.png";
    const string OriginalSpriteName = "Table_Neo_0";

    // 원래 테이블(Table_Neo_0)로 되돌린다. 열려 있는 씬에서만 바꾸므로 손으로 고친 내용은 그대로 남는다.
    [MenuItem("Tools/UI/Restore Original Table")]
    public static void Restore()
    {
        Scene scene = SceneManager.GetActiveScene();
        SpriteRenderer table = FindTable(scene);
        if (table == null) return;

        Sprite original = null;
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(OriginalPath))
        {
            Sprite candidate = asset as Sprite;
            if (candidate != null && candidate.name == OriginalSpriteName) original = candidate;
        }
        if (original == null)
        {
            Debug.LogError(LogTag + "원래 테이블 그림을 찾지 못했습니다: " + OriginalPath + " / " + OriginalSpriteName);
            return;
        }

        Undo.RecordObject(table, "Restore Original Table");
        table.sprite = original;
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log(LogTag + "원래 테이블로 되돌렸습니다. Ctrl+S로 씬을 저장해 주세요.");
    }

    static SpriteRenderer FindTable(Scene scene)
    {
        if (scene.name != "MainScene_Map")
        {
            Debug.LogError(LogTag + "MainScene_Map을 연 상태에서 실행해 주세요. 지금 씬: " + scene.name);
            return null;
        }
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name != "KeepSlot") continue;
            Transform child = root.transform.Find("DiceTable (1)");
            if (child != null) return child.GetComponent<SpriteRenderer>();
        }
        Debug.LogError(LogTag + "KeepSlot/DiceTable (1)의 SpriteRenderer를 찾지 못했습니다.");
        return null;
    }
}
#endif
