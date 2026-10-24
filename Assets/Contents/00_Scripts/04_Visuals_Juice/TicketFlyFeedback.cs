using Coffee.UIEffects;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

// 티켓 획득 연출: 고른 티켓이 제자리에서 금빛으로 녹아 사라지고, 작은 금빛 불꽃이 연기 꼬리를 남기며
// 왼쪽 패널의 도감 버튼으로 날아간다. 도착하면 도감 아이콘이 튀며 반짝인다. (디버프 주사위가 덱으로 스며드는 연출과 같은 방식)
// 도감 버튼(Canvas/ScorePanel/Book_Button)에 붙어 있다. 이 컴포넌트가 없는 씬(튜토리얼 등)에서는 연출 없이 넘어간다.
// 게임 RNG 순서를 건드리지 않도록 Random은 쓰지 않는다.
public class TicketFlyFeedback : MonoBehaviour
{
    [Header("연결")]
    public Image bookIcon;     // 도착 이펙트가 터지는 도감 아이콘(Icon_Book)
    public Sprite glowSprite;  // 불꽃 빛 (디버프 안개와 같은 Debuff_RoundGlow)
    public Sprite smokeSprite; // 연기 덩어리 (디버프 연기와 같은 Smoke_Puff)

    [Header("색 (금색 하나로 통일)")]
    public Color goldColor = new Color(1f, 0.82f, 0.32f, 1f);     // 녹는 테두리, 불꽃
    public Color coreColor = new Color(1f, 0.97f, 0.85f, 1f);     // 불꽃 가운데
    public Color smokeColor = new Color(0.95f, 0.75f, 0.35f, 0.85f);

    [Header("녹기 (고른 티켓이 제자리에서 녹아 사라짐)")]
    public float meltDuration = 0.3f;
    public float meltSwell = 1.08f;      // 녹으며 살짝 부푸는 배율
    [Range(0f, 1f)] public float launchAt = 0.55f; // 녹는 시간 중 이 비율이 지나면 불꽃이 출발

    [Header("날아가기")]
    public float flyDuration = 0.5f;
    public float arcHeight = 160f;       // 곡선으로 날아갈 때 위로 휘는 높이(캔버스 단위)
    public float wispSize = 64f;
    public float wispCoreSize = 26f;
    public float wispEndScale = 0.6f;    // 도착할 때 불꽃 크기 배율
    public int launchPuffs = 3;          // 출발할 때 피어오르는 연기 수
    public float launchPuffSize = 56f;
    public int trailPuffs = 7;           // 지나간 자리에 남는 연기 수
    public float trailPuffSize = 34f;
    public int sortingOrder = 120;       // 다른 UI(툴팁 101)보다 위에 그린다

    [Header("도착 이펙트")]
    public float punchScale = 0.25f;     // 도감 아이콘이 튀는 크기(0.25 = 1.25배)
    public float punchDuration = 0.35f;
    public float echoScale = 1.8f;       // 도감 아이콘 잔상이 퍼지는 배율
    public float echoDuration = 0.4f;
    public Color echoColor = new Color(1f, 0.9f, 0.45f, 0.8f);
    public int sparkleCount = 8;
    public float sparkleSize = 14f;
    public float sparkleDistance = 120f;
    public float sparkleDuration = 0.45f;
    public Color sparkleColor = new Color(1f, 0.85f, 0.3f, 1f);
    public bool playSound = true;
    public SfxId arriveSound = SfxId.Mult_Pop;

    const string PunchId = "TicketFlyBookPunch";

    static TicketFlyFeedback instance;
    RectTransform layer;
    int smokeSeed;

