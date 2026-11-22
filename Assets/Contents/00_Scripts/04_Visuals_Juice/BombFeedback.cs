using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

// 폭탄 주사위 연출. 기본 공격 다음에 CombatFlowController.ProcessBombDamage가 부른다.
// (일반 공격 피격 연출이 끝나길 기다림) → 준비: 주사위 뒤 붉은 빛무리 + 1픽셀 떨림
// → 왼쪽부터 터짐: 하얀 섬광 + 주사위를 덮는 폭발(그동안 주사위는 숨김), 몬스터가 바로 맞음
//   → 그을린 주사위가 툭 내려앉고 작은 연기
//   → 충격파가 옆 주사위에 닿으면 그 주사위가 밀렸다 돌아오며 빛무리가 밝아지고 덜덜 떨다가 터짐 (점점 빠르게, 화면 흔들림은 점점 세게)
// → 마지막 직전 모으기(크게 떨림) → 마지막 폭탄은 몬스터 큰 폭발 + 연쇄 보너스까지 더한 피해.
// 각 폭탄은 자기 피해(눈 × 2, 6은 2배)만 넣고, 마지막 폭탄이 연쇄 보너스(총 피해 - 원래 피해 합)를 더한다.
// 예: 5,5,5,5,5 → 10, 10, 10, 10, 110 (총 150). 총합은 TurnCalculator 공식 그대로다.
// 주사위 쪽 그림은 주사위와 같은 점 크기로 그리고, 움직임은 주사위 점 단위로만 한다(회전·비정수 확대 없음).
// 게임 난수 순서를 건드리지 않도록 무작위를 쓰지 않는다. 시간·크기는 BombVisualProfile.
public static class BombFeedback
{
    const int MonsterFxOrderOffset = 7; // 몬스터 피격 금(+6) 위
    const float RingReachPixels = 59.5f; // 충격파 마지막 프레임 반지름(128px 그림 기준, 주사위 점 단위)

    // 중간 폭탄 피해 숫자의 좌우 위치(몬스터 너비 × damageTextSpread 배). 정해진 순서로 돌아가며 쓴다.
    static readonly float[] TextSides = { -1f, 1f, -1.6f, 1.6f };

    sealed class BombDie
    {
        public Dice Die;
        public SpriteRenderer Body;
        public int Damage;
        public bool Crit;
        public Vector3 BaseLocalPos;
        public Vector3 BaseWorldPos;
        public Color BaseColor;
        public string TweenId;
        public SpriteRenderer Glow;
        public float GlowAlpha;
    }

