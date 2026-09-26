using System;
using System.Collections.Generic;
using Coffee.UIEffects;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

// 선택창마다 하나씩 자동 생성합니다. 실제 덱 변경은 선택창이 담당합니다.
public sealed class DiceSelectionFeedback : MonoBehaviour
{
    [Header("코팅 연출")]
    public float coatingDuration = 0.7f;
    public float coatingScale = 1.2f;
    [Header("삭제 연출")]
    public float destructionDuration = 0.7f;
    public float shakeAmount = 4f;

    private readonly Image[] sparks = new Image[10];
    private Sequence sequence;
    private Texture2D burnTexture;
    private Texture2D shineTexture;
    private DeckSlot selected;
    private bool wasEnabled;
    private Action completed;

    public static DiceSelectionFeedback Get(Component owner)
    {
        var feedback = owner.GetComponent<DiceSelectionFeedback>();
        return feedback != null ? feedback : owner.gameObject.AddComponent<DiceSelectionFeedback>();
    }

    private UIEffect Begin(DeckSlot slot, List<GameObject> slots, Action onComplete)
    {
        selected = slot;
        completed = onComplete;
        foreach (var go in slots)
        {
            if (go == null) continue;
            var button = go.GetComponent<Button>();
            if (button != null) button.interactable = false;
            var group = go.GetComponent<CanvasGroup>();
            if (group == null) group = go.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.alpha = slot != null && go == slot.gameObject ? 1f : 0.25f;
        }
        if (slot == null || slot.diceIcon == null) return null;
        wasEnabled = slot.enabled;
        slot.enabled = false; // 눈금·프리즘 색상 갱신이 연출과 충돌하지 않도록 잠시 정지
        var effect = slot.diceIcon.GetComponent<UIEffect>();
        if (effect == null) effect = slot.diceIcon.gameObject.AddComponent<UIEffect>();
        effect.enabled = true;
        effect.colorFilter = ColorFilter.Additive;
        effect.colorIntensity = 0f;
        effect.transitionReverse = false;
        effect.transitionRate = 0f;
        sequence = DOTween.Sequence().SetUpdate(true);
        sequence.OnComplete(Finish).OnKill(Finish);
        return effect;
    }

    public void PlayCoating(DeckSlot slot, List<GameObject> slots, DiceType type, Color color, Action reveal, Action onComplete)
    {
        UIEffect effect = Begin(slot, slots, onComplete);
        if (effect == null) { reveal?.Invoke(); Finish(); return; }
        float duration = Mathf.Max(0.1f, coatingDuration);
        RectTransform icon = slot.diceIcon.rectTransform;
        Vector3 scale = icon.localScale;
        Color light = type == DiceType.Dark ? new Color(0.6f, 0.35f, 1f) : Color.Lerp(color, Color.white, 0.4f);
        effect.color = light;
        effect.transitionFilter = TransitionFilter.Shiny;
        effect.transitionTexture = GetShineTexture();
        effect.transitionWidth = 0.25f;
        effect.transitionSoftness = 0.15f;
        effect.transitionColorFilter = ColorFilter.Additive;
        effect.transitionColor = light;
        sequence.Append(icon.DOScale(scale * coatingScale, duration * 0.4f).SetEase(Ease.OutQuad));
        sequence.Join(DOTween.To(() => effect.colorIntensity, v => effect.colorIntensity = v, 0.7f, duration * 0.4f));
        sequence.AppendCallback(() => reveal?.Invoke());
        sequence.Append(icon.DOScale(scale, duration * 0.6f).SetEase(Ease.OutBack));
        sequence.Join(DOTween.To(() => effect.colorIntensity, v => effect.colorIntensity = v, 0f, duration * 0.6f));
        sequence.Insert(0f, DOTween.To(() => effect.transitionRate, v => effect.transitionRate = v, 1f, duration).SetEase(Ease.Linear));
        if (type == DiceType.Prism)
        {
            sequence.Insert(0f, DOTween.To(() => 0f, h => effect.transitionColor = Color.HSVToRGB(h, 0.65f, 1f), 1f, duration).SetEase(Ease.Linear));
        }
        AddSparks(icon, light, false, duration * 0.4f, duration * 0.6f);
    }

