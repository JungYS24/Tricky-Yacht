using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 로비 UI 리워크를 배치모드로 적용한다.
/// Unity.exe -batchmode -nographics -projectPath ... -executeMethod LobbyUIReworkBatch.Apply
/// </summary>
public static class LobbyUIReworkBatch
{
    const string ScenePath = "Assets/Contents/01_Scenes/Lobby.unity";
    const string SpriteDir = "Assets/Contents/06_UI/Sprites/Lobby";
    const string ResultPath = "Logs/lobby_ui_rework_result.txt";

    static readonly string[] SpriteFiles =
    {
        "bg_nologo.png",
        "Title.png",
        "Title_Glow.png",
        "Btn_NewGame_Blank.png",
        "Btn_Continue_Blank.png",
        "Btn_Tutorial_Blank.png",
        "Btn_Quit_Blank.png",
    };

    public static void Apply()
    {
        var log = new StringBuilder();
        try
        {
            ConfigureImporters(log);
            ApplyScene(log);
            File.WriteAllText(ResultPath, log.ToString());
            EditorApplication.Exit(0);
        }
        catch (System.Exception e)
        {
            log.AppendLine("FAIL");
            log.AppendLine(e.ToString());
            File.WriteAllText(ResultPath, log.ToString());
            EditorApplication.Exit(1);
        }
    }

    static void ConfigureImporters(StringBuilder log)
    {
        foreach (var file in SpriteFiles)
        {
            var path = $"{SpriteDir}/{file}";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                throw new System.Exception("TextureImporter 없음: " + path);

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 2048;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.textureType = TextureImporterType.Sprite;
            settings.spriteMode = (int)SpriteImportMode.Single;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.alphaIsTransparency = true;
            settings.mipmapEnabled = false;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            log.AppendLine("import " + path);
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    static Sprite LoadSprite(string fileName)
    {
        var path = $"{SpriteDir}/{fileName}";
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
            throw new System.Exception("스프라이트 로드 실패: " + path);
        return sprite;
    }

    static void ApplyScene(StringBuilder log)
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        RectTransform canvas = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name == "Canvas")
            {
                canvas = root.GetComponent<RectTransform>();
                break;
            }
        }
        if (canvas == null)
            throw new System.Exception("Canvas를 찾지 못함");

        var scaler = canvas.GetComponent<CanvasScaler>();
        log.AppendLine($"CanvasScaler ref={scaler.referenceResolution} match={scaler.matchWidthOrHeight}");

        var bgSprite = LoadSprite("bg_nologo.png");
        var titleSprite = LoadSprite("Title.png");
        var glowSprite = LoadSprite("Title_Glow.png");
        var newGameSprite = LoadSprite("Btn_NewGame_Blank.png");
        var continueSprite = LoadSprite("Btn_Continue_Blank.png");
        var tutorialSprite = LoadSprite("Btn_Tutorial_Blank.png");
        var quitSprite = LoadSprite("Btn_Quit_Blank.png");

        var lobbyImage = MustFind(canvas, "Lobby_Image");
        var titleImg = MustFind(canvas, "Title_IMG");
        var startBtn = MustFind(canvas, "Start_BTN");
        var continueBtn = MustFind(canvas, "Continue");
        var tutoBtn = MustFind(canvas, "Tuto");
        var finishBtn = MustFind(canvas, "Finish_BTN");

        ApplyBackground(lobbyImage, bgSprite, log);
        var titleGlow = GetOrCreateTitleGlow(canvas, titleImg, glowSprite, log);
        ApplyTitle(titleImg, titleSprite, log);
        ApplyButton(startBtn, newGameSprite, new Vector2(468f, -6f), log);
        ApplyButton(continueBtn, continueSprite, new Vector2(468f, -131f), log);
        ApplyButton(tutoBtn, tutorialSprite, new Vector2(467f, -255f), log);
        ApplyButton(finishBtn, quitSprite, new Vector2(467f, -382f), log);

