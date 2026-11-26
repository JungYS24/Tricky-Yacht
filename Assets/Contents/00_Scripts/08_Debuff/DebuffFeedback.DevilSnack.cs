using System;
using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

// 디버프 연출(DebuffFeedback의 일부): 최종보스 악마주사위 1페이즈 '간식 시간'(2페이즈 '심연의 숨결'도 같은 연출을 쓴다).
// 보스가 먼저 입을 벌리듯 위로 살짝 늘어나고, 그다음 보라 잇몸의 이빨이 왼쪽 체력 바 끝에 나타나 벌렸다가
// 보스가 납작해지며 씹는 순간에 맞춰 '딱' 문다. 무는 순간 피해가 들어간다(bite). 원인(보스)이 결과(체력 바)보다 먼저 보이게 한다.
// 체력이 깎였으면 바가 한 번에 줄며 끝에 한 입 베어 문 자국이 잠깐 남고 빨간 부스러기가 떨어진다.
// 보호막이 막았으면 회색 부스러기만 떨어진다(바 길이는 그대로). 이빨이 씹는 박자에 맞춰 보스도 제자리에서 오물오물 씹는다.
// 일반 공격(앞으로 돌진)과 헷갈리지 않게 보스는 자리를 옮기지 않는다.
// 그림은 체력 바와 같은 픽셀 크기(그림 1px = HUD 캔버스 1칸, 그림 한 칸 = 4칸)로 놓고, 움직임도 그림 한 칸씩 끊는다.
// 매 턴 나오는 연출이라 짧게 끝낸다(기본 속도 기준 약 0.9초). 피해가 들어가는 순간 돌아가고 나머지는 혼자 재생된다.
public static partial class DebuffFeedback
{
    const float SnackArtPixel = 4f;  // 그림 한 칸(HUD 캔버스 단위). 체력 바 그림과 같다.
    const float SnackJawsIn = 0.12f; // 보스가 입을 벌리기 시작한 뒤 이빨이 체력 바에 나타나는 시각
    const float SnackBite = 0.28f;   // 이빨이 다물고 보스가 씹는(피해가 들어가는) 시각
    const float SnackChew = 0.2f;    // 씹는 박자

    // 부스러기: 출발 자리(x: 물려 나간 칸 안 비율, y: 그 옆으로 몇 칸, z: 바 칸 아래에서 몇 칸)와
    // 튀는 빠르기(그림 칸/초). 난수 대신 고정값이다.
    static readonly Vector3[] CrumbStart =
    {
        new Vector3(0.2f, 0f, 2f), new Vector3(0.8f, 0.5f, 3f), new Vector3(0.5f, 1f, 1f),
        new Vector3(1f, 1.5f, 2f), new Vector3(0f, -0.5f, 0f),
    };
    static readonly Vector2[] CrumbVelocity =
    {
        new Vector2(-12f, 30f), new Vector2(10f, 22f), new Vector2(22f, 34f),
        new Vector2(30f, 16f), new Vector2(-4f, 26f),
    };
    const float CrumbGravity = 220f; // 그림 칸/초²
    const float CrumbLife = 0.45f;

    static PlayerHealthCardView healthCard;

