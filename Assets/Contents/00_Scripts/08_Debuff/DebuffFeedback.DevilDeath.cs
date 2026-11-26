using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

// 디버프 연출(DebuffFeedback의 일부): 타락한 악마주사위(2페이즈) 처치. 실제 시간 약 3.3초.
// 보스 그림을 숨기고 같은 자리(보스의 자식)에서 처치 시트(프로필 devilDeathFrames)를 재생한다. 시트 칸은 보스 칸(256)보다 넓게
// (456×256) 잘라 흩어지는 조각이 잘리지 않고, 기준점(바닥 가운데)이 보스와 같아 자리가 그대로 맞는다.
//  1) 멈칫: 마지막 타격에 뒤로 밀렸다가 돌아오면(Enemy가 이 타격에도 밀림을 켜고 색을 바꾸지 않는다) 첫 장에서 잠깐 멈춘다.
//  2) 놀라 움찔(눈 질끈) → 눈을 부릅뜨고 부르르 떤다(약한 흔들림).
//  3) 금이 가며 빛줄기가 새어 나온다(흔들림이 점점 세짐).
//  4) 폭발: 흰 번쩍임·강한 흔들림. 이번 전투에서 영혼 포식으로 삼킨 주사위들이 몸에서 사방으로 튀어나왔다가 떨어지며 사라진다
//     (연출일 뿐 덱으로 돌아오지는 않는다).
//  5) 조각이 흩어지고, 마지막 몇 장은 단계적으로 흐려져 사라진다.
// 처치 시트가 없으면 HasDevilDeathFrames가 false라 호출하는 쪽(Enemy)이 예전 파괴 애니메이션을 쓴다.
// 게임의 난수 추첨 순서를 바꾸지 않도록 UnityEngine.Random을 쓰지 않는다.
public static partial class DebuffFeedback
{
    // 처치 시트 구간(36장)
    const int DeathFlinchFrom = 1, DeathFlinchTo = 4;      // 1~2 움찔, 3~4 눈 질끈
    const int DeathShockFrom = 5, DeathShockTo = 11;       // 눈을 부릅뜨고 떪
    const int DeathCrackFrom = 12, DeathCrackTo = 17;      // 금이 가며 빛줄기가 커짐
    const int DeathBurstFrom = 18, DeathBurstTo = 23;      // 폭발 섬광 → 깨지기 시작
    const int DeathScatterFrom = 24, DeathScatterTo = 35;  // 조각이 흩어짐
    const float DeathHitStop = 0.2f;      // 마지막 타격에 밀렸다가 돌아온 뒤 첫 장에서 멈추는 시간
    const float DeathFlinchFrameTime = 0.08f;
    const float DeathShockFrameTime = 0.07f;
    const float DeathCrackFrameTime = 0.09f;
    const float DeathBurstFrameTime = 0.07f;
    const float DeathScatterFrameTime = 0.08f;
    const int DeathFadeFrames = 4;        // 마지막 이만큼은 단계적으로 흐려진다
    const int DeathDiceMax = 8;           // 튀어나오는 삼킨 주사위 최대 개수
    const float DeathDiceTime = 0.95f;    // 튀어나온 주사위가 떨어지며 사라지기까지
    static readonly float[] DeathFlashSteps = { 0.65f, 0.35f, 0.12f, 0f }; // 폭발 장 다음 장들의 흰 번쩍임 세기

    public static bool HasDevilDeathFrames(Enemy boss)
    {
        var p = Profile;
        return boss != null && boss.monsterImage != null && p != null && HasFrames(p.devilDeathFrames, DeathScatterTo + 1);
    }

