using System;
using System.Collections;
using System.Collections.Generic;
using Coffee.UIEffects;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 디버프 주사위 연출 공용: 전투 시작 등장, 뽑을 때 발동(도둑·족쇄), 결산 발동(가시·저주·가짜), 엘리트 전투 종료 시 임시 디버프 소멸,
// 그리고 같은 녹아내림·안개를 쓰는 조우 제물(제물이 된 소녀에게 바친 주사위가 빨려 들어감).
// 덱·골드·체력 같은 실제 상태는 호출하는 쪽이 바꾸고, 여기서는 화면 표시만 담당합니다.
// 화면 맨 위 전용 오버레이 캔버스(1920×1080 기준)에 그리며, 씬이 바뀌면 캔버스와 함께 정리됩니다.
// 아래 시간 값은 기본 속도 기준이고, 실제 재생은 프로필의 durationScale만큼 느려집니다.
// 게임의 난수 추첨 순서를 바꾸지 않도록 연출에서는 UnityEngine.Random을 쓰지 않습니다.
// 연출별 코드는 같은 폴더의 DebuffFeedback.Arrival / .Triggers / .Vanish / .Sacrifice 파일에 나뉘어 있습니다(모두 같은 DebuffFeedback 클래스).
// 이 파일에는 공용 상태·설정, 연출 시작/건너뛰기 제어, 여러 연출이 같이 쓰는 부품과 도우미가 있습니다.
public static partial class DebuffFeedback
{
    // 뽑는 즉시 발동한 디버프 한 건. stolenGold는 도둑이 실제로 가져간 골드.
    public struct DrawTrigger
    {
        public DiceData1 data;
        public int stolenGold;
    }

    const float FlyTime = 0.42f;
    const string GoldPunchId = "CoinFlyPanelPunch"; // 코인 획득 연출과 같은 펀치를 공유해 겹쳐 커지지 않게 합니다.

    static DebuffVisualProfile profile;
    internal static DebuffVisualProfile Profile => profile != null ? profile : (profile = Resources.Load<DebuffVisualProfile>("DebuffVisualProfile"));

    static RectTransform layer;
    static Material numberMaterial;
    static int smokeSeed;

    // 전투 시작 연출 상태. 등장과 첫 드로우가 같은 건너뛰기 상태를 공유합니다.
    static Sequence introTween;
    static RectTransform introRoot;
    static bool skipRequested;
    static readonly List<Action> introResets = new List<Action>();

    // 기본 속도 기준 시간을 실제 재생 시간으로 바꿉니다. 연출과 맞춰 기다려야 하는 호출부에서 씁니다.
    public static float Scaled(float seconds) => seconds * DurationScale;

    static float DurationScale => Profile != null ? Mathf.Max(0.2f, Profile.durationScale) : 1.5f;
    static float SkipSpeed => Profile != null ? Profile.skipSpeed : 4f;
    static Vector2 EliteIconSize => Vector2.one * (Profile != null ? Profile.eliteIconSize : 115f);
    static Vector2 BossIconSize => Vector2.one * (Profile != null ? Profile.bossIconSize : 154f);
    static Vector2 PortalDieSize => Vector2.one * (Profile != null ? Profile.portalDieSize : 96f);

    // 등장 연출 하나를 만드는 동안 단계들이 함께 쓰는 값.
    sealed class ArrivalContext
    {
        public Sequence seq;
        public RectTransform stage;        // 등장 아이콘이 놓이는 곳. 착지 흔들림도 여기에 줍니다.
        public Vector2 row;                // 주사위 굴림 줄
        public Vector2 deck;               // 덱 버튼 위치
        public Transform deckTarget;
        public Graphic deckGraphic;
        public Color deckColor;
        public Vector3 deckScale;
        public Vector3 deckPosition;
        public float firstLaunch = float.MaxValue; // 어둡게 한 화면을 걷어낼 시점(첫 주사위가 덱으로 출발할 때)
    }

