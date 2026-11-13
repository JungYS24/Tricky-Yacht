using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

// 플레이어 방어막 표시와 연출: 체력 숫자 아래 방패+숫자 줄, 얻음/막음 숫자 팝업, 방패 깨짐 조각, 맞은 자리의 큰 방패 이펙트.
// 피격 붉은 화면·체력 흔들기는 HurtVignetteController가 맡고, 이 클래스는 그 컨트롤러가 하나 만들어 들고 있습니다.
// 색·크기·시간 같은 인스펙터 값은 씬에 저장된 HurtVignetteController에 그대로 있고 여기서 읽습니다.
public sealed class PlayerShieldFeedback
{
    readonly HurtVignetteController owner;

    public PlayerShieldFeedback(HurtVignetteController owner)
    {
        this.owner = owner;
    }
    private RectTransform shieldRow;
    private Image shieldIcon;
    private TextMeshProUGUI shieldNumber;
    private TextMeshProUGUI shieldPopup;
    private readonly Image[] shieldShards = new Image[6];
    private Sequence shieldTween;
    private TMP_Text shieldHealthText;
    private Vector4 originalHealthMargin;
    private TextAlignmentOptions originalHealthAlignment;
    private int displayedShield = -1;

    public void UpdateShieldDisplay(UIManager ui, int amount)
    {
        if (ui == null || ui.heartText == null) return;
        if (shieldRow == null) CreateShieldDisplay(ui);
        if (shieldRow == null) return;
        int previous = displayedShield;
        displayedShield = Mathf.Max(0, amount);
        shieldNumber.SetText("{0}", displayedShield);
        ApplyShieldLayout(displayedShield > 0 || shieldTween != null);
        // 최초 로드/같은 값의 UI 갱신에서는 획득 연출을 반복하지 않습니다.
        if (previous >= 0 && displayedShield > previous) PlayShieldFeedback(ui, displayedShield - previous, true, false);
    }

    private void CreateShieldDisplay(UIManager ui)
    {
        Canvas canvas = ui.heartText.GetComponentInParent<Canvas>();
        if (canvas == null) return;
        owner.EnsureVisuals(canvas.rootCanvas, ui.heartText);
        shieldHealthText = ui.heartText;
        originalHealthMargin = shieldHealthText.margin;
        originalHealthAlignment = shieldHealthText.alignment;
        shieldHealthText.fontSizeMax = shieldHealthText.fontSize;
        shieldHealthText.fontSizeMin = 8f;
        Image oldIcon = ui.shieldRoot != null ? ui.shieldRoot.GetComponentInChildren<Image>(true) : null;
        GameObject row = new GameObject("Shield Row", typeof(RectTransform), typeof(LayoutElement));
        shieldRow = row.GetComponent<RectTransform>();
        shieldRow.SetParent(ui.heartText.transform, false);
        row.GetComponent<LayoutElement>().ignoreLayout = true;
        shieldRow.anchorMin = Vector2.zero;
        shieldRow.anchorMax = new Vector2(1f, owner.shieldRowHeightRatio);
        shieldRow.offsetMin = Vector2.zero;
        shieldRow.offsetMax = Vector2.zero;
        GameObject icon = new GameObject("Shield Icon", typeof(RectTransform), typeof(Image));
        icon.transform.SetParent(shieldRow, false);
        shieldIcon = icon.GetComponent<Image>();
        shieldIcon.sprite = oldIcon != null ? oldIcon.sprite : null;
        shieldIcon.preserveAspect = true;
        shieldIcon.raycastTarget = false;
        shieldIcon.color = owner.shieldColor;
        shieldIcon.rectTransform.anchorMin = Vector2.zero;
        shieldIcon.rectTransform.anchorMax = new Vector2(0.22f, 1f);
        shieldIcon.rectTransform.offsetMin = Vector2.zero;
        shieldIcon.rectTransform.offsetMax = Vector2.zero;
        GameObject number = new GameObject("Shield Number", typeof(RectTransform), typeof(TextMeshProUGUI));
        number.transform.SetParent(shieldRow, false);
        shieldNumber = number.GetComponent<TextMeshProUGUI>();
        shieldNumber.font = ui.heartText.font;
        shieldNumber.color = owner.shieldColor;
        shieldNumber.raycastTarget = false;
        shieldNumber.alignment = TextAlignmentOptions.MidlineLeft;
        shieldNumber.enableWordWrapping = false;
        shieldNumber.enableAutoSizing = true;
        shieldNumber.fontSizeMin = 8f;
        shieldNumber.fontSizeMax = ui.heartText.fontSizeMax;
        shieldNumber.rectTransform.anchorMin = new Vector2(0.25f, 0f);
        shieldNumber.rectTransform.anchorMax = Vector2.one;
        shieldNumber.rectTransform.offsetMin = Vector2.zero;
        shieldNumber.rectTransform.offsetMax = Vector2.zero;
        // 기존 참조는 보존하고 옛 표시만 숨깁니다. 체력 UI의 부모라면 비활성화하지 않습니다.
        if (ui.shieldRoot != null && !ui.heartText.transform.IsChildOf(ui.shieldRoot.transform)) ui.shieldRoot.SetActive(false);
        if (ui.shieldText != null && ui.shieldText != ui.heartText) ui.shieldText.text = "";
        GameObject popup = new GameObject("Shield Feedback", typeof(RectTransform), typeof(TextMeshProUGUI));
        popup.transform.SetParent(owner.Overlay, false);
        shieldPopup = popup.GetComponent<TextMeshProUGUI>();
        shieldPopup.font = ui.heartText.font;
        shieldPopup.fontSize = ui.heartText.fontSizeMax * 0.7f;
        shieldPopup.alignment = TextAlignmentOptions.Center;
        shieldPopup.raycastTarget = false;
        shieldPopup.rectTransform.sizeDelta = new Vector2(220f, 50f);
        shieldPopup.color = Color.clear;
        for (int i = 0; i < shieldShards.Length; i++)
        {
            GameObject shard = new GameObject("Shield Shard", typeof(RectTransform), typeof(Image));
            shard.transform.SetParent(owner.Overlay, false);
            shieldShards[i] = shard.GetComponent<Image>();
            shieldShards[i].raycastTarget = false;
            shieldShards[i].color = Color.clear;
            shieldShards[i].rectTransform.sizeDelta = new Vector2(4f, 9f);
        }
    }

