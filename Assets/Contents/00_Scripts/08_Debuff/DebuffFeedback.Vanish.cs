using System;
using System.Collections;
using System.Collections.Generic;
using Coffee.UIEffects;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 디버프 연출(DebuffFeedback의 일부): 전투가 끝날 때 이번 전투 한정 디버프가 왼쪽 위 표시에서 사라지는 연출.
// 공용 상태·부품은 DebuffFeedback.cs에 있습니다.
public static partial class DebuffFeedback
{
    // ---------- 전투 종료: 이번 전투 한정 디버프 소멸 ----------

    // 이번 전투 한정 디버프가 왼쪽 위 디버프 표시에서 튀어나와 연기처럼 사라집니다(표시가 없으면 덱 버튼에서).
    // 종류별로 한 번씩 보여 주고, 보상 창이 뜨기 전에 끝나도록 짧게 재생합니다. 호출한 뒤 디버프 표시를 새로 고치면 됩니다.
    public static void PlayTemporaryVanish(Transform deckTarget, List<DiceData1> dice)
    {
        if (dice == null || dice.Count == 0) return;
        var p = Profile;
        var types = new List<DebuffDiceType>();
        var sprites = new List<Sprite>();
        foreach (var die in dice)
        {
            if (die == null || types.Contains(die.debuffType)) continue;
            types.Add(die.debuffType);
            sprites.Add(DieSprite(die));
        }
        Sequence seq = NewSequence(Layer.gameObject);
        bool punchDeck = false;
        for (int i = 0; i < types.Count; i++)
        {
            Transform origin = DebuffTrackerHud.IconTransform(types[i]);
            bool fromHud = origin != null;
            if (!fromHud)
            {
                origin = deckTarget;
                punchDeck = true;
            }
            Vector2 start = UiToLocal(origin, new Vector2(0.08f, 0.15f));
            // 왼쪽 위 표시에서는 오른쪽으로, 덱 버튼에서는 위로 빠져나와 사라집니다.
            Vector2 drift = fromHud ? new Vector2(80f, 0f) : new Vector2((i - (types.Count - 1) * 0.5f) * 80f, 100f);
            Vector2 size = fromHud && p != null ? Vector2.one * p.trackerIconSize * 0.8f : EliteIconSize * 0.75f;
            Image icon = CreateImage("Vanishing Debuff", sprites[i], size, Layer);
            RectTransform rect = icon.rectTransform;
            rect.anchoredPosition = start;
            rect.localScale = Vector3.zero;
            float at = i * 0.1f;
            Vector2 end = start + drift;
            seq.Insert(at, rect.DOScale(1f, 0.22f).SetEase(Ease.OutBack));
            seq.Insert(at, rect.DOAnchorPos(end, 0.26f).SetEase(Ease.OutQuad));
            float vanish = at + 0.42f;
            if (p != null) seq.InsertCallback(vanish, () => Smoke(end, p.vanishSmoke, 0f, 3, 60f));
            seq.Insert(vanish, rect.DOScale(0.2f, 0.2f).SetEase(Ease.InBack));
            seq.Insert(vanish, icon.DOFade(0f, 0.2f));
            seq.InsertCallback(vanish + 0.22f, () => { if (icon != null) UnityEngine.Object.Destroy(icon.gameObject); });
        }
        if (punchDeck && deckTarget != null) seq.Insert(0f, deckTarget.DOPunchScale(Vector3.one * 0.1f, 0.2f, 1, 0.5f));
    }
}