    // 디버프 주사위 아이콘: 이동용 부모 → 크기용 자식 → (테두리 빛, 주사위 그림, 번쩍임).
    // 이동·포물선은 부모가, 커짐·찌그러짐은 크기용 자식이 맡아 서로 부딪히지 않습니다.
    sealed class DieVisual
    {
        public RectTransform mover;
        public RectTransform scaler;
        public Image glow;   // 주사위 뒤 단색 테두리 빛 (기본 꺼짐)
        public Image body;
        public Image flash;  // 주사위 위에 겹친 흰 실루엣. 균열에서 튕겨 나오는 순간 번쩍입니다.
        public UIEffect dissolve; // 덱으로 스며들 때 녹아내리는 효과
        public DebuffDiceType type;
        public Vector2 restPos;   // 덱으로 출발하기 전 머무는 자리
        public float restScale = 1f;
    }

    // 전투 시작 연출을 끝낸다. 중간에 끊겨도 몬스터·덱 버튼·주사위 위치를 원래대로 돌린다.
    public static void EndIntro()
    {
        KillIntroTween();
        if (introRoot != null) UnityEngine.Object.Destroy(introRoot.gameObject);
        introRoot = null;
        skipRequested = false;
    }

    // 보여 준 주사위가 가장자리부터 보랏빛으로 녹아 사라지고, 그 자리에서 밝은 보라 핵을 가진 검보라 안개가
    // 꼬리를 남기며 곡선을 그려 덱으로 스며든다. 도착하는 순간 덱 버튼이 반응하고 왼쪽 위 디버프 표시가 하나 올라갑니다.
    // 도착 시각을 돌려줍니다. bend·travelEase·travel을 주면 안개가 지나는 곡선과 빠르기를 바꿉니다(조우 제물 연출).
    // absorb(0~1)를 주면 날아가는 길의 마지막 그 비율 동안 작아지며 흐려져 대상에 스며듭니다(조우 제물 연출).
    static float InsertDieFly(ArrivalContext ctx, DieVisual visual, float at, Vector2? bend = null, Ease travelEase = Ease.InOutSine, float travel = 0.36f, float absorb = 0f)
    {
        var p = Profile;
        Sequence seq = ctx.seq;
        Color core = p != null ? p.wispCoreColor : new Color(0.8f, 0.5f, 1f);
        Color trail = p != null ? p.wispTrailColor : new Color(0.24f, 0.1f, 0.34f);
        const float melt = 0.3f; // travel: 안개가 덱까지 날아가는 시간(기본 속도 기준)
        ctx.firstLaunch = Mathf.Min(ctx.firstLaunch, at);

        // 녹아내림: 살짝 부풀며 가장자리부터 보랏빛으로 타들어 갑니다.
        UIEffect dissolve = visual.dissolve;
        if (dissolve != null) seq.Insert(at, DOTween.To(() => dissolve.transitionRate, v => dissolve.transitionRate = v, 1f, melt).SetEase(Ease.InQuad));
        else seq.Insert(at, visual.body.DOFade(0f, melt));
        seq.Insert(at, visual.scaler.DOScale(visual.restScale * 1.08f, melt).SetEase(Ease.OutQuad));
        if (visual.glow != null) seq.Insert(at, visual.glow.DOFade(0f, melt * 0.6f));

        // 안개: 녹는 도중 솟아올라 위로 둥글게 휘며 덱으로 날아갑니다.
        Vector2 from = visual.restPos;
        Vector2 to = ctx.deck;
        Vector2 control = bend ?? (from + to) * 0.5f + Vector2.up * 120f;
        Image wisp = null, wispCore = null;
        if (p != null && p.roundGlow != null)
        {
            wisp = CreateImage("Wisp", p.roundGlow, new Vector2(56f, 56f), ctx.stage);
            wisp.color = new Color(core.r, core.g, core.b, 0f);
            wisp.rectTransform.anchoredPosition = from;
            wispCore = CreateImage("Wisp Core", p.roundGlow, new Vector2(22f, 22f), ctx.stage);
            wispCore.color = new Color(1f, 0.92f, 1f, 0f);
            wispCore.rectTransform.anchoredPosition = from;
        }
        float launch = at + melt * 0.55f;
        seq.InsertCallback(launch, () => Smoke(from, trail, 0f, 3, 56f));
        if (wisp != null)
        {
            RectTransform wispRect = wisp.rectTransform, coreRect = wispCore.rectTransform;
            seq.Insert(launch, wisp.DOFade(0.95f, 0.08f));
            seq.Insert(launch, wispCore.DOFade(1f, 0.08f));
            seq.Insert(launch, DOTween.To(() => 0f, t =>
            {
                Vector2 point = Bezier(from, control, to, t);
                if (wispRect != null) wispRect.anchoredPosition = point;
                if (coreRect != null) coreRect.anchoredPosition = point;
            }, 1f, travel).SetEase(travelEase));
            float fadeOut = absorb > 0f ? travel * Mathf.Clamp01(absorb) : 0.08f;
            seq.Insert(launch, wispRect.DOScale(absorb > 0f ? 0.25f : 0.6f, travel).SetEase(Ease.InQuad));
            seq.Insert(launch + travel - fadeOut, wisp.DOFade(0f, fadeOut).SetEase(Ease.InQuad));
            seq.Insert(launch + travel - fadeOut, wispCore.DOFade(0f, fadeOut).SetEase(Ease.InQuad));
            // 지나간 자리에 옅은 안개 꼬리를 남깁니다(같은 곡선·같은 속도 곡선 위).
            const int trailCount = 7;
            for (int k = 1; k < trailCount; k++)
            {
                float progress = DOVirtual.EasedValue(0f, 1f, (float)k / trailCount, travelEase);
                Vector2 point = Bezier(from, control, to, progress);
                seq.InsertCallback(launch + travel * k / trailCount, () => Smoke(point, trail, 0f, 1, 34f));
            }
        }
        float arrive = launch + travel;
        DebuffDiceType type = visual.type;
        seq.InsertCallback(arrive, () => DebuffTrackerHud.Bump(type));
        InsertDeckReaction(ctx, arrive);
        return arrive;
    }

