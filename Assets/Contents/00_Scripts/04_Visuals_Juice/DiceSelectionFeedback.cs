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
    private Texture2D shadowTexture;
    private Sprite shadowSprite;
    private DeckSlot selected;
    private bool wasEnabled;
    private Action completed;
    private Action cleanup; // 연출마다 따로 만든 오브젝트·바꾼 값 정리(도장 연출)

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
        // 연출이 눈에 보이기 시작하는 순간(early return 포함)에 한 번 재생한다.
        Sfx.Play(SfxId.Coating_Apply);
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

    // 도장 찍기: 나무 도장이 비스듬히 떠올라 조준하듯 흔들리다 똑바로 서서 쾅 내려찍고,
    // 진하게 찍힌 잉크가 마르며 연한 자국만 남는다. 이미지·시간은 StampVisualProfile에서 조절한다.
    // 흔들림·잉크 방울은 무작위 없이 사인 함수와 고정 각도로 만든다.
    public void PlayStamp(DeckSlot slot, List<GameObject> slots, StampType type, Action reveal, Action onComplete)
    {
        UIEffect effect = Begin(slot, slots, onComplete);
        if (effect == null)
        {
            Sfx.Play(SfxId.Stamp_Apply);
            reveal?.Invoke();
            Finish();
            return;
        }
        StampVisualProfile profile = StampVisualProfile.Profile;
        Sprite mark = StampVisualProfile.GetSprite(type);
        if (profile == null || mark == null)
        {
            Sfx.Play(SfxId.Stamp_Apply);
            sequence.AppendCallback(() => reveal?.Invoke());
            return;
        }

        RectTransform icon = slot.diceIcon.rectTransform;
        Vector3 baseScale = icon.localScale;
        float width = Mathf.Max(20f, icon.rect.width);
        float height = Mathf.Max(20f, icon.rect.height);
        Color ink = profile.GetInk(type);
        float speed = Mathf.Max(0.2f, profile.durationScale);
        float sealPx = width * profile.sealSize;
        float facePx = sealPx * profile.sealFaceWidth;

        RectTransform layer = CreateStampLayer(icon);

        // 갓 찍힌 진한 잉크. 실제 연한 자국과 같은 자리에 겹쳐 두었다가 마르면서 사라진다.
        Image fresh = CreateImage("FreshInk", layer, mark);
        StampUIVisual.PlaceMark(fresh.rectTransform, profile);
        fresh.color = new Color(1f, 1f, 1f, 0f);

        // 도장 그림자: 도장이 내려올수록 커지고 진해져 높이감을 준다.
        Image shadow = CreateImage("SealShadow", layer, GetShadowSprite());
        shadow.preserveAspect = false;
        RectTransform shadowRect = shadow.rectTransform;
        shadowRect.anchorMin = shadowRect.anchorMax = shadowRect.pivot = new Vector2(0.5f, 0.5f);
        shadowRect.sizeDelta = new Vector2(facePx, facePx * 0.3f);
        shadowRect.localScale = Vector3.one * 0.5f;
        shadow.color = new Color(0f, 0f, 0f, 0f);

        // 도장 본체. 기준점을 찍는 면 바닥에 두어 눌릴 때 바닥이 주사위에 붙은 채로 납작해진다.
        Image seal = CreateImage("Seal", layer, profile.sealTool);
        seal.enabled = profile.sealTool != null;
        RectTransform sealRect = seal.rectTransform;
        sealRect.anchorMin = sealRect.anchorMax = new Vector2(0.5f, 0.5f);
        sealRect.pivot = new Vector2(0.5f, 0.5f - profile.sealFaceOffset);
        sealRect.sizeDelta = new Vector2(sealPx, sealPx);
        CanvasGroup sealGroup = seal.gameObject.AddComponent<CanvasGroup>();
        sealGroup.alpha = 0f;

        // 찍는 면 바닥의 잉크 줄 (도장 그림 2픽셀 높이)
        Image inkLine = CreateImage("InkLine", sealRect, null);
        inkLine.preserveAspect = false;
        inkLine.enabled = seal.enabled;
        inkLine.color = ink;
        RectTransform inkRect = inkLine.rectTransform;
        float sealPixel = sealPx / (profile.sealTool != null ? Mathf.Max(1f, profile.sealTool.rect.width) : 64f);
        inkRect.anchorMin = inkRect.anchorMax = new Vector2(0.5f, 0.5f);
        inkRect.pivot = new Vector2(0.5f, 0f);
        inkRect.sizeDelta = new Vector2(facePx, sealPixel * 2f);
        inkRect.anchoredPosition = new Vector2(0f, -sealPx * profile.sealFaceOffset);

        // 찍는 면 바닥이 닿는 높이(주사위 가운데)와 떠 있는 높이
        const float pressY = 0f;
        float hoverY = height * 0.5f;
        sealRect.anchoredPosition = new Vector2(0f, hoverY + height * 0.15f);
        sealRect.localEulerAngles = new Vector3(0f, 0f, profile.liftAngle);
        sealRect.localScale = Vector3.one * 1.12f;

        RectTransform panel = transform as RectTransform;
        Vector2 panelOrigin = panel != null ? panel.anchoredPosition : Vector2.zero;
        cleanup = () =>
        {
            if (icon != null) icon.localScale = baseScale;
            if (panel != null) panel.anchoredPosition = panelOrigin;
            if (effect != null) effect.colorIntensity = 0f;
            if (layer != null) Destroy(layer.gameObject);
        };

        float lift = Mathf.Max(0.01f, profile.liftDuration) * speed;
        float aim = Mathf.Max(0f, profile.aimDuration) * speed;
        float slam = Mathf.Max(0.01f, profile.slamDuration) * speed;
        float hold = Mathf.Max(0f, profile.pressHold) * speed;
        float release = Mathf.Max(0.01f, profile.releaseDuration) * speed;
        float dry = Mathf.Max(0.01f, profile.inkDryDuration) * speed;

        // 1) 비스듬히 나타나며 떠오름
        sequence.Insert(0f, sealRect.DOAnchorPosY(hoverY, lift).SetEase(Ease.OutQuad));
        sequence.Insert(0f, sealGroup.DOFade(1f, lift * 0.6f));
        sequence.Insert(0f, sealRect.DOScale(1f, lift).SetEase(Ease.OutQuad));
        sequence.Insert(0f, shadowRect.DOScale(0.75f, lift));
        sequence.Insert(0f, shadow.DOFade(0.2f, lift));

        // 2) 조준: 반대쪽으로 살짝 흔들렸다가 똑바로 서며 조금 더 들어올림
        if (aim > 0f)
        {
            sequence.Insert(lift, sealRect.DOLocalRotate(new Vector3(0f, 0f, -profile.liftAngle * 0.4f), aim * 0.5f).SetEase(Ease.InOutSine));
            sequence.Insert(lift + aim * 0.5f, sealRect.DOLocalRotate(Vector3.zero, aim * 0.5f).SetEase(Ease.InOutSine));
            sequence.Insert(lift, sealRect.DOAnchorPosY(hoverY + height * 0.06f, aim).SetEase(Ease.OutSine));
        }

        // 3) 쾅: 똑바로 빠르게 내려찍는다
        float slamStart = lift + aim;
        sequence.Insert(slamStart, sealRect.DOAnchorPosY(pressY, slam).SetEase(Ease.InQuad));
        sequence.Insert(slamStart, sealRect.DOLocalRotate(Vector3.zero, slam).SetEase(Ease.InQuad));
        sequence.Insert(slamStart, shadowRect.DOScale(1f, slam).SetEase(Ease.InQuad));
        sequence.Insert(slamStart, shadow.DOFade(0.45f, slam));

        // 4) 찍힌 순간: 진한 잉크를 띄우고 그 아래에서 슬롯을 다시 그려 실제 자국으로 바꾼다.
        //    도장과 주사위가 눌렸다 튀어나오고, 하얗게 번쩍이며, 선택창이 흔들리고, 잉크 방울이 튄다.
        float impact = slamStart + slam;
        sequence.InsertCallback(impact, () =>
        {
            fresh.color = Color.white;
            reveal?.Invoke();
            Sfx.Play(SfxId.Stamp_Apply);
        });
        Vector3 sealPressed = new Vector3(1f + (1f - profile.sealSquash) * 0.5f, profile.sealSquash, 1f);
        sequence.Insert(impact, sealRect.DOScale(sealPressed, 0.06f * speed).SetEase(Ease.OutQuad));
        sequence.Insert(impact + 0.06f * speed, sealRect.DOScale(Vector3.one, 0.22f * speed).SetEase(Ease.OutBack));
        Vector3 pressed = new Vector3(baseScale.x * (1f + (1f - profile.squash) * 0.6f), baseScale.y * profile.squash, baseScale.z);
        sequence.Insert(impact, icon.DOScale(pressed, 0.06f * speed).SetEase(Ease.OutQuad));
        sequence.Insert(impact + 0.06f * speed, icon.DOScale(baseScale, 0.3f * speed).SetEase(Ease.OutBack));
        effect.color = Color.white;
        sequence.Insert(impact, DOTween.To(() => effect.colorIntensity, v => effect.colorIntensity = v, 0.45f, 0.05f * speed));
        sequence.Insert(impact + 0.05f * speed, DOTween.To(() => effect.colorIntensity, v => effect.colorIntensity = v, 0f, 0.25f * speed));
        if (panel != null && profile.shake > 0f)
        {
            float shake = profile.shake;
            sequence.Insert(impact, DOTween.To(() => 0f, t => panel.anchoredPosition = panelOrigin + new Vector2(
                Mathf.Sin(t * Mathf.PI * 5f) * shake * (1f - t),
                Mathf.Sin(t * Mathf.PI * 4f + 1f) * shake * 0.5f * (1f - t)), 1f, 0.22f * speed).SetEase(Ease.Linear));
        }
        AddInk(layer, width, ink, impact, 0.45f * speed);

        // 5) 꾹 누르고 있다가 톡 떼며 사라짐
        float releaseAt = impact + hold;
        sequence.Insert(releaseAt, sealRect.DOAnchorPosY(hoverY + height * 0.1f, release).SetEase(Ease.OutBack));
        sequence.Insert(releaseAt, sealRect.DOLocalRotate(new Vector3(0f, 0f, -profile.liftAngle * 0.3f), release).SetEase(Ease.OutQuad));
        sequence.Insert(releaseAt + release * 0.35f, sealGroup.DOFade(0f, release * 0.65f));
        sequence.Insert(releaseAt, shadow.DOFade(0f, release * 0.6f));
        sequence.Insert(releaseAt, shadowRect.DOScale(0.6f, release * 0.6f));

        // 6) 진한 잉크가 마르며 아래의 연한 자국만 남는다
        sequence.Insert(releaseAt, fresh.DOFade(0f, dry).SetEase(Ease.InQuad));
    }

    // 도장 그림자용 픽셀 타원 (흰색, 코드에서 검게 칠함)
    private Sprite GetShadowSprite()
    {
        if (shadowSprite != null) return shadowSprite;
        const int w = 16, h = 6;
        shadowTexture = new Texture2D(w, h, TextureFormat.RGBA32, false, true) { name = "StampShadow", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dx = (x + 0.5f - w * 0.5f) / (w * 0.5f);
                float dy = (y + 0.5f - h * 0.5f) / (h * 0.5f);
                pixels[y * w + x] = new Color32(255, 255, 255, (byte)(dx * dx + dy * dy <= 1f ? 255 : 0));
            }
        shadowTexture.SetPixels32(pixels);
        shadowTexture.Apply(false, true);
        shadowSprite = Sprite.Create(shadowTexture, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        return shadowSprite;
    }

    // 옆 슬롯과 잉크 방울보다 위에 그리도록 정렬 순서를 따로 가진 연출 층을 아이콘 위에 만든다.
    private static RectTransform CreateStampLayer(RectTransform icon)
    {
        var go = new GameObject("StampFx", typeof(RectTransform));
        go.layer = icon.gameObject.layer;
        var rect = (RectTransform)go.transform;
        rect.SetParent(icon, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        rect.SetAsLastSibling();

        // 실제로 정렬을 정하는 캔버스(루트이거나 정렬을 덮어쓴 캔버스)를 찾아 그 위에 올린다.
        Canvas source = icon.GetComponentInParent<Canvas>();
        while (source != null && !source.isRootCanvas && !source.overrideSorting)
            source = source.transform.parent != null ? source.transform.parent.GetComponentInParent<Canvas>() : null;
        var canvas = go.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        if (source != null)
        {
            canvas.sortingLayerID = source.sortingLayerID;
            canvas.sortingOrder = source.sortingOrder + 10;
        }
        return rect;
    }

    private static Image CreateImage(string objectName, RectTransform parent, Sprite sprite)
    {
        var go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        image.preserveAspect = true;
        return image;
    }

    // 찍는 순간 도장 바닥에서 잉크 방울이 옆으로 납작하게 퍼진다. 주사위 그림 픽셀(아이콘 폭/64) 단위 크기의 네모라 픽셀아트와 어울린다.
    private void AddInk(RectTransform parent, float width, Color color, float start, float duration)
    {
        float pixel = width / 64f;
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
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = Vector2.one * Mathf.Max(2f, pixel * (i % 2 == 0 ? 3f : 2f));
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            float angle = (i + 0.5f) * Mathf.PI * 2f / sparks.Length;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * 0.5f);
            float reach = width * (i % 3 == 0 ? 0.62f : 0.48f);
            rect.anchoredPosition = direction * width * 0.22f;
            Vector2 destination = direction * reach + Vector2.down * width * 0.06f;
            image.color = new Color(color.r, color.g, color.b, 0f);
            image.gameObject.SetActive(true);
            sequence.InsertCallback(start, () => image.color = color);
            sequence.Insert(start, rect.DOAnchorPos(destination, duration).SetEase(Ease.OutCubic));
            sequence.Insert(start + duration * 0.5f, DOTween.To(() => 1f, alpha => image.color = new Color(color.r, color.g, color.b, alpha), 0f, duration * 0.5f));
        }
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
        // 잉크 방울을 먼저 되돌려 둔 뒤 연출 층을 지운다.
        Action clean = cleanup;
        cleanup = null;
        clean?.Invoke();
        callback.Invoke();
    }

    private void OnDisable() { sequence?.Kill(); Finish(); }
    private void OnDestroy()
    {
        sequence?.Kill();
        if (burnTexture != null) Destroy(burnTexture);
        if (shineTexture != null) Destroy(shineTexture);
        if (shadowSprite != null) Destroy(shadowSprite);
        if (shadowTexture != null) Destroy(shadowTexture);
    }
}
