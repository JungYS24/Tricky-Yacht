#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 코인 주사위 시트(CoinDice_SS) 참조를 면별 스프라이트로 바꾼다.
// 임포트 설정과 참조는 값이 다를 때만 기록해서, 다시 실행해도 파일이 같게 남는다.
public static class CoinDiceSpriteReplace
{
    const string OldSheetGuid = "236a5a6623a5b2b489f7c4b51414a517";
    const string CoinSoPath = "Assets/Contents/10_Resources/Data/DiceData/DiceData/Coin/Coin.asset";
    const string ResultPath = "Logs/coin_dice_replace_result.txt";

    static readonly string[] SpritePaths =
    {
        "Assets/Contents/02_Sprites/04_Dice/CoinDice/Dice_Coin_1.png",
        "Assets/Contents/02_Sprites/04_Dice/CoinDice/Dice_Coin_2.png",
        "Assets/Contents/02_Sprites/04_Dice/CoinDice/Dice_Coin_3.png",
        "Assets/Contents/02_Sprites/04_Dice/CoinDice/Dice_Coin_4.png",
        "Assets/Contents/02_Sprites/04_Dice/CoinDice/Dice_Coin_5.png",
        "Assets/Contents/02_Sprites/04_Dice/CoinDice/Dice_Coin_6.png"
    };

    // 시트 하위 스프라이트 fileID. 순서는 Dice_Coin_1 ~ 6.
    // SS_3과 SS_4는 번호와 눈 수가 어긋나 있다.
    static readonly long[] OldFileIds =
    {
        8066959083524236513L,   // CoinDice_SS_0 → Dice_Coin_1
        -7414221777594581807L,  // CoinDice_SS_1 → Dice_Coin_2
        -7738011609060927955L,  // CoinDice_SS_2 → Dice_Coin_3
        8481627583809092357L,   // CoinDice_SS_4 → Dice_Coin_4
        7453760724211168763L,   // CoinDice_SS_3 → Dice_Coin_5
        -6209686449909653958L   // CoinDice_SS_5 → Dice_Coin_6
    };

    static readonly string[] OldSpriteNames =
    {
        "CoinDice_SS_0",
        "CoinDice_SS_1",
        "CoinDice_SS_2",
        "CoinDice_SS_4",
        "CoinDice_SS_3",
        "CoinDice_SS_5"
    };

    [MenuItem("Tools/Dice/Replace Coin Dice Sprites")]
    public static void RunFromMenu()
    {
        Run();
        Debug.Log("[CoinDice] 코인 주사위 스프라이트를 맞췄습니다.");
    }

    public static void Apply()
    {
        try
        {
            Run();
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Debug.LogError("[CoinDice] 실패\n" + e);
            EditorApplication.Exit(1);
        }
    }

    static void Run()
    {
        var log = new StringBuilder();
        List<string> users = FindUsers();
        log.AppendLine("users " + users.Count);
        for (int i = 0; i < users.Count; i++)
            log.AppendLine("user " + users[i]);

        bool dirty = FixImports(log);
        Sprite[] faces = LoadFaces(log);
        dirty |= ReplaceUsers(users, faces, log);
        dirty |= ApplyCoinSo(faces, log);

        if (dirty)
            AssetDatabase.SaveAssets();

        List<string> left = FindUsers();
        if (left.Count > 0)
            throw new InvalidOperationException("[CoinDice] 시트 참조가 남았습니다: " + string.Join(", ", left));

        log.AppendLine(dirty ? "wrote 1" : "wrote 0");
        Directory.CreateDirectory("Logs");
        File.WriteAllText(ResultPath, log.ToString());
        Debug.Log("[CoinDice] 배치 적용 완료.\n" + log);
    }

