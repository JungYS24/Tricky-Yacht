using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

// 디버프 연출(DebuffFeedback의 일부): 타락한 악마주사위(2페이즈)의 입.
// 보스 그림을 잠깐 숨기고 같은 자리(보스의 자식)에서 입 시트(프로필 devilMouthFrames)를 재생한 뒤 다시 보스로 돌아온다.
// 입 시트는 타락 아이들과 같은 칸·기준점(바닥 가운데)으로 잘라 두어 자리가 그대로 맞는다.
//  - 영혼 포식(PlayDevilDevour): 입이 열려 가장 크게 벌어진 채 잠깐 기다리고(주사위가 떤다), 킵한 주사위가 점점 작아지며 입 가운데로 날아 들어간다.
//    닿는 순간 콱 다물며(닫혀 찌그러진 장으로 바로 넘어간다) 흔들리고, 천천히 두 번 씹은 뒤 원래 모습으로 돌아온다.
//  - 타락의 낙인(PlayDevilSpit): 입이 열리면 검붉은 연기가 흘러나와 천천히 테이블 가운데(주사위 줄)로 내려가고,
//    그 연기 속에서 디버프 주사위가 나타났다가 다른 디버프 주사위처럼 녹아 안개가 되어 덱으로 스며든다. 입은 연기를 내보낸 뒤 스르르 닫힌다.
// 입 시트는 실제 시간(초)으로 재생한다. 낙인의 연기·주사위는 다른 디버프 등장 연출과 같은 속도(프로필 durationScale)로 재생한다.
// 게임의 난수 추첨 순서를 바꾸지 않도록 UnityEngine.Random을 쓰지 않는다.
public static partial class DebuffFeedback
{
    // 입 시트 구간(26장)
    const int MouthOpenTo = 9;            // 0~9: 입이 열리며 몸이 젖혀진다(9 = 가장 크게 벌어짐)
    const int MouthCloseFrom = 10;        // 10~13: 스르르 닫힌다(뱉은 뒤)
    const int MouthCloseTo = 13;
    const int MouthShutFrame = 14;        // 닫히며 찌그러진 장: 콱 다물 때 바로 넘어간다
    const int MouthChewOpenFrame = 12;    // 씹을 때 가장 벌어지는 장
    const int MouthChewMidFrame = 13;     // 씹을 때 다문 장과 벌어진 장 사이
    const int MouthSettleFrom = 15;       // 15~25: 원래 모습으로 돌아온다
    const int MouthSettleTo = 25;
    const float MouthOpenFrameTime = 0.045f;
    const float MouthCloseFrameTime = 0.06f;
    const float MouthSettleFrameTime = 0.04f;
    // 가장 크게 벌어진 장(9)의 입 가운데(256칸 그림의 픽셀 좌표, 왼쪽 위 기준). 주사위가 들어가는 자리, 연기가 나오는 자리.
    static readonly Vector2 MouthCenterPixel = new Vector2(131f, 155f);

    // 영혼 포식(실제 시간, 초)
    const float DevourOpenHold = 0.15f;   // 입이 다 벌어진 뒤 벌린 채 기다리는 시간(먹힐 주사위가 계속 떤다)
    const float DevourFlyTime = 0.65f;    // 먹힐 주사위가 입까지 날아가는 시간(점점 빨라짐)
    const float DevourEndScale = 0.25f;   // 입에 닿을 때 크기(처음 크기 대비)
    const float DevourShake = 0.04f;      // 먹힐 주사위가 떠는 폭(월드 단위)
    const float DevourBiteHold = 0.2f;    // 콱 다문 뒤 씹기 시작하기 전까지
    const int ChewCount = 2;
    // 한 번 씹기: 다문 장 → 사이 장 → 벌어진 장(잠깐 머묾) → 사이 장 → 다문 장(흔들림, 잠깐 머묾). 한 번에 약 0.48초.
    const float ChewOpenStep = 0.07f;
    const float ChewOpenHold = 0.17f;
    const float ChewCloseStep = 0.06f;
    const float ChewShutHold = 0.18f;

