using UnityEngine;
using DG.Tweening;
using UnityEngine.UI;
using Coffee.UIEffects;

public enum EnemyHitKind { Other, Normal, Flame, Dark }

// 피해 계산과 분리된 피격 연출. Enemy가 필요할 때 자동 연결합니다.
[DisallowMultipleComponent]
public sealed class MonsterHitFeedback : MonoBehaviour
{
    public float duration = 0.3f;
    public float impactSizeRatio = 0.4f;
    public float recoilRatio = 0.075f;
    public float squashRatio = 0.11f;
    public float hitStopDuration = 0.05f;
    public float whiteFlashDuration = 0.1f;
    [Header("약한 속성 피격")]
    public float elementalDuration = 0.28f;
    [Range(0f, 1f)] public float elementalMotion = 0.3f;
    [Range(0f, 1f)] public float elementalFlashAlpha = 0.28f;
    public Color flameColor = new Color(1f, 0.42f, 0.08f);
    public Color darkColor = new Color(0.58f, 0.25f, 0.9f);
    [Header("일반 공격 PNG 소재")]
    public Sprite[] normalFlashSprites;
    public Sprite[] normalShardSprites;
    public Sprite normalGlowSprite;
    public Sprite normalRingSprite;
    [Header("속성 공격 PNG 시트 (4열 x 2행)")]
    public Texture2D flameSheet;
    public Texture2D darkSheet;
    public Texture2D darkCrackTexture;
    public Sprite emberSprite;
    public float flameAnimationDuration = 0.56f;
    public float darkAnimationDuration = 0.56f;
    public float elementalSpriteSize = 1.8f;
    [Header("화염 접촉 타이밍")]
    [Range(0.125f, 0.5f)] public float flameContactProgress = 0.3f;
    public float flameBodyOffset = -0.18f;
    public float flameSizeMultiplier = 1f;
    private float flashStartedAt;
    public float FlameContactDelay => Mathf.Max(0.05f, flameFrames != null ? flameAnimationDuration : elementalDuration) * Mathf.Clamp(flameContactProgress, 0.125f, 0.5f);
    public bool IsFlamePlaying => activeKind == EnemyHitKind.Flame && tween != null && tween.IsActive();

    public void ShowFlameContact()
    {
        if (!IsFlamePlaying || whiteOverlay == null) return;
        flashStartedAt = elapsed;
        whiteOverlay.gameObject.SetActive(true);
    }
    private Sprite[] flameFrames;
    private Sprite[] darkFrames;
    private Sprite crackSprite;
    private SpriteRenderer crackRenderer;
    private bool sheetsInitialized;
    private static MonsterHitFeedback spriteDefaults;
    private static bool spriteDefaultsLoaded;
    private int nextFlash;
    private SpriteRenderer normalGlow;
    private SpriteRenderer normalRing;

    private EnemyHitKind activeKind;
    private bool darkAccent;
    private SpriteRenderer darkCore;
    private readonly SpriteRenderer[] darkMotes = new SpriteRenderer[4];
    private UIEffect overlayEffect;
    private float activeMotion = 1f;
    private SpriteRenderer targetSprite;
    private Image whiteOverlay;
    private Canvas overlayCanvas;
    private Animator pausedAnimator;
    private float savedAnimatorSpeed;
    private bool animatorPaused;
    private float elapsed;
    private float strength = 1f;
    public Color flashColor = new Color(1f, 0.96f, 0.76f);
    public Color shardColor = new Color(1f, 0.72f, 0.3f);
    private SpriteRenderer flash;
    private readonly SpriteRenderer[] shards = new SpriteRenderer[6];
    private Transform effectRoot;
    private Transform body;
    private Vector3 bodyPosition, bodyScale;
    private bool moving, registered;
    private Tween tween;
    private static Sprite diamond;
    private static Material material;
    private static int users;

    public static MonsterHitFeedback Get(Enemy enemy)
    {
        var feedback = enemy.GetComponent<MonsterHitFeedback>();
        return feedback != null ? feedback : enemy.gameObject.AddComponent<MonsterHitFeedback>();
    }