    static bool FixImports(StringBuilder log)
    {
        bool any = false;
        for (int i = 0; i < SpritePaths.Length; i++)
        {
            var importer = AssetImporter.GetAtPath(SpritePaths[i]) as TextureImporter;
            if (importer == null)
                throw new InvalidOperationException("[CoinDice] TextureImporter 없음: " + SpritePaths[i]);

            bool dirty = false;
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                dirty = true;
            }
            if (importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                dirty = true;
            }
            if (!Mathf.Approximately(importer.spritePixelsPerUnit, 52f))
            {
                importer.spritePixelsPerUnit = 52f;
                dirty = true;
            }
            if (importer.filterMode != FilterMode.Point)
            {
                importer.filterMode = FilterMode.Point;
                dirty = true;
            }
            if (importer.textureCompression != TextureImporterCompression.Uncompressed)
            {
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                dirty = true;
            }
            if (importer.mipmapEnabled)
            {
                importer.mipmapEnabled = false;
                dirty = true;
            }
            if (!importer.alphaIsTransparency)
            {
                importer.alphaIsTransparency = true;
                dirty = true;
            }

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            if (settings.spriteAlignment != (int)SpriteAlignment.Center)
            {
                settings.spriteAlignment = (int)SpriteAlignment.Center;
                dirty = true;
            }
            if (settings.spritePivot != new Vector2(0.5f, 0.5f))
            {
                settings.spritePivot = new Vector2(0.5f, 0.5f);
                dirty = true;
            }
            if (settings.filterMode != FilterMode.Point)
            {
                settings.filterMode = FilterMode.Point;
                dirty = true;
            }
            if (settings.mipmapEnabled)
            {
                settings.mipmapEnabled = false;
                dirty = true;
            }
            if (!settings.alphaIsTransparency)
            {
                settings.alphaIsTransparency = true;
                dirty = true;
            }
            if (dirty)
                importer.SetTextureSettings(settings);

            string[] platforms = { "DefaultTexturePlatform", "Standalone", "Android", "WebGL" };
            for (int p = 0; p < platforms.Length; p++)
            {
                TextureImporterPlatformSettings platform = importer.GetPlatformTextureSettings(platforms[p]);
                if (platform.overridden || platform.textureCompression != TextureImporterCompression.Uncompressed)
                {
                    platform.overridden = false;
                    platform.textureCompression = TextureImporterCompression.Uncompressed;
                    platform.crunchedCompression = false;
                    importer.SetPlatformTextureSettings(platform);
                    dirty = true;
                }
            }

            if (dirty)
            {
                importer.SaveAndReimport();
                any = true;
                log.AppendLine("reimport " + SpritePaths[i]);
            }

            importer = AssetImporter.GetAtPath(SpritePaths[i]) as TextureImporter;
            log.AppendLine(
                "import " + SpritePaths[i]
                + " type=" + importer.textureType
                + " mode=" + importer.spriteImportMode
                + " ppu=" + importer.spritePixelsPerUnit
                + " filter=" + importer.filterMode
                + " compression=" + importer.textureCompression
                + " mipmap=" + importer.mipmapEnabled
                + " guid=" + AssetDatabase.AssetPathToGUID(SpritePaths[i]));
        }
        return any;
    }

    static Sprite[] LoadFaces(StringBuilder log)
    {
        var faces = new Sprite[SpritePaths.Length];
        for (int i = 0; i < SpritePaths.Length; i++)
        {
            faces[i] = LoadSprite(SpritePaths[i]);
            if (faces[i] == null)
                throw new InvalidOperationException("[CoinDice] 스프라이트를 찾지 못했습니다: " + SpritePaths[i]);
            if (!Mathf.Approximately(faces[i].pixelsPerUnit, 52f))
                throw new InvalidOperationException("[CoinDice] PPU가 52가 아닙니다: " + SpritePaths[i] + " " + faces[i].pixelsPerUnit);
            if (faces[i].texture != null && faces[i].texture.filterMode != FilterMode.Point)
                throw new InvalidOperationException("[CoinDice] 필터가 Point가 아닙니다: " + SpritePaths[i]);
            log.AppendLine("sprite " + faces[i].name + " rect=" + faces[i].rect + " pivot=" + faces[i].pivot);
        }
        return faces;
    }

    static Sprite LoadSprite(string path)
    {
        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        if (assets == null) return null;
        Sprite best = null;
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is Sprite sprite && (best == null || sprite.rect.width >= best.rect.width))
                best = sprite;
        }
        return best;
    }

    static bool ReplaceUsers(List<string> users, Sprite[] faces, StringBuilder log)
    {
        bool any = false;
        for (int i = 0; i < users.Count; i++)
        {
            string path = users[i];
            if (path.EndsWith("MainScene.unity") && !path.EndsWith("MainScene_Map.unity"))
                throw new InvalidOperationException("[CoinDice] MainScene.unity는 수정하지 않습니다: " + path);

            if (path.EndsWith(".prefab"))
                any |= ReplacePrefab(path, faces, log);
            else if (path.EndsWith(".unity"))
                any |= ReplaceScene(path, faces, log);
            else
                any |= ReplaceAsset(path, faces, log);
        }
        return any;
    }

    static bool ReplacePrefab(string path, Sprite[] faces, StringBuilder log)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            bool dirty = RemapHierarchy(root, faces, path, log);
            if (dirty)
                PrefabUtility.SaveAsPrefabAsset(root, path);
            return dirty;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static bool ReplaceScene(string path, Sprite[] faces, StringBuilder log)
    {
        var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try
        {
            bool dirty = false;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
                dirty |= RemapHierarchy(roots[i], faces, path, log);
            if (dirty)
                EditorSceneManager.SaveScene(scene);
            return dirty;
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    static bool ReplaceAsset(string path, Sprite[] faces, StringBuilder log)
    {
        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        bool dirty = false;
        if (assets == null) return false;
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] == null) continue;
            dirty |= RemapObject(assets[i], faces, path, log);
        }
        return dirty;
    }

    static bool RemapHierarchy(GameObject root, Sprite[] faces, string path, StringBuilder log)
    {
        bool dirty = false;
        Component[] components = root.GetComponentsInChildren<Component>(true);
        for (int i = 0; i < components.Length; i++)
        {
            if (components[i] == null) continue;
            dirty |= RemapObject(components[i], faces, path, log);
        }
        return dirty;
    }

    static bool RemapObject(UnityEngine.Object obj, Sprite[] faces, string path, StringBuilder log)
    {
        var so = new SerializedObject(obj);
        SerializedProperty prop = so.GetIterator();
        bool dirty = false;
        int guard = 0;
        while (prop.Next(true))
        {
            if (++guard > 20000)
                throw new InvalidOperationException("[CoinDice] 프로퍼티 순회가 너무 깁니다: " + path);
            if (prop.propertyType != SerializedPropertyType.ObjectReference) continue;
            UnityEngine.Object referenced = prop.objectReferenceValue;
            if (referenced == null) continue;
            int face = FaceIndexOf(referenced);
            if (face < 0) continue;
            if (prop.objectReferenceValue == faces[face]) continue;
            log.AppendLine("replace " + path + " " + prop.propertyPath + " -> " + faces[face].name);
            prop.objectReferenceValue = faces[face];
            dirty = true;
        }
        if (dirty)
        {
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(obj);
        }
        return dirty;
    }

    // 시트 스프라이트가 아니면 -1. 매핑에 없는 시트 스프라이트면 예외.
    static int FaceIndexOf(UnityEngine.Object referenced)
    {
        if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(referenced, out string guid, out long localId))
            return -1;
        if (guid != OldSheetGuid) return -1;

        for (int i = 0; i < OldFileIds.Length; i++)
        {
            if (OldFileIds[i] == localId) return i;
        }
        for (int i = 0; i < OldSpriteNames.Length; i++)
        {
            if (referenced.name == OldSpriteNames[i]) return i;
        }
        throw new InvalidOperationException(
            "[CoinDice] 매핑되지 않은 시트 스프라이트: " + referenced.name + " fileID=" + localId);
    }

    static bool ApplyCoinSo(Sprite[] faces, StringBuilder log)
    {
        var dice = AssetDatabase.LoadAssetAtPath<DiceItemSO>(CoinSoPath);
        if (dice == null)
            throw new InvalidOperationException("[CoinDice] 상점 SO를 찾지 못했습니다: " + CoinSoPath);

        var so = new SerializedObject(dice);
        SerializedProperty icon = so.FindProperty("icon");
        SerializedProperty faceProp = so.FindProperty("customFaceSprites");
        if (icon == null || faceProp == null)
            throw new InvalidOperationException("[CoinDice] Coin SO 필드를 찾지 못했습니다.");

        bool dirty = false;
        if (icon.objectReferenceValue != faces[5])
        {
            icon.objectReferenceValue = faces[5];
            dirty = true;
            log.AppendLine("coin icon -> " + faces[5].name);
        }
        if (faceProp.arraySize != faces.Length)
        {
            faceProp.arraySize = faces.Length;
            dirty = true;
        }
        for (int i = 0; i < faces.Length; i++)
        {
            SerializedProperty element = faceProp.GetArrayElementAtIndex(i);
            if (element.objectReferenceValue == faces[i]) continue;
            element.objectReferenceValue = faces[i];
            dirty = true;
            log.AppendLine("coin face " + i + " -> " + faces[i].name);
        }
        if (dirty)
        {
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(dice);
        }
        return dirty;
    }

    static readonly string[] TargetExtensions =
    {
        ".asset", ".prefab", ".unity", ".mat", ".controller", ".overrideController",
        ".anim", ".playable", ".mask", ".spriteatlas", ".preset"
    };

    static List<string> FindUsers()
    {
        var list = new List<string>();
        string[] files = Directory.GetFiles("Assets", "*.*", SearchOption.AllDirectories);
        for (int i = 0; i < files.Length; i++)
        {
            string ext = Path.GetExtension(files[i]).ToLowerInvariant();
            bool target = false;
            for (int e = 0; e < TargetExtensions.Length; e++)
            {
                if (ext == TargetExtensions[e])
                {
                    target = true;
                    break;
                }
            }
            if (!target) continue;
            string text = File.ReadAllText(files[i]);
            if (text.IndexOf(OldSheetGuid, StringComparison.Ordinal) < 0) continue;
            string assetPath = ToAssetPath(files[i]);
            if (!string.IsNullOrEmpty(assetPath))
                list.Add(assetPath);
        }
        return list;
    }

    static string ToAssetPath(string file)
    {
        string full = Path.GetFullPath(file).Replace('\\', '/');
        string assets = Path.GetFullPath("Assets").Replace('\\', '/');
        if (!full.StartsWith(assets, StringComparison.OrdinalIgnoreCase)) return null;
        return "Assets" + full.Substring(assets.Length);
    }
}
#endif
