using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

// EnemyHPSlider에 붙는 HP 변동 연출. 슬라이더 값은 즉시 맞추고, 화면은 Filled Image가 그린다.
public class HpBarFx : MonoBehaviour
{
    [SerializeField] HpBarFxConfig config;
    [SerializeField] Slider slider;
    [SerializeField] RectTransform barRoot;
    [SerializeField] CanvasGroup canvasGroup;
    [SerializeField] Image frame;
    [SerializeField] Image ghost;
    [SerializeField] Image fill;
    [SerializeField] Image flash;
    [SerializeField] Image healFlash;
    [SerializeField] Image glow;
    [SerializeField] TextMeshProUGUI hpText;
    [SerializeField] RectTransform particleRoot;

    public bool IsDeathSequenceActive { get; private set; }

    int logicalHp = 100;
    int logicalMax = 100;
    int displayHp = 100;

    bool baselinesReady;
    bool lowHpActive;
    Color originalFrameColor;
    Vector3 textBaseScale = Vector3.one;

    Tween fillTween;
    Tween flashSeq;
    Tween shakeTween;
    Tween textTween;
    Tween punchTween;
    Tween ghostSeq;
    Tween ghostColorTween;
    Tween glowTween;
    Tween fillColorTween;
    Tween deathFadeTween;
    Tween frameTween;

    readonly List<Bit> sparkPool = new List<Bit>();
    readonly List<Bit> healPool = new List<Bit>();

    struct Bit
    {
        public GameObject go;
        public RectTransform rect;
        public Image image;
        public bool busy;
    }

    void Awake()
    {
        if (slider == null) slider = GetComponent<Slider>();
        CaptureBaselines();
    }

    void OnDestroy()
    {
        DOTween.Kill(this, false);
    }

    public void SetImmediate(int cur, int max)
    {
        if (config == null) return;
        CaptureBaselines();
        logicalMax = Mathf.Max(1, max);
        logicalHp = Mathf.Clamp(cur, 0, logicalMax);
        displayHp = logicalHp;
        IsDeathSequenceActive = false;
        lowHpActive = false;

        DOTween.Kill(this, false);
        ReleaseAll(sparkPool);
        ReleaseAll(healPool);

        ApplySlider(logicalHp, logicalMax);
        float ratio = Ratio(logicalHp, logicalMax);

        if (fill != null) fill.fillAmount = ratio;
        if (ghost != null)
        {
            ghost.fillAmount = ratio;
            SetRgb(ghost, config.ghostColor, 1f);
        }
        if (flash != null)
        {
            flash.fillAmount = 1f;
            SetRgb(flash, config.flashColor, 0f);
        }
        if (healFlash != null) SetRgb(healFlash, config.flashColor, 0f);
        if (canvasGroup != null) canvasGroup.alpha = 1f;
        if (frame != null) frame.color = originalFrameColor;
        RestoreTextScale();
        WriteText(logicalHp, logicalMax);

        bool low = ratio > 0f && ratio <= config.lowHpThreshold;
        if (fill != null) fill.color = low ? config.lowFillColor : config.fillColor;
        SetGlowAlpha(0f);
        if (low)
        {
            lowHpActive = true;
            StartGlowLoop();
        }
    }

    public void OnHpChanged(int from, int to, int max)
    {
        if (config == null) return;
        CaptureBaselines();
        logicalMax = Mathf.Max(1, max);
        from = Mathf.Clamp(from, 0, logicalMax);
        to = Mathf.Clamp(to, 0, logicalMax);
        logicalHp = to;
        ApplySlider(to, logicalMax);

        if (to == from) return;

        if (to > from) PlayHeal(to);
        else PlayDamage(from, to, to <= 0);
    }