    // bite는 실제 피해를 넣는 일(호출하는 쪽이 정함). 이빨이 다무는 순간 한 번 부르고, 연출이 없거나 끊겨도 반드시 부른다.
    public static IEnumerator PlayDevilSnack(DiceManager dm, Enemy boss, Action bite)
    {
        bool bitten = false;
        void Bite()
        {
            if (bitten) return;
            bitten = true;
            bite?.Invoke();
        }

        var p = Profile;
        if (healthCard == null) healthCard = UnityEngine.Object.FindAnyObjectByType<PlayerHealthCardView>();
        RectTransform fill = healthCard != null ? healthCard.Fill : null;
        RectTransform fillArea = fill != null ? fill.parent as RectTransform : null;
        if (dm == null || p == null || p.snackJawUpper == null || p.snackJawLower == null || fillArea == null || !fillArea.gameObject.activeInHierarchy)
        {
            Bite();
            yield break;
        }

        // 체력 바 칸(빨간 바가 차는 곳)을 오버레이 좌표로 옮긴다. unit은 HUD 1칸이 오버레이에서 차지하는 크기.
        Rect area = UiRectInLayer(fillArea);
        float unit = fillArea.rect.width > 0.01f ? area.width / fillArea.rect.width : 1f;
        float px = SnackArtPixel * unit;
        float barTop = area.yMax + px; // 바 테두리 윗변(칸 위에 테두리 한 칸)
        float oldEnd = area.xMin + area.width * (fill.gameObject.activeSelf ? Mathf.Clamp01(fill.anchorMax.x) : 0f);

        RectTransform stage = NewContainer("Devil Snack", Layer);
        Image upper = CreateSnackImage("Snack Jaw Upper", p.snackJawUpper, unit, stage, new Vector2(0.5f, 1f));
        Image lower = CreateSnackImage("Snack Jaw Lower", p.snackJawLower, unit, stage, new Vector2(0.5f, 1f));

        // 다문 자리: 윗니 아래 4칸이 바 위쪽 4칸을, 아랫니 위 3칸이 바 아래쪽 3칸을 덮어 이가 엇갈려 맞물린다.
        float jawX = Mathf.Round(oldEnd);
        float upperClosed = barTop + 4f * px;
        float lowerClosed = barTop - 3f * px;
        void SetGap(float gap) // 다문 자리에서 위아래로 벌어진 칸 수
        {
            if (upper != null) upper.rectTransform.anchoredPosition = new Vector2(jawX, Mathf.Round(upperClosed + gap * px));
            if (lower != null) lower.rectTransform.anchoredPosition = new Vector2(jawX, Mathf.Round(lowerClosed - gap * px));
        }
        void SetAlpha(float alpha)
        {
            if (upper != null) upper.color = new Color(1f, 1f, 1f, alpha);
            if (lower != null) lower.color = new Color(1f, 1f, 1f, alpha);
        }

        void OnBite()
        {
            int hpBefore = dm.currentPlayerHP, shieldBefore = dm.currentShield;
            Bite();
            int hpLost = Mathf.Max(0, hpBefore - dm.currentPlayerHP);
            int blocked = Mathf.Max(0, shieldBefore - dm.currentShield);
            float newEnd = oldEnd;
            if (hpLost > 0)
            {
                healthCard.JumpToCurrent();
                float ratio = dm.playerMaxHP > 0 ? Mathf.Clamp01((float)dm.currentPlayerHP / dm.playerMaxHP) : 0f;
                newEnd = area.xMin + area.width * ratio;
                ShowBiteMark(p.snackBiteMark, stage, unit, newEnd, area);
            }
            SpillCrumbs(p, stage, unit, area, newEnd, oldEnd, hpLost > 0, blocked > 0);
        }

        // 보스가 먼저 움직이고(입 벌리기), 이빨은 그 뒤에 반투명하게 나타나 한 칸 더 벌렸다가
        // 보스가 씹는 순간 딱 다문다. 두 번 더 씹은 뒤 두 단계로 흐려진다.
        ChewBoss(boss);
        SetGap(3f);
        SetAlpha(0f);
        Sequence seq = NewSequence(stage.gameObject);
        seq.InsertCallback(SnackJawsIn, () => SetAlpha(0.5f));
        seq.InsertCallback(SnackJawsIn + 0.04f, () => { SetAlpha(1f); SetGap(4f); });
        seq.InsertCallback(SnackBite - 0.05f, () => SetGap(2f));
        seq.InsertCallback(SnackBite, () => { SetGap(0f); OnBite(); });
        for (int i = 1; i <= 2; i++)
        {
            float beat = SnackBite + SnackChew * i;
            seq.InsertCallback(beat - 0.08f, () => SetGap(1f));
            seq.InsertCallback(beat, () => SetGap(0f));
        }
        float fade = SnackBite + SnackChew * 2f + 0.1f;
        seq.InsertCallback(fade, () => { SetGap(1f); SetAlpha(0.5f); });
        seq.InsertCallback(fade + 0.06f, () => SetAlpha(0f));
        seq.InsertCallback(fade + 0.12f, () => { }); // 물린 자국·부스러기가 끝날 때까지 무대를 남긴다
        seq.OnKill(() => { if (stage != null) UnityEngine.Object.Destroy(stage.gameObject); });

        while (!bitten && seq.IsActive()) yield return null;
        Bite(); // 연출이 끊겨도 피해는 들어간다
    }

    // 그림을 체력 바와 같은 픽셀 크기로 놓는다(그림 1px = HUD 1칸).
    static Image CreateSnackImage(string name, Sprite sprite, float unit, Transform parent, Vector2 pivot)
    {
        Image image = CreateImage(name, sprite, new Vector2(sprite.rect.width, sprite.rect.height) * unit, parent);
        image.preserveAspect = false;
        image.rectTransform.pivot = pivot;
        return image;
    }