    public static IEnumerator PlayDevilDeath(Enemy boss)
    {
        var p = Profile;
        SpriteRenderer body = boss != null ? boss.monsterImage : null;
        Sprite[] frames = p != null ? p.devilDeathFrames : null;
        if (body == null || !HasFrames(frames, DeathScatterTo + 1)) yield break;

        bool bodyHidden = body.forceRenderingOff;
        SpriteRenderer actor = CreateAwakenActor(body, frames[0]);
        // 체력에 따라 붉게 물든 색 그대로 시작해(마지막 타격도 색을 바꾸지 않는다) 움찔하는 동안 원래 색으로 돌아온다.
        Color hitTint = body.color;
        actor.color = hitTint;
        body.forceRenderingOff = true;
        RectTransform stage = NewContainer("Devil Death", Layer);
        try
        {
            // 1) 멈칫(마지막 타격에 뒤로 밀렸다가 돌아온 직후)
            yield return WaitReal(DeathHitStop);

            // 2) 놀라 움찔(눈 질끈) → 눈을 부릅뜨고 부르르 떤다.
            CameraShake.Instance?.ShakePixels(2f, 0.12f);
            for (int i = DeathFlinchFrom; i <= DeathFlinchTo; i++)
            {
                actor.sprite = frames[i];
                actor.color = Color.Lerp(hitTint, Color.white, (float)(i - DeathFlinchFrom + 1) / (DeathFlinchTo - DeathFlinchFrom + 1));
                yield return WaitReal(DeathFlinchFrameTime);
            }
            actor.color = Color.white;
            CameraShake.Instance?.ShakePixels(1.5f, (DeathShockTo - DeathShockFrom + 1) * DeathShockFrameTime);
            yield return PlayActorFrames(actor, frames, DeathShockFrom, DeathShockTo, DeathShockFrameTime);

            // 3) 금이 가며 빛줄기가 새어 나온다. 흔들림이 점점 세진다.
            for (int i = DeathCrackFrom; i <= DeathCrackTo; i++)
            {
                actor.sprite = frames[i];
                float grow = (float)(i - DeathCrackFrom) / Mathf.Max(1, DeathCrackTo - DeathCrackFrom);
                CameraShake.Instance?.ShakePixels(Mathf.Lerp(2f, 4.5f, grow), DeathCrackFrameTime * 0.9f);
                yield return WaitReal(DeathCrackFrameTime);
            }

            // 4) 폭발: 흰 번쩍임·강한 흔들림, 삼킨 주사위가 튀어나온다. 번쩍임은 다음 장들에서 단계적으로 걷힌다.
            Image flash = CreateImage("Death Flash", null, Vector2.zero, stage);
            Stretch(flash.rectTransform);
            flash.color = Color.white;
            CameraShake.Instance?.ShakeImpact(1.35f);
            BurstDevouredDice(body, stage);
            for (int i = DeathBurstFrom; i <= DeathBurstTo; i++)
            {
                actor.sprite = frames[i];
                int step = i - DeathBurstFrom - 1;
                if (step >= 0 && step < DeathFlashSteps.Length && flash != null) flash.color = new Color(1f, 1f, 1f, DeathFlashSteps[step]);
                yield return WaitReal(DeathBurstFrameTime);
            }
            if (flash != null) flash.color = Color.clear;

            // 5) 조각이 흩어지고, 마지막 몇 장은 단계적으로 흐려진다.
            for (int i = DeathScatterFrom; i <= DeathScatterTo; i++)
            {
                actor.sprite = frames[i];
                int left = DeathScatterTo - i; // 남은 장 수
                if (left < DeathFadeFrames) actor.color = new Color(1f, 1f, 1f, (float)(left + 1) / (DeathFadeFrames + 1));
                yield return WaitReal(DeathScatterFrameTime);
            }
            actor.color = Color.clear;
        }
        finally
        {
            if (actor != null) Object.Destroy(actor.gameObject);
            if (body != null) body.forceRenderingOff = bodyHidden;
            if (stage != null) Object.Destroy(stage.gameObject);
        }
    }

    static IEnumerator PlayActorFrames(SpriteRenderer actor, Sprite[] frames, int from, int to, float frameTime)
    {
        for (int i = from; i <= to; i++)
        {
            if (actor != null) actor.sprite = frames[i];
            yield return WaitReal(frameTime);
        }
    }

    // 이번 전투에서 영혼 포식으로 삼킨 주사위들이 보스 몸 가운데에서 위쪽 부채꼴로 튀어나왔다가 떨어지며 단계적으로 사라진다.
    // 그림은 판 위 모습 그대로(DiceManager.DeckDiePreview). 많으면 최근 것부터 DeathDiceMax개만.
    static void BurstDevouredDice(SpriteRenderer body, RectTransform stage)
    {
        IReadOnlyList<DiceData1> eaten = DevilDiceBoss.Devoured;
        DiceManager dm = DiceManager.Instance;
        if (eaten == null || eaten.Count == 0 || dm == null || body == null) return;

        Vector2 center = WorldToLocal(body.bounds.center);
        Vector2 size = PortalDieSize;
        int count = Mathf.Min(DeathDiceMax, eaten.Count);
        const float gravity = 1500f; // 오버레이 단위/초²
        for (int i = 0; i < count; i++)
        {
            DiceData1 data = eaten[eaten.Count - 1 - i];
            Sprite sprite = dm.DeckDiePreview(data, out Color tint);
            if (sprite == null) continue;
            Image die = CreateImage("Devoured Die", sprite, size, stage);
            die.color = tint;
            RectTransform rect = die.rectTransform;
            rect.anchoredPosition = center;
            rect.localScale = Vector3.one * 0.3f;

            // 위쪽 부채꼴(30°~150°)로 고르게, 번갈아 조금 더 세게 튄다.
            float angle = count > 1 ? Mathf.Lerp(30f, 150f, (float)i / (count - 1)) : 90f;
            Vector2 dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            float speed = i % 2 == 0 ? 560f : 680f;
            Color baseColor = tint;
            Sequence seq = DOTween.Sequence().SetUpdate(true).SetLink(die.gameObject);
            seq.Insert(0f, rect.DOScale(1f, 0.15f).SetEase(Ease.OutBack));
            seq.Insert(0f, DOTween.To(() => 0f, t =>
            {
                if (rect == null) return;
                Vector2 pos = center + dir * speed * t + Vector2.down * (0.5f * gravity * t * t);
                rect.anchoredPosition = new Vector2(Mathf.Round(pos.x), Mathf.Round(pos.y));
            }, DeathDiceTime, DeathDiceTime).SetEase(Ease.Linear));
            seq.InsertCallback(DeathDiceTime - 0.3f, () => { if (die != null) die.color = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * 0.66f); });
            seq.InsertCallback(DeathDiceTime - 0.15f, () => { if (die != null) die.color = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * 0.33f); });
            seq.OnComplete(() => { if (die != null) Object.Destroy(die.gameObject); });
        }
    }
}