    public void Play(SpriteRenderer sprite, bool allowRecoil, Animator animator = null, float hitStrength = 1f, EnemyHitKind kind = EnemyHitKind.Normal, bool includesDark = false, bool prepareFlame = false)
    {
        Stop();
        if (sprite == null || sprite.sprite == null || !isActiveAndEnabled) return;
        EnsureObjects();
        targetSprite = sprite;
        activeKind = kind;
        ConfigureNormalSprites(kind, sprite);
        activeMotion = kind == EnemyHitKind.Normal ? 1f : Mathf.Clamp01(elementalMotion);
        darkAccent = includesDark && kind == EnemyHitKind.Normal;
        strength = Mathf.Clamp(hitStrength, 1f, 1.35f);
        elapsed = 0f;
        flashStartedAt = 0f;
        EnsureWhiteOverlay();
        overlayEffect.color = kind == EnemyHitKind.Flame ? flameColor : kind == EnemyHitKind.Dark ? darkColor : Color.white;
        whiteOverlay.gameObject.SetActive(!prepareFlame);
        if (kind == EnemyHitKind.Normal && allowRecoil && animator != null && animator.isActiveAndEnabled)
        {
            pausedAnimator = animator;
            savedAnimatorSpeed = animator.speed;
            animator.speed = 0f;
            animatorPaused = true;
        }
        Bounds bounds = sprite.bounds;
        float size = Mathf.Max(0.05f, Mathf.Min(bounds.size.x, bounds.size.y));
        // 몸통에 집중하고 머리 위 피해 숫자 및 아래 HP바를 피합니다.
        effectRoot.position = bounds.center + Vector3.up * bounds.size.y * (kind == EnemyHitKind.Flame ? flameBodyOffset : 0.05f);
        effectRoot.gameObject.SetActive(true);
        body = transform;
        bodyPosition = body.localPosition;
        bodyScale = body.localScale;
        moving = allowRecoil && kind != EnemyHitKind.Flame;
        float impactSize = size * impactSizeRatio * strength * (kind == EnemyHitKind.Normal ? 1f : 0.7f);
        if (kind == EnemyHitKind.Flame) impactSize *= Mathf.Max(0.1f, flameSizeMultiplier);
        float recoil = size * recoilRatio * strength * activeMotion / Mathf.Max(0.001f, body.parent != null ? Mathf.Abs(body.parent.lossyScale.y) : 1f);
        flash.sortingLayerID = sprite.sortingLayerID;
        flash.sortingOrder = sprite.sortingOrder + 3;
        for (int i = 0; i < shards.Length; i++)
        {
            shards[i].sortingLayerID = sprite.sortingLayerID;
            shards[i].sortingOrder = sprite.sortingOrder + 4;
        }
        if (darkAccent) EnsureDarkAccent(sprite);
        if (darkCore != null)
        {
            darkCore.gameObject.SetActive(darkAccent);
            for (int i = 0; i < darkMotes.Length; i++) darkMotes[i].gameObject.SetActive(darkAccent);
        }
        float playDuration = kind == EnemyHitKind.Normal ? duration : elementalDuration;
        if (kind == EnemyHitKind.Flame && flameFrames != null) playDuration = flameAnimationDuration;
        if ((kind == EnemyHitKind.Dark || darkAccent) && darkFrames != null) playDuration = Mathf.Max(playDuration, darkAnimationDuration);
        Draw(0f, impactSize, recoil);
        tween = DOVirtual.Float(0f, 1f, Mathf.Max(0.05f, playDuration), t =>
            {
                elapsed = t * Mathf.Max(0.05f, playDuration);
                if (elapsed >= hitStopDuration) ResumeAnimator();
                Draw(t, impactSize, recoil);
            })
            .SetEase(Ease.Linear).SetUpdate(true).SetLink(gameObject, LinkBehaviour.KillOnDisable).OnComplete(Stop);
    }

    private void Draw(float t, float size, float recoil)
    {
        float normalTime = Mathf.Clamp01(elapsed / Mathf.Max(0.05f, duration));
        if (activeKind == EnemyHitKind.Normal) DrawNormal(normalTime, size);
        else DrawElemental(t, size);
        if (darkAccent) DrawDarkAccent(t, size * 0.8f);
        if (activeKind == EnemyHitKind.Dark || darkAccent) DrawCrack(t, size);
        if (moving && body != null)
        {
            float kick = RecoilWeight(activeKind == EnemyHitKind.Normal ? normalTime : t);
            body.localPosition = bodyPosition + Vector3.up * recoil * kick;
            body.localScale = Vector3.Scale(bodyScale, new Vector3(1f + squashRatio * activeMotion * kick, 1f - squashRatio * activeMotion * kick, 1f));
        }
    }

