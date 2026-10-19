#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

// MainScene_Map의 Dialogue Root / Choice Root에 대화창 패널을 직접 심는다.
// 런타임 Awake뿐 아니라 에디터 하이어라키에서도 나무 프레임이 보이게 한다.
public static class EncounterDialogSkinSetup
{
    const string ScenePath = "Assets/Contents/01_Scenes/MainScene_Map.unity";
    const string PanelPath = "Assets/Contents/06_UI/Sprites/Encounter/Encounter_Dialog_Panel.png";

    [MenuItem("Tools/Encounters/Apply Dialog Panel Skin")]
    public static void ApplyFromMenu()
    {
        Apply();
        Debug.Log("[Encounter Dialog] 대화창 패널을 씬 하이어라키에 적용했습니다.");
    }

    public static void RunBatch()
    {
        Apply();
    }

    public static void Apply()
    {
        Sprite panel = AssetDatabase.LoadAssetAtPath<Sprite>(PanelPath);
        if (panel == null)
            throw new InvalidOperationException("Encounter_Dialog_Panel 스프라이트를 찾지 못했습니다.");

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        EncounterEventPanel encounter = FindInScene<EncounterEventPanel>(scene);
        if (encounter == null)
            throw new InvalidOperationException("MainScene_Map에서 EncounterEventPanel을 찾지 못했습니다.");

        encounter.dialogPanelSprite = panel;
        ApplyPanelImage(encounter.dialogueRoot, panel, Mathf.Max(0.01f, encounter.dialogBorderScale));
        ApplyPanelImage(encounter.choiceRoot, panel, Mathf.Max(0.01f, encounter.dialogBorderScale));

        if (encounter.dialogueText != null)
            encounter.dialogueText.margin = new Vector4(140f, 60f, 140f, 60f);

        ApplyChoiceButtonColors(encounter.choiceAButton);
        ApplyChoiceButtonColors(encounter.choiceBButton);

        EditorUtility.SetDirty(encounter);
        if (encounter.dialogueRoot != null) EditorUtility.SetDirty(encounter.dialogueRoot);
        if (encounter.choiceRoot != null) EditorUtility.SetDirty(encounter.choiceRoot);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[Encounter Dialog] MainScene_Map에 대화창 패널을 저장했습니다.");
    }

    static void ApplyPanelImage(GameObject root, Sprite sprite, float borderMul)
    {
        if (root == null || sprite == null) return;
        Image image = root.GetComponent<Image>();
        if (image == null) return;
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.color = Color.white;
        image.fillCenter = true;
        image.pixelsPerUnitMultiplier = borderMul;
        EditorUtility.SetDirty(image);
    }

    static void ApplyChoiceButtonColors(Button button)
    {
        if (button == null) return;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.95f, 0.8f, 1f);
        colors.pressedColor = new Color(0.8f, 0.75f, 0.65f, 1f);
        colors.selectedColor = new Color(1f, 0.95f, 0.8f, 1f);
        colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 1f);
        button.colors = colors;
        EditorUtility.SetDirty(button);
    }

    static T FindInScene<T>(Scene scene) where T : Component
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            T found = roots[i].GetComponentInChildren<T>(true);
            if (found != null) return found;
        }
        return null;
    }
}
#endif