    void PlayDamage(int from, int to, bool death)
    {
        KillReactive();
        float target = Ratio(to, logicalMax);
        bool big = death || (from - to) >= logicalMax * config.bigHitRatio;

        ghostColorTween?.Kill(false);
        if (ghost != null) SetRgb(ghost, config.ghostColor, 1f);

        float sparkAt = fill != null ? fill.fillAmount : target;
        PlayFill(target, 0f, config.hitFillDuration, config.hitFillEase);
        PlayFlash(big ? config.bigFlashPeakAlpha : config.hitFlashPeakAlpha);
        PlayShake(big ? config.bigShakeStrength : config.hitShakeStrength,
            big ? config.bigShakeDuration : config.hitShakeDuration);
        PlayCount(to, 0f, config.textCountDuration, config.textCountEase, true);
        PlaySparks(big, sparkAt);

        if (death)
        {
            IsDeathSequenceActive = true;
            FadeGlowOut();
            PlayGhost(0f, config.deathGhostDelay, config.deathGhostDuration, config.deathGhostEase);
            PlayDeathFade();
            return;
        }

        PlayGhost(target,
            big ? config.bigGhostDelay : config.ghostDelay,
            big ? config.bigGhostDuration : config.ghostDuration,
            config.ghostEase);
        RefreshLowHp(target);
    }

    void PlayHeal(int to)
    {
        KillReactive();
        CancelDeathVisuals();
        float target = Ratio(to, logicalMax);

        if (ghost != null) SetRgb(ghost, config.healGhostColor, 1f);
        PlayGhost(target, 0f, config.healGhostDuration, config.healGhostEase);
        PlayHealParticles();
        PlayFill(target, config.healFillDelay, config.healFillDuration, config.healFillEase);
        PlayCount(to, config.healTextDelay, config.healTextDuration, config.healTextEase, false);

        ghostColorTween?.Kill(false);
        ghostColorTween = DOVirtual.DelayedCall(config.healGhostRestoreDelay, () =>
        {
            if (ghost != null) SetRgb(ghost, config.ghostColor, 1f);
        });
        Prepare(ghostColorTween);
        RefreshLowHp(target);
    }

    void CancelDeathVisuals()
    {
        if (!IsDeathSequenceActive && deathFadeTween == null && frameTween == null) return;
        IsDeathSequenceActive = false;
        deathFadeTween?.Kill(false);
        frameTween?.Kill(false);
        deathFadeTween = null;
        frameTween = null;
        if (canvasGroup != null) canvasGroup.alpha = 1f;
        if (frame != null) frame.color = originalFrameColor;
    }

    void PlayFill(float target, float delay, float duration, Ease ease)
    {
        if (fill == null) return;
        fillTween?.Kill(false);
        Sequence seq = DOTween.Sequence();
        if (delay > 0f) seq.AppendInterval(delay);
        seq.Append(fill.DOFillAmount(target, Mathf.Max(0.0001f, duration)).SetEase(ease));
        fillTween = seq;
        Prepare(fillTween);
    }

    void PlayFlash(float peak)
    {
        if (flash == null) return;
        flashSeq?.Kill(false);
        flash.fillAmount = 1f;
        SetRgb(flash, config.flashColor, 0f);
        Sequence seq = DOTween.Sequence();
        seq.Append(flash.DOFade(peak, config.hitFlashInDuration).SetEase(config.hitFlashEase));
        seq.Append(flash.DOFade(0f, config.hitFlashOutDuration).SetEase(config.hitFlashEase));
        flashSeq = seq;
        Prepare(flashSeq);
    }

    void PlayShake(float strength, float duration)
    {
        shakeTween?.Kill(false);
        if (barRoot == null) return;
        Vector2 origin = barRoot.anchoredPosition;
        shakeTween = barRoot.DOShakeAnchorPos(duration, strength, config.shakeVibrato, 90f, false, true);
        shakeTween.OnKill(() =>
        {
            if (barRoot != null) barRoot.anchoredPosition = origin;
        });
        shakeTween.OnComplete(() =>
        {
            if (barRoot != null) barRoot.anchoredPosition = origin;
        });
        Prepare(shakeTween);
    }