    // hit(피해, 피해 숫자 좌우 이동): 실제 피해를 넣는 곳. 몬스터가 이미 쓰러졌으면 넣는 쪽에서 무시한다.
    public static IEnumerator Play(DiceManager dm, List<Dice> keptDice, int totalDamage, Action<int, float> hit)
    {
        int applied = 0;
        Action<int, float> deal = (amount, offset) =>
        {
            if (amount <= 0) return;
            applied += amount;
            hit?.Invoke(amount, offset);
        };

        BombVisualProfile profile = BombVisualProfile.Load();
        Enemy enemy = dm != null ? dm.enemy : null;
        List<BombDie> bombs = CollectBombs(keptDice);
        if (profile == null || enemy == null || enemy.monsterImage == null || bombs.Count == 0)
        {
            deal(totalDamage, 0f);
            yield break;
        }

        int count = bombs.Count;
        int rawSum = 0;
        foreach (var b in bombs) rawSum += b.Damage;
        int chainBonus = Mathf.Max(0, totalDamage - rawSum);
        SpriteRenderer firstBody = bombs[0].Body;
        float dieWidth = firstBody != null ? firstBody.bounds.size.x : 1f;
        float diePixel = firstBody != null && firstBody.sprite != null ? dieWidth / Mathf.Max(1f, firstBody.sprite.rect.width) : dieWidth / 64f;

        var root = new GameObject("Bomb FX");
        UnityEngine.Object.Destroy(root, 10f); // 중간에 끊겨도 남지 않게

        try
        {
            if (profile.startDelay > 0f) yield return new WaitForSeconds(profile.startDelay);
            if (IsDown(enemy)) yield break;

            // 준비: 주사위마다 뒤에 붉은 빛무리가 켜지고 1픽셀씩 덜덜 떤다.
            foreach (var b in bombs)
            {
                b.Glow = SpawnGlow(root, profile, b, diePixel);
                b.GlowAlpha = profile.glowChargeAlpha;
                Jitter(b, profile.chargeDuration, profile.fuseJitterPixels, diePixel, profile.jitterHz);
            }
            if (profile.chargeDuration > 0f) yield return new WaitForSeconds(profile.chargeDuration);

            for (int i = 0; i < count; i++)
            {
                if (IsDown(enemy)) break; // 앞 폭탄에 쓰러졌으면 나머지는 터지지 않는다
                BombDie b = bombs[i];
                bool last = i == count - 1;
                bool windup = last && count >= 2; // 폭탄이 1개면 모으기 없이 바로 터진다

                // 마지막 직전 모으기(연쇄 2개 이상): 마지막 주사위만 크게 떨고 빛무리가 가장 밝다.
                if (windup && profile.finalWindup > 0f)
                {
                    b.GlowAlpha = profile.glowFuseAlpha;
                    Jitter(b, profile.finalWindup, profile.windupJitterPixels, diePixel, profile.jitterHz);
                    yield return new WaitForSeconds(profile.finalWindup);
                    if (IsDown(enemy)) break;
                }

                // 주사위: 섬광 + 주사위를 덮는 폭발, 연기가 걷히면 그을린 주사위가 툭 내려앉는다.
                Explode(root, profile, b, diePixel, !last);

                // 몬스터: 바로 맞는다. 마지막 폭탄은 1개든 여러 개든 큰 폭발 + 강한 흔들림.
                SpriteRenderer m = enemy.monsterImage;
                Bounds mb = m.bounds;
                if (last)
                {
                    SpawnSized(root, profile.blastFrames, profile.blastFps, mb.center, mb.size.y * profile.blastScale, m.sortingLayerID, m.sortingOrder + MonsterFxOrderOffset);
                    CameraShake.Instance?.ShakeImpact(profile.impactShake);
                }
                else
                {
                    // 중간 폭탄은 몬스터에 번쩍임 + 숫자만(시선은 주사위 연쇄로). 흔들림은 점점 세게, 1번째는 0.
                    CameraShake.Instance?.ShakePixels(profile.tickShakeStep * i, profile.tickShakeDuration);
                }

                float textOffset = last ? 0f : TextSides[i % TextSides.Length] * mb.size.x * profile.damageTextSpread;
                deal(b.Damage + (last ? chainBonus : 0), textOffset);

                if (last)
                {
                    if (profile.finalHold > 0f) yield return new WaitForSeconds(profile.finalHold);
                    break;
                }

                // 충격파가 옆 주사위에 닿는 순간 그 주사위가 밀렸다 돌아오며 떨기 시작한다. 간격은 점점 짧아진다.
                float interval = Mathf.Max(profile.minInterval, profile.firstInterval - profile.intervalShrink * i) + (b.Crit ? profile.critExtra : 0f);
                BombDie next = bombs[i + 1];
                float distance = Mathf.Abs(next.BaseWorldPos.x - b.BaseWorldPos.x);
                float reach = Mathf.Clamp01((distance - dieWidth * 0.5f) / Mathf.Max(0.0001f, RingReachPixels * diePixel));
                float joltDelay = Mathf.Min(interval, profile.ringDuration * reach);
                if (joltDelay > 0f) yield return new WaitForSeconds(joltDelay);

                float fuse = Mathf.Max(0f, interval - joltDelay);
                Jolt(next, Mathf.Sign(next.BaseWorldPos.x - b.BaseWorldPos.x), fuse, profile, diePixel);
                next.GlowAlpha = profile.glowFuseAlpha;
                if (fuse > 0f) yield return new WaitForSeconds(fuse);
            }
        }
        finally
        {
            // 끝나거나 끊기면 주사위 모습을 원래대로 돌린다.
            foreach (var b in bombs)
            {
                KillTweens(b);
                if (b.Glow != null) UnityEngine.Object.Destroy(b.Glow.gameObject);
                if (b.Die == null) continue;
                b.Die.transform.localPosition = b.BaseLocalPos;
                if (b.Body != null) b.Body.color = b.BaseColor;
            }
            if (root != null) UnityEngine.Object.Destroy(root, 2f); // 남은 폭발·연기 프레임이 끝날 시간
            // 연출이 중간에 끊겼으면 남은 피해를 한 번에 넣는다(이미 쓰러졌으면 넣는 쪽에서 무시).
            if (applied < totalDamage) hit?.Invoke(totalDamage - applied, 0f);
        }
    }

    static bool IsDown(Enemy enemy) => enemy == null || enemy.IsDead || enemy.CurrentHP <= 0;

