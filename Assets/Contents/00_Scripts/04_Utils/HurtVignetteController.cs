using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal; 
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
        else Destroy(gameObject);

        if (globalVolume != null && globalVolume.profile.TryGet<Vignette>(out var targetVignette))
        {
            vignette = targetVignette;
            vignette.intensity.value = 0f;
        }
    }

    /// <summary>
    /// 몬스터에게 피격당했을 때 이 함수를 외부에 부르면 됩니다!
    /// 예: HurtVignetteController.Instance.TriggerHurtEffect();
    /// </summary>
    public void TriggerHurtEffect()
    {
        if (vignette == null) return;

        if (fadeTween != null && fadeTween.IsActive())
        {
            fadeTween.Kill();
        }

        vignette.intensity.value = maxIntensity;
        fadeTween = DOTween.To(() => vignette.intensity.value,
                               x => vignette.intensity.value = x,
                               0f,
                               fadeDuration)
                             .SetEase(Ease.OutSine); // 서서히 부드럽게 감속하는 이징 값
    }

    void OnDestroy()
    {
        if (fadeTween != null && fadeTween.IsActive())
        {
            fadeTween.Kill();
        }
    }
}