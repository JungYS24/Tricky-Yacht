using System;
using System.Collections;
using Coffee.UIEffects;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

// 디버프 연출(DebuffFeedback의 일부): 최종보스 악마주사위 1페이즈 '장난의 저주'.
// 보스 뒤로 보라 오라가 차오르고(제물 소녀 오라와 같은 겹 실루엣), 보라 안개가 보스에서 대상 주사위로 곡선을 그리며 날아가
// 닿는 순간 주사위가 보랏빛으로 번쩍이며 저주·가시 주사위로 바뀐다. 실제로 바꾸는 일은 swap이 한다(닿는 순간 한 번).
// 기본 속도 기준 약 1.55초(오라 0.3초 → 0.25초 머묾 → 안개 0.55초 → 변신 후 0.45초). 화면을 눌러도 건너뛰지 않는다. 공용 부품은 DebuffFeedback.cs에 있다.
public static partial class DebuffFeedback
{
    public static IEnumerator PlayDevilCurse(Enemy boss, Dice die, Action swap)
    {
        bool swapped = false;
        void Swap()
        {
            if (swapped) return;
            swapped = true;
            swap?.Invoke();
        }

        SpriteRenderer body = boss != null ? boss.monsterImage : null;
        SpriteRenderer dieBody = die != null ? die.GetComponent<SpriteRenderer>() : null;
        if (body == null || dieBody == null)
        {
            Swap();
            yield break;
        }

        var p = Profile;
        Color tint = p != null ? p.sacrificeTint : new Color(0.72f, 0.45f, 1f);
        Color core = p != null ? p.wispCoreColor : new Color(0.8f, 0.5f, 1f);
        Color trail = p != null ? p.wispTrailColor : new Color(0.24f, 0.1f, 0.34f);

        RectTransform root = BeginIntro(skippable: false);
        RectTransform stage = NewContainer("Devil Curse", root);
        Sequence seq = NewIntroTween();
        Image[] aura = CreateBossAura(body, tint); // NewIntroTween 뒤에 만들어야 정리 목록이 지워지지 않음

        const float auraRise = 0.3f; // 오라가 차오르는 시간
        const float auraGrow = 0.16f; // 오라 가장 바깥 겹이 보스보다 커지는 비율
        const float launch = 0.55f;  // 안개 출발: 오라가 다 차오른(0.3초) 뒤 잠깐 머물렀다가
        const float travel = 0.55f;  // 안개가 주사위까지 날아가는 시간
        float arrive = launch + travel;
        float end = arrive + 0.45f;
        InsertBossAura(seq, aura, body, auraRise, arrive, end, auraGrow);
        // 보스가 시전하는 순간에만 화면을 아주 약하게 한 번 툭 흔든다(능력을 썼다는 신호, 0.25초).
        CameraShake.Instance?.ShakePixels(1.5f, 0.25f);

        // 안개: 보스 가운데에서 나와 바깥으로 휘었다가 점점 빨라지며 주사위에 스며든다.
        Vector2 from = WorldToLocal(body.bounds.center);
        Vector2 to = DieLocal(die);
        Vector2 dir = to - from;
        Vector2 side = new Vector2(-dir.y, dir.x).normalized;
        if (side.x * dir.x < 0f) side = -side; // 주사위가 있는 쪽으로 휜다
        Vector2 control = (from + to) * 0.5f + side * 120f;
        InsertCurseWisp(seq, stage, from, control, to, launch, travel, core, trail);

        // 주사위 변신: 안개가 닿기 직전 보라 실루엣이 덮고, 닿는 순간 빛 조각이 튀며 바뀐 뒤 실루엣이 걷힌다(연기 없음).
        Bounds b = dieBody.bounds;
        Vector2 min = WorldToLocal(b.min), max = WorldToLocal(b.max);
        Vector2 size = new Vector2(Mathf.Abs(max.x - min.x), Mathf.Abs(max.y - min.y));
        Image cover = CreateSilhouette("Curse Cover", dieBody.sprite, size, stage, tint);
        cover.rectTransform.anchoredPosition = to;
        seq.Insert(arrive - 0.12f, cover.DOFade(0.95f, 0.12f));
        seq.InsertCallback(arrive, () =>
        {
            Swap();
            if (cover != null && dieBody != null) cover.sprite = dieBody.sprite;
            Sparks(to, core, 8, size.x * 0.8f);
        });
        seq.Insert(arrive + 0.05f, cover.DOFade(0f, 0.3f));
        seq.InsertCallback(end, () => { });

        try { yield return seq.WaitForCompletion(); }
        finally
        {
            Swap(); // 연출이 끊겨도 주사위는 바꾼다
            EndIntro();
        }
    }