        ReorderHierarchy(canvas, lobbyImage, titleGlow, titleImg, startBtn, continueBtn, tutoBtn, finishBtn, log);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        log.AppendLine("scene saved " + ScenePath);
    }

    static RectTransform MustFind(RectTransform canvas, string name)
    {
        var t = canvas.Find(name) as RectTransform;
        if (t == null)
            throw new System.Exception("오브젝트 없음: " + name);
        return t;
    }

    static void ApplyBackground(RectTransform rt, Sprite sprite, StringBuilder log)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;

        var sr = rt.GetComponent<SpriteRenderer>();
        if (sr != null)
            sr.enabled = false;

        var image = rt.GetComponent<Image>();
        if (image == null)
            image = rt.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = false;
        image.type = Image.Type.Simple;
        image.color = Color.white;
        log.AppendLine($"Lobby_Image sprite={sprite.name} stretch anchors sizeDelta=0 scale=1 SR.enabled={sr != null && sr.enabled}");
    }

    static RectTransform GetOrCreateTitleGlow(RectTransform canvas, RectTransform titleImg, Sprite sprite, StringBuilder log)
    {
        var existing = canvas.Find("Title_Glow") as RectTransform;
        GameObject go;
        if (existing != null)
        {
            go = existing.gameObject;
        }
        else
        {
            go = new GameObject("Title_Glow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.layer = canvas.gameObject.layer;
            go.transform.SetParent(canvas, false);
        }

        var rt = go.GetComponent<RectTransform>();
        SetCenterRect(rt, new Vector2(477f, 289f), new Vector2(767f, 516f));
        var image = go.GetComponent<Image>();
        if (image == null)
            image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = false;
        image.raycastTarget = false;
        image.color = Color.white;
        log.AppendLine($"Title_Glow sprite={sprite.name} size=767x516 pos=(477,289) raycast=off");
        return rt;
    }

    static void ApplyTitle(RectTransform rt, Sprite sprite, StringBuilder log)
    {
        SetCenterRect(rt, new Vector2(469f, 281f), new Vector2(574f, 374f));
        var image = rt.GetComponent<Image>();
        if (image == null)
            throw new System.Exception("Title_IMG에 Image가 없음");
        image.sprite = sprite;
        image.preserveAspect = false;
        log.AppendLine($"Title_IMG sprite={sprite.name} size=574x374 pos=(469,281) juice={rt.GetComponent<LobbyTitleJuice>() != null}");
    }

    static void ApplyButton(RectTransform rt, Sprite sprite, Vector2 pos, StringBuilder log)
    {
        SetCenterRect(rt, pos, new Vector2(467f, 109f));
        var image = rt.GetComponent<Image>();
        if (image == null)
            throw new System.Exception(rt.name + "에 Image가 없음");
        image.sprite = sprite;
        image.preserveAspect = false;
        var button = rt.GetComponent<Button>();
        log.AppendLine($"{rt.name} sprite={sprite.name} size=467x109 pos={pos} children={rt.childCount} onClick={(button != null ? button.onClick.GetPersistentEventCount() : 0)}");
    }

    static void SetCenterRect(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    static void ReorderHierarchy(
        RectTransform canvas,
        RectTransform lobbyImage,
        RectTransform titleGlow,
        RectTransform titleImg,
        RectTransform startBtn,
        RectTransform continueBtn,
        RectTransform tutoBtn,
        RectTransform finishBtn,
        StringBuilder log)
    {
        var remaining = new List<Transform>();
        foreach (Transform child in canvas)
            remaining.Add(child);

        var ordered = new List<Transform>
        {
            lobbyImage, titleGlow, titleImg, startBtn, continueBtn, tutoBtn, finishBtn
        };
        foreach (var child in remaining)
        {
            if (!ordered.Contains(child))
                ordered.Add(child);
        }

        for (int i = 0; i < ordered.Count; i++)
            ordered[i].SetSiblingIndex(i);

        log.Append("hierarchy:");
        foreach (Transform child in canvas)
            log.Append(" " + child.name);
        log.AppendLine();
    }
}
