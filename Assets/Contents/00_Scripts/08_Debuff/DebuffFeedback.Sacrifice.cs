using System;
using System.Collections;
using System.Collections.Generic;
using Coffee.UIEffects;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 디버프 연출(DebuffFeedback의 일부): 조우 '제물이 된 소녀'에게 바친 주사위가 녹아 소녀에게 빨려 들어가는 연출.
// 공용 상태·부품은 DebuffFeedback.cs에 있습니다.
public static partial class DebuffFeedback
{
    // ---------- 조우: 제물이 된 소녀 ----------

    // 바친 주사위가 화면 아래에서 떠올라 소녀 앞에 멈추고, 끌려가듯 점점 세게 떨리다가 엘리트 등장 때처럼 가장자리부터 보랏빛으로 녹는다.
    // 녹은 자리에서 나온 안개는 바깥으로 살짝 휘었다가 점점 빨라지며, 작아지고 흐려지면서 소녀의 가슴께로 스며든다.
    // 소녀 자체는 건드리지 않고(맞은 것처럼 보이지 않게), 소녀 모양을 따라 뒤에서 보라 오라가 차올랐다가 천천히 빠진다.
    // 덱에서 빼는 일은 호출하는 쪽이 끝냈고 여기서는 보여 주기만 합니다. 화면을 눌러도 건너뛰지 않습니다(클릭은 막음).
    public static IEnumerator PlaySacrifice(IList<Sprite> sprites, IList<Color> tints, Graphic receiver)
    {
        if (sprites == null || sprites.Count == 0) yield break;
        var p = Profile;
        RectTransform root = BeginIntro(skippable: false);
        RectTransform stage = NewContainer("Sacrifice", root);

        Rect area = receiver != null ? UiRectInLayer(receiver.rectTransform) : new Rect(-300f, -200f, 600f, 520f);
        Vector2 heart = area.min + Vector2.Scale(area.size, p != null ? p.sacrificeHeart : new Vector2(0.4f, 0.55f));
        Vector2 size = Vector2.one * (p != null ? p.sacrificeDieSize : 100f);
        float hoverY = area.yMin + area.height * (p != null ? p.sacrificeHoverHeight : 0.22f);
        float startY = Layer.rect.yMin - size.y;
        Color drain = p != null ? p.wispCoreColor : new Color(0.8f, 0.5f, 1f);
        float travel = p != null ? Mathf.Max(0.1f, p.sacrificeTravelTime) : 0.7f; // 안개가 소녀에게 날아가는 시간

        var ctx = new ArrivalContext { seq = NewIntroTween(), stage = stage, deck = heart };
        Sequence seq = ctx.seq;
        Image[] aura = CreateAura(receiver as Image); // NewIntroTween 뒤에 만들어야 정리 목록이 지워지지 않음

        int count = sprites.Count;
        var arrivals = new List<float>();
        for (int i = 0; i < count; i++)
        {
            if (sprites[i] == null) continue;
            Color tint = tints != null && i < tints.Count ? tints[i] : Color.white;
            DieVisual visual = CreateDieVisual("Offered Die", sprites[i], size, stage, DebuffDiceType.None);
            visual.body.color = tint;
            float offset = (i - (count - 1) * 0.5f) * (size.x + 48f);
            Vector2 slot = new Vector2(heart.x + offset, hoverY);
            visual.mover.anchoredPosition = new Vector2(slot.x, startY);
            visual.restPos = slot;

            // 떠오르기: 아래에서 올라와 살짝 넘쳤다가 소녀 앞에 자리를 잡습니다.
            float rise = i * 0.08f;
            seq.Insert(rise, visual.mover.DOAnchorPos(slot, 0.42f).SetEase(Ease.OutBack, 1.1f));

            // 끌려가는 떨림: 녹기 시작할 때까지 점점 세게 떨리고, 보랏빛이 조금씩 스며듭니다.
            float launch = 0.68f + i * 0.16f;
            float shakeFrom = rise + 0.4f;
            RectTransform shaker = visual.scaler;
            seq.Insert(shakeFrom, DOTween.To(() => 0f, t =>
            {
                if (shaker != null) shaker.anchoredPosition = new Vector2(Mathf.Sin(t * Mathf.PI * 14f) * 4f * t, 0f);
            }, 1f, launch + 0.16f - shakeFrom).SetEase(Ease.Linear));
            seq.Insert(shakeFrom, visual.body.DOColor(Color.Lerp(tint, drain, 0.35f), launch - shakeFrom));

            // 녹아 안개가 되어 빨려 들어갑니다. 양옆 주사위는 바깥으로 휘었다가 안으로 감겨 들어 서로 겹치지 않습니다.
            // 안개는 날아가는 길의 마지막 40% 동안 작아지며 흐려져 부딪히지 않고 스며듭니다.
            float side = Mathf.Abs(offset) > 0.01f ? Mathf.Sign(offset) : (i % 2 == 0 ? 1f : -1f);
            Vector2 bend = (slot + heart) * 0.5f + new Vector2(side * 90f, -20f);
            arrivals.Add(InsertDieFly(ctx, visual, launch, bend, Ease.InQuad, travel, 0.4f));
        }

        float end = InsertAura(seq, aura, receiver as Image, arrivals);
        seq.InsertCallback(end + 0.1f, () => { });

        try { yield return seq.WaitForCompletion(); }
        finally { EndIntro(); }
    }

