#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.Localization;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.Tables;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 전투 화면 왼쪽 계산 패널(Canvas/ScorePanel)을 남색 픽셀아트로 바꾸고, 오른쪽 아래 끝내기·굴리기 버튼 그림도 바꾼다.
/// 기존 오브젝트의 이름과 스크립트 연결은 그대로 두고 그림·위치·크기만 바꾸며,
/// 새로 필요한 것(체력 바, 점수·굴리기·끝내기 라벨, 주사위·칩 아이콘, 점수 칸)은 새 오브젝트로 붙인다.
/// 이름으로 찾아서 바꾸므로 여러 번 실행해도 결과가 같다. 실행하면 MainScene_Map을 열어 저장한다.
/// 메뉴: Tools/UI/Setup Calc Panel (Navy)
/// </summary>
public static class CalcPanelNavySetup
{
    const string ScenePath = "Assets/Contents/01_Scenes/MainScene_Map.unity";
    const string SpriteFolder = "Assets/Contents/06_UI/Sprites/calc_panel_navy";
    const string TableName = "UI_StringTable";
    const string LogTag = "[CalcPanelNavy] ";

    // 아래 좌표는 모두 1920x1080 화면(왼쪽 위 원점) 기준이다. ScorePanel 중심이 이 화면의 (244, 513)에 놓인다.
    static readonly Vector2 ScoreCenter = new Vector2(244f, 513f);
    // 패널 전체를 오른쪽으로 옮기는 양. AI 예시처럼 왼쪽 벽에서 약 45px 떨어지게 한다(판 x 45~473).
    const float PanelShiftX = 13f;
    static float offsetX;
    static readonly Color ShieldTextColor = new Color(0.682f, 0.722f, 0.769f, 1f);

    // 9-slice 테두리(왼, 아래, 오른, 위). 그림은 1픽셀 = 화면 4픽셀로 그려져 있다.
    static readonly Dictionary<string, Vector4> Borders = new Dictionary<string, Vector4>
    {
        { "calc_card_dark", new Vector4(12f, 12f, 12f, 12f) },
        { "calc_card_info", new Vector4(20f, 20f, 20f, 20f) },
        { "calc_box_chips", new Vector4(24f, 24f, 24f, 24f) },
        { "calc_box_mult", new Vector4(24f, 24f, 24f, 24f) },
        { "calc_btn_frame", new Vector4(20f, 20f, 20f, 20f) },
        { "calc_hpbar_back", new Vector4(8f, 0f, 8f, 0f) },
    };

    // 남색 패널에서 또렷하게 보이도록 바꾸는 글자 색(골드는 금색, 체력은 밝은 빨강).
    static readonly Color GoldTextColor = new Color32(0xF2, 0xCC, 0x55, 0xFF);
    static readonly Color HeartTextColor = new Color32(0xEA, 0x34, 0x34, 0xFF);
    // 누를 수 없을 때 반투명 대신 어둡게만 보이게 한다.
    static readonly Color ButtonDisabledColor = new Color(0.75f, 0.75f, 0.75f, 1f);

