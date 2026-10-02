using UnityEngine;
using DG.Tweening;

public class SatelliteVisualManager : MonoBehaviour
{
    [Header("별 위성 공용 설정 (비어 있으면 Resources 기본값 사용)")]
    [SerializeField] private SatelliteVisualProfile visualProfile;

    // 이전 프리팹 직렬화 호환용 필드. 새 궤도/잔상 수치는 위 공용 설정을 사용합니다.
    [HideInInspector] public Sprite mercurySprite; // 수성
    [HideInInspector] public Sprite venusSprite;   // 금성
    [HideInInspector] public Sprite marsSprite;    // 화성
    [HideInInspector] public Sprite jupiterSprite; // 목성

    [HideInInspector] public float orbitSpeed = 3f;      // 공전 속도
    [HideInInspector] public float orbitWidth = 1.2f;    // 궤도 가로폭
    [HideInInspector] public float orbitHeight = 0.35f;  // 궤도 세로폭

    // 주사위 이미지의 기준점(Pivot) 차이로 인해 궤도가 쏠리는 현상을 보정합니다.
    // 인스펙터에서 X, Y 값을 조금씩 조절하며 정중앙을 맞춰보세요.
    [HideInInspector] public Vector3 centerOffset = new Vector3(0f, 0.2f, 0f);

    [HideInInspector] public float frontScale = 0.4f;    // 앞으로 올 때 크기
    [HideInInspector] public float backScale = 0.2f;     // 뒤로 갈 때 크기
    [HideInInspector] public float backDarkness = 0.4f;  // 뒤로 갈 때 어두워지는 정도

    [HideInInspector] public Material trailMaterial;     // 꼬리에 쓰일 재질 (인스펙터 할당 권장)
    [HideInInspector] public float trailTime = 0.4f;     // 꼬리가 유지되는 시간 (길이)
    [HideInInspector] public float trailStartWidth = 0.15f; // 꼬리 시작 두께
    [HideInInspector] public float trailEndWidth = 0.0f;    // 꼬리 끝 두께


    private Dice dice;
    private SpriteRenderer diceSpriteRenderer;
    private Transform effectsRoot;
    private readonly SatelliteInstance[] instances = new SatelliteInstance[4];
    private float orbitAngle;
    private float twinklePhase;
    private Tween twinkleTween;
    private float tweenPeriod;
    private bool usesSharedAssets;

    private static SatelliteVisualProfile sharedProfile;
    private static Sprite glowSprite;
    private static Sprite dotSprite;
    private static Material effectMaterial;
    private static int sharedUsers;

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
        if (visualProfile == null)
        {
            if (sharedProfile == null) sharedProfile = Resources.Load<SatelliteVisualProfile>("SatelliteVisualProfile");
            visualProfile = sharedProfile;
        }
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
        if (!usesSharedAssets || --sharedUsers != 0) return;
        // 런타임 생성 리소스는 마지막 사용자가 사라질 때 반환합니다. 원본 PNG/설정 에셋은 유지합니다.
        if (glowSprite != null) { Destroy(glowSprite.texture); Destroy(glowSprite); }
        if (dotSprite != null) { Destroy(dotSprite.texture); Destroy(dotSprite); }
        if (effectMaterial != null) Destroy(effectMaterial);
        glowSprite = null;
        dotSprite = null;
        effectMaterial = null;
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

    private static Vector3 OrbitPosition(Vector3 center, Vector2 radius, float degrees)
    {
        float angle = degrees * Mathf.Deg2Rad;
        return center + new Vector3(Mathf.Cos(angle) * radius.x, Mathf.Sin(angle) * radius.y, 0f);
    }

    private static float StartAngle(SatelliteType type)
    {
        switch (type)
        {
            case SatelliteType.Mercury: return 45f;
            case SatelliteType.Mars: return 135f;
            case SatelliteType.Venus: return 225f;
            case SatelliteType.Jupiter: return 315f;
            default: return 0f;
        }
    }

    private void EnsureSharedAssets()
    {
        if (usesSharedAssets) return;
        usesSharedAssets = true;
        sharedUsers++;
        if (glowSprite == null) glowSprite = CreateRadialSprite(64, false);
        if (dotSprite == null) dotSprite = CreateRadialSprite(16, true);
        if (effectMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null) effectMaterial = new Material(shader) { name = "Satellite Shared Unlit", hideFlags = HideFlags.DontSave };
        }
    }

    private static Sprite CreateRadialSprite(int size, bool dot)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = dot ? "SatelliteDot" : "SatelliteGlow", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = (x + 0.5f) / size * 2f - 1f;
            float dy = (y + 0.5f) / size * 2f - 1f;
            float distance = Mathf.Sqrt(dx * dx + dy * dy);
            float alpha = dot ? 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 0.95f, distance)) : Mathf.Pow(Mathf.Clamp01(1f - distance), 2f);
            pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, true); // CPU 픽셀 복사본은 해제하고 GPU 텍스처만 공유합니다.
        var sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size, 0, SpriteMeshType.FullRect);
        sprite.hideFlags = HideFlags.DontSave;
        return sprite;
    }
}