    // 타락의 낙인(기본 속도 기준 시간. 실제 재생은 durationScale배)
    const float SpitMouthHold = 0.45f;    // 연기를 내보내는 동안 입을 벌리고 있는 시간(실제 시간)
    const float SpitSmokeTravel = 0.6f;   // 연기가 입에서 테이블 가운데까지 흘러가는 시간
    const float SpitDieGrow = 0.3f;       // 주사위가 연기 속에서 나타나 다 커지는 시간
    const float SpitDieHold = 0.3f;       // 다 나타난 뒤 덱으로 가기 전까지 머무는 시간
    const float SpitDieStagger = 0.12f;   // 두 번째 주사위가 뒤따르는 간격
    static readonly Color SpitSmokeColor = new Color(0.42f, 0.07f, 0.11f, 0.9f);  // 검붉은 연기
    static readonly Color SpitGlowColor = new Color(1f, 0.42f, 0.28f);            // 연기 앞머리의 용암빛

    // 연출 동안 보스 대신 입 시트를 그리는 그림(각성 연출의 CreateAwakenActor와 같은 방식).
    sealed class DevilMouthRig
    {
        public SpriteRenderer body;
        public SpriteRenderer actor;
        public Sprite[] frames;
        public bool bodyHidden;

        public void Show(int index)
        {
            if (actor != null && index >= 0 && index < frames.Length && frames[index] != null) actor.sprite = frames[index];
        }

        // 가장 크게 벌어진 장 기준 입 가운데의 월드 위치.
        public Vector3 MouthWorld()
        {
            Sprite s = frames[MouthOpenTo];
            Vector2 local = new Vector2(MouthCenterPixel.x - s.pivot.x, s.rect.height - MouthCenterPixel.y - s.pivot.y) / s.pixelsPerUnit;
            Transform t = actor != null ? actor.transform : body.transform;
            return t.TransformPoint(local);
        }

        // 여러 번 불러도 한 번만 되돌린다.
        public void End()
        {
            if (ended) return;
            ended = true;
            if (actor != null) UnityEngine.Object.Destroy(actor.gameObject);
            actor = null;
            if (body != null) body.forceRenderingOff = bodyHidden;
            if (activeMouth == this) activeMouth = null;
        }

        bool ended;
    }

    // 지금 보스 대신 그리고 있는 입(한 번에 하나). 새 입을 시작하면 남은 입을 먼저 끝내 보스 그림 숨김 상태가 꼬이지 않는다.
    static DevilMouthRig activeMouth;

    // 입 시트가 없거나 보스가 없으면 null(호출하는 쪽이 연출 없이 처리한다).
    static DevilMouthRig BeginDevilMouth(Enemy boss)
    {
        var p = Profile;
        SpriteRenderer body = boss != null ? boss.monsterImage : null;
        Sprite[] frames = p != null ? p.devilMouthFrames : null;
        if (body == null || !HasFrames(frames, MouthSettleTo + 1)) return null;
        activeMouth?.End();
        var rig = new DevilMouthRig { body = body, frames = frames, bodyHidden = body.forceRenderingOff };
        rig.actor = CreateAwakenActor(body, frames[0]);
        body.forceRenderingOff = true;
        activeMouth = rig;
        return rig;
    }

    static IEnumerator PlayMouthFrames(DevilMouthRig rig, int from, int to, float frameTime)
    {
        for (int i = from; i <= to; i++)
        {
            rig.Show(i);
            yield return WaitReal(frameTime);
        }
    }

    static Vector3 Bezier3(Vector3 a, Vector3 b, Vector3 c, float t)
    {
        float u = 1f - t;
        return u * u * a + 2f * u * t * b + t * t * c;
    }