    private void ApplyShieldLayout(bool visible)
    {
        if (shieldRow == null || shieldHealthText == null) return;
        if (!owner.showShieldRow) visible = false;
        shieldRow.gameObject.SetActive(visible);
        shieldRow.anchorMax = new Vector2(1f, owner.shieldRowHeightRatio);
        Vector4 margin = originalHealthMargin;
        if (visible) margin.w += shieldHealthText.rectTransform.rect.height * owner.shieldRowHeightRatio;
        shieldHealthText.margin = margin;
        shieldHealthText.alignment = visible ? TextAlignmentOptions.MidlineLeft : originalHealthAlignment;
    }

    internal void PlayShieldFeedback(UIManager ui, int amount, bool gained, bool broken)
    {
        if (shieldRow == null) return;
        shieldTween?.Kill();
        ApplyShieldLayout(true);
        shieldPopup.rectTransform.position = shieldRow.TransformPoint(new Vector3(shieldRow.rect.width * 0.7f, 0f, 0f));
        shieldPopup.color = owner.shieldColor;
        shieldPopup.SetText(gained ? "+{0}" : LocalizationManager.GetUi("UI_SHIELD_BLOCKED", "막음 {0}"), amount);
        float duration = Mathf.Max(0.12f, broken ? owner.shieldBreakDuration : owner.shieldFeedbackDuration);
        shieldTween = DOTween.Sequence();
        shieldTween.Append(shieldRow.DOScale(owner.shieldPulseScale, 0.07f).SetEase(Ease.OutQuad));
        shieldTween.Append(shieldRow.DOScale(1f, duration - 0.07f).SetEase(Ease.OutSine));
        shieldIcon.color = Color.white;
        shieldTween.Insert(0f, shieldIcon.DOColor(owner.shieldColor, duration));
        shieldTween.Insert(0f, shieldPopup.rectTransform.DOAnchorPosY(shieldPopup.rectTransform.anchoredPosition.y + 24f, duration));
        shieldTween.Insert(duration * 0.4f, shieldPopup.DOFade(0f, duration * 0.6f));
        if (broken)
        {
            for (int i = 0; i < shieldShards.Length; i++)
            {
                Image shard = shieldShards[i];
                shard.rectTransform.position = shieldIcon.rectTransform.position;
                shard.color = owner.shieldColor;
                float angle = i * Mathf.PI / 3f;
                Vector2 travel = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 32f;
                shard.rectTransform.localRotation = Quaternion.Euler(0f, 0f, i * 60f);
                shieldTween.Insert(0f, shard.rectTransform.DOAnchorPos(shard.rectTransform.anchoredPosition + travel, duration).SetEase(Ease.OutQuad));
                shieldTween.Insert(0f, shard.DOFade(0f, duration));
            }
        }
        shieldTween.OnKill(() =>
        {
            shieldTween = null;
            if (shieldRow != null) shieldRow.localScale = Vector3.one;
            if (shieldIcon != null) shieldIcon.color = owner.shieldColor;
            if (shieldPopup != null) shieldPopup.color = Color.clear;
            for (int i = 0; i < shieldShards.Length; i++) if (shieldShards[i] != null) shieldShards[i].color = Color.clear;
            ApplyShieldLayout(displayedShield > 0);
        });
    }