    void PlayCount(int target, float delay, float duration, Ease ease, bool punch)
    {
        textTween?.Kill(false);
        punchTween?.Kill(false);
        int start = displayHp;
        int max = logicalMax;
        Sequence seq = DOTween.Sequence();
        if (delay > 0f) seq.AppendInterval(delay);
        seq.Append(DOVirtual.Int(start, target, Mathf.Max(0.0001f, duration), value =>
        {
            displayHp = value;
            WriteText(value, max);
        }).SetEase(ease));
        seq.OnComplete(() =>
        {
            displayHp = target;
            WriteText(target, max);
        });
        textTween = seq;
        Prepare(textTween);

        if (!punch || hpText == null) return;
        punchTween = hpText.rectTransform.DOPunchScale(Vector3.one * config.textPunch, config.textPunchDuration, 10, 0.5f);
        punchTween.OnKill(RestoreTextScale);
        punchTween.OnComplete(RestoreTextScale);
        Prepare(punchTween);
    }

    void PlayGhost(float target, float delay, float duration, Ease ease)
    {
        if (ghost == null) return;
        ghostSeq?.Kill(false);
        Sequence seq = DOTween.Sequence();
        if (delay > 0f) seq.AppendInterval(delay);
        seq.Append(ghost.DOFillAmount(target, Mathf.Max(0.0001f, duration)).SetEase(ease));
        ghostSeq = seq;
        Prepare(ghostSeq);
    }

    void PlayDeathFade()
    {
        if (canvasGroup != null)
        {
            deathFadeTween?.Kill(false);
            Sequence fade = DOTween.Sequence();
            fade.AppendInterval(config.deathFadeDelay);
            fade.Append(canvasGroup.DOFade(config.deathFadeAlpha, config.deathFadeDuration).SetEase(config.deathFadeEase));
            deathFadeTween = fade;
            Prepare(deathFadeTween);
        }

        if (frame == null) return;
        frameTween?.Kill(false);
        Color dead = config.deathFrameColor;
        dead.a = originalFrameColor.a;
        Sequence frameSeq = DOTween.Sequence();
        frameSeq.AppendInterval(config.deathFrameDelay);
        frameSeq.Append(frame.DOColor(dead, config.deathFrameDuration).SetEase(config.deathFrameEase));
        frameTween = frameSeq;
        Prepare(frameTween);
    }

    void RefreshLowHp(float ratio)
    {
        bool low = ratio > 0f && ratio <= config.lowHpThreshold;
        if (low)
        {
            if (lowHpActive) return;
            lowHpActive = true;
            if (fill != null)
            {
                fillColorTween?.Kill(false);
                fillColorTween = fill.DOColor(config.lowFillColor, config.lowFillDuration).SetEase(Ease.Linear);
                Prepare(fillColorTween);
            }
            StartGlowLoop();
            return;
        }

        if (!lowHpActive) return;
        lowHpActive = false;
        FadeGlowOut();
        if (fill == null) return;
        fillColorTween?.Kill(false);
        fillColorTween = fill.DOColor(config.fillColor, config.lowExitDuration).SetEase(Ease.Linear);
        Prepare(fillColorTween);
    }

    void StartGlowLoop()
    {
        if (glow == null) return;
        glowTween?.Kill(false);
        SetGlowAlpha(config.glowMinAlpha);
        glowTween = glow.DOFade(config.glowMaxAlpha, config.glowDuration)
            .SetEase(Ease.Linear)
            .SetLoops(-1, LoopType.Yoyo);
        Prepare(glowTween);
    }

    void FadeGlowOut()
    {
        lowHpActive = false;
        glowTween?.Kill(false);
        if (glow == null) return;
        glowTween = glow.DOFade(0f, config.lowExitDuration).SetEase(Ease.Linear);
        Prepare(glowTween);
    }

