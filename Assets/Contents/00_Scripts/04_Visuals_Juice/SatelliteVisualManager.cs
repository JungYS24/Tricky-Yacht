using UnityEngine;
using DG.Tweening;
using static SatelliteVisualResources;

public class SatelliteVisualManager : MonoBehaviour
{
    [Header("별 위성 공용 설정 (비어 있으면 Resources 기본값 사용)")]
    [SerializeField] private SatelliteVisualProfile visualProfile;

    private Dice dice;
    private SpriteRenderer diceSpriteRenderer;
    private Transform effectsRoot;
    private readonly SatelliteInstance[] instances = new SatelliteInstance[4];
    private float orbitAngle;
    private float twinklePhase;
    private Tween twinkleTween;
    private float tweenPeriod;
    private bool usesSharedAssets;

    private struct TrailDot
    {
        public Transform transform;
        public SpriteRenderer renderer;
    }

    // 위성과 꼬리 이펙트를 함께 묶어서 관리하는 클래스. 장착 변경 시에도 재사용합니다.
    private sealed class SatelliteInstance
    {
        public SatelliteType type;
        public Transform root;
        public Transform starTransform;
        public SpriteRenderer star;
        public Transform glowTransform;
        public SpriteRenderer glow;
        public TrailDot[] dots = System.Array.Empty<TrailDot>();
        public float appearedAt;
        public bool assigned;
    }

    private void Awake()
    {
        dice = GetComponent<Dice>();
        diceSpriteRenderer = GetComponent<SpriteRenderer>();
        if (visualProfile == null) visualProfile = Profile;
        if (visualProfile == null)
            Debug.LogWarning("[위성 연출] Resources/SatelliteVisualProfile 설정이 필요합니다.", this);
    }

    private void OnDisable()
    {
        twinkleTween?.Pause();
        if (effectsRoot != null) effectsRoot.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        twinkleTween?.Kill();
        if (effectsRoot != null) Destroy(effectsRoot.gameObject);
        if (usesSharedAssets) Release();
    }

    private void LateUpdate()
    {
        var satellites = dice != null && dice.myData != null ? dice.myData.activeSatellites : null;
        int count = satellites != null ? Mathf.Min(satellites.Count, instances.Length) : 0;
        if (count == 0 || visualProfile == null || diceSpriteRenderer == null || diceSpriteRenderer.sprite == null)
        {
            if (effectsRoot != null) effectsRoot.gameObject.SetActive(false);
            for (int i = 0; i < instances.Length; i++) if (instances[i] != null) instances[i].assigned = false;
            twinkleTween?.Pause();
            return;
        }

        EnsureSharedAssets();
        if (effectsRoot == null)
        {
            effectsRoot = new GameObject("SatelliteStarEffects").transform;
            effectsRoot.SetParent(transform, false);
        }
        effectsRoot.gameObject.SetActive(true);
        EnsureTwinkle();

        // 종류별 방향 분기 없이 모두 동일한 시계 방향으로 진행합니다. 게임 난수는 사용하지 않습니다.
        orbitAngle = Mathf.Repeat(orbitAngle - Mathf.Abs(visualProfile.clockwiseDegreesPerSecond) * Time.deltaTime, 360f);
        Bounds bounds = diceSpriteRenderer.sprite.bounds;
        Vector3 center = bounds.center;
        if (diceSpriteRenderer.flipX) center.x = -center.x;
        if (diceSpriteRenderer.flipY) center.y = -center.y;
        center.x += bounds.size.x * visualProfile.centerOffsetRatio.x;
        center.y += bounds.size.y * visualProfile.centerOffsetRatio.y;
        float width = Mathf.Max(0.01f, bounds.size.x);
        float height = Mathf.Max(0.01f, bounds.size.y);
        Vector2 radius = new Vector2(width * Mathf.Max(0.1f, visualProfile.radiusRatio.x), height * Mathf.Max(0.1f, visualProfile.radiusRatio.y));
        float dieSize = Mathf.Min(width, height);
        float starSize = dieSize * Mathf.Max(0.01f, visualProfile.starSizeRatio);
        int dotCount = Mathf.Clamp(visualProfile.trailDotCount, 0, 24);

        for (int i = 0; i < instances.Length; i++)
        {
            if (i >= count)
            {
                if (instances[i] != null) { instances[i].root.gameObject.SetActive(false); instances[i].assigned = false; }
                continue;
            }

            var inst = instances[i] ?? (instances[i] = CreateInstance());
            SatelliteType type = satellites[i];
            Sprite star = visualProfile.GetStar(type);
            if (!inst.assigned || inst.type != type || inst.star.sprite != star)
            {
                inst.type = type;
                inst.star.sprite = star;
                inst.appearedAt = Time.time;
                inst.assigned = true;
            }
            inst.root.gameObject.SetActive(star != null);
            if (star == null) continue;
            EnsureDots(inst, dotCount);

            float angle = orbitAngle + StartAngle(type);
            float pulse = 0.5f + 0.5f * Mathf.Sin(twinklePhase + (int)type * 1.7f);
            Color light = visualProfile.GetLight(type);
            inst.root.localPosition = OrbitPosition(center, radius, angle);
            // 별 PNG 자체에는 색상/투명도 변조를 하지 않아 뾰족한 모양을 선명하게 유지합니다.
            float spriteSize = Mathf.Max(0.001f, Mathf.Max(star.bounds.size.x, star.bounds.size.y));
            float starScale = starSize / spriteSize * (1f + pulse * visualProfile.starPulse);
            inst.starTransform.localScale = Vector3.one * starScale;
            inst.starTransform.localPosition = -star.bounds.center * starScale;
            inst.star.color = Color.white;
            inst.glowTransform.localScale = Vector3.one * (starSize * visualProfile.glowSize * (0.9f + 0.1f * pulse));
            inst.glow.color = new Color(light.r, light.g, light.b, visualProfile.glowAlpha * (0.65f + 0.35f * pulse));

            int layer = diceSpriteRenderer.sortingLayerID;
            int order = diceSpriteRenderer.sortingOrder;
            SetSorting(inst.star, layer, order + 3);
            SetSorting(inst.glow, layer, order + 2);
            // 주사위와 함께 움직이는 로컬 잔상이라 킵/교체 때 화면을 가로지르는 긴 꼬리가 생기지 않습니다.
            float warmup = Mathf.Clamp01((Time.time - inst.appearedAt) / 0.45f);
            for (int j = 0; j < inst.dots.Length; j++)
            {
                var dot = inst.dots[j];
                dot.renderer.enabled = j < dotCount;
                if (j >= dotCount) continue;
                float progress = (j + 1f) / (dotCount + 1f);
                dot.transform.localPosition = OrbitPosition(center, radius, angle + progress * visualProfile.trailArcDegrees) - inst.root.localPosition;
                float sparkle = 0.8f + 0.2f * Mathf.Sin(twinklePhase * 1.3f + j * 2.1f + i);
                float size = dieSize * visualProfile.dotSizeRatio * (1f - 0.45f * progress);
                dot.transform.localScale = Vector3.one * size;
                float alpha = visualProfile.trailAlpha * (1f - progress * 0.85f) * sparkle * Mathf.Clamp01(warmup * 2f - progress);
                dot.renderer.color = new Color(Mathf.Lerp(light.r, 1f, 0.45f), Mathf.Lerp(light.g, 1f, 0.45f), Mathf.Lerp(light.b, 1f, 0.45f), alpha);
                SetSorting(dot.renderer, layer, order + 1);
            }
        }
    }

