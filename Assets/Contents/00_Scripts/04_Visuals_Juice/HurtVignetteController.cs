using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal; 
using UnityEngine.UI;
using TMPro;
using DG.Tweening; 

public class HurtVignetteController : MonoBehaviour
{
    public static HurtVignetteController Instance { get; private set; }

    [Header("Volume Reference")]
    public Volume globalVolume;

    [Header("Vignette Settings")]
    [Range(0f, 1f)] public float maxIntensity = 0.45f; 
    public float fadeDuration = 0.6f; 

    private Vignette vignette;
    private Tween fadeTween;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(this); return; }

        if (globalVolume != null && globalVolume.profile.TryGet<Vignette>(out var targetVignette))
        {
            vignette = targetVignette;
            vignette.intensity.value = 0f; // 기존 초기화 유지. 이후 피격은 전용 UI 가장자리로 표시합니다.
        }
    }

    /// <summary>
    /// 몬스터에게 피격당했을 때 이 함수를 외부에 부르면 됩니다!
    /// 예: HurtVignetteController.Instance.TriggerHurtEffect();
    /// </summary>
    public void TriggerHurtEffect()
    {
        if (edgeImage == null) return;
        fadeTween?.Kill();
        edgeImage.color = new Color(0.85f, 0.04f, 0.03f, maxIntensity);
        fadeTween = edgeImage.DOFade(0f, Mathf.Max(0.01f, fadeDuration)).SetEase(Ease.OutSine);
    }

    [Header("플레이어 피격 표시")]
    public float hurtShakeStrength = 0.10f;
    public float hurtShakeDuration = 0.15f;
    public float healthPunchScale = 1.14f;
    public float damageTextDuration = 0.65f;
    public Vector2 impactAnchor = new Vector2(0.60f, 0.28f);
    public float impactSize = 90f;
    public float impactDuration = 0.20f;

    private RawImage edgeImage;
    private Texture2D edgeTexture;
    private RectTransform overlay;
    private readonly Image[] strikes = new Image[3];
    private TextMeshProUGUI damageLabel;
    private TMP_Text healthTarget;
    private Vector3 healthBaseScale;
    private Sequence hitTween;

    public static HurtVignetteController Get(DiceManager owner)
    {
        if (Instance != null) return Instance;
        return owner.gameObject.AddComponent<HurtVignetteController>();
    }

    public void PlayPlayerHit(UIManager ui, int damage)
    {
        if (damage <= 0) return;
        CameraShake.Instance?.Shake(hurtShakeStrength, hurtShakeDuration);
        if (ui == null || ui.heartText == null) return;
        Canvas canvas = ui.heartText.GetComponentInParent<Canvas>();
        if (canvas == null) return;
        EnsureVisuals(canvas.rootCanvas, ui.heartText);
        StopHit();
        TriggerHurtEffect();
        healthTarget = ui.heartText;
        healthBaseScale = healthTarget.transform.localScale;
        damageLabel.gameObject.SetActive(true);
        damageLabel.rectTransform.position = ui.heartText.rectTransform.TransformPoint(new Vector3(0f, ui.heartText.rectTransform.rect.yMin - 12f, 0f));
        damageLabel.SetText("-{0}", damage);
        damageLabel.color = new Color(1f, 0.3f, 0.25f, 1f);
        damageLabel.transform.localScale = Vector3.one;
        hitTween = DOTween.Sequence();
        hitTween.Append(healthTarget.transform.DOScale(healthBaseScale * healthPunchScale, 0.07f));
        hitTween.Append(healthTarget.transform.DOScale(healthBaseScale, 0.18f));
        hitTween.Insert(0f, damageLabel.rectTransform.DOAnchorPosY(damageLabel.rectTransform.anchoredPosition.y + 28f, Mathf.Max(0.1f, damageTextDuration)));
        hitTween.Insert(0.15f, damageLabel.DOFade(0f, Mathf.Max(0.1f, damageTextDuration - 0.15f)));
        for (int i = 0; i < strikes.Length; i++)
        {
            Image strike = strikes[i];
            strike.rectTransform.anchorMin = impactAnchor;
            strike.rectTransform.anchorMax = impactAnchor;
            strike.rectTransform.anchoredPosition = new Vector2((i - 1) * 22f, (i % 2) * 12f);
            strike.rectTransform.sizeDelta = new Vector2(i == 1 ? 5f : 3f, impactSize * (i == 1 ? 1f : 0.65f));
            strike.rectTransform.localScale = Vector3.one * 0.35f;
            strike.color = new Color(1f, 0.85f, 0.65f, 0.95f);
            hitTween.Insert(0f, strike.rectTransform.DOScale(Vector3.one, Mathf.Max(0.01f, impactDuration)).SetEase(Ease.OutQuad));
            hitTween.Insert(0f, strike.DOFade(0f, Mathf.Max(0.01f, impactDuration)));
        }
        hitTween.OnKill(RestoreHealth);
    }

    [Header("보호막 표시와 연출")]
    public Color shieldColor = new Color(0.4f, 0.82f, 1f, 1f);
    public float shieldPulseScale = 1.18f;
    public float shieldFeedbackDuration = 0.4f;
    public float shieldBreakDuration = 0.3f;
    public float shieldToHealthDelay = 0.1f;
    public float shieldShakeStrength = 0.035f;
    [Range(0.25f, 0.55f)] public float shieldRowHeightRatio = 0.45f;

    private RectTransform shieldRow;
    private Image shieldIcon;
    private TextMeshProUGUI shieldNumber;
    private TextMeshProUGUI shieldPopup;
    private readonly Image[] shieldShards = new Image[6];
    private Sequence shieldTween;
    private Tween pendingHealthHit;
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

    public void PlayShieldOrHealthHit(UIManager ui, int blocked, int hpDamage, int remainingShield)
    {
        pendingHealthHit?.Kill();
        pendingHealthHit = null;
        if (blocked > 0)
        {
            UpdateShieldDisplay(ui, remainingShield);
            PlayShieldFeedback(ui, blocked, false, remainingShield == 0);
            PlayShieldImpact(remainingShield == 0);
            if (hpDamage <= 0) CameraShake.Instance?.Shake(shieldShakeStrength, hurtShakeDuration);
        }
        if (hpDamage <= 0) return;
        if (blocked <= 0) PlayPlayerHit(ui, hpDamage);
        else pendingHealthHit = DOVirtual.DelayedCall(Mathf.Max(0f, shieldToHealthDelay), () => { pendingHealthHit = null; PlayPlayerHit(ui, hpDamage); }, false);
    }

    private void CreateShieldDisplay(UIManager ui)
    {
        Canvas canvas = ui.heartText.GetComponentInParent<Canvas>();
        if (canvas == null) return;
        EnsureVisuals(canvas.rootCanvas, ui.heartText);
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
        shieldRow.anchorMax = new Vector2(1f, shieldRowHeightRatio);
        shieldRow.offsetMin = Vector2.zero;
        shieldRow.offsetMax = Vector2.zero;
        GameObject icon = new GameObject("Shield Icon", typeof(RectTransform), typeof(Image));
        icon.transform.SetParent(shieldRow, false);
        shieldIcon = icon.GetComponent<Image>();
        shieldIcon.sprite = oldIcon != null ? oldIcon.sprite : null;
        shieldIcon.preserveAspect = true;
        shieldIcon.raycastTarget = false;
        shieldIcon.color = shieldColor;
        shieldIcon.rectTransform.anchorMin = Vector2.zero;
        shieldIcon.rectTransform.anchorMax = new Vector2(0.22f, 1f);
        shieldIcon.rectTransform.offsetMin = Vector2.zero;
        shieldIcon.rectTransform.offsetMax = Vector2.zero;
        GameObject number = new GameObject("Shield Number", typeof(RectTransform), typeof(TextMeshProUGUI));
        number.transform.SetParent(shieldRow, false);
        shieldNumber = number.GetComponent<TextMeshProUGUI>();
        shieldNumber.font = ui.heartText.font;
        shieldNumber.color = shieldColor;
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
        popup.transform.SetParent(overlay, false);
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
            shard.transform.SetParent(overlay, false);
            shieldShards[i] = shard.GetComponent<Image>();
            shieldShards[i].raycastTarget = false;
            shieldShards[i].color = Color.clear;
            shieldShards[i].rectTransform.sizeDelta = new Vector2(4f, 9f);
        }
    }

    private void ApplyShieldLayout(bool visible)
    {
        if (shieldRow == null || shieldHealthText == null) return;
        shieldRow.gameObject.SetActive(visible);
        shieldRow.anchorMax = new Vector2(1f, shieldRowHeightRatio);
        Vector4 margin = originalHealthMargin;
        if (visible) margin.w += shieldHealthText.rectTransform.rect.height * shieldRowHeightRatio;
        shieldHealthText.margin = margin;
        shieldHealthText.alignment = visible ? TextAlignmentOptions.MidlineLeft : originalHealthAlignment;
    }

    private void PlayShieldFeedback(UIManager ui, int amount, bool gained, bool broken)
    {
        if (shieldRow == null) return;
        shieldTween?.Kill();
        ApplyShieldLayout(true);
        shieldPopup.rectTransform.position = shieldRow.TransformPoint(new Vector3(shieldRow.rect.width * 0.7f, 0f, 0f));
        shieldPopup.color = shieldColor;
        shieldPopup.SetText(gained ? "+{0}" : "막음 {0}", amount);
        float duration = Mathf.Max(0.12f, broken ? shieldBreakDuration : shieldFeedbackDuration);
        shieldTween = DOTween.Sequence();
        shieldTween.Append(shieldRow.DOScale(shieldPulseScale, 0.07f).SetEase(Ease.OutQuad));
        shieldTween.Append(shieldRow.DOScale(1f, duration - 0.07f).SetEase(Ease.OutSine));
        shieldIcon.color = Color.white;
        shieldTween.Insert(0f, shieldIcon.DOColor(shieldColor, duration));
        shieldTween.Insert(0f, shieldPopup.rectTransform.DOAnchorPosY(shieldPopup.rectTransform.anchoredPosition.y + 24f, duration));
        shieldTween.Insert(duration * 0.4f, shieldPopup.DOFade(0f, duration * 0.6f));
        if (broken)
        {
            for (int i = 0; i < shieldShards.Length; i++)
            {
                Image shard = shieldShards[i];
                shard.rectTransform.position = shieldIcon.rectTransform.position;
                shard.color = shieldColor;
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
            if (shieldIcon != null) shieldIcon.color = shieldColor;
            if (shieldPopup != null) shieldPopup.color = Color.clear;
            for (int i = 0; i < shieldShards.Length; i++) if (shieldShards[i] != null) shieldShards[i].color = Color.clear;
            ApplyShieldLayout(displayedShield > 0);
        });
    }

    [Header("화면 방어 이펙트")]
    public Vector2 shieldImpactAnchor = new Vector2(0.60f, 0.32f);
    public float shieldImpactSize = 150f;
    public float shieldImpactDuration = 0.48f;
    public float shieldImpactBreakDuration = 0.42f;
    public float shieldWaveScale = 1.65f;
    public float shieldFragmentDistance = 110f;

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
        if (shieldImpactRoot != null || overlay == null) return;
        GameObject root = new GameObject("Shield Impact", typeof(RectTransform));
        shieldImpactRoot = root.GetComponent<RectTransform>();
        shieldImpactRoot.SetParent(overlay, false);
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

    private void PlayShieldImpact(bool broken)
    {
        EnsureShieldImpact();
        if (shieldImpactRoot == null) return;
        shieldImpactTween?.Kill();
        shieldImpactRoot.gameObject.SetActive(true);
        shieldImpactRoot.anchorMin = shieldImpactAnchor;
        shieldImpactRoot.anchorMax = shieldImpactAnchor;
        shieldImpactRoot.anchoredPosition = Vector2.zero;
        float size = Mathf.Max(20f, shieldImpactSize);
        float duration = Mathf.Max(0.25f, broken ? shieldImpactBreakDuration : shieldImpactDuration);
        impactShield.rectTransform.sizeDelta = Vector2.one * size;
        impactShield.rectTransform.localScale = Vector3.one * 0.8f;
        impactShield.color = new Color(shieldColor.r, shieldColor.g, shieldColor.b, 0.9f);
        shieldWave.rectTransform.sizeDelta = Vector2.one * size * 1.2f;
        shieldWave.rectTransform.localScale = Vector3.one * 0.65f;
        shieldWave.color = new Color(shieldColor.r, shieldColor.g, shieldColor.b, 0.85f);
        shieldFlash.rectTransform.sizeDelta = Vector2.one * size * 0.8f;
        shieldFlash.rectTransform.localScale = Vector3.one * 0.2f;
        shieldFlash.color = new Color(0.8f, 0.96f, 1f, 1f);
        shieldImpactTween = DOTween.Sequence();
        shieldImpactTween.Append(impactShield.rectTransform.DOScale(new Vector3(1.14f, 0.85f, 1f), 0.07f).SetEase(Ease.OutQuad));
        shieldImpactTween.Append(impactShield.rectTransform.DOScale(broken ? 1.18f : 1f, 0.10f).SetEase(Ease.OutBack));
        shieldImpactTween.Insert(0.07f, impactShield.DOColor(Color.white, 0.04f));
        shieldImpactTween.Insert(0.11f, impactShield.DOColor(shieldColor, 0.06f));
        shieldImpactTween.Insert(broken ? 0.17f : duration * 0.55f, impactShield.DOFade(0f, broken ? 0.08f : duration * 0.45f));
        shieldImpactTween.Insert(0f, shieldWave.rectTransform.DOScale(Mathf.Max(1f, shieldWaveScale), duration).SetEase(Ease.OutQuad));
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
            fragment.color = new Color(shieldColor.r, shieldColor.g, shieldColor.b, 0f);
            float start = broken ? 0.1f : 0.04f;
            float flight = duration - start;
            shieldImpactTween.Insert(start, fragment.DOFade(1f, 0.025f));
            shieldImpactTween.Insert(start, fragment.rectTransform.DOAnchorPos(direction * shieldFragmentDistance * (broken ? 1f : 0.6f), flight).SetEase(Ease.OutQuad));
            shieldImpactTween.Insert(start + 0.025f, fragment.DOFade(0f, flight - 0.025f));
        }
        shieldImpactTween.OnKill(() =>
        {
            shieldImpactTween = null;
            if (shieldImpactRoot != null) shieldImpactRoot.gameObject.SetActive(false);
        });
    }

    private void EnsureVisuals(Canvas canvas, TMP_Text reference)
    {
        if (overlay != null) return;
        GameObject root = new GameObject("Player Hit Overlay", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.LayoutElement));
        overlay = root.GetComponent<RectTransform>();
        overlay.SetParent(canvas.transform, false);
        overlay.anchorMin = Vector2.zero;
        overlay.anchorMax = Vector2.one;
        overlay.offsetMin = Vector2.zero;
        overlay.offsetMax = Vector2.zero;
        root.GetComponent<UnityEngine.UI.LayoutElement>().ignoreLayout = true;
        Canvas layer = root.GetComponent<Canvas>();
        layer.overrideSorting = true;
        layer.sortingOrder = canvas.sortingOrder + 20;
        // 작은 텍스처를 한 번만 생성합니다. 중앙은 투명하고 화면 가장자리만 붉어집니다.
        edgeTexture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        edgeTexture.wrapMode = TextureWrapMode.Clamp;
        edgeTexture.filterMode = FilterMode.Bilinear;
        Color32[] pixels = new Color32[64 * 64];
        for (int y = 0; y < 64; y++)
        for (int x = 0; x < 64; x++)
        {
            float distance = Mathf.Max(Mathf.Abs(x / 63f * 2f - 1f), Mathf.Abs(y / 63f * 2f - 1f));
            float alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.65f, 1f, distance));
            pixels[y * 64 + x] = new Color(1f, 1f, 1f, alpha);
        }
        edgeTexture.SetPixels32(pixels);
        edgeTexture.Apply(false, true);
        GameObject edge = new GameObject("Hurt Edges", typeof(RectTransform), typeof(RawImage));
        edge.transform.SetParent(overlay, false);
        edgeImage = edge.GetComponent<RawImage>();
        edgeImage.texture = edgeTexture;
        edgeImage.raycastTarget = false;
        edgeImage.color = Color.clear;
        edgeImage.rectTransform.anchorMin = Vector2.zero;
        edgeImage.rectTransform.anchorMax = Vector2.one;
        edgeImage.rectTransform.offsetMin = Vector2.zero;
        edgeImage.rectTransform.offsetMax = Vector2.zero;
        for (int i = 0; i < strikes.Length; i++)
        {
            GameObject slash = new GameObject("Hit Slash", typeof(RectTransform), typeof(Image));
            slash.transform.SetParent(overlay, false);
            strikes[i] = slash.GetComponent<Image>();
            strikes[i].raycastTarget = false;
            strikes[i].color = Color.clear;
            slash.transform.localRotation = Quaternion.Euler(0f, 0f, -35f + i * 8f);
        }
        GameObject label = new GameObject("Player Damage", typeof(RectTransform), typeof(TextMeshProUGUI));
        label.transform.SetParent(overlay, false);
        damageLabel = label.GetComponent<TextMeshProUGUI>();
        damageLabel.font = reference.font;
        damageLabel.fontSize = reference.fontSize * 0.85f;
        damageLabel.alignment = TextAlignmentOptions.Center;
        damageLabel.raycastTarget = false;
        damageLabel.rectTransform.sizeDelta = new Vector2(200f, 60f);
        damageLabel.gameObject.SetActive(false);
    }

    private void RestoreHealth()
    {
        if (healthTarget != null) healthTarget.transform.localScale = healthBaseScale;
        healthTarget = null;
        hitTween = null;
    }

    private void StopHit()
    {
        hitTween?.Kill();
        RestoreHealth();
        if (damageLabel != null) damageLabel.gameObject.SetActive(false);
        for (int i = 0; i < strikes.Length; i++) if (strikes[i] != null) strikes[i].color = Color.clear;
    }

    private void OnDisable()
    {
        shieldImpactTween?.Kill();
        pendingHealthHit?.Kill();
        pendingHealthHit = null;
        shieldTween?.Kill();
        StopHit();
        fadeTween?.Kill();
        if (edgeImage != null) edgeImage.color = Color.clear;
    }

    void OnDestroy()
    {
        shieldImpactTween?.Kill();
        if (shieldRingTexture != null) Destroy(shieldRingTexture);
        pendingHealthHit?.Kill();
        shieldTween?.Kill();
        if (shieldHealthText != null) { shieldHealthText.margin = originalHealthMargin; shieldHealthText.alignment = originalHealthAlignment; }
        if (shieldRow != null) Destroy(shieldRow.gameObject);
        StopHit();
        if (Instance == this) Instance = null;
        if (overlay != null) Destroy(overlay.gameObject);
        if (edgeTexture != null) Destroy(edgeTexture);
        if (fadeTween != null && fadeTween.IsActive())
        {
            fadeTween.Kill();
        }
    }
}