    void Awake()
    {
        instance = this;
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    // from(티켓 선택창의 티켓 이미지 등) 위치에서 도감 버튼으로 티켓을 날린다. 게임 진행은 기다리지 않는다.
    public static void Play(TicketItemSO ticket, RectTransform from)
    {
        if (ticket == null || ticket.icon == null || from == null) return;
        if (instance == null) instance = FindFirstObjectByType<TicketFlyFeedback>(FindObjectsInactive.Include);
        if (instance == null || !instance.isActiveAndEnabled) return; // 도감 버튼이 숨겨진 화면에서는 생략

        instance.Fly(ticket.icon, from);
    }

    void Fly(Sprite sprite, RectTransform from)
    {
        RectTransform flyLayer = GetLayer();
        if (flyLayer == null) return;

        Vector3 start = from.position;
        PlayMelt(flyLayer, sprite, from);

        // 녹는 도중 불꽃이 솟아 도감으로 날아간다.
        DOVirtual.DelayedCall(meltDuration * launchAt, () => LaunchWisp(start), true).SetLink(gameObject);
    }

    // 선택창에서 보이던 크기·자리 그대로 티켓 복사본을 두고, 가장자리부터 금빛으로 녹인다.
    void PlayMelt(RectTransform flyLayer, Sprite sprite, RectTransform from)
    {
        Image ticket = CreateImage("TicketMelt", sprite, flyLayer, SizeInLayer(from, flyLayer, new Vector2(150f, 150f)));
        RectTransform rect = ticket.rectTransform;
        rect.position = from.position;

        var dissolve = ticket.gameObject.AddComponent<UIEffect>();
        dissolve.transitionFilter = TransitionFilter.Dissolve;
        dissolve.transitionTexture = DebuffFeedback.DissolveNoise();
        dissolve.transitionWidth = 0.18f;
        dissolve.transitionSoftness = 0.06f;
        dissolve.transitionColorFilter = ColorFilter.Replace;
        dissolve.transitionColor = goldColor;
        dissolve.transitionRate = 0f;

        Sequence seq = DOTween.Sequence().SetUpdate(true).SetLink(ticket.gameObject);
        seq.Append(DOTween.To(() => dissolve.transitionRate, v => dissolve.transitionRate = v, 1f, meltDuration).SetEase(Ease.InQuad));
        seq.Join(rect.DOScale(meltSwell, meltDuration).SetEase(Ease.OutQuad));
        seq.OnComplete(() => Destroy(ticket.gameObject));
    }

    void LaunchWisp(Vector3 start)
    {
        RectTransform flyLayer = GetLayer();
        if (flyLayer == null) return;

        float layerScale = Mathf.Max(0.0001f, flyLayer.lossyScale.x);
        float arc = arcHeight * layerScale;
        RectTransform target = (RectTransform)transform;
        Smoke(flyLayer, start, launchPuffs, launchPuffSize);

        if (glowSprite == null)
        {
            // 불꽃 이미지가 없으면 날아가는 시간만큼 기다렸다가 도착 이펙트만 보여 준다.
            DOVirtual.DelayedCall(flyDuration, PlayArrive, true).SetLink(gameObject);
            return;
        }

        Image glow = CreateImage("TicketWisp", glowSprite, flyLayer, Vector2.one * wispSize);
        Image core = CreateImage("TicketWispCore", glowSprite, flyLayer, Vector2.one * wispCoreSize);
        glow.color = new Color(goldColor.r, goldColor.g, goldColor.b, 0f);
        core.color = new Color(coreColor.r, coreColor.g, coreColor.b, 0f);
        RectTransform glowRect = glow.rectTransform, coreRect = core.rectTransform;
        glowRect.position = start;
        coreRect.position = start;

        int nextPuff = 1;
        Sequence seq = DOTween.Sequence().SetUpdate(true).SetLink(glow.gameObject);
        seq.Insert(0f, glow.DOFade(goldColor.a * 0.95f, 0.08f));
        seq.Insert(0f, core.DOFade(coreColor.a, 0.08f));
        // 도감 버튼은 화면(전투/지도)에 따라 자리가 바뀌므로 매 프레임 현재 위치를 따라간다.
        seq.Insert(0f, DOVirtual.Float(0f, 1f, flyDuration, t =>
        {
            if (glowRect == null || coreRect == null) return;
            Vector3 end = target != null ? target.position : start;
            Vector3 control = (start + end) * 0.5f + Vector3.up * arc;
            Vector3 point = Bezier(start, control, end, t);
            glowRect.position = point;
            coreRect.position = point;
            glowRect.localScale = Vector3.one * Mathf.Lerp(1f, wispEndScale, t * t);
            // 지나간 자리에 연기 꼬리를 남긴다.
            while (trailPuffs > 1 && nextPuff < trailPuffs && t >= nextPuff / (float)trailPuffs)
            {
                Smoke(flyLayer, Bezier(start, control, end, nextPuff / (float)trailPuffs), 1, trailPuffSize);
                nextPuff++;
            }
        }).SetEase(Ease.InOutSine));
        seq.Insert(Mathf.Max(0f, flyDuration - 0.08f), glow.DOFade(0f, 0.08f));
        seq.Insert(Mathf.Max(0f, flyDuration - 0.08f), core.DOFade(0f, 0.08f));
        seq.OnComplete(() =>
        {
            Destroy(glow.gameObject);
            if (core != null) Destroy(core.gameObject);
            PlayArrive();
        });
    }

    static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, float t)
    {
        float u = 1f - t;
        return u * u * a + 2f * u * t * b + t * t * c;
    }

    // 흰 연기 이미지에 금색을 곱해 몇 덩이를 피운다. 위치는 난수 대신 황금각으로 흩뜨린다.
    void Smoke(RectTransform flyLayer, Vector3 position, int puffs, float size)
    {
        if (smokeSprite == null || puffs <= 0) return;
        float scale = flyLayer.lossyScale.x;
        float baseAngle = smokeSeed++ * 2.399f;
        for (int i = 0; i < puffs; i++)
        {
            Image puff = CreateImage("TicketSmoke", smokeSprite, flyLayer, Vector2.one * size);
            puff.color = smokeColor;
            RectTransform rect = puff.rectTransform;
            float angle = baseAngle + i * Mathf.PI * 2f / puffs;
            Vector3 offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * size * 0.35f * scale;
            rect.position = position + offset * 0.4f;
            rect.localScale = Vector3.one * 0.4f;
            puff.gameObject.SetActive(false);

            Sequence seq = DOTween.Sequence().SetUpdate(true).SetLink(puff.gameObject);
            seq.AppendInterval(i * 0.04f);
            seq.AppendCallback(() => puff.gameObject.SetActive(true));
            seq.Append(rect.DOScale(1.25f, 0.45f).SetEase(Ease.OutQuad));
            seq.Join(rect.DOMove(position + offset + Vector3.up * size * 0.4f * scale, 0.45f).SetEase(Ease.OutQuad));
            seq.Join(puff.DOFade(0f, 0.45f).SetEase(Ease.InQuad));
            seq.OnComplete(() => Destroy(puff.gameObject));
        }
    }