    private void EnsureTwinkle()
    {
        float period = Mathf.Max(0.2f, visualProfile.twinkleSeconds);
        if (twinkleTween == null || !twinkleTween.IsActive() || !Mathf.Approximately(period, tweenPeriod))
        {
            twinkleTween?.Kill();
            tweenPeriod = period;
            // 주사위당 Tween 하나만 사용하고 각 위성의 밝기는 위상 차이로 달리합니다.
            twinkleTween = DOVirtual.Float(0f, Mathf.PI * 2f, period, value => twinklePhase = value).SetEase(Ease.Linear).SetLoops(-1, LoopType.Restart);
        }
        else if (!twinkleTween.IsPlaying()) twinkleTween.Play();
    }

    private SatelliteInstance CreateInstance()
    {
        var root = new GameObject("StarOrbit").transform;
        root.SetParent(effectsRoot, false);
        var star = CreateRenderer("Star", root, null);
        var glow = CreateRenderer("Glow", root, glowSprite);
        return new SatelliteInstance { root = root, star = star, starTransform = star.transform, glow = glow, glowTransform = glow.transform };
    }

    private void EnsureDots(SatelliteInstance inst, int count)
    {
        if (inst.dots.Length >= count) return;
        int oldCount = inst.dots.Length;
        System.Array.Resize(ref inst.dots, count);
        for (int i = oldCount; i < count; i++)
        {
            // 각 위성의 root 아래에 두되 위치는 주사위 로컬 궤도를 따릅니다.
            var renderer = CreateRenderer("OrbitDot", inst.root, dotSprite);
            inst.dots[i] = new TrailDot { renderer = renderer, transform = renderer.transform };
        }
    }

    private static SpriteRenderer CreateRenderer(string objectName, Transform parent, Sprite sprite)
    {
        var go = new GameObject(objectName);
        go.transform.SetParent(parent, false);
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        if (effectMaterial != null) renderer.sharedMaterial = effectMaterial;
        return renderer;
    }

    private static void SetSorting(SpriteRenderer renderer, int layer, int order)
    {
        renderer.sortingLayerID = layer;
        renderer.sortingOrder = order;
    }

    private void EnsureSharedAssets()
    {
        if (usesSharedAssets) return;
        Acquire(true);
        usesSharedAssets = true;
    }
}