    private void DrawNormal(float t, float size)
    {
        float burst = 1f - Mathf.Pow(1f - t, 3f);
        float flashAlpha = 1f - Mathf.Clamp01(t / 0.45f);
        SetSpriteSize(flash, size * (0.5f + burst));
        flash.transform.localRotation = Quaternion.Euler(0f, 0f, 18f);
        flash.color = new Color(flashColor.r, flashColor.g, flashColor.b, flashAlpha);
        if (normalGlow != null && normalGlow.gameObject.activeSelf)
        {
            SetSpriteSize(normalGlow, size * (1.8f + burst * 0.6f));
            normalGlow.color = new Color(flashColor.r, flashColor.g, flashColor.b, flashAlpha * 0.65f);
        }
        if (normalRing != null && normalRing.gameObject.activeSelf)
        {
            SetSpriteSize(normalRing, size * (0.7f + burst * 1.9f));
            normalRing.color = new Color(flashColor.r, flashColor.g, flashColor.b, (1f - t) * 0.5f);
        }
        for (int i = 0; i < shards.Length; i++)
        {
            // 규칙적인 각도 차이로 파편을 펼쳐 게임 난수 추첨에 영향을 주지 않습니다.
            float degrees = 15f + i * 60f;
            float radians = degrees * Mathf.Deg2Rad;
            float travel = size * (0.3f + burst * (i % 2 == 0 ? 1.6f : 1.25f));
            shards[i].transform.localPosition = new Vector3(Mathf.Cos(radians) * travel, Mathf.Sin(radians) * travel, 0f);
            shards[i].transform.localRotation = Quaternion.Euler(0f, 0f, degrees);
            float remaining = 1f - t;
            if (shards[i].sprite == diamond) shards[i].transform.localScale = new Vector3(size * 0.65f * remaining, size * 0.13f * remaining, 1f);
            else SetSpriteSize(shards[i], size * 0.38f * remaining);
            shards[i].color = new Color(shardColor.r, shardColor.g, shardColor.b, remaining);
        }
    }

    // PNG마다 PPU/해상도가 달라도 월드 공간의 목표 크기로 맞춥니다.
    private static void SetSpriteSize(SpriteRenderer renderer, float size)
    {
        Vector3 bounds = renderer.sprite.bounds.size;
        renderer.transform.localScale = Vector3.one * (size / Mathf.Max(0.001f, Mathf.Max(bounds.x, bounds.y)));
    }

    private void ConfigureNormalSprites(EnemyHitKind kind, SpriteRenderer target)
    {
        if (!spriteDefaultsLoaded)
        {
            spriteDefaultsLoaded = true;
            GameObject preset = Resources.Load<GameObject>("MonsterHitSprites");
            spriteDefaults = preset != null ? preset.GetComponent<MonsterHitFeedback>() : null;
        }
        if (spriteDefaults != null)
        {
            if (normalFlashSprites == null || normalFlashSprites.Length == 0) normalFlashSprites = spriteDefaults.normalFlashSprites;
            if (normalShardSprites == null || normalShardSprites.Length == 0) normalShardSprites = spriteDefaults.normalShardSprites;
            if (normalGlowSprite == null) normalGlowSprite = spriteDefaults.normalGlowSprite;
            if (normalRingSprite == null) normalRingSprite = spriteDefaults.normalRingSprite;
            if (flameSheet == null) flameSheet = spriteDefaults.flameSheet;
            if (darkSheet == null) darkSheet = spriteDefaults.darkSheet;
            if (darkCrackTexture == null) darkCrackTexture = spriteDefaults.darkCrackTexture;
            if (emberSprite == null) emberSprite = spriteDefaults.emberSprite;
        }
        EnsureElementalFrames();
        if (crackSprite != null && crackRenderer == null) crackRenderer = CreateRenderer("DarkCrack");
        if (crackRenderer != null)
        {
            crackRenderer.sprite = crackSprite;
            crackRenderer.gameObject.SetActive(false); // DrawCrack에서 실제 다크 여부와 시점에 맞춰 켭니다.
            crackRenderer.sortingLayerID = target.sortingLayerID;
            crackRenderer.sortingOrder = target.sortingOrder + 6;
        }
        bool normal = kind == EnemyHitKind.Normal;
        flash.sprite = diamond;
        if (normal && normalFlashSprites != null && normalFlashSprites.Length > 0)
        {
            flash.sprite = normalFlashSprites[nextFlash % normalFlashSprites.Length] ?? diamond;
            nextFlash = (nextFlash + 1) % normalFlashSprites.Length;
        }
        for (int i = 0; i < shards.Length; i++)
            shards[i].sprite = normal && normalShardSprites != null && normalShardSprites.Length > 0 ? normalShardSprites[i % normalShardSprites.Length] ?? diamond : diamond;
        if (normal && normalGlowSprite != null && normalGlow == null) normalGlow = CreateRenderer("NormalGlow");
        if (normal && normalRingSprite != null && normalRing == null) normalRing = CreateRenderer("NormalRing");
        if (normalGlow != null)
        {
            normalGlow.sprite = normalGlowSprite;
            normalGlow.gameObject.SetActive(normal && normalGlowSprite != null);
            normalGlow.sortingLayerID = target.sortingLayerID;
            normalGlow.sortingOrder = target.sortingOrder + 2;
        }
        if (normalRing != null)
        {
            normalRing.sprite = normalRingSprite;
            normalRing.gameObject.SetActive(normal && normalRingSprite != null);
            normalRing.sortingLayerID = target.sortingLayerID;
            normalRing.sortingOrder = target.sortingOrder + 3;
        }
    }