    [MenuItem("Tools/UI/Setup Calc Panel (Navy)")]
    public static void SetupFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        try
        {
            ConfigureSprites();
            StringTableCollection table = AddStrings();
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Apply(scene, table);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log(LogTag + "setup OK (MainScene_Map 저장됨)");
        }
        catch (Exception exception)
        {
            Debug.LogError(LogTag + exception);
        }
    }

    // ───────────────────────── 그림 가져오기 설정 ─────────────────────────

    static void ConfigureSprites()
    {
        AssetDatabase.Refresh();
        string[] files = System.IO.Directory.GetFiles(SpriteFolder, "*.png");
        if (files.Length == 0) throw new InvalidOperationException("그림이 없습니다: " + SpriteFolder);
        foreach (string file in files)
        {
            string path = file.Replace('\\', '/');
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("그림을 가져오지 못했습니다: " + path);

            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            Vector4 border;
            if (!Borders.TryGetValue(name, out border)) border = Vector4.zero;

            bool changed = false;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            if (settings.spriteMeshType != SpriteMeshType.FullRect)
            {
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                changed = true;
            }
            if (importer.textureType != TextureImporterType.Sprite) { importer.textureType = TextureImporterType.Sprite; changed = true; }
            if (importer.spriteImportMode != SpriteImportMode.Single) { importer.spriteImportMode = SpriteImportMode.Single; changed = true; }
            if (importer.mipmapEnabled) { importer.mipmapEnabled = false; changed = true; }
            if (importer.filterMode != FilterMode.Point) { importer.filterMode = FilterMode.Point; changed = true; }
            if (importer.textureCompression != TextureImporterCompression.Uncompressed) { importer.textureCompression = TextureImporterCompression.Uncompressed; changed = true; }
            if (importer.wrapMode != TextureWrapMode.Clamp) { importer.wrapMode = TextureWrapMode.Clamp; changed = true; }
            if (!importer.alphaIsTransparency) { importer.alphaIsTransparency = true; changed = true; }
            if (Mathf.Abs(importer.spritePixelsPerUnit - 100f) > 0.01f) { importer.spritePixelsPerUnit = 100f; changed = true; }
            if (importer.spriteBorder != border) { importer.spriteBorder = border; changed = true; }
            if (changed) importer.SaveAndReimport();
        }
    }

    static Sprite LoadSprite(string name)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteFolder + "/" + name + ".png");
        if (sprite == null) throw new InvalidOperationException("스프라이트를 찾지 못했습니다: " + name);
        return sprite;
    }

    // ───────────────────────── 번역 키 (이 3개만 넣는다) ─────────────────────────

    static StringTableCollection AddStrings()
    {
        StringTableCollection collection = LocalizationEditorSettings.GetStringTableCollection(TableName);
        if (collection == null) throw new InvalidOperationException(TableName + " 표를 찾지 못했습니다.");
        AddKey(collection, "UI_PANEL_SCORE", "점수", "Score", "スコア", "得分");
        AddKey(collection, "UI_PANEL_ROLLS", "굴리기", "Rolls", "ロール", "投骰");
        AddKey(collection, "UI_PANEL_FINISHES", "끝내기", "Finish", "終了", "结束");
        EditorUtility.SetDirty(collection.SharedData);
        foreach (StringTable stringTable in collection.StringTables)
            EditorUtility.SetDirty(stringTable);
        EditorUtility.SetDirty(collection);
        AssetDatabase.SaveAssets();
        return collection;
    }

    static void AddKey(StringTableCollection collection, string key, string ko, string en, string ja, string zh)
    {
        SharedTableData.SharedTableEntry entry = collection.SharedData.GetEntry(key);
        if (entry == null) entry = collection.SharedData.AddKey(key);
        SetValue(collection, "ko", entry.Id, ko);
        SetValue(collection, "en", entry.Id, en);
        SetValue(collection, "ja", entry.Id, ja);
        SetValue(collection, "zh-Hans", entry.Id, zh);
    }

    static void SetValue(StringTableCollection collection, string code, long id, string value)
    {
        StringTable stringTable = collection.GetTable(new LocaleIdentifier(code)) as StringTable;
        if (stringTable == null) return;
        StringTableEntry entry = stringTable.GetEntry(id);
        if (entry == null) entry = stringTable.AddEntry(id, value);
        entry.Value = value;
    }

    // ───────────────────────── 씬 ─────────────────────────

    static void Apply(Scene scene, StringTableCollection table)
    {
        // 1) 필요한 것을 모두 먼저 찾는다. 하나라도 없으면 아무것도 바꾸지 않고 멈춘다.
        RectTransform score = FindScorePanel(scene);
        RectTransform outer = Child(score, "ScorePanel_Outer");
        SpriteRenderer board = outer.GetComponent<SpriteRenderer>();
        if (board == null) throw new InvalidOperationException("ScorePanel_Outer에 SpriteRenderer가 없습니다.");

        RectTransform coin = Child(score, "CoinPanel");
        RectTransform coinCard = Child(coin, "panel_hp_gold_narrow");
        RectTransform coinIcon = Child(coin, "Icon_Gold");
        TextMeshProUGUI coinText = Text(Child(coin, "CoinText"));

        RectTransform heart = Child(score, "HeartPanel");
        RectTransform heartCard = Child(heart, "panel_hp_gold_narrow");
        RectTransform heartIcon = Child(heart, "Icon_Heart");
        TextMeshProUGUI heartText = Text(Child(heart, "HeartText"));

        RectTransform info = Child(score, "panel_score");
        TextMeshProUGUI handInfo = Text(Child(info, "handInfoText"));

        RectTransform plaque = Child(score, "plaque_chips_mult");
        RectTransform multiply = Child(plaque, "Image");
        TextMeshProUGUI chipsSum = Text(Child(plaque, "chipsSumText"));
        TextMeshProUGUI multSum = Text(Child(plaque, "multSumText"));
        TextMeshProUGUI finalDamage = Text(Child(plaque, "finalDamageText"));
        TextMeshProUGUI chipsLog = Text(Child(plaque, "Chips Log Text"));
        TextMeshProUGUI multLog = Text(Child(plaque, "Mult Log Text"));

        RectTransform rolls = Child(score, "slot_counter_Dice");
        TextMeshProUGUI rollsText = Text(Child(rolls, "Round"));
        RectTransform finishes = Child(score, "slot_counter_chip (1)");
        TextMeshProUGUI finishesText = Text(Child(finishes, "Round_End"));

        RectTransform book = Child(score, "Book_Button");
        RectTransform bookIcon = Child(book, "Icon_Book");
        RectTransform deck = Child(score, "Deck_BTN");
        RectTransform deckIcon = Child(deck, "Icon_Deck");

        RectTransform drop = Child(score, "Drop_Rate_Panel");
        TextMeshProUGUI dropText = Text(Child(drop, "Drop Rate Text"));
        RectTransform mintIcon = Child(dropText.rectTransform, "Image");

        RectTransform stage = Child(score, "Stage_Panel");
        TextMeshProUGUI stageText = Text(Child(stage, "Stage"));

        Image rollButton = Child(score, "Reroll_BTN").GetComponent<Image>();
        Image finishButton = Child(score, "Finish_BTN").GetComponent<Image>();
        if (rollButton == null || finishButton == null) throw new InvalidOperationException("굴리기·끝내기 버튼 Image가 없습니다.");

        UIManager ui = FindInScene<UIManager>(scene);
        if (ui == null) throw new InvalidOperationException("UIManager를 찾지 못했습니다.");
        HurtVignetteController hurt = FindInScene<HurtVignetteController>(scene);

        Sprite boardSprite = LoadSprite("calc_panel_board");
        Sprite card = LoadSprite("calc_card_dark");
        Sprite infoCard = LoadSprite("calc_card_info");
        Sprite chipsBox = LoadSprite("calc_box_chips");
        Sprite multBox = LoadSprite("calc_box_mult");
        Sprite multiplySprite = LoadSprite("calc_multiply");
        Sprite barBack = LoadSprite("calc_hpbar_back");
        Sprite barRed = LoadSprite("calc_hpbar_fill_red");
        Sprite barGray = LoadSprite("calc_hpbar_fill_gray");
        Sprite shieldIcon = LoadSprite("calc_icon_shield");
        Sprite diceIcon = LoadSprite("calc_icon_dice");
        Sprite chipIcon = LoadSprite("calc_icon_chip");
        Sprite buttonFrame = LoadSprite("calc_btn_frame");
        Sprite finishSprite = LoadSprite("calc_btn_finish");
        Sprite rollSprite = LoadSprite("calc_btn_roll");

        TMP_FontAsset labelFont = rollsText.font;
        Material labelMaterial = rollsText.fontSharedMaterial;

        offsetX = PanelShiftX;

        // 2) 패널 판: 나무 판 SpriteRenderer에 남색 판 그림을 1:1(그림 1픽셀 = 캔버스 1단위)로 넣는다.
        //    화면 x 32~460, y 16~1048 (428x1032). AI 예시 이미지와 같은 크기.
        board.sprite = boardSprite;
        board.drawMode = SpriteDrawMode.Simple;
        board.color = Color.white;
        outer.localScale = new Vector3(100f, 100f, 1f);
        outer.position = ToWorld(score, 246f, 532f);

        // 3) 골드 칸
        Place(score, coin, 64f, 48f, 364f, 88f);
        StretchCard(coinCard, card);
        Place(score, coinIcon, 100f, 52f, 80f, 80f);
        PlaceText(score, coinText, 180f, 48f, 240f, 88f, 1.32f, true, true);
        Regular(coinText);
        coinText.color = GoldTextColor;

        // 4) 체력 칸 + 체력 바
        Place(score, heart, 64f, 148f, 364f, 112f);
        StretchCard(heartCard, card);
        Place(score, heartIcon, 108f, 158f, 64f, 64f);
        PlaceText(score, heartText, 180f, 158f, 240f, 64f, 1.27f, false, true);
        Regular(heartText);
        heartText.color = HeartTextColor;
        RectTransform bar = NewImage(heart, "HpBar", barBack, true);
        bar.SetSiblingIndex(heartCard.GetSiblingIndex() + 1);
        Place(score, bar, 84f, 228f, 324f, 24f);
        RectTransform fillArea = GetOrCreate(bar, "FillArea");
        Stretch(fillArea, new Vector2(4f, 4f), new Vector2(-4f, -4f));
        RectTransform fill = NewImage(fillArea, "Fill", barRed, false);
        Stretch(fill, Vector2.zero, Vector2.zero);
        fill.pivot = new Vector2(0f, 0.5f);

        PlayerHealthCardView view = heart.GetComponent<PlayerHealthCardView>();
        if (view == null) view = heart.gameObject.AddComponent<PlayerHealthCardView>();
        var viewSo = new SerializedObject(view);
        viewSo.FindProperty("icon").objectReferenceValue = heartIcon.GetComponent<Image>();
        viewSo.FindProperty("shieldSprite").objectReferenceValue = shieldIcon;
        viewSo.FindProperty("fill").objectReferenceValue = fill;
        viewSo.FindProperty("fillImage").objectReferenceValue = fill.GetComponent<Image>();
        viewSo.FindProperty("redFill").objectReferenceValue = barRed;
        viewSo.FindProperty("grayFill").objectReferenceValue = barGray;
        viewSo.FindProperty("hpText").objectReferenceValue = heartText;
        viewSo.FindProperty("shieldTextColor").colorValue = ShieldTextColor;
        viewSo.ApplyModifiedPropertiesWithoutUndo();

        // 5) 족보 칸
        Place(score, info, 64f, 272f, 364f, 84f);
        SetSprite(info.GetComponent<Image>(), infoCard, true);
        PlaceText(score, handInfo, 74f, 278f, 344f, 72f, 1.265f, false);

        // 6) 점수 칸: 어두운 카드 + "점수" + 청록 칩 칸 × 주황 배수 칸
        Place(score, plaque, 64f, 368f, 364f, 172f);
        SetSprite(plaque.GetComponent<Image>(), card, true);
        RectTransform chipsBoxRect = NewImage(plaque, "ChipsBox", chipsBox, true);
        RectTransform multBoxRect = NewImage(plaque, "MultBox", multBox, true);
        chipsBoxRect.SetSiblingIndex(0);
        multBoxRect.SetSiblingIndex(1);
        multiply.SetSiblingIndex(2);
        Place(score, chipsBoxRect, 76f, 412f, 156f, 120f);
        Place(score, multBoxRect, 260f, 412f, 156f, 120f);
        Place(score, multiply, 224f, 452f, 44f, 44f);
        SetSprite(multiply.GetComponent<Image>(), multiplySprite, false);
        Label(score, plaque, "ScoreTitle", table, "UI_PANEL_SCORE", "점수", 30f, labelFont, labelMaterial, 64f, 369f, 364f, 44f);
        PlaceText(score, chipsSum, 76f, 422f, 156f, 100f, 1.6f, true);
        Regular(chipsSum);
        PlaceText(score, multSum, 260f, 422f, 156f, 100f, 1.6f, true);
        Regular(multSum);
        PlaceText(score, chipsLog, 76f, 414f, 156f, 22f, 1.265f, true);
        PlaceText(score, multLog, 260f, 414f, 156f, 22f, 1.265f, true);
        PlaceText(score, finalDamage, 146f, 500f, 200f, 40f, 1.265f, true);

        // 7) 굴리기 / 끝내기 칸
        Place(score, rolls, 64f, 552f, 172f, 116f);
        SetSprite(rolls.GetComponent<Image>(), card, true);
        Label(score, rolls, "RollsLabel", table, "UI_PANEL_ROLLS", "굴리기", 30f, labelFont, labelMaterial, 64f, 553f, 172f, 44f);
        Place(score, NewImage(rolls, "DiceIcon", diceIcon, false), 92f, 604f, 48f, 48f);
        PlaceText(score, rollsText, 150f, 600f, 80f, 56f, 2.64f, true);
        Regular(rollsText);

        Place(score, finishes, 256f, 552f, 172f, 116f);
        SetSprite(finishes.GetComponent<Image>(), card, true);
        Label(score, finishes, "FinishesLabel", table, "UI_PANEL_FINISHES", "끝내기", 30f, labelFont, labelMaterial, 256f, 553f, 172f, 44f);
        Place(score, NewImage(finishes, "ChipIcon", chipIcon, false), 284f, 604f, 48f, 48f);
        PlaceText(score, finishesText, 342f, 600f, 80f, 56f, 2.64f, true);
        Regular(finishesText);

        // 8) 도감 책 / 주사위 컵 버튼 (지도 화면 배치 코드가 그대로 쓸 수 있는 크기)
        Place(score, book, 64f, 680f, 172f, 144f);
        SetSprite(book.GetComponent<Image>(), buttonFrame, true);
        Place(score, bookIcon, 86f, 688f, 128f, 128f);
        Place(score, deck, 256f, 680f, 172f, 144f);
        SetSprite(deck.GetComponent<Image>(), buttonFrame, true);
        Place(score, deckIcon, 278f, 688f, 128f, 128f);

        // 9) 포획률(페퍼민트) 칸
        Place(score, drop, 64f, 836f, 364f, 80f);
        SetSprite(drop.GetComponent<Image>(), card, true);
        PlaceText(score, dropText, 200f, 836f, 220f, 80f, 0.9f, false);
        Regular(dropText);
        Place(score, mintIcon, 136f, 848f, 56f, 56f);

        // 10) 바이옴 진행 칸
        Place(score, stage, 64f, 928f, 364f, 84f);
        SetSprite(stage.GetComponent<Image>(), card, true);
        PlaceText(score, stageText, 74f, 932f, 344f, 76f, 2.2f, false);
        Regular(stageText);

        // 11) 오른쪽 아래 버튼: 같은 크기(3:1) 그림으로 바꾸고, AI 예시처럼 화면 오른쪽·아래 끝에서 조금 띄운다.
        //     (오른쪽 끝 약 39px, 굴리기 아래 끝 약 62px, 두 버튼 사이 10px). 크기·배율(1.26)은 그대로 둔다.
        //     글자는 기존 텍스트 그대로. 누를 수 없을 때는 반투명 대신 어둡게만 보이게 한다.
        offsetX = 0f;
        SetSprite(finishButton, finishSprite, false);
        SetSprite(rollButton, rollSprite, false);
        Place(score, finishButton.rectTransform, 1526f, 766f, 355.32f, 120.96f, 1.26f);
        Place(score, rollButton.rectTransform, 1526f, 897f, 355.32f, 120.96f, 1.26f);
        SetDisabledColor(finishButton.GetComponent<Button>());
        SetDisabledColor(rollButton.GetComponent<Button>());

        // 12) 보호막: 체력 글자에 (수치)를 붙이고, 체력 숫자 아래 방패 줄은 띄우지 않는다.
        var uiSo = new SerializedObject(ui);
        uiSo.FindProperty("shieldInHealthText").boolValue = true;
        uiSo.ApplyModifiedPropertiesWithoutUndo();
        if (hurt != null)
        {
            var hurtSo = new SerializedObject(hurt);
            hurtSo.FindProperty("showShieldRow").boolValue = false;
            hurtSo.ApplyModifiedPropertiesWithoutUndo();
        }
        else
        {
            Debug.LogWarning(LogTag + "HurtVignetteController가 씬에 없어 방패 줄 끄기를 건너뜁니다.");
        }
    }

    // ───────────────────────── 도우미 ─────────────────────────

    static RectTransform FindScorePanel(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name != "Canvas") continue;
            Transform panel = root.transform.Find("ScorePanel");
            if (panel != null) return (RectTransform)panel;
        }
        throw new InvalidOperationException("Canvas/ScorePanel을 찾지 못했습니다.");
    }

    static RectTransform Child(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        if (child == null) throw new InvalidOperationException(parent.name + "/" + name + "을(를) 찾지 못했습니다.");
        return (RectTransform)child;
    }

    static TextMeshProUGUI Text(RectTransform rect)
    {
        TextMeshProUGUI text = rect.GetComponent<TextMeshProUGUI>();
        if (text == null) throw new InvalidOperationException(rect.name + "에 TextMeshProUGUI가 없습니다.");
        return text;
    }

    static T FindInScene<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T found = root.GetComponentInChildren<T>(true);
            if (found != null) return found;
        }
        return null;
    }

    static Vector3 ToWorld(RectTransform score, float x, float y)
    {
        return score.TransformPoint(new Vector3(x + offsetX - ScoreCenter.x, ScoreCenter.y - y, 0f));
    }

    // 화면 사각형(x, y, w, h)에 맞춘다. 앵커가 한 점이면 그대로 두고(CoinPanel·HeartPanel의 오른쪽 위 기준 유지),
    // 늘어나는 앵커면 가운데 한 점으로 바꾼다. scale은 이 오브젝트 자신의 크기 배율이다.
    static void Place(RectTransform score, RectTransform rect, float x, float y, float w, float h, float scale = 1f)
    {
        if (rect.anchorMin != rect.anchorMax)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
        }
        rect.localRotation = Quaternion.identity;
        rect.localScale = new Vector3(scale, scale, 1f);
        float parentScale = rect.parent.lossyScale.x / score.lossyScale.x;
        rect.sizeDelta = new Vector2(w, h) / (parentScale * scale);
        Vector2 pivot = rect.pivot;
        rect.position = ToWorld(score, x + w * pivot.x, y + h * (1f - pivot.y));
    }

    // 글자 크기(fontSize)는 그대로 두고 배율로 지금과 같은 화면 크기를 유지한다. 칸 크기로 위치를 잡도록 여백은 0으로 한다.
    static void PlaceText(RectTransform score, TextMeshProUGUI text, float x, float y, float w, float h, float scale, bool noWrap, bool center = false)
    {
        Place(score, text.rectTransform, x, y, w, h, scale);
        text.margin = Vector4.zero;
        if (noWrap) text.textWrappingMode = TextWrappingModes.NoWrap;
        if (center) text.horizontalAlignment = HorizontalAlignmentOptions.Center;
    }

    // TMP 굵게 스타일을 끈다. 폰트 자체가 이미 Bold라 덧씌우면 획이 뭉쳐 보인다(원래 씬 값도 굵게 꺼짐).
    static void Regular(TMP_Text text)
    {
        text.fontStyle &= ~FontStyles.Bold;
    }

    static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    static void StretchCard(RectTransform rect, Sprite sprite)
    {
        Stretch(rect, Vector2.zero, Vector2.zero);
        SetSprite(rect.GetComponent<Image>(), sprite, true);
    }

    static void SetSprite(Image image, Sprite sprite, bool sliced)
    {
        if (image == null) throw new InvalidOperationException("Image가 없습니다.");
        image.sprite = sprite;
        image.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
        image.fillCenter = true;
        image.pixelsPerUnitMultiplier = 1f;
        image.preserveAspect = false;
        image.color = Color.white;
    }

    static void SetDisabledColor(Button button)
    {
        if (button == null) return;
        ColorBlock colors = button.colors;
        colors.disabledColor = ButtonDisabledColor;
        button.colors = colors;
    }

    static RectTransform GetOrCreate(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null) return (RectTransform)existing;
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static RectTransform NewImage(Transform parent, string name, Sprite sprite, bool sliced)
    {
        RectTransform rect = GetOrCreate(parent, name);
        Image image = rect.GetComponent<Image>();
        if (image == null) image = rect.gameObject.AddComponent<Image>();
        SetSprite(image, sprite, sliced);
        image.raycastTarget = false;
        return rect;
    }

    // 번역 키가 달린 새 라벨. 다른 버튼 글자와 같은 방식(LocalizeStringEvent → TMP SetText)으로 연결한다.
    static void Label(RectTransform score, Transform parent, string name, StringTableCollection table, string key, string korean,
        float size, TMP_FontAsset font, Material material, float x, float y, float w, float h)
    {
        RectTransform rect = GetOrCreate(parent, name);
        TextMeshProUGUI text = rect.GetComponent<TextMeshProUGUI>();
        if (text == null) text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.fontSharedMaterial = material;
        text.text = korean;
        text.fontSize = size;
        text.fontStyle = FontStyles.Normal;
        text.enableAutoSizing = true;
        text.fontSizeMin = Mathf.Round(size * 0.6f);
        text.fontSizeMax = size;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.margin = Vector4.zero;
        text.raycastTarget = false;
        Place(score, rect, x, y, w, h);

        LocalizeStringEvent localizer = rect.GetComponent<LocalizeStringEvent>();
        if (localizer == null) localizer = rect.gameObject.AddComponent<LocalizeStringEvent>();
        long id = table.SharedData.GetEntry(key).Id;
        localizer.StringReference.SetReference(table.TableCollectionNameReference, id);
        for (int i = localizer.OnUpdateString.GetPersistentEventCount() - 1; i >= 0; i--)
            UnityEventTools.RemovePersistentListener(localizer.OnUpdateString, i);
        UnityEventTools.AddPersistentListener(localizer.OnUpdateString, text.SetText);
    }
}
#endif
