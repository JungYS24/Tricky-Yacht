using UnityEngine;
using UnityEditor;
using TMPro;

public class FontBulkChanger : EditorWindow
{
    [MenuItem("Editor/Font Changer")]
    public static void ShowWindow()
    {
        GetWindow<FontBulkChanger>("Font Changer");
    }

    private TMP_FontAsset targetFont;

    void OnGUI()
    {
        GUILayout.Label("씬 내 모든 TMP 폰트 일괄 변경 타겟", EditorStyles.boldLabel);

        // 1. 바꿀 목적지 폰트 에셋을 넣는 슬롯
        targetFont = (TMP_FontAsset)EditorGUILayout.ObjectField("Target Font Asset", targetFont, typeof(TMP_FontAsset), false);

        GUILayout.Space(15);

        // 2. 일괄 변경 실행 버튼
        if (GUILayout.Button("현재 씬의 모든 TMP 폰트 변경하기", GUILayout.Height(40)))
        {
            if (targetFont == null)
            {
                EditorUtility.DisplayDialog("경고", "교체할 Target Font Asset을 먼저 지정해주세요!", "확인");
                return;
            }

            ChangeAllFontsInActiveScene();
        }
    }

    private void ChangeAllFontsInActiveScene()
    {
        TextMeshProUGUI[] allTexts = Resources.FindObjectsOfTypeAll<TextMeshProUGUI>();
        int changeCount = 0;

        foreach (TextMeshProUGUI textComp in allTexts)
        {
            // 에디터 배경에 있는 에셋 프리팹 원본은 제외하고, 현재 씬(Hierarchy)에 배치된 오브젝트만 필터링
            if (textComp.gameObject.scene.name == null) continue;

            Undo.RecordObject(textComp, "Bulk Change Font");

            // 폰트 교체 및 컴포넌트 더티(갱신 필요) 처리
            textComp.font = targetFont;
            EditorUtility.SetDirty(textComp);
            changeCount++;
        }
        EditorUtility.DisplayDialog("변경 완료", $"현재 씬의 총 {changeCount}개 텍스트 오브젝트를 성공적으로 교체했습니다!", "확인");
    }
}