    void PlaySparks(bool big, float fillAmount)
    {
        if (particleRoot == null) return;
        int count = Mathf.Max(1, config.sparkCount);
        if (big) count *= Mathf.Max(1, config.bigSparkCountMultiplier);
        float duration = big ? config.bigSparkDuration : config.sparkDuration;
        Sprite sprite = fill != null ? fill.sprite : null;
        float width = particleRoot.rect.width;
        float height = particleRoot.rect.height;
        float x = (Mathf.Clamp01(fillAmount) - 0.5f) * width;

        for (int i = 0; i < count; i++)
        {
            int index = Rent(sparkPool, config.hitSparkPrefab, sprite);
            if (index < 0) return;
            Bit bit = sparkPool[index];
            float size = Random.Range(config.sparkSizeMin, config.sparkSizeMax);
            bit.rect.sizeDelta = new Vector2(size, size);
            bit.rect.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-40f, 40f));
            Vector2 start = new Vector2(x + Random.Range(-4f, 4f), Random.Range(-height * 0.15f, height * 0.15f));
            bit.rect.anchoredPosition = start;
            bit.image.color = config.sparkStartColor;

            Vector2 dir = Random.insideUnitCircle;
            if (dir.sqrMagnitude < 0.01f) dir = Vector2.up;
            dir.y = Mathf.Abs(dir.y);
            Vector2 end = start + dir.normalized * Random.Range(config.sparkDistanceMin, config.sparkDistanceMax);
            Color endColor = config.sparkEndColor;
            endColor.a = 0f;

