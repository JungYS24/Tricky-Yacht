#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using TMPro;

/// <summary>
/// Dynamic JP/SC Atlas는 Play 중 글리프가 에셋에 저장된다.
/// Play 종료 후 Atlas를 비워 Git에 용량 diff가 안 남게 한다. 런타임에는 다시 채워진다.
/// </summary>
[InitializeOnLoad]
public static class CjkDynamicFontPlayModeGuard
{
    private const string JpSdfPath = "Assets/Contents/10_Resources/Fonts/NotoSansJP-Bold SDF.asset";
    private const string ScSdfPath = "Assets/Contents/10_Resources/Fonts/NotoSansSC-Bold SDF.asset";

    static CjkDynamicFontPlayModeGuard()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode)
            return;

        ClearDynamicAtlases();
    }

    [MenuItem("Studio 10&6/Localization/Clear CJK Dynamic Atlases")]
    public static void ClearDynamicAtlases()
    {
        bool changed = false;
        changed |= ClearIfDynamic(JpSdfPath);
        changed |= ClearIfDynamic(ScSdfPath);

        if (!changed)
            return;

        AssetDatabase.SaveAssets();
        Debug.Log("[CJK Font] Play로 늘어난 JP/SC Dynamic Atlas를 비웠습니다. Git에 올리지 마세요.");
    }

    private static bool ClearIfDynamic(string assetPath)
    {
        TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        if (fontAsset == null)
            return false;

        if (fontAsset.atlasPopulationMode != AtlasPopulationMode.Dynamic)
            return false;

        bool hasGlyphs = (fontAsset.characterTable != null && fontAsset.characterTable.Count > 0)
            || (fontAsset.glyphTable != null && fontAsset.glyphTable.Count > 0);

        if (!hasGlyphs)
            return false;

        fontAsset.ClearFontAssetData(true);
        EditorUtility.SetDirty(fontAsset);
        return true;
    }
}
#endif
