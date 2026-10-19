using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UiHoverIdleJuice : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] float hoverScale = 1.1f;
    [SerializeField] float hoverDuration = 0.12f;
    [SerializeField] float shakeAmount = 0.7f;
    [SerializeField] float worldShakeAmount = 0.015f;

    RectTransform rect;
    Vector3 baseScale;
    Vector2 idleAnchoredPos;
    Vector3 idleLocalPos;
    Tween hoverTween;
    Tween shakeTween;
    float noiseSeedX;
    float noiseSeedY;
    float noiseSpeed;
    bool cached;

    void Awake()
    {
        rect = GetComponent<RectTransform>();
        CacheRestPose();

        var graphic = GetComponent<Graphic>();
        if (graphic != null)
            graphic.raycastTarget = true;
    }

    void OnEnable()
    {
        if (cached)
            CacheRestPose();
        PlayIdleShake();
    }

    void OnDisable()
    {
        KillTweens();
        transform.localScale = baseScale;
    }

    void CacheRestPose()
    {
        baseScale = transform.localScale;
        if (rect != null)
            idleAnchoredPos = rect.anchoredPosition;
        else
            idleLocalPos = transform.localPosition;
        cached = true;
    }

    void PlayIdleShake()
    {
        shakeTween?.Kill();

        noiseSeedX = Random.Range(0f, 64f);
        noiseSeedY = Random.Range(0f, 64f);
        noiseSpeed = Random.Range(0.22f, 0.4f);

        shakeTween = DOVirtual.Float(0f, 1f, 8f, _ => ApplyIdleOffset())
            .SetEase(Ease.Linear)
            .SetLoops(-1)
            .SetLink(gameObject);
    }

    void ApplyIdleOffset()
    {
        float t = Time.unscaledTime * noiseSpeed;
        float x = (Mathf.PerlinNoise(noiseSeedX + t, noiseSeedY) - 0.5f) * 2f;
        float y = (Mathf.PerlinNoise(noiseSeedX, noiseSeedY + t * 1.17f) - 0.5f) * 2f;
        x += (Mathf.PerlinNoise(noiseSeedX + t * 1.83f, noiseSeedY + 17f) - 0.5f) * 0.45f;
        y += (Mathf.PerlinNoise(noiseSeedX + 23f, noiseSeedY + t * 1.61f) - 0.5f) * 0.45f;

        Vector3 worldDelta = new Vector3(x, y, 0f) * worldShakeAmount;
        Vector3 parentDelta = transform.parent != null
            ? transform.parent.InverseTransformVector(worldDelta)
            : worldDelta;

        if (rect != null)
        {
            rect.anchoredPosition = idleAnchoredPos + (Vector2)parentDelta;
            return;
        }

        transform.localPosition = idleLocalPos + parentDelta;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hoverTween?.Kill();
        hoverTween = transform
            .DOScale(baseScale * hoverScale, hoverDuration)
            .SetEase(Ease.OutQuad)
            .SetLink(gameObject);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hoverTween?.Kill();
        hoverTween = transform
            .DOScale(baseScale, hoverDuration)
            .SetEase(Ease.OutQuad)
            .SetLink(gameObject);
    }

    void KillTweens()
    {
        hoverTween?.Kill();
        shakeTween?.Kill();
        hoverTween = null;
        shakeTween = null;
    }
}
