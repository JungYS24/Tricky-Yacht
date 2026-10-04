using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

[DisallowMultipleComponent]
public sealed class FinalDamageFeedback : MonoBehaviour
{
    private TextMeshProUGUI label;
    private float baseFont, minFont, maxFont;
    private bool autoSize, wrapping, prepared;
    private TextOverflowModes overflow;
    private Vector2 baseSize;
    private Vector3 baseScale;
    private RawImage glow;
    private Texture2D glowTexture;
    private Sequence pulse;

    public static FinalDamageFeedback Get(TextMeshProUGUI text)
    {
        var effect = text.GetComponent<FinalDamageFeedback>();
        return effect != null ? effect : text.gameObject.AddComponent<FinalDamageFeedback>();
    }

    public void Prepare(float fontScale, float widthScale)
    {
        Restore();
        label = GetComponent<TextMeshProUGUI>();
        baseFont = label.fontSize;
        minFont = label.fontSizeMin;
        maxFont = label.fontSizeMax;
        autoSize = label.enableAutoSizing;
        wrapping = label.enableWordWrapping;
        overflow = label.overflowMode;
        baseSize = label.rectTransform.sizeDelta;
        baseScale = label.transform.localScale;
        prepared = true;
        float width = label.rectTransform.rect.width * Mathf.Max(1f, widthScale);
        if (label.transform.parent is RectTransform parent)
            width = Mathf.Min(width, Mathf.Max(label.rectTransform.rect.width, parent.rect.width * 0.9f));
        label.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        label.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, label.rectTransform.rect.height * Mathf.Max(1f, fontScale));
        label.enableWordWrapping = false;
        label.enableAutoSizing = true;
        label.fontSizeMin = Mathf.Min(baseFont, minFont);
        label.fontSizeMax = baseFont * Mathf.Max(1f, fontScale);
        label.fontSize = label.fontSizeMax;
        label.overflowMode = TextOverflowModes.Overflow;
    }

    public void Pulse(float scale, float hold)
    {
        if (!prepared || hold <= 0f) return;
        EnsureGlow();
        glow.rectTransform.anchorMin = label.rectTransform.anchorMin;
        glow.rectTransform.anchorMax = label.rectTransform.anchorMax;
        glow.rectTransform.pivot = label.rectTransform.pivot;
        glow.rectTransform.sizeDelta = label.rectTransform.sizeDelta;
        glow.rectTransform.anchoredPosition3D = label.rectTransform.anchoredPosition3D;
        glow.rectTransform.localScale = label.rectTransform.localScale;
        glow.gameObject.SetActive(true);
        glow.color = new Color(1f, 0.8f, 0.25f, 0.45f);
        float duration = Mathf.Min(0.25f, hold);
        pulse = DOTween.Sequence().SetUpdate(true);
        pulse.Append(label.transform.DOScale(baseScale * scale, duration * 0.3f).SetEase(Ease.OutQuad));
        pulse.Append(label.transform.DOScale(baseScale, duration * 0.7f).SetEase(Ease.OutQuad));
        pulse.Insert(0f, glow.DOFade(0f, duration));
        pulse.OnComplete(() => { if (glow != null) glow.gameObject.SetActive(false); });
    }

    public void Restore()
    {
        pulse?.Kill();
        pulse = null;
        if (glow != null) glow.gameObject.SetActive(false);
        if (!prepared || label == null) return;
        label.fontSize = baseFont;
        label.fontSizeMin = minFont;
        label.fontSizeMax = maxFont;
        label.enableAutoSizing = autoSize;
        label.enableWordWrapping = wrapping;
        label.overflowMode = overflow;
        label.rectTransform.sizeDelta = baseSize;
        label.transform.localScale = baseScale;
        prepared = false;
    }

    private void EnsureGlow()
    {
        if (glow != null) return;
        glowTexture = new Texture2D(32, 32, TextureFormat.RGBA32, false) { hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color32[32 * 32];
        for (int y = 0; y < 32; y++)
        for (int x = 0; x < 32; x++)
        {
            float dx = (x + 0.5f) / 16f - 1f, dy = (y + 0.5f) / 16f - 1f;
            float alpha = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy)), 2f);
            pixels[y * 32 + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
        }
        glowTexture.SetPixels32(pixels);
        glowTexture.Apply(false, true);
        var go = new GameObject("FinalDamageGlow", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage), typeof(LayoutElement));
        go.layer = gameObject.layer;
        go.transform.SetParent(transform.parent, false);
        go.transform.SetSiblingIndex(transform.GetSiblingIndex());
        go.GetComponent<LayoutElement>().ignoreLayout = true;
        glow = go.GetComponent<RawImage>();
        glow.texture = glowTexture;
        glow.raycastTarget = false;
    }

    private void OnDisable() => Restore();
    private void OnDestroy()
    {
        Restore();
        if (glow != null) Destroy(glow.gameObject);
        if (glowTexture != null) Destroy(glowTexture);
    }
}