    private RectTransform shieldImpactRoot;
    private Image impactShield;
    private RawImage shieldWave;
    private RawImage shieldFlash;
    private readonly Image[] impactFragments = new Image[8];
    private Texture2D shieldRingTexture;
    private Sequence shieldImpactTween;

    // 작은 UI 방패와 별도로 타격 위치에서 방어를 보여줍니다. 생성한 도형은 계속 재사용합니다.
    private void EnsureShieldImpact()
    {
        if (shieldImpactRoot != null || owner.Overlay == null) return;
        GameObject root = new GameObject("Shield Impact", typeof(RectTransform));
        shieldImpactRoot = root.GetComponent<RectTransform>();
        shieldImpactRoot.SetParent(owner.Overlay, false);
        shieldRingTexture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        shieldRingTexture.wrapMode = TextureWrapMode.Clamp;
        shieldRingTexture.filterMode = FilterMode.Bilinear;
        Color32[] pixels = new Color32[64 * 64];
        for (int y = 0; y < 64; y++)
        for (int x = 0; x < 64; x++)
        {
            float radius = new Vector2((x - 31.5f) / 31.5f, (y - 31.5f) / 31.5f).magnitude;
            float alpha = 1f - Mathf.SmoothStep(0f, 0.11f, Mathf.Abs(radius - 0.78f));
            pixels[y * 64 + x] = new Color(1f, 1f, 1f, alpha);
        }
        shieldRingTexture.SetPixels32(pixels);
        shieldRingTexture.Apply(false, true);
        GameObject wave = new GameObject("Blue Shockwave", typeof(RectTransform), typeof(RawImage));
        wave.transform.SetParent(shieldImpactRoot, false);
        shieldWave = wave.GetComponent<RawImage>();
        shieldWave.texture = shieldRingTexture;
        shieldWave.raycastTarget = false;
        GameObject body = new GameObject("Large Shield", typeof(RectTransform), typeof(Image));
        body.transform.SetParent(shieldImpactRoot, false);
        impactShield = body.GetComponent<Image>();
        impactShield.sprite = shieldIcon != null ? shieldIcon.sprite : null;
        impactShield.preserveAspect = true;
        impactShield.raycastTarget = false;
        GameObject flash = new GameObject("Contact Flash", typeof(RectTransform), typeof(RawImage));
        flash.transform.SetParent(shieldImpactRoot, false);
        shieldFlash = flash.GetComponent<RawImage>();
        shieldFlash.texture = shieldRingTexture;
        shieldFlash.raycastTarget = false;
        for (int i = 0; i < impactFragments.Length; i++)
        {
            GameObject fragment = new GameObject("Impact Fragment", typeof(RectTransform), typeof(Image));
            fragment.transform.SetParent(shieldImpactRoot, false);
            impactFragments[i] = fragment.GetComponent<Image>();
            impactFragments[i].raycastTarget = false;
        }
        shieldImpactRoot.gameObject.SetActive(false);
    }

