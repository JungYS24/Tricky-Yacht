#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class HpBarFxSetup
{
    const string ScenePath = "Assets/Contents/01_Scenes/MainScene_Map.unity";
    const string ConfigPath = "Assets/Contents/05_DataSO/HpBarFxConfig.asset";
    const string FrameSourcePath = "Assets/Contents/06_UI/Sprites/HP Slider/hp_bar_only_5x.png";
    const string FrameOverlayPath = "Assets/Contents/06_UI/Sprites/HP Slider/hp_bar_frame_overlay.png";
    const string SolidPath = "Assets/Contents/06_UI/Sprites/HP Slider/hp_bar_solid.png";

    [MenuItem("Tools/HPBar/Setup EnemyHPSlider FX")]
    public static void SetupFromMenu()
    {
        Run(false);
    }

    public static void BatchSetup()
    {
        Run(true);
    }

    static void Run(bool fromBatch)
    {
        try
        {
            EnsureConfig();
            PrepareSprites();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            // 씬을 열면 아직 참조되지 않은 에셋이 언로드된다. 연 뒤에 다시 불러온다.
            HpBarFxConfig config = AssetDatabase.LoadAssetAtPath<HpBarFxConfig>(ConfigPath);
            if (config == null)
                throw new System.InvalidOperationException("HpBarFxConfig 에셋을 불러오지 못했습니다.");
            GameObject sliderGo = FindSlider(scene);
            if (sliderGo == null)
            {
                Debug.LogError("[HpBarFx] EnemyHPSlider를 찾지 못했습니다.");
                ExitIfBatch(fromBatch, 1);
                return;
            }

            SetupSlider(sliderGo, config);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[HpBarFx] setup OK");
            ExitIfBatch(fromBatch, 0);
        }
        catch (System.Exception exception)
        {
            Debug.LogError("[HpBarFx] " + exception);
            ExitIfBatch(fromBatch, 1);
        }
    }

    static void ExitIfBatch(bool fromBatch, int code)
    {
        if (fromBatch || Application.isBatchMode)
            EditorApplication.Exit(code);
    }

    static HpBarFxConfig EnsureConfig()
    {
        var config = AssetDatabase.LoadAssetAtPath<HpBarFxConfig>(ConfigPath);
        if (config == null)
        {
            config = ScriptableObject.CreateInstance<HpBarFxConfig>();
            AssetDatabase.CreateAsset(config, ConfigPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(ConfigPath, ImportAssetOptions.ForceSynchronousImport);
            config = AssetDatabase.LoadAssetAtPath<HpBarFxConfig>(ConfigPath);
        }
        if (config == null)
            throw new System.InvalidOperationException("HpBarFxConfig 에셋을 만들지 못했습니다.");
        return config;
    }

    static GameObject FindSlider(UnityEngine.SceneManagement.Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            foreach (Transform transform in transforms)
            {
                if (transform.name == "EnemyHPSlider" && transform.GetComponent<Slider>() != null)
                    return transform.gameObject;
            }
        }
        return null;
    }

    static void SetupSlider(GameObject sliderGo, HpBarFxConfig config)
    {
        Slider slider = sliderGo.GetComponent<Slider>();
        if (slider.fillRect == null)
            throw new System.InvalidOperationException("EnemyHPSlider에 Fill Rect가 없습니다.");

        RectTransform legacyRect = slider.fillRect;
        Image legacyFill = legacyRect.GetComponent<Image>();
        RectTransform fillArea = legacyRect.parent as RectTransform;
        if (fillArea == null)
            throw new System.InvalidOperationException("Fill Area를 찾지 못했습니다.");

        RectTransform barRoot = fillArea.parent as RectTransform;
        if (barRoot == null) barRoot = slider.transform as RectTransform;

        Image background = FindDirectImage(barRoot, "Background");
        if (background == null)
            throw new System.InvalidOperationException("Background를 찾지 못했습니다.");

        Vector4 padding = MeasurePadding(FrameSourcePath, config.innerPadding);
        config.innerPadding = padding;
        EditorUtility.SetDirty(config);
        float left = padding.x;
        float bottom = padding.y;
        float right = padding.z;
        float top = padding.w;
        float outset = config.glowOutset;

        Sprite solid = AssetDatabase.LoadAssetAtPath<Sprite>(SolidPath);
        Sprite frameSprite = AssetDatabase.LoadAssetAtPath<Sprite>(FrameOverlayPath);
        if (solid == null || frameSprite == null)
            throw new System.InvalidOperationException("HP바 스프라이트를 불러오지 못했습니다.");

        legacyRect.name = "SliderFill_Unused";
        if (legacyFill != null)
        {
            legacyFill.enabled = false;
            legacyFill.raycastTarget = false;
        }

        StretchInset(background.rectTransform, left, bottom, right, top);
        PaintSolid(background, solid, config.emptyColor, 1f, Image.Type.Simple);

        StretchInset(fillArea, left, bottom, right, top);
        Image ghost = EnsureFilled(fillArea, "Ghost", legacyRect, solid, config.ghostColor, 1f);
        Image fxFill = EnsureFilled(fillArea, "Fill", legacyRect, solid, config.fillColor, 1f);
        Image flash = EnsureFilled(fillArea, "Flash", legacyRect, solid, config.flashColor, 0f);
        Image healFlash = EnsureFilled(fillArea, "HealFlash", legacyRect, solid, config.flashColor, 0f);
        Image glow = EnsureGlow(barRoot, solid, config.glowColor, outset);
        RectTransform particleRoot = EnsureParticleRoot(fillArea);
        Image woodFrame = EnsureWoodFrame(barRoot, frameSprite);
        TextMeshProUGUI hpText = FindOrCreateText(barRoot);
        StyleText(hpText, barRoot, config, left, bottom, right, top);

        Image woodOnRoot = barRoot.GetComponent<Image>();
        if (woodOnRoot != null) woodOnRoot.enabled = false;

        float ratio = slider.maxValue <= 0f ? 0f : Mathf.Clamp01(slider.value / slider.maxValue);
        ghost.fillAmount = ratio;
        fxFill.fillAmount = ratio;
        flash.fillAmount = 1f;
        healFlash.fillAmount = 1f;

        ghost.rectTransform.SetSiblingIndex(0);
        fxFill.rectTransform.SetSiblingIndex(1);
        flash.rectTransform.SetSiblingIndex(2);
        healFlash.rectTransform.SetSiblingIndex(3);
        particleRoot.SetSiblingIndex(4);
        legacyRect.SetSiblingIndex(5);

        glow.rectTransform.SetSiblingIndex(0);
        background.rectTransform.SetSiblingIndex(1);
        fillArea.SetSiblingIndex(2);
        woodFrame.rectTransform.SetSiblingIndex(3);
        hpText.rectTransform.SetSiblingIndex(4);

        Debug.Log("[HpBarFx] padding L=" + left + " B=" + bottom + " R=" + right + " T=" + top);
        Debug.Log("[HpBarFx] order " + ChildNames(barRoot));
        Debug.Log("[HpBarFx] fillArea " + ChildNames(fillArea));
        Debug.Log("[HpBarFx] Fill " + Hex(fxFill.color) + " sprite=" + fxFill.sprite.name);
        Debug.Log("[HpBarFx] Background " + Hex(background.color));
        Debug.Log("[HpBarFx] Ghost " + Hex(ghost.color));
        Debug.Log("[HpBarFx] HealGhost " + Hex(config.healGhostColor));
        Debug.Log("[HpBarFx] LowFill " + Hex(config.lowFillColor));
        Debug.Log("[HpBarFx] Glow " + Hex(config.glowColor) + " a=" + glow.color.a + " sprite=" + glow.sprite.name);
        Debug.Log("[HpBarFx] Flash a=" + flash.color.a + " sprite=" + flash.sprite.name);
        Debug.Log("[HpBarFx] HealFlash a=" + healFlash.color.a + " sprite=" + healFlash.sprite.name);

        CanvasGroup group = barRoot.GetComponent<CanvasGroup>();
        if (group == null) group = barRoot.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 1f;

        HpBarFx fx = sliderGo.GetComponent<HpBarFx>();
        if (fx == null) fx = sliderGo.AddComponent<HpBarFx>();

        SerializedObject so = new SerializedObject(fx);
        so.FindProperty("config").objectReferenceValue = config;
        so.FindProperty("slider").objectReferenceValue = slider;
        so.FindProperty("barRoot").objectReferenceValue = barRoot;
        so.FindProperty("canvasGroup").objectReferenceValue = group;
        so.FindProperty("frame").objectReferenceValue = background;
        so.FindProperty("ghost").objectReferenceValue = ghost;
        so.FindProperty("fill").objectReferenceValue = fxFill;
        so.FindProperty("flash").objectReferenceValue = flash;
        so.FindProperty("healFlash").objectReferenceValue = healFlash;
        so.FindProperty("glow").objectReferenceValue = glow;
        so.FindProperty("hpText").objectReferenceValue = hpText;
        so.FindProperty("particleRoot").objectReferenceValue = particleRoot;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(fx);
        EditorUtility.SetDirty(sliderGo);
    }

    static Image EnsureFilled(RectTransform parent, string name, Transform skip, Sprite sprite, Color color, float alpha)
    {
        Transform found = FindDirect(parent, name, skip);
        GameObject go;
        if (found == null)
        {
            go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
        }
        else go = found.gameObject;

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;

        Image image = go.GetComponent<Image>();
        if (image == null) image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Filled;
        image.fillMethod = Image.FillMethod.Horizontal;
        image.fillOrigin = (int)Image.OriginHorizontal.Left;
        color.a = alpha;
        image.color = color;
        image.raycastTarget = false;
        image.pixelsPerUnitMultiplier = 1f;
        return image;
    }

    static Image EnsureGlow(RectTransform parent, Sprite sprite, Color color, float outset)
    {
        Transform found = FindDirect(parent, "Glow", null);
        GameObject go;
        if (found == null)
        {
            go = new GameObject("Glow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
        }
        else go = found.gameObject;

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        rect.offsetMin = new Vector2(-outset, -outset);
        rect.offsetMax = new Vector2(outset, outset);

        Image image = go.GetComponent<Image>();
        if (image == null) image = go.AddComponent<Image>();
        PaintSolid(image, sprite, color, 0f, Image.Type.Simple);
        return image;
    }

    static RectTransform EnsureParticleRoot(RectTransform parent)
    {
        Transform found = FindDirect(parent, "HealParticleRoot", null);
        GameObject go;
        if (found == null)
        {
            go = new GameObject("HealParticleRoot", typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
        }
        else go = found.gameObject;

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.localScale = Vector3.one;
        return rect;
    }

    static TextMeshProUGUI FindOrCreateText(RectTransform barRoot)
    {
        TextMeshProUGUI named = FindText(barRoot, "HPText");
        if (named != null) return named;
        TextMeshProUGUI existing = FindText(barRoot, "EnemyHPText");
        if (existing != null) return existing;

        GameObject go = new GameObject("HPText", typeof(RectTransform));
        go.layer = barRoot.gameObject.layer;
        go.transform.SetParent(barRoot, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        TextMeshProUGUI sample = Object.FindFirstObjectByType<TextMeshProUGUI>();
        if (sample != null) tmp.font = sample.font;
        tmp.text = "100/100";
        tmp.raycastTarget = false;
        return tmp;
    }

    static void StyleText(TextMeshProUGUI tmp, RectTransform barRoot, HpBarFxConfig config, float left, float bottom, float right, float top)
    {
        StretchInset(tmp.rectTransform, left, bottom, right, top);
        tmp.enableAutoSizing = false;
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.margin = Vector4.zero;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.horizontalAlignment = HorizontalAlignmentOptions.Center;
        tmp.verticalAlignment = VerticalAlignmentOptions.Middle;
        tmp.color = config.textColor;
        tmp.raycastTarget = false;
        float height = barRoot.rect.height;
        if (height < 1f) height = barRoot.sizeDelta.y;
        float inner = Mathf.Max(1f, height - top - bottom);
        tmp.fontSize = inner * config.textHeightRatio;
        Material material = tmp.fontMaterial;
        if (material != null && material.HasProperty("_OutlineWidth"))
        {
            material.SetFloat("_OutlineWidth", config.textOutlineWidth);
            material.SetColor("_OutlineColor", config.textOutlineColor);
            tmp.fontMaterial = material;
        }
    }

    static Image EnsureWoodFrame(RectTransform parent, Sprite sprite)
    {
        Transform found = FindDirect(parent, "Frame", null);
        GameObject go;
        if (found == null)
        {
            go = new GameObject("Frame", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
        }
        else go = found.gameObject;

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;

        Image image = go.GetComponent<Image>();
        if (image == null) image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.color = HpBarFxConfig.Rgb(0xFF, 0xFF, 0xFF);
        image.raycastTarget = false;
        image.pixelsPerUnitMultiplier = 1f;
        return image;
    }

    static void PrepareSprites()
    {
        Vector4 padding = MeasurePadding(FrameSourcePath, new Vector4(10f, 10f, 10f, 10f));
        EnsureSolidSprite();
        EnsureFrameOverlay(padding);
        ForcePoint(FrameSourcePath, false);
        ForcePoint(FrameOverlayPath, true);
        ForcePoint(SolidPath, true);
    }

    static void EnsureSolidSprite()
    {
        if (File.Exists(ProjectFile(SolidPath)) && SolidLooksWhite()) return;
        var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        var pixels = new Color32[16];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
        tex.SetPixels32(pixels);
        tex.Apply();
        File.WriteAllBytes(ProjectFile(SolidPath), tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(SolidPath);
    }

    static bool SolidLooksWhite()
    {
        Texture2D tex = LoadPng(SolidPath);
        Color pixel = tex.GetPixel(0, 0);
        Object.DestroyImmediate(tex);
        return pixel.r > 0.99f && pixel.g > 0.99f && pixel.b > 0.99f && pixel.a > 0.99f;
    }

    static void EnsureFrameOverlay(Vector4 padding)
    {
        int left = Mathf.RoundToInt(padding.x);
        int bottom = Mathf.RoundToInt(padding.y);
        int right = Mathf.RoundToInt(padding.z);
        int top = Mathf.RoundToInt(padding.w);
        if (File.Exists(ProjectFile(FrameOverlayPath)) && OverlayHoleIsClear(left, bottom)) return;

        Texture2D source = LoadPng(FrameSourcePath);
        int w = source.width;
        int h = source.height;
        Color32[] pixels = source.GetPixels32();
        Object.DestroyImmediate(source);
        for (int y = bottom; y < h - top; y++)
        {
            for (int x = left; x < w - right; x++)
                pixels[y * w + x] = new Color32(0, 0, 0, 0);
        }
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.SetPixels32(pixels);
        tex.Apply();
        File.WriteAllBytes(ProjectFile(FrameOverlayPath), tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(FrameOverlayPath);
    }

    static bool OverlayHoleIsClear(int left, int bottom)
    {
        Texture2D tex = LoadPng(FrameOverlayPath);
        Color pixel = tex.GetPixel(left + 2, bottom + 2);
        Object.DestroyImmediate(tex);
        return pixel.a < 0.01f;
    }

    static Vector4 MeasurePadding(string assetPath, Vector4 fallback)
    {
        if (!File.Exists(ProjectFile(assetPath))) return fallback;
        Texture2D tex = LoadPng(assetPath);
        int w = tex.width;
        int h = tex.height;
        if (w < 4 || h < 4)
        {
            Object.DestroyImmediate(tex);
            return fallback;
        }
        Color center = tex.GetPixel(w / 2, h / 2);
        int left = EdgeFromCenter(tex, center, -1, 0);
        int right = EdgeFromCenter(tex, center, 1, 0);
        int bottom = EdgeFromCenter(tex, center, 0, -1);
        int top = EdgeFromCenter(tex, center, 0, 1);
        Object.DestroyImmediate(tex);
        if (left <= 0 || right <= 0 || bottom <= 0 || top <= 0) return fallback;
        return new Vector4(left, bottom, right, top);
    }

    static int EdgeFromCenter(Texture2D tex, Color center, int dx, int dy)
    {
        int x = tex.width / 2;
        int y = tex.height / 2;
        int guard = 0;
        while (x >= 0 && y >= 0 && x < tex.width && y < tex.height && guard < 512)
        {
            if (ColorDistance(tex.GetPixel(x, y), center) > 40f)
            {
                if (dx < 0) return x + 1;
                if (dx > 0) return tex.width - x;
                if (dy < 0) return y + 1;
                return tex.height - y;
            }
            x += dx;
            y += dy;
            guard++;
        }
        return 0;
    }

    static float ColorDistance(Color a, Color b)
    {
        return Mathf.Abs(a.r - b.r) * 255f + Mathf.Abs(a.g - b.g) * 255f + Mathf.Abs(a.b - b.b) * 255f;
    }

    static string ProjectFile(string assetPath)
    {
        if (Path.IsPathRooted(assetPath)) return assetPath;
        string project = Directory.GetParent(Application.dataPath).FullName;
        return Path.Combine(project, assetPath);
    }

    static Texture2D LoadPng(string path)
    {
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.LoadImage(File.ReadAllBytes(ProjectFile(path)));
        return tex;
    }

    static void ForcePoint(string path, bool singleSprite)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;
        bool changed = false;
        if (importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            changed = true;
        }
        if (singleSprite && importer.spriteImportMode != SpriteImportMode.Single)
        {
            importer.spriteImportMode = SpriteImportMode.Single;
            changed = true;
        }
        if (importer.filterMode != FilterMode.Point)
        {
            importer.filterMode = FilterMode.Point;
            changed = true;
        }
        if (importer.textureCompression != TextureImporterCompression.Uncompressed)
        {
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            changed = true;
        }
        if (importer.mipmapEnabled)
        {
            importer.mipmapEnabled = false;
            changed = true;
        }
        if (!importer.alphaIsTransparency)
        {
            importer.alphaIsTransparency = true;
            changed = true;
        }
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        if (settings.spriteMeshType != SpriteMeshType.FullRect)
        {
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            changed = true;
        }
        if (changed) importer.SaveAndReimport();
    }

    static void StretchInset(RectTransform rect, float left, float bottom, float right, float top)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    static void PaintSolid(Image image, Sprite sprite, Color color, float alpha, Image.Type type)
    {
        image.sprite = sprite;
        image.type = type;
        if (type == Image.Type.Filled)
        {
            image.fillMethod = Image.FillMethod.Horizontal;
            image.fillOrigin = (int)Image.OriginHorizontal.Left;
        }
        color.a = alpha;
        image.color = color;
        image.raycastTarget = false;
        image.pixelsPerUnitMultiplier = 1f;
    }

    static Image FindDirectImage(Transform parent, string name)
    {
        Transform found = FindDirect(parent, name, null);
        return found != null ? found.GetComponent<Image>() : null;
    }

    static string ChildNames(Transform parent)
    {
        var names = new System.Text.StringBuilder();
        for (int i = 0; i < parent.childCount; i++)
        {
            if (i > 0) names.Append(" > ");
            names.Append(parent.GetChild(i).name);
        }
        return names.ToString();
    }

    static string Hex(Color color)
    {
        return "#" + ColorUtility.ToHtmlStringRGB(color);
    }

    static TextMeshProUGUI FindText(Transform root, string name)
    {
        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        foreach (Transform transform in transforms)
        {
            if (transform.name != name) continue;
            TextMeshProUGUI text = transform.GetComponent<TextMeshProUGUI>();
            if (text != null) return text;
        }
        return null;
    }

    static Transform FindDirect(Transform parent, string name, Transform skip)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child == skip) continue;
            if (child.name == name) return child;
        }
        return null;
    }
}
#endif