    static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, float t)
    {
        float u = 1f - t;
        return u * u * a + 2f * u * t * b + t * t * c;
    }

    static Texture2D dissolveNoise;

    // 가장자리부터 안쪽으로 녹도록 바깥이 낮고 가운데가 높은 잡음 마스크(난수 대신 해시). 티켓 획득 연출도 같이 쓴다.
    internal static Texture2D DissolveNoise()
    {
        if (dissolveNoise != null) return dissolveNoise;
        const int size = 32;
        dissolveNoise = new Texture2D(size, size, TextureFormat.RGBA32, false, true) { name = "DebuffDissolveMask", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float edge = Mathf.Max(Mathf.Abs((x + 0.5f) / size * 2f - 1f), Mathf.Abs((y + 0.5f) / size * 2f - 1f));
                uint hash = unchecked((uint)(x * 374761393 + y * 668265263));
                hash = (hash ^ (hash >> 13)) * 1274126177u;
                float noise = (hash & 255) / 255f;
                byte value = (byte)Mathf.RoundToInt(Mathf.Clamp01((1f - edge) * 0.75f + noise * 0.25f) * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, value);
            }
        dissolveNoise.SetPixels32(pixels);
        dissolveNoise.Apply(false, true);
        return dissolveNoise;
    }

    static DieVisual CreateDieVisual(string name, Sprite sprite, Vector2 size, RectTransform parent, DebuffDiceType type, bool withFlash = false)
    {
        var visual = new DieVisual { type = type };
        visual.mover = NewPoint(name, parent);
        visual.scaler = NewPoint("Scale", visual.mover);
        var p = Profile;
        if (p != null && p.edgeGlow && sprite != null)
        {
            // 같은 모양을 단색으로 칠해 살짝 크게 뒤에 깔면 테두리가 빛나 보입니다.
            visual.glow = CreateSilhouette("Edge Glow", sprite, size, visual.scaler, p.edgeGlowColor);
            visual.glow.rectTransform.localScale = Vector3.one * 1.14f;
        }
        visual.body = CreateImage("Die", sprite, size, visual.scaler);
        // 덱으로 스며들 때 가장자리부터 보랏빛으로 녹는 효과(처음엔 꺼진 상태).
        var dissolve = visual.body.gameObject.AddComponent<UIEffect>();
        dissolve.transitionFilter = TransitionFilter.Dissolve;
        dissolve.transitionTexture = DissolveNoise();
        dissolve.transitionWidth = 0.18f;
        dissolve.transitionSoftness = 0.06f;
        dissolve.transitionColorFilter = ColorFilter.Replace;
        dissolve.transitionColor = p != null ? p.wispCoreColor : new Color(0.8f, 0.5f, 1f);
        dissolve.transitionRate = 0f;
        visual.dissolve = dissolve;
        if (withFlash && sprite != null) visual.flash = CreateSilhouette("Exit Flash", sprite, size, visual.scaler, Color.white);
        return visual;
    }

    // 주사위 모양 그대로 한 가지 색으로 칠한 이미지(처음엔 투명). 테두리 빛·번쩍임에 씁니다.
    static Image CreateSilhouette(string name, Sprite sprite, Vector2 size, RectTransform parent, Color color)
    {
        Image image = CreateImage(name, sprite, size, parent);
        var effect = image.gameObject.AddComponent<UIEffect>();
        effect.colorFilter = ColorFilter.Replace;
        effect.color = color;
        effect.colorIntensity = 1f;
        image.color = new Color(1f, 1f, 1f, 0f);
        return image;
    }

    const float GlowAlpha = 0.85f;

    static RectTransform NewPoint(string name, Transform parent)
    {
        var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rect.SetParent(parent, false);
        rect.sizeDelta = Vector2.zero;
        return rect;
    }

    static Image CreateDim(RectTransform parent)
    {
        Image dim = CreateImage("Intro Dim", null, Vector2.zero, parent);
        Stretch(dim.rectTransform);
        dim.color = new Color(0f, 0f, 0f, 0f);
        return dim;
    }

    // 사방으로 튀는 작은 사각 빛 조각(이미지 없이 단색 사각형).
    static void Sparks(Vector2 position, Color color, int count, float distance)
    {
        Sequence seq = NewSequence(Layer.gameObject);
        for (int i = 0; i < count; i++)
        {
            Image spark = CreateImage("Debuff Spark", null, new Vector2(8f, 8f), Layer);
            spark.color = color;
            RectTransform rect = spark.rectTransform;
            rect.anchoredPosition = position;
            float angle = i * Mathf.PI * 2f / count + 0.3f;
            float reach = distance * (i % 2 == 0 ? 1f : 0.7f);
            Vector2 target = position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * reach;
            seq.Insert(0f, rect.DOAnchorPos(target, 0.35f).SetEase(Ease.OutQuad));
            seq.Insert(0f, rect.DOScale(0.2f, 0.35f));
            seq.Insert(0.15f, spark.DOFade(0f, 0.2f));
            seq.InsertCallback(0.36f, () => { if (spark != null) UnityEngine.Object.Destroy(spark.gameObject); });
        }
    }

    // 납작한 바닥 그림자. 이미지가 없으면 null.
    static Image CreateShadow(string name, float width, RectTransform parent)
    {
        Sprite sprite = Profile != null ? Profile.groundShadow : null;
        if (sprite == null) return null;
        float aspect = sprite.rect.width > 0f ? sprite.rect.height / sprite.rect.width : 0.18f;
        Image shadow = CreateImage(name, sprite, new Vector2(width, width * aspect), parent);
        shadow.preserveAspect = false;
        return shadow;
    }

    // skippable이 false면 화면을 눌러도 빨리 감기지 않고(조우 제물 연출), 아래 UI 클릭만 막습니다.
    static RectTransform BeginIntro(bool skippable = true)
    {
        if (introRoot != null) return introRoot;
        introRoot = NewContainer("Debuff Intro", Layer);
        // 화면을 누르면 남은 연출을 빠르게 재생합니다. 투명 이미지가 입력을 받아 아래 UI 클릭도 막습니다.
        Image catcher = CreateImage("Skip Catcher", null, Vector2.zero, introRoot);
        Stretch(catcher.rectTransform);
        catcher.color = new Color(0f, 0f, 0f, 0f);
        catcher.raycastTarget = true;
        if (!skippable) return introRoot;
        var trigger = catcher.gameObject.AddComponent<EventTrigger>();
        var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
        entry.callback.AddListener(_ => RequestSkip());
        trigger.triggers.Add(entry);
        return introRoot;
    }

    static void RequestSkip()
    {
        skipRequested = true;
        if (introTween != null && introTween.IsActive()) introTween.timeScale = SkipSpeed / DurationScale;
    }

    static Sequence NewIntroTween()
    {
        KillIntroTween();
        introTween = DOTween.Sequence().SetUpdate(true);
        introTween.timeScale = (skipRequested ? SkipSpeed : 1f) / DurationScale;
        return introTween;
    }

    static void KillIntroTween()
    {
        introTween?.Kill(false);
        introTween = null;
        foreach (var reset in introResets) reset();
        introResets.Clear();
    }

    // ---------- 공용 도우미 ----------

    public static Sprite DieSprite(DiceData1 data)
    {
        if (data == null) return null;
        if (data.customFaceSprites != null)
            foreach (var sprite in data.customFaceSprites)
                if (sprite != null) return sprite;
        return data.customDiceShell;
    }

    // 프로필의 연출 속도를 반영한 시퀀스. 아래 시간 값은 모두 기본 속도 기준입니다.
    static Sequence NewSequence(GameObject link)
    {
        Sequence seq = DOTween.Sequence().SetUpdate(true);
        seq.timeScale = 1f / DurationScale;
        if (link != null) seq.SetLink(link);
        return seq;
    }

    static RectTransform Layer
    {
        get
        {
            if (layer != null) return layer;
            var go = new GameObject("Debuff FX Layer", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            layer = (RectTransform)go.transform;
            Canvas.ForceUpdateCanvases();
            return layer;
        }
    }

    static RectTransform NewContainer(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        Stretch(rect);
        return rect;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    static Image CreateImage(string name, Sprite sprite, Vector2 size, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent != null ? parent : Layer, false);
        rect.sizeDelta = size;
        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        image.preserveAspect = true;
        return image;
    }

    // 흰 연기 이미지에 색을 곱해 몇 덩이를 피웁니다. 위치는 난수 대신 황금각으로 흩뜨립니다.
    static void Smoke(Vector2 position, Color color, float delay, int puffs, float size)
    {
        Sprite sprite = Profile != null ? Profile.smokePuff : null;
        if (sprite == null) return;
        float baseAngle = smokeSeed++ * 2.399f;
        for (int i = 0; i < puffs; i++)
        {
            Image puff = CreateImage("Debuff Smoke", sprite, Vector2.one * size, Layer);
            puff.color = color;
            RectTransform rect = puff.rectTransform;
            float angle = baseAngle + i * Mathf.PI * 2f / puffs;
            Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * size * 0.35f;
            rect.anchoredPosition = position + offset * 0.4f;
            rect.localScale = Vector3.one * 0.4f;
            puff.gameObject.SetActive(false);
            Sequence seq = NewSequence(puff.gameObject);
            seq.AppendInterval(delay + i * 0.04f);
            seq.AppendCallback(() => puff.gameObject.SetActive(true));
            seq.Append(rect.DOScale(1.25f, 0.45f).SetEase(Ease.OutQuad));
            seq.Join(rect.DOAnchorPos(position + offset + Vector2.up * size * 0.4f, 0.45f).SetEase(Ease.OutQuad));
            seq.Join(puff.DOFade(0f, 0.45f).SetEase(Ease.InQuad));
            seq.OnComplete(() => { if (puff != null) UnityEngine.Object.Destroy(puff.gameObject); });
        }
    }

    // 짧게 튀어 올랐다가 사라지는 숫자(−100, −1 등).
    static void PopNumber(string text, Color color, Vector2 position, float delay = 0f, float fontSize = 40f)
    {
        var go = new GameObject("Debuff Number", typeof(RectTransform), typeof(TextMeshProUGUI));
        var rect = (RectTransform)go.transform;
        rect.SetParent(Layer, false);
        rect.sizeDelta = new Vector2(240f, 64f);
        rect.anchoredPosition = position;
        var label = go.GetComponent<TextMeshProUGUI>();
        TMP_FontAsset font = NumberFont();
        if (font != null) label.font = font;
        Material outline = NumberMaterial(label.font);
        if (outline != null) label.fontSharedMaterial = outline;
        label.text = text;
        label.fontSize = fontSize;
        label.color = color;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        rect.localScale = Vector3.one * 0.6f;
        go.SetActive(false);

        Sequence seq = NewSequence(go);
        seq.AppendInterval(delay);
        seq.AppendCallback(() => go.SetActive(true));
        seq.Append(rect.DOScale(1.15f, 0.14f).SetEase(Ease.OutBack));
        seq.Append(rect.DOScale(1f, 0.08f));
        seq.Insert(delay, rect.DOAnchorPosY(position.y + 34f, 0.6f).SetEase(Ease.OutQuad));
        seq.Insert(delay + 0.45f, label.DOFade(0f, 0.25f));
        seq.OnComplete(() => { if (go != null) UnityEngine.Object.Destroy(go); });
    }

    internal static TMP_FontAsset NumberFont()
    {
        DiceManager manager = DiceManager.Instance;
        UIManager ui = manager != null ? manager.ui : null;
        return ui != null && ui.goldText != null ? ui.goldText.font : null;
    }

    // 배경과 겹쳐도 읽히도록 외곽선을 넣은 공유 머티리얼. 팝업마다 머티리얼을 복제하지 않습니다.
    internal static Material NumberMaterial(TMP_FontAsset font)
    {
        if (font == null || font.material == null) return null;
        if (numberMaterial != null && numberMaterial.mainTexture == font.material.mainTexture) return numberMaterial;
        numberMaterial = new Material(font.material) { name = "Debuff Number Outline" };
        numberMaterial.EnableKeyword(ShaderUtilities.Keyword_Outline);
        numberMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.25f);
        numberMaterial.SetColor(ShaderUtilities.ID_OutlineColor, new Color32(20, 12, 10, 255));
        return numberMaterial;
    }

    static void ShowGold(ShopManager shop, int value)
    {
        if (shop == null) return;
        if (GoldCounter.Instance != null)
        {
            GoldCounter.Instance.SetGold(value);
            return;
        }
        DiceManager manager = shop.diceManager;
        UIManager ui = manager != null ? manager.ui : null;
        if (ui != null && ui.goldText != null) ui.UpdateGoldUI(value);
    }

    // HUD 골드 패널(CoinPanel)과 그 안의 골드 아이콘. 코인 획득 연출과 같은 기준으로 찾습니다.
    static Transform GoldPanel(UIManager ui)
    {
        if (ui == null || ui.goldText == null) return null;
        Transform panel = ui.goldText.transform;
        while (panel != null && panel.name != "CoinPanel") panel = panel.parent;
        return panel != null ? panel : ui.goldText.transform.parent;
    }

    static Transform GoldIcon(UIManager ui)
    {
        Transform panel = GoldPanel(ui);
        if (panel == null) return ui != null && ui.goldText != null ? ui.goldText.transform : null;
        Transform icon = panel.Find("Icon_Gold");
        return icon != null ? icon : panel;
    }

    static void PunchGoldPanel(UIManager ui)
    {
        Transform panel = GoldPanel(ui);
        if (panel == null) return;
        DOTween.Complete(GoldPunchId);
        panel.DOPunchScale(Vector3.one * 0.08f, 0.15f, 6, 0.5f).SetId(GoldPunchId).SetUpdate(true).SetLink(panel.gameObject);
    }

    static Dice FindDie(List<Dice> board, DiceData1 data)
    {
        if (board == null || data == null) return null;
        foreach (var die in board)
            if (die != null && die.gameObject.activeInHierarchy && die.myData == data) return die;
        return null;
    }

    static Vector2 DieLocal(Dice die)
    {
        if (die == null) return ToLocal(new Vector2(Screen.width * 0.55f, Screen.height * 0.25f));
        var body = die.GetComponent<SpriteRenderer>();
        return WorldToLocal(body != null ? body.bounds.center : die.transform.position);
    }

    static Vector2 ToLocal(Vector2 screen)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(Layer, screen, null, out Vector2 local);
        return local;
    }

    static Vector2 WorldToLocal(Vector3 world)
    {
        Camera camera = Camera.main;
        Vector2 screen = camera != null ? (Vector2)camera.WorldToScreenPoint(world) : new Vector2(Screen.width * 0.6f, Screen.height * 0.65f);
        return ToLocal(screen);
    }

    static Vector2 UiScreenPoint(Transform target, Vector2 fallbackViewport)
    {
        if (target == null) return new Vector2(Screen.width * fallbackViewport.x, Screen.height * fallbackViewport.y);
        Vector3 point = target is RectTransform rect ? rect.TransformPoint(rect.rect.center) : target.position;
        return RectTransformUtility.WorldToScreenPoint(CanvasCamera(target), point);
    }

    static Vector2 UiToLocal(Transform target, Vector2 fallbackViewport) => ToLocal(UiScreenPoint(target, fallbackViewport));

    static Rect UiRectInLayer(RectTransform target)
    {
        var corners = new Vector3[4];
        target.GetWorldCorners(corners);
        Camera camera = CanvasCamera(target);
        Vector2 min = ToLocal(RectTransformUtility.WorldToScreenPoint(camera, corners[0]));
        Vector2 max = ToLocal(RectTransformUtility.WorldToScreenPoint(camera, corners[2]));
        return Rect.MinMaxRect(Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y), Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y));
    }

    // 화면(오버레이 캔버스) 안쪽으로 잘라낸 영역. 화면 끝에 걸친 버튼에 사슬이 밖으로 삐져나가지 않게 합니다.
    static Rect ClampToLayer(Rect area, float margin)
    {
        Rect bounds = Layer.rect;
        float xMin = Mathf.Max(area.xMin, bounds.xMin + margin), xMax = Mathf.Min(area.xMax, bounds.xMax - margin);
        float yMin = Mathf.Max(area.yMin, bounds.yMin + margin), yMax = Mathf.Min(area.yMax, bounds.yMax - margin);
        return xMax > xMin && yMax > yMin ? Rect.MinMaxRect(xMin, yMin, xMax, yMax) : area;
    }

    // 대상이 그려지는 캔버스의 카메라. 오버레이면 null, 월드 캔버스에 카메라가 비어 있으면 메인 카메라를 씁니다.
    static Camera CanvasCamera(Transform target)
    {
        Canvas canvas = target.GetComponentInParent<Canvas>();
        if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay) return null;
        if (canvas.renderMode == RenderMode.WorldSpace && canvas.worldCamera == null) return Camera.main;
        return canvas.worldCamera;
    }
}