    internal void PlayShieldImpact(bool broken)
    {
        EnsureShieldImpact();
        if (shieldImpactRoot == null) return;
        shieldImpactTween?.Kill();
        shieldImpactRoot.gameObject.SetActive(true);
        shieldImpactRoot.anchorMin = owner.shieldImpactAnchor;
        shieldImpactRoot.anchorMax = owner.shieldImpactAnchor;
        shieldImpactRoot.anchoredPosition = Vector2.zero;
        float size = Mathf.Max(20f, owner.shieldImpactSize);
        float duration = Mathf.Max(0.25f, broken ? owner.shieldImpactBreakDuration : owner.shieldImpactDuration);
        impactShield.rectTransform.sizeDelta = Vector2.one * size;
        impactShield.rectTransform.localScale = Vector3.one * 0.8f;
        impactShield.color = new Color(owner.shieldColor.r, owner.shieldColor.g, owner.shieldColor.b, 0.9f);
        shieldWave.rectTransform.sizeDelta = Vector2.one * size * 1.2f;
        shieldWave.rectTransform.localScale = Vector3.one * 0.65f;
        shieldWave.color = new Color(owner.shieldColor.r, owner.shieldColor.g, owner.shieldColor.b, 0.85f);
        shieldFlash.rectTransform.sizeDelta = Vector2.one * size * 0.8f;
        shieldFlash.rectTransform.localScale = Vector3.one * 0.2f;
        shieldFlash.color = new Color(0.8f, 0.96f, 1f, 1f);
        shieldImpactTween = DOTween.Sequence();
        shieldImpactTween.Append(impactShield.rectTransform.DOScale(new Vector3(1.14f, 0.85f, 1f), 0.07f).SetEase(Ease.OutQuad));
        shieldImpactTween.Append(impactShield.rectTransform.DOScale(broken ? 1.18f : 1f, 0.10f).SetEase(Ease.OutBack));
        shieldImpactTween.Insert(0.07f, impactShield.DOColor(Color.white, 0.04f));
        shieldImpactTween.Insert(0.11f, impactShield.DOColor(owner.shieldColor, 0.06f));
        shieldImpactTween.Insert(broken ? 0.17f : duration * 0.55f, impactShield.DOFade(0f, broken ? 0.08f : duration * 0.45f));
        shieldImpactTween.Insert(0f, shieldWave.rectTransform.DOScale(Mathf.Max(1f, owner.shieldWaveScale), duration).SetEase(Ease.OutQuad));
        shieldImpactTween.Insert(0.07f, shieldWave.DOFade(0f, duration - 0.07f));
        shieldImpactTween.Insert(0f, shieldFlash.rectTransform.DOScale(1.15f, 0.17f).SetEase(Ease.OutQuad));
        shieldImpactTween.Insert(0.04f, shieldFlash.DOFade(0f, 0.13f));
        for (int i = 0; i < impactFragments.Length; i++)
        {
            Image fragment = impactFragments[i];
            float angle = (i * 45f + 22.5f) * Mathf.Deg2Rad;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            fragment.rectTransform.anchoredPosition = direction * size * 0.18f;
            fragment.rectTransform.sizeDelta = broken ? new Vector2(9f, 19f) : new Vector2(3f, 13f);
            fragment.rectTransform.localRotation = Quaternion.Euler(0f, 0f, i * 45f);
            fragment.color = new Color(owner.shieldColor.r, owner.shieldColor.g, owner.shieldColor.b, 0f);
            float start = broken ? 0.1f : 0.04f;
            float flight = duration - start;
            shieldImpactTween.Insert(start, fragment.DOFade(1f, 0.025f));
            shieldImpactTween.Insert(start, fragment.rectTransform.DOAnchorPos(direction * owner.shieldFragmentDistance * (broken ? 1f : 0.6f), flight).SetEase(Ease.OutQuad));
            shieldImpactTween.Insert(start + 0.025f, fragment.DOFade(0f, flight - 0.025f));
        }
        shieldImpactTween.OnKill(() =>
        {
            shieldImpactTween = null;
            if (shieldImpactRoot != null) shieldImpactRoot.gameObject.SetActive(false);
        });
    }

    // 컨트롤러가 꺼지거나 사라질 때 부른다. 예전과 같은 순서로 정리되도록 나눠 둔다.
    internal void KillImpact() => shieldImpactTween?.Kill();
    internal void KillFeedback() => shieldTween?.Kill();

    internal void DestroyImpactTexture()
    {
        if (shieldRingTexture != null) Object.Destroy(shieldRingTexture);
    }

    // 체력 숫자 칸의 여백·정렬을 원래대로 되돌리고 방패 줄을 지운다.
    internal void Release()
    {
        if (shieldHealthText != null) { shieldHealthText.margin = originalHealthMargin; shieldHealthText.alignment = originalHealthAlignment; }
        if (shieldRow != null) Object.Destroy(shieldRow.gameObject);
    }
}