    private void EnsureElementalFrames()
    {
        if (sheetsInitialized) return;
        sheetsInitialized = true;
        flameFrames = SliceSheet(flameSheet);
        darkFrames = SliceSheet(darkSheet);
        if (darkCrackTexture != null) crackSprite = Sprite.Create(darkCrackTexture, new Rect(0f, 0f, darkCrackTexture.width, darkCrackTexture.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
    }

    private static Sprite[] SliceSheet(Texture2D texture)
    {
        if (texture == null) return null;
        if (texture.width % 4 != 0 || texture.height % 2 != 0)
        {
            Debug.LogWarning("피격 시트는 동일한 4열 x 2행이어야 합니다.");
            return null;
        }
        int width = texture.width / 4;
        int height = texture.height / 2;
        Sprite[] frames = new Sprite[8];
        for (int i = 0; i < frames.Length; i++)
        {
            // Unity의 Rect 원점은 왼쪽 아래이므로 윗줄부터 읽도록 Y를 뒤집습니다.
            Rect rect = new Rect((i % 4) * width, (1 - i / 4) * height, width, height);
            frames[i] = Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        }
        return frames;
    }

    private void DrawCrack(float t, float size)
    {
        if (crackRenderer == null) return;
        float progress = Mathf.Clamp01((t - 0.5f) / 0.5f);
        crackRenderer.gameObject.SetActive(t >= 0.5f && t < 1f);
        SetSpriteSize(crackRenderer, size * elementalSpriteSize * (0.85f + progress * 0.3f));
        crackRenderer.color = new Color(1f, 1f, 1f, (1f - progress) * 0.9f);
    }

    private static void ReleaseFrames(Sprite[] frames)
    {
        if (frames == null) return;
        for (int i = 0; i < frames.Length; i++) if (frames[i] != null) Destroy(frames[i]);
    }

    private void DrawElemental(float t, float size)
    {
        bool flame = activeKind == EnemyHitKind.Flame;
        Sprite[] frames = flame ? flameFrames : darkFrames;
        Color color = flame ? flameColor : darkColor;
        float alpha = flame ? 1f - t : Mathf.Sin(t * Mathf.PI);
        float coreSize = flame ? size * (0.5f + t * 0.5f) : size * (0.8f - t * 0.5f);
        flash.transform.localRotation = Quaternion.identity;
        flash.transform.localScale = new Vector3(coreSize, coreSize * (flame ? 1.3f : 1f), 1f);
        flash.color = new Color(color.r, color.g, color.b, alpha * 0.5f);
        if (frames != null)
        {
            flash.sprite = frames[Mathf.Min(frames.Length - 1, Mathf.FloorToInt(t * frames.Length))];
            SetSpriteSize(flash, size * elementalSpriteSize);
            flash.color = new Color(1f, 1f, 1f, t < 0.9f ? 1f : (1f - t) / 0.1f);
        }
        for (int i = 0; i < shards.Length; i++)
        {
            var piece = shards[i];
            if (flame)
            {
                float x = (i - 2.5f) * size * 0.24f + Mathf.Sin(t * 5f + i) * size * 0.08f;
                piece.transform.localPosition = new Vector3(x, size * (-0.35f + t * (1.2f + (i % 3) * 0.2f)), 0f);
                piece.transform.localRotation = Quaternion.Euler(0, 0, (i % 2 == 0 ? 1f : -1f) * 12f);
                piece.transform.localScale = new Vector3(size * 0.15f * (1f - t), size * 0.35f * (1f - t), 1f);
            }
            else
            {
                float angle = (i * 60f + t * 35f) * Mathf.Deg2Rad;
                float radius = size * (1.2f - t);
                piece.transform.localPosition = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
                piece.transform.localRotation = Quaternion.Euler(0, 0, i * 60f + t * 35f);
                piece.transform.localScale = Vector3.one * size * 0.18f * (1f - t);
            }
            if (flame && emberSprite != null)
            {
                piece.sprite = emberSprite;
                SetSpriteSize(piece, size * 0.22f * (1f - t));
                piece.color = new Color(1f, 1f, 1f, alpha * 0.8f);
            }
            else piece.color = new Color(color.r, color.g, color.b, alpha * 0.8f);
        }
    }

    private void EnsureDarkAccent(SpriteRenderer sprite)
    {
        if (darkCore == null)
        {
            darkCore = CreateRenderer("DarkCore");
            for (int i = 0; i < darkMotes.Length; i++) darkMotes[i] = CreateRenderer("DarkMote");
        }
        darkCore.sortingLayerID = sprite.sortingLayerID;
        darkCore.sortingOrder = sprite.sortingOrder + 5;
        for (int i = 0; i < darkMotes.Length; i++)
        {
            darkMotes[i].sortingLayerID = sprite.sortingLayerID;
            darkMotes[i].sortingOrder = sprite.sortingOrder + 5;
        }
    }

    private void DrawDarkAccent(float t, float size)
    {
        // 다크는 합산 피해의 원인을 표현할 뿐 추가 피해·숫자·히트스톱을 발생시키지 않습니다.
        float alpha = Mathf.Sin(t * Mathf.PI) * 0.55f;
        darkCore.transform.localScale = Vector3.one * size * (0.6f - t * 0.35f);
        darkCore.color = new Color(darkColor.r, darkColor.g, darkColor.b, alpha);
        if (darkFrames != null)
        {
            darkCore.sprite = darkFrames[Mathf.Min(darkFrames.Length - 1, Mathf.FloorToInt(t * darkFrames.Length))];
            SetSpriteSize(darkCore, size * elementalSpriteSize);
            darkCore.color = new Color(1f, 1f, 1f, t < 0.9f ? 0.9f : (1f - t) * 9f);
        }
        for (int i = 0; i < darkMotes.Length; i++)
        {
            float angle = (45f + i * 90f + t * 40f) * Mathf.Deg2Rad;
            float radius = size * (1.35f - t * 1.1f);
            darkMotes[i].transform.localPosition = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0);
            darkMotes[i].transform.localScale = Vector3.one * size * 0.17f * (1f - t);
            darkMotes[i].color = new Color(darkColor.r, darkColor.g, darkColor.b, alpha);
        }
    }

    public void Stop()
    {
        ResumeAnimator();
        if (whiteOverlay != null) whiteOverlay.gameObject.SetActive(false);
        Tween previous = tween;
        tween = null;
        previous?.Kill();
        if (moving && body != null)
        {
            body.localPosition = bodyPosition;
            body.localScale = bodyScale;
        }
        moving = false;
        if (effectRoot != null) effectRoot.gameObject.SetActive(false);
    }

    public static float RecoilWeight(float t)
    {
        t = Mathf.Clamp01(t);
        if (t < 0.18f) return t / 0.18f;
        if (t < 0.4f) return 1f;
        return Mathf.Pow((1f - t) / 0.6f, 2f);
    }

    private void ResumeAnimator()
    {
        if (!animatorPaused) return;
        if (pausedAnimator != null) pausedAnimator.speed = savedAnimatorSpeed;
        animatorPaused = false;
        pausedAnimator = null;
    }

    private void EnsureWhiteOverlay()
    {
        if (whiteOverlay != null) return;
        var go = new GameObject("MonsterWhiteFlash", typeof(RectTransform), typeof(Canvas), typeof(CanvasRenderer), typeof(Image));
        go.layer = gameObject.layer;
        go.transform.SetParent(targetSprite.transform, false);
        overlayCanvas = go.GetComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.WorldSpace;
        overlayCanvas.overrideSorting = true;
        whiteOverlay = go.GetComponent<Image>();
        whiteOverlay.raycastTarget = false;
        overlayEffect = go.AddComponent<UIEffect>();
        overlayEffect.colorFilter = ColorFilter.Replace;
        overlayEffect.color = Color.white;
        overlayEffect.colorIntensity = 1f;
    }

    private void LateUpdate()
    {
        if (whiteOverlay == null || !whiteOverlay.gameObject.activeSelf || targetSprite == null || targetSprite.sprite == null) return;
        // Animator가 프레임을 교체한 뒤 현재 스프라이트를 그대로 따라가며 실루엣만 흰색으로 바꿉니다.
        whiteOverlay.sprite = targetSprite.sprite;
        Bounds bounds = targetSprite.sprite.bounds;
        var rect = whiteOverlay.rectTransform;
        rect.localPosition = new Vector3(targetSprite.flipX ? -bounds.center.x : bounds.center.x, targetSprite.flipY ? -bounds.center.y : bounds.center.y, bounds.center.z);
        rect.sizeDelta = new Vector2(bounds.size.x, bounds.size.y);
        rect.localScale = new Vector3(targetSprite.flipX ? -1f : 1f, targetSprite.flipY ? -1f : 1f, 1f);
        overlayCanvas.sortingLayerID = targetSprite.sortingLayerID;
        overlayCanvas.sortingOrder = targetSprite.sortingOrder + 1;
        float alpha = 1f - Mathf.Clamp01((elapsed - flashStartedAt) / Mathf.Max(0.01f, whiteFlashDuration));
        whiteOverlay.color = new Color(1f, 1f, 1f, alpha * targetSprite.color.a * (activeKind == EnemyHitKind.Normal ? 1f : elementalFlashAlpha));
        if (alpha <= 0f) whiteOverlay.gameObject.SetActive(false);
    }

    private void EnsureObjects()
    {
        if (effectRoot != null) return;
        if (!registered)
        {
            if (diamond == null)
            {
                const int n = 32;
                var texture = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
                var pixels = new Color32[n * n];
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float edge = Mathf.Abs((x + 0.5f) / n * 2f - 1f) + Mathf.Abs((y + 0.5f) / n * 2f - 1f);
                    pixels[y * n + x] = new Color32(255, 255, 255, edge <= 1f ? (byte)255 : (byte)0);
                }
                texture.SetPixels32(pixels);
                texture.Apply(false, true);
                diamond = Sprite.Create(texture, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
                diamond.hideFlags = HideFlags.DontSave;
                var shader = Shader.Find("Sprites/Default");
                if (shader != null) material = new Material(shader) { hideFlags = HideFlags.DontSave };
            }
            users++;
            registered = true;
        }
        // 사망 확대·디졸브 및 피격 밀림에 파편의 크기/위치가 끌려가지 않게 독립 배치합니다.
        effectRoot = new GameObject("MonsterImpact").transform;
        flash = CreateRenderer("ImpactFlash");
        for (int i = 0; i < shards.Length; i++) shards[i] = CreateRenderer("ImpactShard");
    }

    private SpriteRenderer CreateRenderer(string objectName)
    {
        var go = new GameObject(objectName);
        go.layer = gameObject.layer;
        go.transform.SetParent(effectRoot, false);
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = diamond;
        if (material != null) renderer.sharedMaterial = material;
        return renderer;
    }

    private void OnDisable() => Stop();
    private void OnDestroy()
    {
        Stop();
        if (effectRoot != null) Destroy(effectRoot.gameObject);
        if (whiteOverlay != null) Destroy(whiteOverlay.gameObject);
        ReleaseFrames(flameFrames);
        ReleaseFrames(darkFrames);
        if (crackSprite != null) Destroy(crackSprite);
        if (!registered || --users != 0) return;
        if (diamond != null) { Destroy(diamond.texture); Destroy(diamond); }
        if (material != null) Destroy(material);
        diamond = null;
        material = null;
    }
}
