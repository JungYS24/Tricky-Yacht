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

    // 방어막 연출(PlayerShieldFeedback)이 같은 오버레이에 그립니다.
    internal RectTransform Overlay => overlay;

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
    [Tooltip("끄면 체력 숫자 아래 방패+숫자 줄을 띄우지 않습니다(체력 칸이 방패 그림·회색으로 대신 표시할 때).")]
    public bool showShieldRow = true;

    private Tween pendingHealthHit;

    // 방어막 표시와 연출은 PlayerShieldFeedback이 맡습니다(값은 위 인스펙터 칸을 씁니다). 처음 쓸 때 만듭니다.
    PlayerShieldFeedback shield;
    PlayerShieldFeedback Shield => shield ??= new PlayerShieldFeedback(this);

    public void UpdateShieldDisplay(UIManager ui, int amount) => Shield.UpdateShieldDisplay(ui, amount);

    public void PlayShieldOrHealthHit(UIManager ui, int blocked, int hpDamage, int remainingShield)
    {
        pendingHealthHit?.Kill();
        pendingHealthHit = null;
        if (blocked > 0)
        {
            UpdateShieldDisplay(ui, remainingShield);
            Shield.PlayShieldFeedback(ui, blocked, false, remainingShield == 0);
            Shield.PlayShieldImpact(remainingShield == 0);
            if (hpDamage <= 0) CameraShake.Instance?.Shake(shieldShakeStrength, hurtShakeDuration);
        }
        if (hpDamage <= 0) return;
        if (blocked <= 0) PlayPlayerHit(ui, hpDamage);
        else pendingHealthHit = DOVirtual.DelayedCall(Mathf.Max(0f, shieldToHealthDelay), () => { pendingHealthHit = null; PlayPlayerHit(ui, hpDamage); }, false);
    }

    [Header("화면 방어 이펙트")]
    public Vector2 shieldImpactAnchor = new Vector2(0.60f, 0.32f);
    public float shieldImpactSize = 150f;
    public float shieldImpactDuration = 0.48f;
    public float shieldImpactBreakDuration = 0.42f;
    public float shieldWaveScale = 1.65f;
    public float shieldFragmentDistance = 110f;

    internal void EnsureVisuals(Canvas canvas, TMP_Text reference)
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
        shield?.KillImpact();
        pendingHealthHit?.Kill();
        pendingHealthHit = null;
        shield?.KillFeedback();
        StopHit();
        fadeTween?.Kill();
        if (edgeImage != null) edgeImage.color = Color.clear;
    }

    void OnDestroy()
    {
        shield?.KillImpact();
        shield?.DestroyImpactTexture();
        pendingHealthHit?.Kill();
        shield?.KillFeedback();
        shield?.Release();
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