    static void KillTweens(BombDie b)
    {
        DOTween.Kill(b.TweenId);
        DOTween.Kill(b.TweenId + "g");
        DOTween.Kill(b.TweenId + "r");
    }

    static List<BombDie> CollectBombs(List<Dice> keptDice)
    {
        var bombs = new List<BombDie>();
        if (keptDice == null) return bombs;
        foreach (var d in keptDice)
        {
            if (d == null || d.myData == null || !d.gameObject.activeInHierarchy) continue;
            int damage = TurnCalculator.BombDieDamage(d, out bool crit);
            if (damage <= 0) continue;
            var body = d.GetComponent<SpriteRenderer>();
            bombs.Add(new BombDie
            {
                Die = d, Body = body, Damage = damage, Crit = crit,
                BaseLocalPos = d.transform.localPosition,
                BaseWorldPos = d.transform.position,
                BaseColor = body != null ? body.color : Color.white,
                TweenId = "BombFx" + d.GetInstanceID()
            });
        }
        bombs.Sort((a, b) => a.BaseWorldPos.x.CompareTo(b.BaseWorldPos.x));
        return bombs;
    }

    // 주사위를 좌우로 n픽셀씩 번갈아 옮겨 덜덜 떨게 한다(무작위·회전 없이 픽셀 단위).
    static void Jitter(BombDie b, float duration, int pixels, float diePixel, float hz)
    {
        if (b.Die == null) return;
        DOTween.Kill(b.TweenId);
        Transform t = b.Die.transform;
        t.position = b.BaseWorldPos;
        if (duration <= 0f || pixels <= 0) return;
        Vector3 basePos = b.BaseWorldPos;
        float step = pixels * diePixel;
        DOVirtual.Float(0f, duration, duration, e =>
        {
            if (t == null) return;
            float side = ((int)(e * hz) % 2 == 0) ? 1f : -1f;
            t.position = basePos + Vector3.right * side * step;
        }).SetEase(Ease.Linear).SetId(b.TweenId).OnComplete(() => { if (t != null) t.position = basePos; });
    }

    // 충격파에 맞은 주사위: 맞은 반대쪽으로 툭 밀렸다 돌아온 뒤, 남은 시간 동안 덜덜 떤다.
    static void Jolt(BombDie b, float direction, float fuse, BombVisualProfile profile, float diePixel)
    {
        if (b.Die == null) return;
        DOTween.Kill(b.TweenId);
        Transform t = b.Die.transform;
        Vector3 basePos = b.BaseWorldPos;
        float shove = Mathf.Min(0.09f, fuse);
        Vector3 pushed = basePos + Vector3.right * (direction == 0f ? 1f : direction) * profile.joltPixels * diePixel;
        t.position = pushed;
        DOTween.Sequence().SetId(b.TweenId)
            .AppendInterval(shove * 0.4f)
            .Append(t.DOMove(basePos, shove * 0.6f).SetEase(Ease.OutQuad))
            .AppendCallback(() => Jitter(b, fuse - shove, profile.fuseJitterPixels, diePixel, profile.jitterHz));
    }

    // 주사위 뒤 붉은 빛무리. 밝기는 b.GlowAlpha를 따라가며 깜빡인다.
    static SpriteRenderer SpawnGlow(GameObject root, BombVisualProfile profile, BombDie b, float diePixel)
    {
        if (profile.glowSprite == null || b.Body == null) return null;
        var go = new GameObject("Bomb Glow");
        go.transform.SetParent(root.transform, false);
        go.transform.position = b.BaseWorldPos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = profile.glowSprite;
        sr.sortingLayerID = b.Body.sortingLayerID;
        sr.sortingOrder = b.Body.sortingOrder - 1;
        go.transform.localScale = Vector3.one * diePixel * profile.glowSprite.pixelsPerUnit;
        sr.color = new Color(1f, 1f, 1f, 0f);
        float hz = profile.glowFlickerHz;
        DOVirtual.Float(0f, 600f, 600f, e =>
        {
            if (sr == null) return;
            float flicker = 0.75f + 0.25f * Mathf.Sin(e * hz * 2f * Mathf.PI);
            sr.color = new Color(1f, 1f, 1f, Mathf.Clamp01(b.GlowAlpha * flicker));
        }).SetEase(Ease.Linear).SetId(b.TweenId + "g").SetLink(go);
        return sr;
    }

