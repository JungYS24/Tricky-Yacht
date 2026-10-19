using UnityEngine;
using UnityEngine.UI;
using static SatelliteVisualResources;

// Image 기반 선택창용 표시. 전투와 같은 프로필·궤도·공유 텍스처를 사용합니다.
[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
public sealed class SatelliteUIVisual : MonoBehaviour
{
    private Image diceIcon;
    private DiceData1 data;
    private SatelliteVisualProfile profile;
    private RectTransform effectsRoot;
    private bool acquired;
    private float angle;
    private float phase;
    private readonly Orbit[] orbits = new Orbit[4];

    private sealed class Orbit
    {
        public RectTransform root;
        public Image star;
        public Image glow;
        public Image[] dots = System.Array.Empty<Image>();
        public SatelliteType type;
        public float appearedAt;
        public bool assigned;
    }

    private void Awake()
    {
        diceIcon = GetComponent<Image>();
        profile = Profile;
    }

    public void Bind(DiceData1 value)
    {
        if (data != value) Hide();
        data = value;
        if (value == null) Hide();
    }

    private void Hide()
    {
        if (effectsRoot != null) effectsRoot.gameObject.SetActive(false);
        for (int i = 0; i < orbits.Length; i++) if (orbits[i] != null) orbits[i].assigned = false;
    }

    private void OnDisable() => Hide();

    private void OnDestroy()
    {
        if (effectsRoot != null) Destroy(effectsRoot.gameObject);
        if (acquired) Release();
    }

    private void LateUpdate()
    {
        var satellites = data != null ? data.activeSatellites : null;
        int count = satellites != null ? Mathf.Min(satellites.Count, orbits.Length) : 0;
        if (count == 0 || profile == null || diceIcon == null || !diceIcon.enabled || diceIcon.sprite == null)
        {
            Hide();
            return;
        }
        if (!acquired) { Acquire(false); acquired = true; }
        if (effectsRoot == null) effectsRoot = CreateRoot("SatelliteStarUI", transform);
        effectsRoot.gameObject.SetActive(true);

        // 일시정지된 덱 창에서도 동작하며 게임 난수를 소비하지 않습니다.
        angle = Mathf.Repeat(angle - Mathf.Abs(profile.clockwiseDegreesPerSecond) * Time.unscaledDeltaTime, 360f);
        phase = Mathf.Repeat(phase + Time.unscaledDeltaTime * Mathf.PI * 2f / Mathf.Max(0.2f, profile.twinkleSeconds), Mathf.PI * 2f);
        Rect rect = diceIcon.GetPixelAdjustedRect();
        Vector2 size = rect.size;
        Vector2 center = rect.center;
        if (diceIcon.preserveAspect && size.x > 0f && size.y > 0f)
        {
            float aspect = diceIcon.sprite.rect.width / Mathf.Max(1f, diceIcon.sprite.rect.height);
            Vector2 fitted = size;
            if (aspect > size.x / size.y) fitted.y = size.x / aspect;
            else fitted.x = size.y * aspect;
            center += Vector2.Scale(size - fitted, new Vector2(0.5f, 0.5f) - diceIcon.rectTransform.pivot);
            size = fitted;
        }
        center += Vector2.Scale(size, profile.centerOffsetRatio);
        // effectsRoot의 원점은 부모 rect의 중앙입니다. 비중앙 Pivot도 보정합니다.
        effectsRoot.anchoredPosition = center - diceIcon.rectTransform.rect.center;
        Vector2 radius = new Vector2(size.x * Mathf.Max(0.1f, profile.radiusRatio.x), size.y * Mathf.Max(0.1f, profile.radiusRatio.y));
        float dieSize = Mathf.Min(size.x, size.y);
        float starSize = dieSize * Mathf.Max(0.01f, profile.starSizeRatio);
        int dotCount = Mathf.Clamp(profile.trailDotCount, 0, 24);
        float visibility = diceIcon.color.a;

        for (int i = 0; i < orbits.Length; i++)
        {
            if (i >= count)
            {
                if (orbits[i] != null) { orbits[i].root.gameObject.SetActive(false); orbits[i].assigned = false; }
                continue;
            }
            Orbit orbit = orbits[i] ?? (orbits[i] = CreateOrbit());
            SatelliteType type = satellites[i];
            Sprite sprite = profile.GetStar(type);
            if (!orbit.assigned || orbit.type != type || orbit.star.sprite != sprite)
            {
                orbit.type = type;
                orbit.star.sprite = sprite;
                orbit.appearedAt = Time.unscaledTime;
                orbit.assigned = true;
            }
            orbit.root.gameObject.SetActive(sprite != null);
            if (sprite == null) continue;
            EnsureDots(orbit, dotCount);
            float degrees = angle + StartAngle(type);
            float pulse = 0.5f + 0.5f * Mathf.Sin(phase + (int)type * 1.7f);
            Color light = profile.GetLight(type);
            Vector2 position = OrbitPosition(Vector3.zero, radius, degrees);
            orbit.root.anchoredPosition = position;
            Vector2 spriteSize = sprite.rect.size;
            orbit.star.rectTransform.sizeDelta = spriteSize * (starSize * (1f + pulse * profile.starPulse) / Mathf.Max(1f, Mathf.Max(spriteSize.x, spriteSize.y)));
            orbit.star.color = new Color(1f, 1f, 1f, visibility);
            orbit.glow.rectTransform.sizeDelta = Vector2.one * starSize * profile.glowSize * (0.9f + 0.1f * pulse);
            orbit.glow.color = new Color(light.r, light.g, light.b, visibility * profile.glowAlpha * (0.65f + 0.35f * pulse));
            float warmup = Mathf.Clamp01((Time.unscaledTime - orbit.appearedAt) / 0.45f);
            for (int j = 0; j < orbit.dots.Length; j++)
            {
                Image dot = orbit.dots[j];
                dot.enabled = j < dotCount;
                if (j >= dotCount) continue;
                float progress = (j + 1f) / (dotCount + 1f);
                dot.rectTransform.anchoredPosition = (Vector2)OrbitPosition(Vector3.zero, radius, degrees + progress * profile.trailArcDegrees) - position;
                dot.rectTransform.sizeDelta = Vector2.one * dieSize * profile.dotSizeRatio * (1f - 0.45f * progress);
                float sparkle = 0.8f + 0.2f * Mathf.Sin(phase * 1.3f + j * 2.1f + i);
                float alpha = visibility * profile.trailAlpha * (1f - progress * 0.85f) * sparkle * Mathf.Clamp01(warmup * 2f - progress);
                dot.color = new Color(Mathf.Lerp(light.r, 1f, 0.45f), Mathf.Lerp(light.g, 1f, 0.45f), Mathf.Lerp(light.b, 1f, 0.45f), alpha);
            }
        }
    }

    private Orbit CreateOrbit()
    {
        RectTransform root = CreateRoot("StarOrbit", effectsRoot);
        Image glow = CreateImage("Glow", root, glowSprite);
        Image star = CreateImage("Star", root, null);
        return new Orbit { root = root, star = star, glow = glow };
    }

    private void EnsureDots(Orbit orbit, int count)
    {
        if (orbit.dots.Length >= count) return;
        int oldCount = orbit.dots.Length;
        System.Array.Resize(ref orbit.dots, count);
        for (int i = oldCount; i < count; i++)
        {
            orbit.dots[i] = CreateImage("OrbitDot", orbit.root, dotSprite);
            orbit.dots[i].transform.SetAsFirstSibling();
        }
    }

    private static RectTransform CreateRoot(string objectName, Transform parent)
    {
        var go = new GameObject(objectName, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = Vector2.zero;
        return rect;
    }

    private static Image CreateImage(string objectName, Transform parent, Sprite sprite)
    {
        var rect = CreateRoot(objectName, parent);
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        // 기본 UI 머티리얼로 CanvasGroup, 마스크, 클릭 입력을 기존 창과 맞춥니다.
        return image;
    }
}
