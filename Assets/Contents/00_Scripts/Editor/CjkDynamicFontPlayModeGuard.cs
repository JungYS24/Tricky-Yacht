#if UNITY_EDITOR
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using TMPro;

/// <summary>
/// Dynamic JP/SC Atlas는 Play 중 글리프가 에셋에 기록된다.
/// 그 YAML을 저장하면 Git에 매번 잡히므로, 자동 저장을 막고 Play 종료 후 워킹트리를 되돌린다.
/// </summary>
[InitializeOnLoad]
public static class CjkDynamicFontPlayModeGuard
{
    public const string JpSdfPath = "Assets/Contents/10_Resources/Fonts/NotoSansJP-Bold SDF.asset";
    public const string ScSdfPath = "Assets/Contents/10_Resources/Fonts/NotoSansSC-Bold SDF.asset";

    internal static bool AllowPersist;

    static CjkDynamicFontPlayModeGuard()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode)
            return;

        DiscardDynamicAtlasChanges();
    }

    [MenuItem("Studio 10&6/Localization/Clear CJK Dynamic Atlases")]
    public static void DiscardDynamicAtlasChanges()
    {
        ClearInMemory(JpSdfPath);
        ClearInMemory(ScSdfPath);
        RestoreFromGit(JpSdfPath);
        RestoreFromGit(ScSdfPath);
        AssetDatabase.Refresh();
    }

    private static void ClearInMemory(string assetPath)
    {
        TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        if (fontAsset == null)
            return;

        if (fontAsset.atlasPopulationMode == AtlasPopulationMode.Dynamic)
            fontAsset.ClearFontAssetData(true);

        EditorUtility.ClearDirty(fontAsset);
    }

    private static void RestoreFromGit(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
        if (string.IsNullOrEmpty(projectRoot))
            return;

        string normalized = assetPath.Replace('\\', '/');
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = $"restore -- \"{normalized}\"",
            WorkingDirectory = projectRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        try
        {
            using (Process process = Process.Start(startInfo))
            {
                if (process == null)
                    return;
                process.WaitForExit(5000);
            }
        }
        catch
        {
            // git이 없으면 메모리 ClearDirty만으로 저장을 막는다.
        }
    }
}

public class CjkDynamicFontSaveGuard : UnityEditor.AssetModificationProcessor
{
    private static string[] OnWillSaveAssets(string[] paths)
    {
        if (CjkDynamicFontPlayModeGuard.AllowPersist || paths == null || paths.Length == 0)
            return paths;

        var kept = new List<string>(paths.Length);
        for (int i = 0; i < paths.Length; i++)
        {
            string path = paths[i].Replace('\\', '/');
            if (path == CjkDynamicFontPlayModeGuard.JpSdfPath
                || path == CjkDynamicFontPlayModeGuard.ScSdfPath)
                continue;

            kept.Add(paths[i]);
        }

        return kept.ToArray();
    }
}
#endif
