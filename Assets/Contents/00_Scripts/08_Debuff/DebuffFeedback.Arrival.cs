using System;
using System.Collections;
using System.Collections.Generic;
using Coffee.UIEffects;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 디버프 연출(DebuffFeedback의 일부): 전투 시작 때 이번 전투 디버프 주사위가 등장해 덱으로 들어가는 연출(엘리트·차원문·보스)과 첫 드로우.
// 공용 상태·부품은 DebuffFeedback.cs에 있습니다.
public static partial class DebuffFeedback
{
    // ---------- 전투 시작: 디버프 주사위 등장 ----------

    // 이번 전투에서 새로 들어오는 디버프만 보여 준다. 엘리트(도둑·족쇄), 차원문(가시·저주), 보스(가짜)를 종류별로 연출한다.
    // 모든 등장 연출은 주사위가 굴러가는 줄(등장 중에는 비어 있음)에서 재생하고, 주사위는 마지막에 덱으로 들어갑니다.
    public static IEnumerator PlayArrival(Enemy enemy, List<DiceData1> incoming, Transform deckTarget, Transform rollRow, Sprite normalFace)
    {
        if (incoming == null || incoming.Count == 0 || enemy == null) yield break;
        var p = Profile;
        RectTransform root = BeginIntro();
        Image dim = p == null || p.dimScreen ? CreateDim(root) : null; // 등장 아이콘보다 먼저 만들어 그 아래에 깝니다.
        RectTransform stage = NewContainer("Arrival", root);

        var elite = new List<DiceData1>();
        var portal = new List<DiceData1>();
        var boss = new List<DiceData1>();
        foreach (var die in incoming)
        {
            if (die == null) continue;
            if (die.debuffType == DebuffDiceType.Fake) boss.Add(die);
            else if (die.debuffType == DebuffDiceType.Spine || die.debuffType == DebuffDiceType.Curse) portal.Add(die);
            else elite.Add(die);
        }

        var ctx = new ArrivalContext
        {
            seq = NewIntroTween(),
            stage = stage,
            row = ArrivalRow(rollRow, enemy),
            deck = UiToLocal(deckTarget, new Vector2(0.08f, 0.15f)),
            deckTarget = deckTarget
        };
        PrepareDeck(ctx);
        if (p == null || p.pauseMonster) PauseMonster(enemy);

        Transform monster = enemy.transform;
        if (boss.Count > 0) InsertMonsterShake(ctx.seq, monster, enemy.monsterImage, 0f);
        else InsertMonsterSquash(ctx.seq, monster, 0f);

        float end = 0.1f;
        if (elite.Count > 0) end = InsertElite(ctx, elite, end);
        if (portal.Count > 0) end = InsertPortal(ctx, portal, Mathf.Max(0.1f, end - 0.3f));
        if (boss.Count > 0) end = InsertBoss(ctx, boss, normalFace, Mathf.Max(0.1f, end - 0.3f));

        if (dim != null)
        {
            float alpha = p != null ? p.dimAlpha : 0.35f;
            ctx.seq.Insert(0f, dim.DOFade(alpha, 0.2f));
            // 주사위가 덱으로 출발할 때 걷어 내 덱 버튼 반응이 어둡게 가려지지 않게 합니다.
            ctx.seq.Insert(Mathf.Min(ctx.firstLaunch, end), dim.DOFade(0f, 0.25f));
        }

        try { yield return ctx.seq.WaitForCompletion(); }
        finally
        {
            KillIntroTween();
            if (stage != null) UnityEngine.Object.Destroy(stage.gameObject);
            if (dim != null) UnityEngine.Object.Destroy(dim.gameObject);
        }
    }