    public void PlayDestruction(DeckSlot slot, List<GameObject> slots, Action onComplete)
    {
        UIEffect effect = Begin(slot, slots, onComplete);
        if (effect == null) { Finish(); return; }
        float duration = Mathf.Max(0.1f, destructionDuration);
        RectTransform icon = slot.diceIcon.rectTransform;
        Vector2 position = icon.anchoredPosition;
        effect.color = new Color(1f, 0.3f, 0.05f);
        effect.transitionFilter = TransitionFilter.Dissolve;
        effect.transitionTexture = GetBurnTexture();
        effect.transitionWidth = 0.12f;
        effect.transitionSoftness = 0.02f;
        effect.transitionColorFilter = ColorFilter.Replace;
        effect.transitionColor = new Color(1f, 0.5f, 0.05f);
        // 게임의 난수 추첨 순서를 바꾸지 않도록 사인 함수로 흔듭니다.
        sequence.Append(DOTween.To(() => 0f, t => icon.anchoredPosition = position + new Vector2(Mathf.Sin(t * Mathf.PI * 6f) * shakeAmount * (1f - t), 0f), 1f, duration * 0.2f).SetEase(Ease.Linear));
        sequence.Join(DOTween.To(() => effect.colorIntensity, v => effect.colorIntensity = v, 0.6f, duration * 0.2f));
        sequence.Append(DOTween.To(() => effect.transitionRate, v => effect.transitionRate = v, 1f, duration * 0.8f).SetEase(Ease.Linear));
        sequence.Join(DOTween.To(() => effect.colorIntensity, v => effect.colorIntensity = v, 0f, duration * 0.6f));
        var visual = slot.filledVisual.GetComponent<CanvasGroup>();
        if (visual == null) visual = slot.filledVisual.AddComponent<CanvasGroup>();
        sequence.Insert(duration * 0.75f, visual.DOFade(0f, duration * 0.25f));
        AddSparks(icon, new Color(1f, 0.65f, 0.12f), true, duration * 0.2f, duration * 0.8f);
    }

    private void AddSparks(RectTransform icon, Color color, bool burning, float start, float duration)
    {
        float width = Mathf.Max(20f, icon.rect.width);
        for (int i = 0; i < sparks.Length; i++)
        {
            if (sparks[i] == null)
            {
                var go = new GameObject("FeedbackPixel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                sparks[i] = go.GetComponent<Image>();
                sparks[i].raycastTarget = false;
            }
            Image image = sparks[i];
            RectTransform rect = image.rectTransform;
            rect.SetParent(icon, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = Vector2.one * Mathf.Max(2f, width * 0.045f);
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            float angle = i * Mathf.PI * 2f / sparks.Length;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            rect.anchoredPosition = burning ? new Vector2(direction.x * width * 0.3f, -width * 0.1f) : direction * width * 0.25f;
            Vector2 destination = rect.anchoredPosition + (burning ? new Vector2(direction.x * width * 0.18f, width * (0.6f + i * 0.035f)) : direction * width * 0.35f);
            image.color = new Color(color.r, color.g, color.b, 0f);
            image.gameObject.SetActive(true);
            sequence.InsertCallback(start, () => image.color = color);
            sequence.Insert(start, rect.DOAnchorPos(destination, duration).SetEase(Ease.OutQuad));
            sequence.Insert(start, rect.DOScale(0.15f, duration));
            sequence.Insert(start, DOTween.To(() => 1f, alpha => image.color = new Color(color.r, color.g, color.b, alpha), 0f, duration));
        }
    }

    private Texture2D GetBurnTexture()
    {
        if (burnTexture != null) return burnTexture;
        const int size = 32;
        burnTexture = new Texture2D(size, size, TextureFormat.RGBA32, false, true) { name = "DiceBurnMask", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float edge = Mathf.Max(Mathf.Abs((x + 0.5f) / size * 2f - 1f), Mathf.Abs((y + 0.5f) / size * 2f - 1f));
                uint hash = unchecked((uint)(x * 374761393 + y * 668265263));
                hash = (hash ^ (hash >> 13)) * 1274126177u;
                float noise = (hash & 255) / 255f;
                byte value = (byte)Mathf.RoundToInt(Mathf.Clamp01((1f - edge) * 0.8f + noise * 0.2f) * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, value);
            }
        burnTexture.SetPixels32(pixels);
        burnTexture.Apply(false, true);
        return burnTexture;
    }

    private Texture2D GetShineTexture()
    {
        if (shineTexture != null) return shineTexture;
        shineTexture = new Texture2D(32, 1, TextureFormat.RGBA32, false, true) { name = "DiceShineMask", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color32[32];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, (byte)(i * 255 / 31));
        shineTexture.SetPixels32(pixels);
        shineTexture.Apply(false, true);
        return shineTexture;
    }

    private void Finish()
    {
        Action callback = completed;
        completed = null; // OnComplete와 OnKill이 모두 호출되어도 한 번만 종료
        if (callback == null) return;
        if (selected != null) selected.enabled = wasEnabled;
        foreach (var spark in sparks)
        {
            if (spark == null) continue;
            spark.gameObject.SetActive(false);
            spark.transform.SetParent(transform, false);
        }
        callback.Invoke();
    }

    private void OnDisable() { sequence?.Kill(); Finish(); }
    private void OnDestroy()
    {
        sequence?.Kill();
        if (burnTexture != null) Destroy(burnTexture);
        if (shineTexture != null) Destroy(shineTexture);
    }
}
