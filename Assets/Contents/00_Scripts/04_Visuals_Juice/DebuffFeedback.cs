using System;
using System.Collections;
using System.Collections.Generic;
using Coffee.UIEffects;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 디버프 주사위 연출 공용: 전투 시작 등장, 뽑을 때 발동(도둑·족쇄), 결산 발동(가시·저주·가짜), 엘리트 전투 종료 시 임시 디버프 소멸.
// 덱·골드·체력 같은 실제 상태는 호출하는 쪽이 바꾸고, 여기서는 화면 표시만 담당합니다.
// 화면 맨 위 전용 오버레이 캔버스(1920×1080 기준)에 그리며, 씬이 바뀌면 캔버스와 함께 정리됩니다.
// 아래 시간 값은 기본 속도 기준이고, 실제 재생은 프로필의 durationScale만큼 느려집니다.
// 게임의 난수 추첨 순서를 바꾸지 않도록 연출에서는 UnityEngine.Random을 쓰지 않습니다.
public static class DebuffFeedback
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

    // ---------- 전투 시작: 디버프 주사위 등장 ----------

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

    // 전투 시작 연출을 끝낸다. 중간에 끊겨도 몬스터·덱 버튼·주사위 위치를 원래대로 돌린다.
    public static void EndIntro()
    {
        KillIntroTween();
        if (introRoot != null) UnityEngine.Object.Destroy(introRoot.gameObject);
        introRoot = null;
        skipRequested = false;
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

    // 보여 준 주사위가 가장자리부터 보랏빛으로 녹아 사라지고, 그 자리에서 밝은 보라 핵을 가진 검보라 안개가
    // 꼬리를 남기며 곡선을 그려 덱으로 스며든다. 도착하는 순간 덱 버튼이 반응하고 왼쪽 위 디버프 표시가 하나 올라갑니다.
    // 도착 시각을 돌려줍니다.
    static float InsertDieFly(ArrivalContext ctx, DieVisual visual, float at)
    {
        var p = Profile;
        Sequence seq = ctx.seq;
        Color core = p != null ? p.wispCoreColor : new Color(0.8f, 0.5f, 1f);
        Color trail = p != null ? p.wispTrailColor : new Color(0.24f, 0.1f, 0.34f);
        const float melt = 0.3f, travel = 0.36f; // 안개가 덱까지 날아가는 시간(기본 속도 기준)
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
        Vector2 bend = (from + to) * 0.5f + Vector2.up * 120f;
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
                Vector2 point = Bezier(from, bend, to, t);
                if (wispRect != null) wispRect.anchoredPosition = point;
                if (coreRect != null) coreRect.anchoredPosition = point;
            }, 1f, travel).SetEase(Ease.InOutSine));
            seq.Insert(launch, wispRect.DOScale(0.6f, travel).SetEase(Ease.InQuad));
            seq.Insert(launch + travel - 0.08f, wisp.DOFade(0f, 0.08f));
            seq.Insert(launch + travel - 0.08f, wispCore.DOFade(0f, 0.08f));
            // 지나간 자리에 옅은 안개 꼬리를 남깁니다(같은 곡선·같은 속도 곡선 위).
            const int trailCount = 7;
            for (int k = 1; k < trailCount; k++)
            {
                float progress = DOVirtual.EasedValue(0f, 1f, (float)k / trailCount, Ease.InOutSine);
                Vector2 point = Bezier(from, bend, to, progress);
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

    static RectTransform BeginIntro()
    {
        if (introRoot != null) return introRoot;
        introRoot = NewContainer("Debuff Intro", Layer);
        // 화면을 누르면 남은 연출을 빠르게 재생합니다. 투명 이미지가 입력을 받아 아래 UI 클릭도 막습니다.
        Image catcher = CreateImage("Skip Catcher", null, Vector2.zero, introRoot);
        Stretch(catcher.rectTransform);
        catcher.color = new Color(0f, 0f, 0f, 0f);
        catcher.raycastTarget = true;
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

    // ---------- 뽑을 때 발동: 도둑·족쇄 ----------

    // 도둑은 골드 패널에서 코인이 빠져나가 주사위로 빨려 들어가고, 족쇄는 굴리기 버튼을 X자로 감쌌다가 사라지며 리롤 숫자 칸에 −1을 띄운다.
    // 실제 골드는 이미 깎여 있고, 화면 골드 숫자만 코인이 빠져나갈 때 내려간다.
    public static void PlayDrawTriggers(List<DrawTrigger> triggers, List<Dice> board, UIManager ui, ShopManager shop)
    {
        if (triggers == null || triggers.Count == 0) return;
        var p = Profile;
        int hiddenLoss = 0;
        foreach (var trigger in triggers)
            if (trigger.data != null && trigger.data.debuffType == DebuffDiceType.Thief) hiddenLoss += trigger.stolenGold;
        if (hiddenLoss > 0 && shop != null) ShowGold(shop, shop.currentGold + hiddenLoss);

        Transform goldIcon = GoldIcon(ui);
        Vector2 gold = UiToLocal(goldIcon, new Vector2(0.12f, 0.92f));
        float delay = 0f;
        int thieves = 0, totalStolen = 0;
        foreach (var trigger in triggers)
        {
            if (trigger.data == null) continue;
            Vector2 die = DieLocal(FindDie(board, trigger.data));
            if (trigger.data.debuffType == DebuffDiceType.Thief)
            {
                int stolen = trigger.stolenGold;
                PlayThief(die, gold, stolen, shop, delay, () =>
                {
                    hiddenLoss -= stolen;
                    if (shop != null) ShowGold(shop, shop.currentGold + Mathf.Max(0, hiddenLoss));
                    PunchGoldPanel(ui);
                });
                thieves++;
                totalStolen += stolen;
                delay += 0.35f;
            }
            else if (trigger.data.debuffType == DebuffDiceType.Shackle)
            {
                PlayShackle(die, ui, delay);
                delay += 0.5f;
            }
        }
        // 도둑이 여럿이면 합계를 한 번 더 보여 줍니다.
        if (thieves >= 2 && totalStolen > 0 && p != null)
            PopNumber($"-{totalStolen}", p.lossColor, gold + new Vector2(60f, -96f), delay + 0.4f, 52f);
    }

    static void PlayThief(Vector2 die, Vector2 gold, int stolen, ShopManager shop, float delay, Action onDepart)
    {
        var p = Profile;
        Color smoke = p != null ? p.thiefSmoke : Color.black;
        Sequence seq = NewSequence(Layer.gameObject);
        seq.InsertCallback(delay, () => Smoke(die, smoke, 0f, 3, 54f));
        if (stolen <= 0) return;

        float depart = delay + 0.15f;
        seq.InsertCallback(depart, () =>
        {
            onDepart?.Invoke();
            if (p != null) PopNumber($"-{stolen}", p.lossColor, gold + new Vector2(60f, -44f));
        });

        Sprite coinSprite = shop != null ? shop.coinIconSprite : null;
        if (coinSprite == null)
        {
            seq.InsertCallback(depart + 0.45f, () => Smoke(die, smoke, 0f, 2, 48f));
            return;
        }
        int coins = Mathf.Clamp(Mathf.CeilToInt(stolen / 34f), 1, 3);
        for (int i = 0; i < coins; i++)
        {
            Image coin = CreateImage("Stolen Coin", coinSprite, new Vector2(36f, 36f), Layer);
            RectTransform rect = coin.rectTransform;
            rect.anchoredPosition = gold;
            coin.gameObject.SetActive(false);
            float angle = i * 2.1f + 0.6f;
            Vector2 burst = gold + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 46f;
            float at = depart + i * 0.07f;
            seq.InsertCallback(at, () => coin.gameObject.SetActive(true));
            seq.Insert(at, rect.DOAnchorPos(burst, 0.14f).SetEase(Ease.OutBack));
            seq.Insert(at + 0.16f, rect.DOAnchorPos(die, 0.42f).SetEase(Ease.InQuad));
            seq.Insert(at + 0.16f, rect.DOScale(0.5f, 0.42f).SetEase(Ease.InQuad));
            seq.InsertCallback(at + 0.58f, () => { if (coin != null) UnityEngine.Object.Destroy(coin.gameObject); });
        }
        seq.InsertCallback(depart + 0.58f + (coins - 1) * 0.07f, () => Smoke(die, smoke, 0f, 2, 48f));
    }

    // 사슬 두 개가 굴리기 버튼을 X자로 감싸고 잠시 머문 뒤 연기와 함께 사라지고, 리롤 숫자 칸에 −1이 뜬다.
    static void PlayShackle(Vector2 die, UIManager ui, float delay)
    {
        var p = Profile;
        TMP_Text count = ui != null ? ui.roundPlaysText : null;
        Vector2 countPos = UiToLocal(count != null ? count.transform : null, new Vector2(0.15f, 0.35f));
        Color loss = p != null ? p.lossColor : Color.red;
        Sequence seq = NewSequence(Layer.gameObject);
        if (p != null) seq.InsertCallback(delay, () => Smoke(die, p.vanishSmoke, 0f, 2, 44f));

        void ShowLoss()
        {
            if (count != null)
            {
                count.transform.DOComplete();
                count.transform.DOPunchScale(Vector3.one * 0.25f, 0.25f, 4, 0.5f).SetUpdate(true);
            }
            PopNumber("-1", loss, countPos + new Vector2(0f, 44f));
        }

        RectTransform button = ui != null && ui.rollButton != null ? ui.rollButton.transform as RectTransform : null;
        Sprite chainSprite = p != null ? p.chainLink : null;
        if (chainSprite == null || button == null)
        {
            seq.InsertCallback(delay + 0.3f, ShowLoss);
            return;
        }

        // 버튼이 화면 밖으로 일부 나가 있어도 사슬은 화면 안쪽 영역에만 그립니다.
        Rect area = ClampToLayer(UiRectInLayer(button), 8f);
        Vector2 tile = new Vector2(chainSprite.rect.width, chainSprite.rect.height);
        float length = Mathf.Sqrt(area.width * area.width + area.height * area.height) * 0.92f;
        float angle = Mathf.Atan2(area.height, area.width) * Mathf.Rad2Deg;
        float wrap = delay + 0.12f;
        float release = wrap + 0.42f; // 사라지는 시간까지 합쳐 연출 속도 1.2 기준 버튼 위에 약 0.6~0.7초
        var chains = new Image[2];
        for (int i = 0; i < 2; i++)
        {
            Image chain = CreateImage("Shackle Chain", chainSprite, new Vector2(length, tile.y), Layer);
            chain.type = Image.Type.Tiled;
            chain.preserveAspect = false;
            RectTransform rect = chain.rectTransform;
            rect.anchoredPosition = area.center;
            rect.localRotation = Quaternion.Euler(0f, 0f, i == 0 ? angle : -angle);
            rect.localScale = new Vector3(0f, 1f, 1f);
            chain.gameObject.SetActive(false);
            chains[i] = chain;

            float at = wrap + i * 0.14f;
            seq.InsertCallback(at, () => chain.gameObject.SetActive(true));
            seq.Insert(at, rect.DOScaleX(1f, 0.18f).SetEase(Ease.OutQuad));
            seq.Insert(at + 0.18f, rect.DOPunchAnchorPos(new Vector2(0f, -6f), 0.22f, 6, 0.5f));
            seq.Insert(release + i * 0.05f, chain.DOFade(0f, 0.16f));
            seq.Insert(release + i * 0.05f, rect.DOScale(new Vector3(1.06f, 1.06f, 1f), 0.16f).SetEase(Ease.OutQuad));
        }
        if (p != null) seq.InsertCallback(release, () => Smoke(area.center, p.vanishSmoke, 0f, 3, 60f));
        seq.InsertCallback(release + 0.1f, ShowLoss);
        seq.InsertCallback(release + 0.23f, () =>
        {
            foreach (var chain in chains)
                if (chain != null) UnityEngine.Object.Destroy(chain.gameObject);
        });
    }

    // ---------- 결산 발동: 가시·저주·가짜 ----------

    // 가시 파편이 주사위에서 튀었다가 체력 표시로 날아간다. 첫 파편이 닿는 순간에 돌아오므로, 호출한 쪽은 그때 피해를 적용한다.
    public static IEnumerator PlayThornBurst(List<Dice> spineDice, Transform heartTarget)
    {
        Sprite shard = Profile != null ? Profile.thornShard : null;
        if (shard == null || spineDice == null || spineDice.Count == 0) yield break;
        Vector2 heart = UiToLocal(heartTarget, new Vector2(0.12f, 0.85f));
        Vector2 size = new Vector2(shard.rect.width, shard.rect.height);
        Sequence seq = NewSequence(Layer.gameObject);
        int order = 0;
        foreach (var die in spineDice)
        {
            if (die == null) continue;
            Vector2 from = DieLocal(die);
            float start = order++ * 0.1f;
            for (int i = 0; i < 4; i++)
            {
                Image piece = CreateImage("Thorn Shard", shard, size, Layer);
                RectTransform rect = piece.rectTransform;
                // 가시 끝이 오른쪽 위를 향하는 이미지라 90도 단위로만 돌려 픽셀이 깨지지 않게 합니다.
                rect.localRotation = Quaternion.Euler(0f, 0f, 90f * i);
                Vector2 direction = Quaternion.Euler(0f, 0f, 90f * i) * new Vector2(0.707f, 0.707f);
                rect.anchoredPosition = from;
                rect.localScale = Vector3.one * 0.6f;
                piece.gameObject.SetActive(false);
                seq.InsertCallback(start, () => piece.gameObject.SetActive(true));
                seq.Insert(start, rect.DOAnchorPos(from + direction * 70f, 0.2f).SetEase(Ease.OutQuad));
                seq.Insert(start, rect.DOScale(1f, 0.2f));
                float fly = start + 0.32f + i * 0.03f;
                seq.Insert(fly, rect.DOAnchorPos(heart, 0.32f).SetEase(Ease.InQuad));
                seq.Insert(fly, rect.DOScale(0.5f, 0.32f));
                seq.InsertCallback(fly + 0.32f, () => { if (piece != null) UnityEngine.Object.Destroy(piece.gameObject); });
            }
        }
        yield return new WaitForSecondsRealtime(Scaled(0.64f));
    }

    // 저주가 배수에 곱해질 때 주사위와 배수 숫자에 초록 연기를 피웁니다.
    public static void PlayCurse(Dice die, Transform multTarget)
    {
        var p = Profile;
        if (p == null) return;
        if (die != null) Smoke(DieLocal(die), p.curseSmoke, 0f, 3, 48f);
        if (multTarget != null) Smoke(UiToLocal(multTarget, new Vector2(0.15f, 0.45f)), p.curseSmoke, 0.08f, 4, 60f);
    }

    public static Color CurseColor => Profile != null ? Profile.curseColor : new Color(0.45f, 1f, 0.35f);

    // 가짜 주사위는 결산에서 흐릿하게 꺼지며 0으로 보입니다.
    public static void PlayFakeFade(Dice die)
    {
        if (die == null) return;
        die.FadeAsFake();
        if (Profile != null) Smoke(DieLocal(die), Profile.vanishSmoke, 0f, 2, 44f);
    }

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