    // 영혼 포식. swallowed는 주사위가 입에 닿는 순간 한 번 부른다(덱에서 지우기·토스트·주사위 파괴 피규어).
    // 부르기 직전에 주사위 위치·크기를 원래대로 되돌려 두므로, 같은 프레임에 풀로 돌려보내도 줄어든 크기가 남지 않는다.
    // 입 시트가 없으면 연출 없이 바로 부른다. 연출이 끊겨도 반드시 한 번 부른다.
    public static IEnumerator PlayDevilDevour(Enemy boss, Dice die, Action swallowed)
    {
        bool done = false;
        Transform dieT = die != null ? die.transform : null;
        Vector3 start = dieT != null ? dieT.position : Vector3.zero;
        Vector3 scale = dieT != null ? dieT.localScale : Vector3.one;
        void Swallow()
        {
            if (done) return;
            done = true;
            if (dieT != null) { dieT.position = start; dieT.localScale = scale; }
            swallowed?.Invoke();
        }

        DevilMouthRig rig = dieT != null ? BeginDevilMouth(boss) : null;
        if (rig == null)
        {
            Swallow();
            yield break;
        }

        try
        {
            // 1) 입이 열린다(몸이 젖혀지며). 다 벌어진 뒤에도 잠깐 벌린 채 기다린다. 그동안 먹힐 주사위가 떤다.
            float openTime = (MouthOpenTo + 1) * MouthOpenFrameTime;
            float waitTime = openTime + DevourOpenHold;
            float time = 0f;
            while (time < waitTime)
            {
                time += Time.unscaledDeltaTime;
                rig.Show(Mathf.Min(MouthOpenTo, Mathf.FloorToInt(time / MouthOpenFrameTime)));
                if (dieT != null) dieT.position = start + Vector3.right * Mathf.Sin(time * 60f) * DevourShake;
                yield return null;
            }
            rig.Show(MouthOpenTo);

            // 2) 가장 크게 벌린 채, 주사위가 위로 휘며 점점 빨라지고 점점 작아지며 입 가운데로 날아간다.
            Vector3 end = rig.MouthWorld();
            Vector3 control = (start + end) * 0.5f + Vector3.up * Mathf.Max(0.5f, Vector3.Distance(start, end) * 0.25f);
            time = 0f;
            while (time < DevourFlyTime && dieT != null)
            {
                time += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(time / DevourFlyTime);
                float e = k * k;
                dieT.position = Bezier3(start, control, end, e);
                dieT.localScale = scale * Mathf.Lerp(1f, DevourEndScale, e);
                yield return null;
            }

            // 3) 콱: 닫히며 찌그러진 장으로 바로 넘어가고 흔들린다. 주사위는 이 순간 사라진다.
            Swallow();
            rig.Show(MouthShutFrame);
            CameraShake.Instance?.ShakeImpact(1.2f);
            yield return WaitReal(DevourBiteHold);

            // 4) 천천히 두 번 씹는다. 한 장씩 벌렸다가 벌린 채 잠깐 머물고, 다시 한 장씩 다물며 다물 때마다 살짝 흔들린다.
            for (int i = 0; i < ChewCount; i++)
            {
                rig.Show(MouthChewMidFrame);
                yield return WaitReal(ChewOpenStep);
                rig.Show(MouthChewOpenFrame);
                yield return WaitReal(ChewOpenHold);
                rig.Show(MouthChewMidFrame);
                yield return WaitReal(ChewCloseStep);
                rig.Show(MouthShutFrame);
                CameraShake.Instance?.ShakePixels(2f, 0.1f);
                yield return WaitReal(ChewShutHold);
            }

            // 5) 원래 모습으로 돌아온다.
            yield return PlayMouthFrames(rig, MouthSettleFrom, MouthSettleTo, MouthSettleFrameTime);
        }
        finally
        {
            Swallow();
            rig.End();
        }
    }

