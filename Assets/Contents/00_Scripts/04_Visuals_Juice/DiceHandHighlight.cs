using Coffee.UIEffects;
using UnityEngine;
using UnityEngine.UI;

// 본체·코팅 재질과 분리된 월드 공간 UI, 콜라이더/레이캐스터는 x.
public sealed class DiceHandHighlight : MonoBehaviour
{
    private static Sprite ringSprite, glowSprite;
    private static Texture2D sweepTexture;
    private static int users;
    private Image ring, glow, dimOverlay;
    private UIEffect shine;
    private Vector3 position, scale;
    private bool captured, registered;
    public float Height { get; private set; } = 1f;

    public static DiceHandHighlight Get(Dice dice)
    {
        var item = dice.GetComponent<DiceHandHighlight>();
        return item != null ? item : dice.gameObject.AddComponent<DiceHandHighlight>();
    }

    public void Prepare()
    {

        Restore();
        var renderer = GetComponent<SpriteRenderer>();
        if (renderer == null || renderer.sprite == null) return;
        if (!registered) { BuildTextures(); users++; registered = true; }
        if (ring == null)
        {
            ring = MakeImage("HandBorder", ringSprite);
            glow = MakeImage("HandBackLight", glowSprite);
            shine = ring.gameObject.AddComponent<UIEffect>();
            shine.transitionFilter = TransitionFilter.Shiny;
            shine.transitionTexture = sweepTexture;
            shine.transitionWidth = 0.25f;
            shine.transitionSoftness = 0.15f;
            shine.transitionColorFilter = ColorFilter.Additive;
            shine.transitionColor = Color.white;
        }
        ring.gameObject.SetActive(true);
        if (dimOverlay == null) dimOverlay = MakeImage("HandDimOverlay", renderer.sprite);
        dimOverlay.sprite = renderer.sprite;
        dimOverlay.gameObject.SetActive(true);
        dimOverlay.color = Color.clear;
        glow.gameObject.SetActive(true);
        Bounds bounds = renderer.sprite.bounds;
        Vector2 size = new Vector2(Mathf.Max(0.1f, bounds.size.x), Mathf.Max(0.1f, bounds.size.y));
        Setup(ring, bounds.center, size * 1.12f, renderer.sortingLayerID, renderer.sortingOrder + 1);
        Setup(glow, bounds.center, size * 1.9f, renderer.sortingLayerID, renderer.sortingOrder - 1);
        Setup(dimOverlay, bounds.center, size, renderer.sortingLayerID, renderer.sortingOrder + 2);
        Height = Mathf.Max(0.1f, renderer.bounds.size.y);
        position = transform.position;
        scale = transform.localScale;
        captured = true;
        Draw(0f, 0f, 0f, 1f, 0f, 1f);
    }

    private Image MakeImage(string imageName, Sprite sprite)
    {
        var go = new GameObject(imageName, typeof(RectTransform), typeof(Canvas), typeof(CanvasRenderer), typeof(Image));
        go.layer = gameObject.layer;
        go.transform.SetParent(transform, false);
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        image.color = Color.clear;
        return image;
    }

    private static void Setup(Image image, Vector3 center, Vector2 size, int layer, int order)
    {
        var rect = image.rectTransform;
        rect.localPosition = center;
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;
        rect.sizeDelta = size;
        var canvas = image.GetComponent<Canvas>();
        canvas.sortingLayerID = layer;
        canvas.sortingOrder = order;
    }

    // 원래 위치·크기를 기준으로 계산하여 확대·이동이 누적되지 않음.
    public void Draw(float light, float sweep, float backLight, float size, float lift, float spread)
    {
        if (!captured || ring == null) return;
        ring.color = new Color(1f, 0.96f, 0.75f, Mathf.Clamp01(light));
        glow.color = new Color(1f, 0.82f, 0.35f, Mathf.Clamp01(backLight));
        glow.rectTransform.localScale = Vector3.one * spread;
        shine.transitionRate = Mathf.Clamp01(sweep);
        transform.localScale = scale * size;
        transform.position = position + Vector3.up * (Height * lift);
    }

    public void SetDim(float amount)
    {
        if (dimOverlay != null) dimOverlay.color = new Color(0f, 0f, 0f, Mathf.Clamp01(amount));
    }

    public void Restore()
    {
        if (captured) { transform.position = position; transform.localScale = scale; }
        captured = false;
        if (dimOverlay != null) { dimOverlay.color = Color.clear; dimOverlay.gameObject.SetActive(false); }
        if (ring != null) { ring.color = Color.clear; ring.gameObject.SetActive(false); }
        if (glow != null) { glow.color = Color.clear; glow.gameObject.SetActive(false); }
    }

    private static Texture2D Texture(int w, int h, FilterMode filter)
    {
        return new Texture2D(w, h, TextureFormat.RGBA32, false, true) { filterMode = filter, wrapMode = TextureWrapMode.Clamp };
    }

    private static void BuildTextures()
    {
        if (ringSprite != null) return;
        const int n = 64;
        var ring = Texture(n, n, FilterMode.Point);
        var glow = Texture(n, n, FilterMode.Bilinear);
        var a = new Color32[n * n];
        var b = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float px = Mathf.Abs((x + 0.5f) / n * 2f - 1f), py = Mathf.Abs((y + 0.5f) / n * 2f - 1f);
                Vector2 q = new Vector2(px - 0.72f, py - 0.72f);
                float d = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - 0.2f;
                a[y * n + x] = new Color32(255, 255, 255, d <= 0f && d >= -0.085f ? (byte)255 : (byte)0);
                float alpha = 1f - Mathf.SmoothStep(0.35f, 1f, Mathf.Max(px, py));
                b[y * n + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
            }
        ring.SetPixels32(a); ring.Apply(false, true);
        glow.SetPixels32(b); glow.Apply(false, true);
        ringSprite = Sprite.Create(ring, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
        glowSprite = Sprite.Create(glow, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
        sweepTexture = Texture(32, 1, FilterMode.Bilinear);
        var gradient = new Color32[32];
        for (int i = 0; i < gradient.Length; i++) gradient[i] = new Color32(255, 255, 255, (byte)(i * 255 / 31));
        sweepTexture.SetPixels32(gradient); sweepTexture.Apply(false, true);
    }

    private void OnDisable() => Restore();
    private void OnDestroy()
    {
        Restore();
        if (!registered || --users > 0) return;
        if (ringSprite != null) { Destroy(ringSprite.texture); Destroy(ringSprite); }
        if (glowSprite != null) { Destroy(glowSprite.texture); Destroy(glowSprite); }
        if (sweepTexture != null) Destroy(sweepTexture);
        ringSprite = null; glowSprite = null; sweepTexture = null;
    }
}