    // 주사위 폭발: 섬광 + 주사위를 덮는 폭발(주사위 숨김) → 몇 프레임 뒤 그을린 주사위가 툭 내려앉고 연기가 피어오른다.
    static void Explode(GameObject root, BombVisualProfile profile, BombDie b, float diePixel, bool withRing)
    {
        KillTweens(b);
        if (b.Glow != null) { UnityEngine.Object.Destroy(b.Glow.gameObject); b.Glow = null; }
        if (b.Die == null) return;
        Transform t = b.Die.transform;
        t.position = b.BaseWorldPos;
        int layer = b.Body != null ? b.Body.sortingLayerID : 0;
        int order = b.Body != null ? b.Body.sortingOrder : 0;
        if (b.Body != null) b.Body.color = new Color(b.BaseColor.r, b.BaseColor.g, b.BaseColor.b, 0f);

        SpawnPixel(root, profile.diePopFrames, profile.diePopFps, b.BaseWorldPos, diePixel, layer, order + 2);
        if (withRing && profile.ringFrames != null && profile.ringFrames.Length > 0)
            SpawnPixel(root, profile.ringFrames, profile.ringFrames.Length / Mathf.Max(0.02f, profile.ringDuration), b.BaseWorldPos, diePixel, layer, order + 3);

        float reveal = profile.revealFrames / Mathf.Max(1f, profile.diePopFps);
        float dieHeight = b.Body != null ? b.Body.bounds.size.y : diePixel * 64f;
        DOVirtual.DelayedCall(reveal, () =>
        {
            if (b.Die == null) return;
            if (b.Body != null)
                b.Body.color = new Color(b.BaseColor.r * profile.scorchTint.r, b.BaseColor.g * profile.scorchTint.g, b.BaseColor.b * profile.scorchTint.b, b.BaseColor.a);
            t.position = b.BaseWorldPos + Vector3.up * profile.dropPixels * diePixel;
            t.DOMove(b.BaseWorldPos, 0.08f).SetEase(Ease.InQuad).SetId(b.TweenId + "r");
            int puffs = b.Crit ? 2 : 1;
            for (int p = 0; p < puffs; p++)
            {
                Vector3 at = b.BaseWorldPos + Vector3.up * dieHeight * 0.42f + Vector3.right * (puffs == 1 ? 0f : (p == 0 ? -8f : 8f)) * diePixel;
                GameObject smoke = SpawnPixel(root, profile.smokeFrames, profile.smokeFps, at, diePixel, layer, order + 1);
                if (smoke != null)
                {
                    float life = profile.smokeFrames.Length / Mathf.Max(1f, profile.smokeFps);
                    smoke.transform.DOMove(at + Vector3.up * profile.smokeRisePixels * diePixel, life).SetEase(Ease.OutQuad).SetLink(smoke);
                }
            }
        }, false).SetId(b.TweenId + "r");
    }

    // 주사위와 같은 점 크기로(1 그림 픽셀 = 주사위 1픽셀) 프레임을 한 번 재생하고 사라진다.
    static GameObject SpawnPixel(GameObject root, Sprite[] frames, float fps, Vector3 position, float diePixel, int sortingLayer, int sortingOrder)
    {
        if (frames == null || frames.Length == 0 || frames[0] == null) return null;
        GameObject go = Spawn(root, frames, fps, position, sortingLayer, sortingOrder);
        go.transform.localScale = Vector3.one * diePixel * frames[0].pixelsPerUnit;
        return go;
    }

    // 지정한 월드 크기로 프레임을 한 번 재생하고 사라진다(몬스터 쪽).
    static void SpawnSized(GameObject root, Sprite[] frames, float fps, Vector3 position, float worldSize, int sortingLayer, int sortingOrder)
    {
        if (frames == null || frames.Length == 0 || frames[0] == null) return;
        GameObject go = Spawn(root, frames, fps, position, sortingLayer, sortingOrder);
        go.transform.localScale = Vector3.one * (worldSize / Mathf.Max(0.0001f, frames[0].bounds.size.x));
    }

    static GameObject Spawn(GameObject root, Sprite[] frames, float fps, Vector3 position, int sortingLayer, int sortingOrder)
    {
        var go = new GameObject("Bomb Frames");
        go.transform.SetParent(root.transform, false);
        go.transform.position = position;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = frames[0];
        sr.sortingLayerID = sortingLayer;
        sr.sortingOrder = sortingOrder;
        float duration = frames.Length / Mathf.Max(1f, fps);
        DOVirtual.Float(0f, frames.Length, duration, v => { if (sr != null) sr.sprite = frames[Mathf.Clamp((int)v, 0, frames.Length - 1)]; })
            .SetEase(Ease.Linear).SetLink(go)
            .OnComplete(() => { if (go != null) UnityEngine.Object.Destroy(go); });
        return go;
    }
}