    // 오라 겹: 소녀 그림보다 얼마나 크게(가장 바깥 겹 배율 대비 비율), 얼마나 진하게. 안쪽은 진하고 바깥으로 갈수록 연하다.
    static readonly float[] AuraLayerReach = { 0.25f, 0.6f, 1f };
    static readonly float[] AuraLayerAlpha = { 0.75f, 0.45f, 0.22f };

    // 소녀 그림을 보라색 실루엣으로 칠한 복사본을 크기를 조금씩 키워 여러 겹 소녀 뒤에 깐다.
    // 바깥으로 갈수록 연해지는 계단식 오라라 도트 그림과 어울리고, 흐림 효과(무거운 새 셰이더)를 쓰지 않는다.
    // 겹들은 소녀와 같은 그림·같은 머티리얼을 함께 쓰고, 연출이 끝나거나 끊기면 지웁니다. 소녀 그림이 없으면 빈 배열.
    static Image[] CreateAura(Image girl)
    {
        if (girl == null || girl.transform.parent == null) return new Image[0];
        var p = Profile;
        Color tint = p != null ? p.sacrificeTint : new Color(0.72f, 0.45f, 1f);
        RectTransform source = girl.rectTransform;
        var layers = new Image[AuraLayerReach.Length];
        // 바깥 겹부터 만들어 소녀 바로 앞 순서에 끼운다 → 바깥 겹이 가장 뒤, 안쪽 겹이 소녀 바로 뒤에 그려짐
        for (int i = layers.Length - 1; i >= 0; i--)
        {
            var go = new GameObject("Sacrifice Aura " + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.layer = girl.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(source.parent, false);
            rect.SetSiblingIndex(source.GetSiblingIndex());
            rect.anchorMin = source.anchorMin;
            rect.anchorMax = source.anchorMax;
            rect.pivot = source.pivot;
            rect.anchoredPosition = source.anchoredPosition;
            rect.sizeDelta = source.sizeDelta;
            rect.localRotation = source.localRotation;
            rect.localScale = source.localScale;

            Image layer = go.GetComponent<Image>();
            layer.sprite = girl.sprite;
            layer.preserveAspect = girl.preserveAspect;
            layer.raycastTarget = false;
            layer.color = new Color(1f, 1f, 1f, 0f);

            var effect = go.AddComponent<UIEffect>();
            effect.colorFilter = ColorFilter.Replace;
            effect.color = tint;
            effect.colorIntensity = 1f;
            layers[i] = layer;
            introResets.Add(() => { if (go != null) UnityEngine.Object.Destroy(go); });
        }
        return layers;
    }

    // 안개가 스며들 때마다 오라가 한 단계씩 부드럽게 차오르며 바깥으로 퍼지고, 다 받아들인 뒤 잠깐 머물렀다가 천천히 빠진다.
    // 소녀 그림이 애니메이션이라 오라도 매 프레임 같은 그림으로 맞춘다. 오라가 다 빠지는 시각을 돌려줍니다.
    static float InsertAura(Sequence seq, Image[] layers, Image girl, List<float> arrivals)
    {
        float last = 0f;
        foreach (float at in arrivals) last = Mathf.Max(last, at);
        const float rise = 0.4f, hold = 0.45f, fade = 1.1f;
        float fadeStart = last + hold, end = fadeStart + fade;
        if (layers == null || layers.Length == 0) return last;

        var p = Profile;
        float peak = p != null ? p.sacrificeAuraAlpha : 0.8f;
        float grow = (p != null ? p.sacrificeAuraScale : 1.08f) - 1f;
        Vector3 baseScale = girl != null ? girl.rectTransform.localScale : layers[0].rectTransform.localScale;
        int count = arrivals.Count;
        seq.Insert(0f, DOTween.To(() => 0f, time =>
        {
            // 단계마다 목표 밝기를 높이고(첫 안개 60% → 마지막 100%), 끝나면 천천히 빠진다.
            float level = 0f;
            for (int k = 0; k < count; k++)
            {
                float target = count == 1 ? 1f : Mathf.Lerp(0.6f, 1f, k / (count - 1f));
                float t = Mathf.Clamp01((time - (arrivals[k] - 0.05f)) / rise);
                level = Mathf.Max(level, target * t * t * (3f - 2f * t));
            }
            float f = Mathf.Clamp01((time - fadeStart) / fade);
            level *= 1f - f * f * (3f - 2f * f);

            for (int i = 0; i < layers.Length; i++)
            {
                Image layer = layers[i];
                if (layer == null) continue;
                if (girl != null) layer.sprite = girl.sprite;
                Color c = layer.color;
                c.a = peak * AuraLayerAlpha[i] * level;
                layer.color = c;
                layer.rectTransform.localScale = baseScale * (1f + grow * AuraLayerReach[i] * level);
            }
        }, end, end).SetEase(Ease.Linear));
        return end;
    }
}
