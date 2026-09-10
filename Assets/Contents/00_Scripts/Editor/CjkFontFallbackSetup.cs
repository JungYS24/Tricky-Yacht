#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using TMPro;

/// <summary>
/// Noto Sans CJK 패밀리(KR 메인 + JP/SC Fallback)를 TMP에 연결한다.
/// Unity 메뉴: Studio 10&amp;6 / Localization / Setup CJK Font Fallbacks
/// </summary>
public static class CjkFontFallbackSetup
{
    private const string FontFolder = "Assets/Contents/10_Resources/Fonts";
    private const string KrSdfPath = FontFolder + "/NotoSansKR-Bold SDF.asset";
    private const string JpFontPath = FontFolder + "/NotoSansJP-Bold.otf";
    private const string ScFontPath = FontFolder + "/NotoSansSC-Bold.otf";
    private const string JpSdfPath = FontFolder + "/NotoSansJP-Bold SDF.asset";
    private const string ScSdfPath = FontFolder + "/NotoSansSC-Bold SDF.asset";
    private const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";

    [MenuItem("Studio 10&6/Localization/Setup CJK Font Fallbacks")]
    public static void Setup()
    {
        Font jpFont = AssetDatabase.LoadAssetAtPath<Font>(JpFontPath);
        Font scFont = AssetDatabase.LoadAssetAtPath<Font>(ScFontPath);

        if (jpFont == null || scFont == null)
        {
            Debug.LogError("[CJK Font] NotoSansJP-Bold.otf / NotoSansSC-Bold.otf 를 Fonts 폴더에서 찾지 못했습니다. Unity 임포트가 끝났는지 확인하세요.");
            return;
        }

        TMP_FontAsset jpSdf = GetOrCreateDynamicSdf(jpFont, JpSdfPath, "NotoSansJP-Bold SDF");
        TMP_FontAsset scSdf = GetOrCreateDynamicSdf(scFont, ScSdfPath, "NotoSansSC-Bold SDF");

        if (jpSdf == null || scSdf == null)
            return;

        TMP_FontAsset krSdf = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(KrSdfPath);
        if (krSdf != null)
            AssignFallbacks(krSdf, jpSdf, scSdf);
        else
            Debug.LogWarning("[CJK Font] NotoSansKR-Bold SDF 를 찾지 못해 메인 폰트 Fallback은 건너뜁니다.");

        AssignGlobalTmpFallbacks(jpSdf, scSdf);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[CJK Font] KR 메인 폰트에 JP/SC Dynamic Fallback을 연결했습니다. Play 후 일본어/중국어 글리프를 확인하세요.");
    }

    private static TMP_FontAsset GetOrCreateDynamicSdf(Font sourceFont, string assetPath, string assetName)
    {
        TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        if (existing != null)
            return existing;

        TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
            sourceFont,
            90,
            9,
            GlyphRenderMode.SDFAA,
            1024,
            1024,
            AtlasPopulationMode.Dynamic,
            true
        );

        if (fontAsset == null)
        {
            Debug.LogError($"[CJK Font] TMP Font Asset 생성 실패: {assetPath}");
            return null;
        }

        fontAsset.name = assetName;
        AssetDatabase.CreateAsset(fontAsset, assetPath);

        if (fontAsset.atlasTextures != null)
        {
            for (int i = 0; i < fontAsset.atlasTextures.Length; i++)
            {
                Texture2D atlas = fontAsset.atlasTextures[i];
                if (atlas != null)
                {
                    atlas.name = assetName + " Atlas";
                    AssetDatabase.AddObjectToAsset(atlas, fontAsset);
                }
            }
        }

        if (fontAsset.material != null)
        {
            fontAsset.material.name = assetName + " Material";
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
        }

        EditorUtility.SetDirty(fontAsset);
        return fontAsset;
    }

    private static void AssignFallbacks(TMP_FontAsset main, TMP_FontAsset jp, TMP_FontAsset sc)
    {
        if (main.fallbackFontAssetTable == null)
            main.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset>();

        AddFallbackUnique(main.fallbackFontAssetTable, jp);
        AddFallbackUnique(main.fallbackFontAssetTable, sc);
        EditorUtility.SetDirty(main);
    }

    private static void AssignGlobalTmpFallbacks(TMP_FontAsset jp, TMP_FontAsset sc)
    {
        Object tmpSettings = AssetDatabase.LoadAssetAtPath<Object>(TmpSettingsPath);
        if (tmpSettings == null)
        {
            Debug.LogWarning("[CJK Font] TMP Settings 를 찾지 못해 전역 Fallback은 건너뜁니다.");
            return;
        }

        SerializedObject so = new SerializedObject(tmpSettings);
        SerializedProperty list = so.FindProperty("m_fallbackFontAssets");
        if (list == null)
            return;

        AddObjectReferenceUnique(list, jp);
        AddObjectReferenceUnique(list, sc);
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(tmpSettings);
    }

    private static void AddFallbackUnique(System.Collections.Generic.List<TMP_FontAsset> list, TMP_FontAsset font)
    {
        if (font == null)
            return;

        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] == font)
                return;
        }

        list.Add(font);
    }

    private static void AddObjectReferenceUnique(SerializedProperty list, Object asset)
    {
        for (int i = 0; i < list.arraySize; i++)
        {
            if (list.GetArrayElementAtIndex(i).objectReferenceValue == asset)
                return;
        }

        int index = list.arraySize;
        list.InsertArrayElementAtIndex(index);
        list.GetArrayElementAtIndex(index).objectReferenceValue = asset;
    }
}

public class CjkFontFallbackPostprocessor : AssetPostprocessor
{
    private static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        bool importedCjkSource = false;
        for (int i = 0; i < importedAssets.Length; i++)
        {
            string path = importedAssets[i].Replace('\\', '/');
            if (path.EndsWith("/NotoSansJP-Bold.otf") || path.EndsWith("/NotoSansSC-Bold.otf"))
                importedCjkSource = true;
        }

        if (!importedCjkSource)
            return;

        if (File.Exists("Assets/Contents/10_Resources/Fonts/NotoSansJP-Bold SDF.asset")
            && File.Exists("Assets/Contents/10_Resources/Fonts/NotoSansSC-Bold SDF.asset"))
            return;

        EditorApplication.delayCall += CjkFontFallbackSetup.Setup;
    }
}
#endif