    // 한 입 자국: 새 바 끝(오른쪽 변)에 맞춰 바 칸 높이로 잠깐 덮는다. 체력 바 바탕색으로 칠한 그림이라 바 끝이 둥글게 패여 보인다.
    // 남은 바가 자국보다 짧으면 그리지 않는다.
    static void ShowBiteMark(Sprite sprite, RectTransform parent, float unit, float end, Rect area)
    {
        if (sprite == null || end - area.xMin < sprite.rect.width * unit) return;
        Image mark = CreateSnackImage("Snack Bite Mark", sprite, unit, parent, new Vector2(1f, 0f));
        mark.rectTransform.anchoredPosition = new Vector2(end, area.yMin);
        Sequence seq = NewSequence(mark.gameObject);
        seq.AppendInterval(0.55f);
        seq.AppendCallback(() => { if (mark != null) mark.gameObject.SetActive(false); });
    }

    // 부스러기: 물려 나간 칸(새 끝~옛 끝)에서 살짝 튀어 올랐다가 떨어진다. 그림 한 칸씩 끊어 움직이고 마지막에 반투명해졌다가 사라진다.
    // 체력과 보호막을 함께 물었으면 회색·빨강을 번갈아 쓴다.
    static void SpillCrumbs(DebuffVisualProfile p, RectTransform parent, float unit, Rect area, float newEnd, float oldEnd, bool hp, bool shield)
    {
        if (!hp && !shield) return;
        float px = SnackArtPixel * unit;
        for (int i = 0; i < CrumbStart.Length; i++)
        {
            bool gray = shield && (!hp || i % 2 == 0);
            Sprite[] set = gray ? p.snackCrumbsGray : p.snackCrumbsRed;
            if (set == null || set.Length == 0) continue;
            Sprite sprite = set[i % set.Length];
            if (sprite == null) continue;

            Image crumb = CreateSnackImage("Snack Crumb", sprite, unit, parent, new Vector2(0.5f, 0.5f));
            RectTransform rect = crumb.rectTransform;
            Vector3 start = CrumbStart[i];
            Vector2 origin = new Vector2(Mathf.Lerp(newEnd, oldEnd, start.x) + start.y * px, area.yMin + (start.z + 0.5f) * px);
            Vector2 velocity = CrumbVelocity[i];
            void Place(float time)
            {
                if (rect == null) return;
                float dx = velocity.x * time;
                float dy = velocity.y * time - 0.5f * CrumbGravity * time * time;
                rect.anchoredPosition = origin + new Vector2(Mathf.Round(dx), Mathf.Round(dy)) * px;
            }
            Place(0f);

            Sequence seq = NewSequence(crumb.gameObject);
            seq.Append(DOTween.To(() => 0f, Place, CrumbLife, CrumbLife).SetEase(Ease.Linear));
            seq.InsertCallback(CrumbLife - 0.1f, () => { if (crumb != null) crumb.color = new Color(1f, 1f, 1f, 0.5f); });
            seq.OnComplete(() => { if (crumb != null) UnityEngine.Object.Destroy(crumb.gameObject); });
        }
    }

    // 보스가 제자리에서 오물오물 씹는다: 먼저 입을 벌리듯 두 걸음에 위로 살짝 늘어났다가, 이빨이 다무는 순간(SnackBite)부터
    // 박자마다 납작해졌다가 돌아온다(그림 피벗이 바닥이라 발은 그대로). 이빨과 같은 시각에 시작해 박자가 맞는다.
    // 부드럽게 늘이지 않고 한 번에 바꿔 픽셀 그림처럼 끊어 보이게 한다. 끝나면(끊겨도) 원래 크기로 돌린다.
    static void ChewBoss(Enemy boss)
    {
        SpriteRenderer body = boss != null ? boss.monsterImage : null;
        if (body == null || boss.IsDead) return;
        Transform t = body.transform;
        Vector3 rest = t.localScale;
        Vector3 open1 = Vector3.Scale(rest, new Vector3(0.98f, 1.02f, 1f));
        Vector3 open2 = Vector3.Scale(rest, new Vector3(0.96f, 1.05f, 1f));
        Vector3 squash = Vector3.Scale(rest, new Vector3(1.04f, 0.95f, 1f));
        void Set(Vector3 scale) { if (t != null) t.localScale = scale; }

        Sequence seq = NewSequence(body.gameObject);
        seq.InsertCallback(0f, () => Set(open1));
        seq.InsertCallback(0.08f, () => Set(open2));
        for (int i = 0; i < 3; i++)
        {
            seq.InsertCallback(SnackBite + SnackChew * i, () => Set(squash));
            seq.InsertCallback(SnackBite + SnackChew * i + 0.08f, () => Set(rest));
        }
        seq.OnKill(() => Set(rest));
    }
}