    static Image CreateImage(string name, Sprite sprite, RectTransform parent, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.sizeDelta = size;
        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }

    // source가 화면에서 보이던 크기를 flyLayer 기준 크기로 바꾼다.
    static Vector2 SizeInLayer(RectTransform source, RectTransform flyLayer, Vector2 fallback)
    {
        float layerScale = Mathf.Max(0.0001f, flyLayer.lossyScale.x);
        Vector2 size = source.rect.size * (source.lossyScale.x / layerScale);
        return size.sqrMagnitude > 1f ? size : fallback;
    }

    void PlayArrive()
    {
        if (playSound) Sfx.Play(arriveSound);
        if (bookIcon == null) return;

        RectTransform icon = bookIcon.rectTransform;
        DOTween.Complete(PunchId);
        icon.DOPunchScale(Vector3.one * punchScale, punchDuration, 6, 0.5f)
            .SetId(PunchId)
            .SetUpdate(true)
            .SetLink(icon.gameObject);

        RectTransform flyLayer = GetLayer();
        if (flyLayer == null) return;
        PlayEcho(flyLayer, icon);
        PlaySparkles(flyLayer, icon.position);
    }

    // 도감 아이콘 모양 잔상이 커지며 사라진다.
    void PlayEcho(RectTransform flyLayer, RectTransform icon)
    {
        var go = new GameObject("TicketFlyEcho", typeof(RectTransform), typeof(Image));
        var rect = (RectTransform)go.transform;
        rect.SetParent(flyLayer, false);
        rect.position = icon.position;
        rect.sizeDelta = icon.rect.size * (icon.lossyScale.x / Mathf.Max(0.0001f, flyLayer.lossyScale.x));

        Image image = go.GetComponent<Image>();
        image.sprite = bookIcon.sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.color = echoColor;

        Sequence seq = DOTween.Sequence().SetUpdate(true).SetLink(go);
        seq.Append(rect.DOScale(echoScale, echoDuration).SetEase(Ease.OutCubic));
        seq.Join(image.DOFade(0f, echoDuration).SetEase(Ease.InQuad));
        seq.OnComplete(() => Destroy(go));
    }

    // 도감 아이콘 둘레로 작은 네모 반짝이가 퍼진다. 각도는 고정 간격, 거리는 번갈아 길고 짧게.
    void PlaySparkles(RectTransform flyLayer, Vector3 center)
    {
        float scale = flyLayer.lossyScale.x;
        for (int i = 0; i < sparkleCount; i++)
        {
            var go = new GameObject("TicketFlySparkle", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(flyLayer, false);
            rect.position = center;
            rect.sizeDelta = new Vector2(sparkleSize, sparkleSize);
            rect.localRotation = Quaternion.Euler(0f, 0f, 45f);

            Image image = go.GetComponent<Image>();
            image.color = sparkleColor;
            image.raycastTarget = false;

            float angle = (i / (float)sparkleCount) * Mathf.PI * 2f + Mathf.PI * 0.125f;
            float distance = sparkleDistance * (i % 2 == 0 ? 1f : 0.65f) * scale;
            Vector3 end = center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * distance;

            Sequence seq = DOTween.Sequence().SetUpdate(true).SetLink(go);
            seq.Append(rect.DOMove(end, sparkleDuration).SetEase(Ease.OutCubic));
            seq.Join(rect.DOScale(0f, sparkleDuration).SetEase(Ease.InQuad));
            seq.OnComplete(() => Destroy(go));
        }
    }

    // 버튼과 같은 캔버스 맨 위에 그린다.
    RectTransform GetLayer()
    {
        if (layer != null)
        {
            layer.SetAsLastSibling();
            return layer;
        }

        Canvas ownCanvas = GetComponentInParent<Canvas>();
        if (ownCanvas == null) return null;
        Canvas root = ownCanvas.rootCanvas;

        var go = new GameObject("TicketFly_Layer", typeof(RectTransform), typeof(Canvas));
        layer = (RectTransform)go.transform;
        layer.SetParent(root.transform, false);
        layer.anchorMin = Vector2.zero;
        layer.anchorMax = Vector2.one;
        layer.offsetMin = Vector2.zero;
        layer.offsetMax = Vector2.zero;

        Canvas canvas = go.GetComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingLayerID = root.sortingLayerID;
        canvas.sortingOrder = sortingOrder;

        layer.SetAsLastSibling();
        return layer;
    }
}
