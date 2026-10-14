#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class HpBarFxSetup
{
    const string ScenePath = "Assets/Contents/01_Scenes/MainScene_Map.unity";
    const string ConfigPath = "Assets/Contents/05_DataSO/HpBarFxConfig.asset";

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

        Image frame = FindImage(barRoot, "Background");
        if (frame == null)
            throw new System.InvalidOperationException("Background(Frame)를 찾지 못했습니다.");

        Sprite sprite = legacyFill != null ? legacyFill.sprite : null;
        Image ghost = EnsureFilled(fillArea, "Ghost", legacyRect, sprite, config.ghostColor, 1f);
        Image fxFill = EnsureFilled(fillArea, "Fill", legacyRect, sprite, config.fillColor, 1f);
        Image flash = EnsureFilled(fillArea, "Flash", legacyRect, sprite, config.flashColor, 0f);
        Image healFlash = EnsureFilled(fillArea, "HealFlash", legacyRect, sprite, config.flashColor, 0f);
        Image glow = EnsureGlow(barRoot, frame, sprite, config.glowColor);
        RectTransform particleRoot = EnsureParticleRoot(fillArea);
        TextMeshProUGUI hpText = FindOrCreateText(barRoot);

        float ratio = slider.maxValue <= 0f ? 0f : Mathf.Clamp01(slider.value / slider.maxValue);
        ghost.fillAmount = ratio;
        fxFill.fillAmount = ratio;
        flash.fillAmount = 1f;
        healFlash.fillAmount = 1f;

        ghost.raycastTarget = false;
        fxFill.raycastTarget = false;
        flash.raycastTarget = false;
        healFlash.raycastTarget = false;
        glow.raycastTarget = false;

        if (legacyFill != null) legacyFill.enabled = false;

        legacyRect.SetSiblingIndex(0);
        ghost.rectTransform.SetSiblingIndex(1);
        fxFill.rectTransform.SetSiblingIndex(2);
        flash.rectTransform.SetSiblingIndex(3);
        healFlash.rectTransform.SetSiblingIndex(4);
        particleRoot.SetSiblingIndex(5);

        int fillAreaIndex = fillArea.GetSiblingIndex();
        glow.rectTransform.SetSiblingIndex(fillAreaIndex + 1);
        hpText.rectTransform.SetSiblingIndex(glow.rectTransform.GetSiblingIndex() + 1);

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
        so.FindProperty("frame").objectReferenceValue = frame;
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
        return image;
    }

    static Image EnsureGlow(RectTransform parent, Image frame, Sprite sprite, Color color)
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

        RectTransform frameRect = frame.rectTransform;
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = frameRect.anchorMin;
        rect.anchorMax = frameRect.anchorMax;
        rect.pivot = frameRect.pivot;
        rect.anchoredPosition = frameRect.anchoredPosition;
        rect.localRotation = frameRect.localRotation;
        rect.localScale = frameRect.localScale;
        float sx = Mathf.Approximately(frameRect.localScale.x, 0f) ? 1f : frameRect.localScale.x;
        float sy = Mathf.Approximately(frameRect.localScale.y, 0f) ? 1f : frameRect.localScale.y;
        rect.sizeDelta = frameRect.sizeDelta + new Vector2(8f / sx, 8f / sy);

        Image image = go.GetComponent<Image>();
        if (image == null) image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        color.a = 0f;
        image.color = color;
        image.raycastTarget = false;
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
        tmp.fontSize = 18f;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        Material material = tmp.fontMaterial;
        if (material != null && material.HasProperty("_OutlineWidth"))
        {
            material.SetFloat("_OutlineWidth", 0.2f);
            material.SetColor("_OutlineColor", new Color(0.08f, 0.06f, 0.06f, 1f));
        }
        return tmp;
    }

    static Image FindImage(Transform root, string name)
    {
        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        foreach (Transform transform in transforms)
        {
            if (transform.name != name) continue;
            Image image = transform.GetComponent<Image>();
            if (image != null) return image;
        }
        return null;
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