    // 보스 그림을 보라 실루엣으로 칠한 복사본 여러 겹을 보스 바로 뒤(배경과 보스 사이)에 깐다. 제물 소녀 오라와 같은 방식.
    // 보스는 월드 스프라이트라 보스 아래에 월드 공간 캔버스를 붙이고, 그리는 순서를 보스보다 하나 낮춘다. 연출이 끝나면 지운다.
    static Image[] CreateBossAura(SpriteRenderer body, Color tint)
    {
        var go = new GameObject("Devil Curse Aura", typeof(RectTransform), typeof(Canvas));
        go.layer = body.gameObject.layer;
        var rect = (RectTransform)go.transform;
        rect.SetParent(body.transform, false);
        rect.localPosition = Vector3.zero;
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;
        rect.sizeDelta = Vector2.one;
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        canvas.sortingLayerID = body.sortingLayerID;
        canvas.sortingOrder = body.sortingOrder - 1;
        introResets.Add(() => { if (go != null) UnityEngine.Object.Destroy(go); });

        var layers = new Image[AuraLayerReach.Length];
        // 바깥 겹부터 만들어 먼저 그린다 → 바깥 겹이 가장 뒤, 안쪽 겹이 보스 바로 뒤
        for (int i = layers.Length - 1; i >= 0; i--)
        {
            var layerGo = new GameObject("Aura " + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            layerGo.layer = go.layer;
            layerGo.transform.SetParent(rect, false);
            Image image = layerGo.GetComponent<Image>();
            image.raycastTarget = false;
            image.color = new Color(1f, 1f, 1f, 0f);
            var effect = layerGo.AddComponent<UIEffect>();
            effect.colorFilter = ColorFilter.Replace;
            effect.color = tint;
            effect.colorIntensity = 1f;
            layers[i] = image;
        }
        return layers;
    }

    // 오라가 차올랐다가 fadeStart부터 end까지 빠진다. 보스 그림이 애니메이션이라 매 프레임 같은 그림·위치로 맞춘다.
    // grow: 가장 바깥 겹이 보스보다 커지는 비율(제물 소녀 오라 설정과 따로 둔다). alphaScale: 진하기 배율(각성 때 떠오르는 동안 옅게).
    static void InsertBossAura(Sequence seq, Image[] layers, SpriteRenderer body, float rise, float fadeStart, float end, float grow, float alphaScale = 1f)
    {
        if (layers == null || layers.Length == 0) return;
        var p = Profile;
        float peak = p != null ? p.sacrificeAuraAlpha : 0.8f;
        float fade = Mathf.Max(0.01f, end - fadeStart);
        seq.Insert(0f, DOTween.To(() => 0f, time =>
        {
            float up = Mathf.Clamp01(time / rise);
            float down = Mathf.Clamp01((time - fadeStart) / fade);
            float level = up * up * (3f - 2f * up) * (1f - down * down * (3f - 2f * down));
            Sprite sprite = body != null ? body.sprite : null;
            if (sprite == null) return;
            Bounds bounds = sprite.bounds;
            var center = new Vector3(body.flipX ? -bounds.center.x : bounds.center.x, body.flipY ? -bounds.center.y : bounds.center.y, 0f);
            var flip = new Vector3(body.flipX ? -1f : 1f, body.flipY ? -1f : 1f, 1f);
            for (int i = 0; i < layers.Length; i++)
            {
                Image layer = layers[i];
                if (layer == null) continue;
                layer.sprite = sprite;
                RectTransform rect = layer.rectTransform;
                rect.localPosition = center;
                rect.sizeDelta = new Vector2(bounds.size.x, bounds.size.y);
                rect.localScale = flip * (1f + grow * AuraLayerReach[i] * level);
                Color c = layer.color;
                c.a = peak * AuraLayerAlpha[i] * level * alphaScale;
                layer.color = c;
            }
        }, end, end).SetEase(Ease.Linear));
    }

    // 보라 안개 한 줄기: 밝은 핵을 가진 안개가 곡선을 따라 점점 빨라지며 날아가고, 지나간 자리에 옅은 꼬리를 남긴다.
    // 마지막 30% 동안 작아지며 흐려져 대상에 스며든다(제물 연출의 안개와 같은 모양·색).
    // 꼬리는 예전 연기(TintedSmoke)를 그대로 쓴다: 새 연기 애니메이션은 진해서 날아가는 빛을 가린다.
    static void InsertCurseWisp(Sequence seq, RectTransform stage, Vector2 from, Vector2 control, Vector2 to, float launch, float travel, Color core, Color trail)
    {
        var p = Profile;
        seq.InsertCallback(launch, () => TintedSmoke(from, trail, 0f, 2, 56f));
        if (p == null || p.roundGlow == null) return;

        Image wisp = CreateImage("Curse Wisp", p.roundGlow, new Vector2(56f, 56f), stage);
        wisp.color = new Color(core.r, core.g, core.b, 0f);
        wisp.rectTransform.anchoredPosition = from;
        Image wispCore = CreateImage("Curse Wisp Core", p.roundGlow, new Vector2(22f, 22f), stage);
        wispCore.color = new Color(1f, 0.92f, 1f, 0f);
        wispCore.rectTransform.anchoredPosition = from;
        RectTransform wispRect = wisp.rectTransform, coreRect = wispCore.rectTransform;

        seq.Insert(launch, wisp.DOFade(0.95f, 0.08f));
        seq.Insert(launch, wispCore.DOFade(1f, 0.08f));
        seq.Insert(launch, DOTween.To(() => 0f, t =>
        {
            Vector2 point = Bezier(from, control, to, t);
            if (wispRect != null) wispRect.anchoredPosition = point;
            if (coreRect != null) coreRect.anchoredPosition = point;
        }, 1f, travel).SetEase(Ease.InQuad));
        float fadeOut = travel * 0.3f;
        seq.Insert(launch, wispRect.DOScale(0.4f, travel).SetEase(Ease.InQuad));
        seq.Insert(launch + travel - fadeOut, wisp.DOFade(0f, fadeOut).SetEase(Ease.InQuad));
        seq.Insert(launch + travel - fadeOut, wispCore.DOFade(0f, fadeOut).SetEase(Ease.InQuad));

        const int trailCount = 6;
        for (int k = 1; k < trailCount; k++)
        {
            float progress = DOVirtual.EasedValue(0f, 1f, (float)k / trailCount, Ease.InQuad);
            Vector2 point = Bezier(from, control, to, progress);
            seq.InsertCallback(launch + travel * k / trailCount, () => TintedSmoke(point, trail, 0f, 1, 34f));
        }
    }
}