    // 타락의 낙인: 입이 열리면 검붉은 연기가 흘러나와 천천히 테이블 가운데(rollRow 주사위 줄, 다른 디버프 주사위가 나타나는 자리)로 내려간다.
    // 연기가 닿으면 뭉게뭉게 피어오르고 그 속에서 디버프 주사위가 나타났다가, 다른 디버프 주사위처럼 녹아 안개가 되어 덱(deckTarget)으로 스며든다.
    // 주사위가 덱에 닿을 때마다 덱 버튼이 반응하고 왼쪽 위 디버프 표시가 하나씩 올라간다. 입은 연기를 내보낸 뒤 스르르 닫힌다.
    // 실제로 덱에 넣는 일은 호출하는 쪽이 이 연출이 끝난 뒤 한다. 입 시트가 없으면 보스 가운데에서 연기가 나온다.
    public static IEnumerator PlayDevilSpit(Enemy boss, List<DiceData1> dice, Transform deckTarget, Transform rollRow)
    {
        if (dice == null || dice.Count == 0) yield break;
        DevilMouthRig rig = BeginDevilMouth(boss);
        RectTransform stage = NewContainer("Devil Spit", Layer);
        ArrivalContext ctx = null;
        try
        {
            // 1) 입이 열린다.
            if (rig != null) yield return PlayMouthFrames(rig, 0, MouthOpenTo, MouthOpenFrameTime);

            // 2) 연기가 입에서 테이블 가운데로 흘러가고, 거기서 주사위가 나타나 덱으로 들어간다(시퀀스가 맡는다).
            Vector3 mouth = rig != null ? rig.MouthWorld()
                : (boss != null && boss.monsterImage != null ? boss.monsterImage.bounds.center : Vector3.zero);
            ctx = new ArrivalContext
            {
                seq = NewSequence(stage.gameObject),
                stage = stage,
                row = ArrivalRow(rollRow, boss),
                deck = UiToLocal(deckTarget, new Vector2(0.08f, 0.15f)),
                deckTarget = deckTarget
            };
            RememberDeck(ctx);
            float land = InsertSpitSmoke(ctx, WorldToLocal(mouth), ctx.row);
            InsertSpitDice(ctx, dice, land);

            // 3) 연기를 다 내보낸 뒤 입이 스르르 닫히고 원래 모습으로 돌아온다(그동안 연기·주사위는 계속 진행된다).
            if (rig != null)
            {
                yield return WaitReal(SpitMouthHold);
                yield return PlayMouthFrames(rig, MouthCloseFrom, MouthCloseTo, MouthCloseFrameTime);
                yield return PlayMouthFrames(rig, MouthSettleFrom, MouthSettleTo, MouthSettleFrameTime);
                rig.End();
                rig = null;
            }
            if (ctx.seq.IsActive()) yield return ctx.seq.WaitForCompletion();
        }
        finally
        {
            rig?.End();
            if (ctx != null)
            {
                ctx.seq?.Kill(false);
                RestoreDeck(ctx);
            }
            if (stage != null) UnityEngine.Object.Destroy(stage.gameObject);
        }
    }

    // 입에서 테이블까지 흘러가는 연기 한 줄기. 앞머리는 흐린 용암빛이고, 지나간 자리마다 검붉은 연기가 피어올라 길게 이어진다.
    // 옆으로 살짝 휘며 천천히 출발했다가 천천히 멈춘다. 테이블에 닿는 시각을 돌려준다.
    static float InsertSpitSmoke(ArrivalContext ctx, Vector2 from, Vector2 to)
    {
        var p = Profile;
        Sequence seq = ctx.seq;
        Color smoke = SpitSmokeColor;
        const float launch = 0.05f;
        float land = launch + SpitSmokeTravel;

        // 옆으로 살짝 부푼 곡선(직선으로 떨어지지 않고 흘러내리는 느낌)
        Vector2 dir = to - from;
        Vector2 side = new Vector2(-dir.y, dir.x).normalized;
        if (side.x < 0f) side = -side;
        Vector2 control = (from + to) * 0.5f + side * dir.magnitude * 0.18f;

        // 입에서 연기가 몇 번 뿜어져 나온다.
        seq.InsertCallback(0f, () =>
        {
            Smoke(from, smoke, 0f, 3, 64f);
            CameraShake.Instance?.ShakePixels(2f, 0.12f);
        });
        seq.InsertCallback(0.12f, () => Smoke(from, smoke, 0f, 2, 52f));
        seq.InsertCallback(0.24f, () => Smoke(from, smoke, 0f, 1, 44f));

        // 앞머리: 흐린 용암빛이 곡선을 따라 내려가며 조금씩 커진다.
        if (p != null && p.roundGlow != null)
        {
            Image head = CreateImage("Spit Smoke Head", p.roundGlow, new Vector2(80f, 80f), ctx.stage);
            head.color = new Color(SpitGlowColor.r, SpitGlowColor.g, SpitGlowColor.b, 0f);
            RectTransform headRect = head.rectTransform;
            headRect.anchoredPosition = from;
            headRect.localScale = Vector3.one * 0.7f;
            seq.Insert(launch, head.DOFade(0.55f, 0.12f));
            seq.Insert(launch, DOTween.To(() => 0f, t => { if (headRect != null) headRect.anchoredPosition = Bezier(from, control, to, t); }, 1f, SpitSmokeTravel).SetEase(Ease.InOutSine));
            seq.Insert(launch, headRect.DOScale(1.3f, SpitSmokeTravel).SetEase(Ease.OutQuad));
            seq.Insert(land - 0.05f, head.DOFade(0f, 0.2f).SetEase(Ease.InQuad));
        }

        // 꼬리: 같은 곡선·같은 빠르기로 촘촘하게 연기를 남긴다.
        const int trailCount = 12;
        for (int k = 1; k <= trailCount; k++)
        {
            float progress = DOVirtual.EasedValue(0f, 1f, (float)k / trailCount, Ease.InOutSine);
            Vector2 point = Bezier(from, control, to, progress);
            float size = k % 2 == 0 ? 56f : 44f;
            seq.InsertCallback(launch + SpitSmokeTravel * k / trailCount, () => Smoke(point, smoke, 0f, 2, size));
        }

        // 테이블에 닿으면 뭉게뭉게 피어오른다.
        seq.InsertCallback(land, () =>
        {
            Smoke(to, smoke, 0f, 6, 120f);
            Smoke(to, SpitGlowColor, 0.06f, 2, 70f);
        });
        return land;
    }