            int captured = index;
            Sequence seq = DOTween.Sequence();
            seq.Join(bit.rect.DOAnchorPos(end, duration).SetEase(Ease.OutQuad));
            seq.Join(bit.image.DOColor(endColor, duration).SetEase(Ease.Linear));
            seq.OnKill(() => Release(sparkPool, captured));
            Prepare(seq);
        }
    }

    void PlayHealParticles()
    {
        if (particleRoot == null) return;
        Sprite sprite = fill != null ? fill.sprite : null;
        float width = particleRoot.rect.width;
        int count = Mathf.Max(1, config.healParticleCount);

        for (int i = 0; i < count; i++)
        {
            int index = Rent(healPool, config.healParticlePrefab, sprite);
            if (index < 0) return;
            Bit bit = healPool[index];
            float size = Random.Range(config.sparkSizeMin, config.sparkSizeMax);
            bit.rect.sizeDelta = new Vector2(size, size);
            float x = Random.Range(-width * 0.5f, width * 0.5f);
            float y = Random.Range(-8f, 8f);
            bit.rect.anchoredPosition = new Vector2(x, y);
            Color color = config.healParticleColor;
            color.a = 1f;
            bit.image.color = color;

            float duration = Random.Range(config.healDurationMin, config.healDurationMax);
            float rise = Random.Range(config.healRiseMin, config.healRiseMax);
            int captured = index;
            Sequence seq = DOTween.Sequence();
            seq.Join(bit.rect.DOAnchorPosY(y + rise, duration).SetEase(Ease.OutQuad));
            seq.Join(bit.image.DOFade(0f, duration));
            seq.OnKill(() => Release(healPool, captured));
            Prepare(seq);
        }
    }

    int Rent(List<Bit> pool, GameObject prefab, Sprite sprite)
    {
        for (int i = 0; i < pool.Count; i++)
        {
            if (pool[i].go == null || pool[i].busy) continue;
            Bit reused = pool[i];
            reused.busy = true;
            reused.go.SetActive(true);
            pool[i] = reused;
            return i;
        }

        if (pool.Count >= 64 || particleRoot == null) return -1;

        GameObject go;
        Image image;
        if (prefab != null)
        {
            go = Instantiate(prefab, particleRoot);
            image = go.GetComponent<Image>();
            if (image == null) image = go.GetComponentInChildren<Image>(true);
        }
        else
        {
            go = new GameObject("HpBit", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.layer = particleRoot.gameObject.layer;
            go.transform.SetParent(particleRoot, false);
            image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
        }

        if (image == null)
        {
            Destroy(go);
            return -1;
        }

        var rect = go.transform as RectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        go.SetActive(true);
        pool.Add(new Bit { go = go, rect = rect, image = image, busy = true });
        return pool.Count - 1;
    }

    static void Release(List<Bit> pool, int index)
    {
        if (index < 0 || index >= pool.Count) return;
        Bit bit = pool[index];
        if (!bit.busy) return;
        bit.busy = false;
        if (bit.go != null) bit.go.SetActive(false);
        pool[index] = bit;
    }

    static void ReleaseAll(List<Bit> pool)
    {
        for (int i = 0; i < pool.Count; i++) Release(pool, i);
    }

    void KillReactive()
    {
        fillTween?.Kill(false);
        flashSeq?.Kill(false);
        shakeTween?.Kill(false);
        textTween?.Kill(false);
        punchTween?.Kill(false);
    }

    void Prepare(Tween tween)
    {
        if (tween == null) return;
        tween.SetLink(gameObject).SetId(this).SetUpdate(false);
    }

    void ApplySlider(int cur, int max)
    {
        if (slider == null) return;
        slider.maxValue = max;
        slider.value = cur;
    }

    void CaptureBaselines()
    {
        if (baselinesReady) return;
        if (frame != null) originalFrameColor = frame.color;
        if (hpText != null) textBaseScale = hpText.rectTransform.localScale;
        baselinesReady = frame != null || hpText != null;
    }

    void RestoreTextScale()
    {
        if (hpText != null) hpText.rectTransform.localScale = textBaseScale;
    }

    void WriteText(int cur, int max)
    {
        if (hpText != null) hpText.text = cur + "/" + max;
    }

    void SetGlowAlpha(float alpha)
    {
        if (glow == null || config == null) return;
        SetRgb(glow, config.glowColor, alpha);
    }

    static void SetRgb(Graphic graphic, Color rgb, float alpha)
    {
        rgb.a = alpha;
        graphic.color = rgb;
    }

    static float Ratio(int cur, int max)
    {
        return max <= 0 ? 0f : Mathf.Clamp01((float)cur / max);
    }

    bool EnsurePlay()
    {
        if (Application.isPlaying) return true;
        Debug.LogWarning("[HpBarFx] 플레이 모드에서 테스트하세요.");
        return false;
    }

    void TestDelta(float signedRatio)
    {
        if (!EnsurePlay() || config == null) return;
        int delta = Mathf.Max(1, Mathf.RoundToInt(logicalMax * Mathf.Abs(signedRatio)));
        int to = signedRatio < 0f
            ? Mathf.Max(0, logicalHp - delta)
            : Mathf.Min(logicalMax, logicalHp + delta);
        OnHpChanged(logicalHp, to, logicalMax);
    }

    [ContextMenu("Test Hit 10%")]
    void TestHit10() => TestDelta(-0.10f);

    [ContextMenu("Test Big Hit 30%")]
    void TestBigHit30() => TestDelta(-0.30f);

    [ContextMenu("Test Heal 20%")]
    void TestHeal20() => TestDelta(0.20f);

    [ContextMenu("Test Low HP")]
    void TestLowHp()
    {
        if (!EnsurePlay() || config == null) return;
        int to = Mathf.Max(1, Mathf.FloorToInt(logicalMax * config.lowHpThreshold));
        if (logicalHp <= to) SetImmediate(logicalMax, logicalMax);
        OnHpChanged(logicalHp, to, logicalMax);
    }

    [ContextMenu("Test Death")]
    void TestDeath()
    {
        if (!EnsurePlay() || config == null) return;
        if (logicalHp <= 0) SetImmediate(logicalMax, logicalMax);
        OnHpChanged(logicalHp, 0, logicalMax);
    }

    [ContextMenu("Reset Full")]
    void ResetFull()
    {
        if (!EnsurePlay() || config == null) return;
        SetImmediate(logicalMax, logicalMax);
    }
}