    // 첫 손패가 덱 버튼에서 튀어나와 제자리로 간다. 주사위 크기는 원래 크기로 돌아옵니다.
    public static IEnumerator PlayFirstDraw(List<Dice> dice, Transform deckTarget)
    {
        Camera camera = Camera.main;
        if (camera == null || dice == null || dice.Count == 0) yield break;
        BeginIntro();
        Vector2 screen = UiScreenPoint(deckTarget, new Vector2(0.08f, 0.15f));
        Sequence seq = NewIntroTween();
        for (int i = 0; i < dice.Count; i++)
        {
            if (dice[i] == null) continue;
            Transform die = dice[i].transform;
            Vector3 position = die.position;
            Vector3 scale = die.localScale;
            introResets.Add(() => { if (die != null) { die.position = position; die.localScale = scale; } });
            float depth = camera.WorldToScreenPoint(position).z;
            die.position = camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, depth));
            die.localScale = Vector3.zero;
            seq.Insert(i * 0.08f, die.DOMove(position, 0.32f).SetEase(Ease.OutCubic));
            seq.Insert(i * 0.08f, die.DOScale(scale, 0.32f).SetEase(Ease.OutBack));
        }
        try { yield return seq.WaitForCompletion(); }
        finally { KillIntroTween(); }
    }

    // 도둑·족쇄 아이콘이 주사위 줄에 한 줄로 나타났다가 하나씩 덱으로 들어간다.
    static float InsertElite(ArrivalContext ctx, List<DiceData1> dice, float start)
    {
        var p = Profile;
        Vector2 size = EliteIconSize;
        int count = dice.Count;
        float launch = start + 0.65f;
        float end = start;
        for (int i = 0; i < count; i++)
        {
            DieVisual visual = CreateDieVisual("Incoming " + dice[i].debuffType, DieSprite(dice[i]), size, ctx.stage, dice[i].debuffType);
            Vector2 slot = ctx.row + new Vector2((i - (count - 1) * 0.5f) * (size.x + 16f), 0f);
            visual.mover.anchoredPosition = slot;
            visual.restPos = slot;
            visual.scaler.localScale = Vector3.zero;
            float appear = start + i * 0.1f;
            ctx.seq.Insert(appear, visual.scaler.DOScale(1f, 0.22f).SetEase(Ease.OutBack));
            if (visual.glow != null) ctx.seq.Insert(appear, visual.glow.DOFade(GlowAlpha, 0.22f));
            if (p != null)
            {
                Color smoke = dice[i].debuffType == DebuffDiceType.Thief ? p.thiefSmoke : p.vanishSmoke;
                ctx.seq.InsertCallback(appear, () => Smoke(slot, smoke, 0f, 2, size.x * 0.55f));
            }
            end = Mathf.Max(end, InsertDieFly(ctx, visual, launch + i * 0.15f));
        }
        return end + 0.2f;
    }

    // 판 위에 보라색 균열이 가는 선에서 벌어지며 열리고, 주사위가 그 안에서 위로 튕겨 나와 앞쪽 판에 떨어진다.
    // 잠깐 크게 보여 주는 동안 균열은 다시 선으로 좁아지며 닫히고, 주사위는 작아지며 덱으로 들어간다.
    // 그리는 순서: 보랏빛 번짐 → 균열 → 그림자 → 주사위(균열에서 솟아오르므로 항상 균열보다 위).
    static float InsertPortal(ArrivalContext ctx, List<DiceData1> dice, float start)
    {
        var p = Profile;
        if (p == null || p.portalRift == null) return InsertElite(ctx, dice, start);
        Sequence seq = ctx.seq;
        RectTransform parent = ctx.stage;

        int pixel = Mathf.Max(1, p.riftPixelScale);
        Vector2 riftSize = p.portalRift.rect.size * pixel;
        Vector2 dieSize = PortalDieSize;
        float emphasis = p.emphasisScale;
        // 균열은 주사위 줄 위쪽 고정 높이에 열고, 착지 지점은 거기서 portalDrop만큼 아래입니다.
        Vector2 riftCenter = ctx.row + Vector2.up * p.riftLift;
        Vector2 closedScale = new Vector2(0.25f, 0f);

        // 판에 번지는 보랏빛
        Image glow = null;
        if (p.riftGlow != null)
        {
            glow = CreateImage("Rift Glow", p.riftGlow, p.riftGlow.rect.size * pixel, parent);
            glow.preserveAspect = false;
            glow.rectTransform.anchoredPosition = riftCenter;
            glow.rectTransform.localScale = new Vector3(0.5f, 0.5f, 1f);
            glow.color = new Color(1f, 1f, 1f, 0f);
            seq.Insert(start, glow.DOFade(1f, 0.25f));
            seq.Insert(start, glow.rectTransform.DOScale(1f, 0.3f).SetEase(Ease.OutQuad));
        }

        // 균열: 가는 선에서 위아래로 벌어지며 열림
        Image rift = CreateImage("Portal Rift", p.portalRift, riftSize, parent);
        rift.preserveAspect = false;
        RectTransform riftRect = rift.rectTransform;
        riftRect.anchoredPosition = riftCenter;
        riftRect.localScale = new Vector3(closedScale.x, closedScale.y, 1f);
        seq.Insert(start + 0.05f, riftRect.DOScale(new Vector3(1.05f, 1.15f, 1f), 0.25f).SetEase(Ease.OutQuad));
        seq.Insert(start + 0.3f, riftRect.DOScale(Vector3.one, 0.1f).SetEase(Ease.InOutQuad));
        // 주사위가 나오기 직전: 균열 위로 빛 조각이 솟으며 예고
        seq.InsertCallback(start + 0.32f, () => RisingSparks(riftCenter, riftSize.x * 0.35f, p.portalSmoke));

        int count = dice.Count;
        var visuals = new DieVisual[count];
        var dieShadows = new Image[count];
        float lastLand = start;
        for (int i = 0; i < count; i++)
        {
            Image dieShadow = CreateShadow("Landing Shadow", dieSize.x * 0.9f, parent);
            DieVisual visual = CreateDieVisual("Incoming " + dice[i].debuffType, DieSprite(dice[i]), dieSize, parent, dice[i].debuffType, p.exitFlash);
            RectTransform mover = visual.mover;
            float spread = (i - (count - 1) * 0.5f) * (dieSize.x * emphasis + 16f);
            Vector2 from = riftCenter + Vector2.right * spread * 0.3f;
            Vector2 peak = from + Vector2.up * p.launchHeight;
            Vector2 landed = new Vector2(ctx.row.x + spread, riftCenter.y - p.portalDrop);
            Vector2 shadowEnd = landed + Vector2.down * dieSize.y * emphasis * 0.45f;
            mover.anchoredPosition = from;
            visual.restPos = landed;
            visual.restScale = emphasis;
            visual.scaler.localScale = Vector3.one * 0.3f;
            visual.body.color = new Color(0.35f, 0.22f, 0.5f, 0f); // 균열 속 어두운 보라 실루엣
            if (dieShadow != null)
            {
                dieShadow.rectTransform.anchoredPosition = new Vector2(from.x, riftCenter.y - riftSize.y * 0.2f);
                dieShadow.rectTransform.localScale = new Vector3(0f, 1f, 1f);
            }

            // 1단계: 균열이 움찔 줄었다가 주사위를 위쪽 앞으로 빠르게 튕겨 냄. 솟구치며 커지고 밝아집니다.
            float launch = start + 0.48f + i * 0.14f;
            seq.Insert(launch - 0.08f, riftRect.DOScale(new Vector3(0.92f, 0.85f, 1f), 0.08f));
            seq.Insert(launch, riftRect.DOScale(Vector3.one, 0.14f).SetEase(Ease.OutBack));
            Vector2 sparkAt = riftCenter;
            seq.InsertCallback(launch, () =>
            {
                Sparks(sparkAt, p.portalSmoke, 10, 90f);
                Smoke(sparkAt, p.portalSmoke, 0f, 2, 52f);
            });
            seq.Insert(launch, visual.body.DOColor(Color.white, 0.12f));
            seq.Insert(launch, visual.scaler.DOScale(new Vector3(emphasis * 0.95f, emphasis * 1.06f, 1f), 0.2f).SetEase(Ease.OutQuad));
            seq.Insert(launch, mover.DOAnchorPos(peak, 0.2f).SetEase(Ease.OutCubic));
            if (visual.flash != null)
            {
                seq.Insert(launch, visual.flash.DOFade(0.9f, 0.04f));
                seq.Insert(launch + 0.06f, visual.flash.DOFade(0f, 0.18f));
            }
            if (visual.glow != null) seq.Insert(launch, visual.glow.DOFade(GlowAlpha, 0.2f));

            // 2단계: 점점 빨라지며 앞쪽 판 위로 떨어짐. 떨어지는 동안 세로로 살짝 늘어나고, 그림자는 판 위를 따라옵니다.
            float fall = launch + 0.2f;
            seq.Insert(fall, mover.DOAnchorPos(landed, 0.26f).SetEase(Ease.InQuad));
            seq.Insert(fall, visual.scaler.DOScale(new Vector3(emphasis * 0.97f, emphasis * 1.03f, 1f), 0.26f));
            if (dieShadow != null)
            {
                seq.Insert(launch, dieShadow.rectTransform.DOAnchorPos(shadowEnd, 0.46f).SetEase(Ease.InOutQuad));
                seq.Insert(launch, dieShadow.rectTransform.DOScaleX(emphasis, 0.46f).SetEase(Ease.InQuad));
            }

            // 착지: 가로로 납작하게 눌렸다 복귀, 먼지, 짧은 흔들림.
            float land = fall + 0.26f;
            seq.Insert(land, visual.scaler.DOScale(new Vector3(emphasis * 1.12f, emphasis * 0.86f, 1f), 0.05f));
            seq.Insert(land + 0.05f, visual.scaler.DOScale(emphasis, 0.1f).SetEase(Ease.OutBack));
            seq.InsertCallback(land, () => Smoke(shadowEnd, p.vanishSmoke, 0f, 2, 40f));
            InsertImpact(ctx, land);
            if (visual.glow != null) seq.Insert(land + 0.15f, visual.glow.DOFade(GlowAlpha * 0.45f, p.confirmHold * 0.5f).SetLoops(2, LoopType.Yoyo));

            visuals[i] = visual;
            dieShadows[i] = dieShadow;
            lastLand = land;
        }

        // 주사위를 보여 주는 동안 균열은 다시 선으로 좁아지며 작고 빠르게 닫힙니다. 시선은 착지한 주사위에 남깁니다.
        float close = lastLand + 0.1f;
        seq.Insert(close, riftRect.DOScale(new Vector3(1.1f, 0f, 1f), 0.18f).SetEase(Ease.InBack));
        if (glow != null) seq.Insert(close, glow.DOFade(0f, 0.22f));
        seq.InsertCallback(close + 0.16f, () => Smoke(riftCenter, p.portalSmoke, 0f, 1, 40f));

        float launchToDeck = lastLand + 0.15f + p.confirmHold;
        float end = launchToDeck;
        for (int i = 0; i < count; i++)
        {
            float at = launchToDeck + i * 0.1f;
            if (dieShadows[i] != null) seq.Insert(at, dieShadows[i].DOFade(0f, 0.1f));
            end = Mathf.Max(end, InsertDieFly(ctx, visuals[i], at));
        }
        return end + 0.2f;
    }

    // 균열 위로 작은 빛 조각이 솟아오르는 예고(난수 없이 고정 간격).
    static void RisingSparks(Vector2 center, float halfWidth, Color color)
    {
        Sequence seq = NewSequence(Layer.gameObject);
        const int count = 7;
        for (int i = 0; i < count; i++)
        {
            Image spark = CreateImage("Rift Spark", null, new Vector2(6f, 6f), Layer);
            spark.color = color;
            RectTransform rect = spark.rectTransform;
            float x = Mathf.Lerp(-halfWidth, halfWidth, (i + 0.5f) / count);
            Vector2 from = center + new Vector2(x, (i % 3 - 1) * 6f);
            rect.anchoredPosition = from;
            float at = (i * 3 % count) * 0.025f;
            spark.gameObject.SetActive(false);
            seq.InsertCallback(at, () => spark.gameObject.SetActive(true));
            seq.Insert(at, rect.DOAnchorPos(from + Vector2.up * (40f + (i % 2) * 20f), 0.4f).SetEase(Ease.OutQuad));
            seq.Insert(at + 0.2f, spark.DOFade(0f, 0.2f));
            seq.InsertCallback(at + 0.42f, () => { if (spark != null) UnityEngine.Object.Destroy(spark.gameObject); });
        }
    }

    // 일반 주사위가 주사위 줄에 나타나 좌우로 두세 번 떨린 뒤, 하얀 연기와 함께 한 번에 가짜 주사위로 바뀐다.
    static float InsertBoss(ArrivalContext ctx, List<DiceData1> dice, Sprite normalFace, float start)
    {
        Sequence seq = ctx.seq;
        Vector2 size = BossIconSize;
        float end = start;
        for (int i = 0; i < dice.Count; i++)
        {
            Sprite fake = DieSprite(dice[i]);
            DieVisual visual = CreateDieVisual("Incoming Fake", fake, size, ctx.stage, dice[i].debuffType);
            if (normalFace != null) visual.body.sprite = normalFace; // 테두리 빛은 가짜 모양으로 미리 만들어 두고 변신 때 켭니다.
            RectTransform mover = visual.mover;
            Vector2 slot = ctx.row + new Vector2((i - (dice.Count - 1) * 0.5f) * (size.x + 16f), 0f);
            mover.anchoredPosition = slot;
            visual.restPos = slot;
            visual.scaler.localScale = Vector3.zero;
            float at = start + i * 0.15f;
            seq.Insert(at, visual.scaler.DOScale(1f, 0.25f).SetEase(Ease.OutBack));

            // 회전 대신 좌우 이동으로만 떨어 픽셀이 깨지지 않게 합니다.
            float amplitude = size.x * 0.07f;
            seq.Insert(at + 0.35f, DOTween.To(() => 0f, t =>
            {
                if (mover != null) mover.anchoredPosition = slot + Vector2.right * Mathf.Sin(t * Mathf.PI * 6f) * amplitude * (1f - t * 0.4f);
            }, 1f, 0.4f).SetEase(Ease.Linear));

            float reveal = at + 0.78f;
            Image body = visual.body;
            seq.InsertCallback(reveal, () =>
            {
                if (mover != null) mover.anchoredPosition = slot;
                if (body != null && fake != null) body.sprite = fake;
                Smoke(slot, Color.white, 0f, 4, size.x * 0.5f);
            });
            seq.Insert(reveal, visual.scaler.DOPunchScale(Vector3.one * 0.25f, 0.3f, 4, 0.6f));
            if (visual.glow != null) seq.Insert(reveal, visual.glow.DOFade(GlowAlpha, 0.15f));
            InsertImpact(ctx, reveal);
            end = Mathf.Max(end, InsertDieFly(ctx, visual, reveal + 0.37f));
        }
        return end + 0.2f;
    }

    // 등장 연출 동안 몬스터 애니메이션을 멈춰 시선이 판 위 소환으로 모이게 합니다. 끝나면 원래 속도로 돌아옵니다.
    static void PauseMonster(Enemy enemy)
    {
        Animator animator = enemy != null ? enemy.monsterAnimator : null;
        if (animator == null) return;
        float speed = animator.speed;
        animator.speed = 0f;
        introResets.Add(() => { if (animator != null) animator.speed = speed; });
    }

    // 착지·변신 순간의 짧은 흔들림. 월드는 기존 카메라 흔들림(난수 없음)을, 등장 아이콘은 같은 박자로 함께 흔듭니다.
    static void InsertImpact(ArrivalContext ctx, float at)
    {
        var p = Profile;
        if (p != null && !p.cameraShake) return;
        ctx.seq.InsertCallback(at, () =>
        {
            if (CameraShake.Instance != null) CameraShake.Instance.ShakeImpact();
        });
        RectTransform stage = ctx.stage;
        ctx.seq.Insert(at, DOTween.To(() => 0f, t =>
        {
            if (stage == null) return;
            float damping = (1f - t) * (1f - t);
            stage.anchoredPosition = new Vector2(Mathf.Sin(t * Mathf.PI * 4f) * 6f, Mathf.Sin(t * Mathf.PI * 2f) * 2.4f) * damping;
        }, 1f, 0.2f).SetEase(Ease.Linear));
    }

    // 덱 버튼 원래 상태를 기억해 두고, 연출이 끝나거나 끊기면 되돌립니다.
    static void PrepareDeck(ArrivalContext ctx)
    {
        Transform deck = ctx.deckTarget;
        if (deck == null) return;
        Vector3 scale = deck.localScale;
        Vector3 position = deck.localPosition;
        Graphic graphic = deck.GetComponent<Graphic>();
        Color color = graphic != null ? graphic.color : Color.white;
        ctx.deckGraphic = graphic;
        ctx.deckColor = color;
        ctx.deckScale = scale;
        ctx.deckPosition = position;
        introResets.Add(() =>
        {
            if (deck != null) { deck.localScale = scale; deck.localPosition = position; }
            if (graphic != null) graphic.color = color;
        });
    }

    // 디버프 주사위가 덱에 들어가는 순간: 덱 버튼이 튕기며 좌우로 흔들리고, 잠깐 보랏빛으로 물들며 보라 빛 조각이 튑니다.
    // 여러 개가 연달아 들어와도 원래 상태를 기준으로 계산해 크기·위치가 어긋나지 않습니다.
    static void InsertDeckReaction(ArrivalContext ctx, float at)
    {
        var p = Profile;
        Transform deck = ctx.deckTarget;
        if (deck == null || (p != null && !p.deckReaction)) return;
        Color glow = p != null ? p.deckGlowColor : new Color(0.72f, 0.45f, 1f);
        Vector3 scale = ctx.deckScale;
        Vector3 position = ctx.deckPosition;
        ctx.seq.Insert(at, DOTween.To(() => 0f, t =>
        {
            if (deck == null) return;
            float pulse = Mathf.Sin(t * Mathf.PI) * (1f - t * 0.5f);
            deck.localScale = scale * (1f + 0.14f * pulse);
            deck.localPosition = position + Vector3.right * Mathf.Sin(t * Mathf.PI * 6f) * 6f * (1f - t);
        }, 1f, 0.35f).SetEase(Ease.Linear));
        Graphic graphic = ctx.deckGraphic;
        if (graphic != null)
        {
            Color baseColor = ctx.deckColor;
            Color tint = Color.Lerp(baseColor, glow, 0.65f);
            ctx.seq.Insert(at, DOTween.To(() => 0f, t =>
            {
                if (graphic != null) graphic.color = Color.Lerp(tint, baseColor, t);
            }, 1f, 0.45f).SetEase(Ease.InQuad));
        }
        Vector2 center = ctx.deck;
        ctx.seq.InsertCallback(at, () =>
        {
            Sparks(center, glow, 8, 70f);
            Smoke(center, glow, 0f, 2, 48f);
        });
    }

    static void InsertMonsterSquash(Sequence seq, Transform monster, float at)
    {
        if (monster == null) return;
        Vector3 scale = monster.localScale;
        introResets.Add(() => { if (monster != null) monster.localScale = scale; });
        seq.Insert(at, monster.DOScale(Vector3.Scale(scale, new Vector3(1.08f, 0.9f, 1f)), 0.12f));
        seq.Insert(at + 0.12f, monster.DOScale(scale, 0.18f).SetEase(Ease.OutBack));
    }

    static void InsertMonsterShake(Sequence seq, Transform monster, SpriteRenderer body, float at)
    {
        if (monster == null) return;
        Vector3 position = monster.position;
        float amplitude = body != null ? body.bounds.size.x * 0.025f : 0.05f;
        introResets.Add(() => { if (monster != null) monster.position = position; });
        // DOShake는 난수를 쓰므로 사인 함수로 좌우로만 흔듭니다.
        seq.Insert(at, DOTween.To(() => 0f, t =>
        {
            if (monster != null) monster.position = position + Vector3.right * Mathf.Sin(t * Mathf.PI * 8f) * amplitude * (1f - t);
        }, 1f, 0.32f).SetEase(Ease.Linear));
    }

    // 등장 연출의 기준점: 주사위가 굴러가는 줄(굴림 칸들의 가운데). 굴림 칸이 없으면 몬스터 HP바 아래로 대신합니다.
    static Vector2 ArrivalRow(Transform rollRow, Enemy enemy)
    {
        float offset = Profile != null ? Profile.rowOffset : 0f;
        if (rollRow != null && rollRow.childCount > 0)
        {
            Vector3 sum = Vector3.zero;
            for (int i = 0; i < rollRow.childCount; i++) sum += rollRow.GetChild(i).position;
            return WorldToLocal(sum / rollRow.childCount) + Vector2.up * offset;
        }
        float below = 16f + EliteIconSize.y * 0.5f;
        RectTransform bar = enemy != null && enemy.enemyHPSlider != null ? enemy.enemyHPSlider.transform as RectTransform : null;
        if (bar != null && bar.gameObject.activeInHierarchy)
        {
            Rect area = UiRectInLayer(bar);
            return new Vector2(area.center.x, area.yMin - below);
        }
        return ToLocal(new Vector2(Screen.width * 0.55f, Screen.height * 0.35f));
    }
}