    // 테이블 가운데 연기 속에서 주사위가 어두운 그림자에서 밝아지며 커져 나타나고, 잠깐 머문 뒤 녹아 안개가 되어 덱으로 간다.
    static void InsertSpitDice(ArrivalContext ctx, List<DiceData1> dice, float land)
    {
        var p = Profile;
        Sequence seq = ctx.seq;
        Vector2 size = PortalDieSize;
        float emphasis = p != null ? p.emphasisScale : 1.1f;
        int count = dice.Count;
        for (int i = 0; i < count; i++)
        {
            DiceData1 data = dice[i];
            DebuffDiceType type = data != null ? data.debuffType : DebuffDiceType.None;
            DieVisual visual = CreateDieVisual("Spit " + type, DieSprite(data), size, ctx.stage, type);
            Vector2 slot = ctx.row + new Vector2((i - (count - 1) * 0.5f) * (size.x * emphasis + 16f), 0f);
            visual.mover.anchoredPosition = slot;
            visual.restPos = slot;
            visual.restScale = emphasis;
            visual.scaler.localScale = Vector3.one * 0.3f;
            visual.body.color = new Color(0.32f, 0.06f, 0.1f, 0f); // 연기 속 검붉은 그림자

            float appear = land + 0.08f + i * SpitDieStagger;
            seq.InsertCallback(appear, () => Smoke(slot, SpitSmokeColor, 0f, 2, 60f));
            seq.Insert(appear, visual.body.DOColor(Color.white, SpitDieGrow).SetEase(Ease.OutQuad));
            seq.Insert(appear, visual.scaler.DOScale(emphasis, SpitDieGrow).SetEase(Ease.OutBack));
            InsertDieFly(ctx, visual, appear + SpitDieGrow + SpitDieHold);
        }
    }

    // 덱 버튼 원래 상태를 기억한다(디버프 도착 반응이 이 값을 기준으로 튄다). 연출이 끝나거나 끊기면 RestoreDeck으로 되돌린다.
    // 전투 시작 연출용 PrepareDeck과 달리 공용 되돌리기 목록에 넣지 않아, 나중의 다른 연출이 지난 값을 덮어쓰지 않는다.
    static void RememberDeck(ArrivalContext ctx)
    {
        Transform deck = ctx.deckTarget;
        if (deck == null) return;
        ctx.deckScale = deck.localScale;
        ctx.deckPosition = deck.localPosition;
        ctx.deckGraphic = deck.GetComponent<Graphic>();
        ctx.deckColor = ctx.deckGraphic != null ? ctx.deckGraphic.color : Color.white;
    }

    static void RestoreDeck(ArrivalContext ctx)
    {
        Transform deck = ctx.deckTarget;
        if (deck == null) return;
        deck.localScale = ctx.deckScale;
        deck.localPosition = ctx.deckPosition;
        if (ctx.deckGraphic != null) ctx.deckGraphic.color = ctx.deckColor;
    }
}
