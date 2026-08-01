using UnityEngine;
using DG.Tweening;

public class LetterShakeController : MonoBehaviour
{
    [Header("흔들 UI 대상 (Canvas 안의 Image)")]
    [SerializeField] private RectTransform letterTarget;

    [Header("스파크 파티클 (없으면 비워두세요)")]
    [SerializeField] private ParticleSystem sparkParticle;

    [Header("Shake 상세 설정 (타격감 최적화)")]
    [SerializeField] private float duration = 0.6f;      // 전체 흔들리는 시간
    [SerializeField] private float posStrength = 20.0f;   // 위치 흔들림 강도 (화끈하게 튕김)
    [SerializeField] private float rotStrength = 12.0f;   // 회전 흔들림 강도
    [SerializeField] private int vibrato = 20;            // 적절한 진동수 (30은 끊겨 보이고, 20이 딱 쫀득함)

    private Tween posTween;
    private Tween rotTween;

    private Vector2 originalAnchoredPosition;
    private Quaternion originalRotation;
    private bool isInitialized = false;

    private void Awake()
    {
        DOTween.Init();

        if (letterTarget != null)
        {
            originalAnchoredPosition = letterTarget.anchoredPosition;
            originalRotation = letterTarget.localRotation;
            isInitialized = true;
        }
    }

    private void Start()
    {
        StartLetterShake();
    }

    [ContextMenu("Play Shake")]
    public void StartLetterShake()
    {
        if (!isInitialized && letterTarget != null)
        {
            originalAnchoredPosition = letterTarget.anchoredPosition;
            originalRotation = letterTarget.localRotation;
            isInitialized = true;
        }

        StopAndReset();

        if (sparkParticle != null)
        {
            sparkParticle.Play();
        }

        if (letterTarget == null) return;

        // [핵심] DOShake 방식 원복 + fadeOut: true로 렉 현상만 제거
        posTween = letterTarget.DOShakeAnchorPos(
            duration: duration,
            strength: new Vector2(posStrength, posStrength),
            vibrato: vibrato,
            randomness: 90f,
            fadeOut: true
        ).SetUpdate(true);

        rotTween = letterTarget.DOShakeRotation(
            duration: duration,
            strength: new Vector3(0f, 0f, rotStrength),
            vibrato: vibrato,
            randomness: 90f,
            fadeOut: true
        ).SetUpdate(true);

        // 흔들림 완료 후 찢어지기 연출
        posTween.OnComplete(() =>
        {
            LetterTearEffect tearEffect = GetComponent<LetterTearEffect>();
            if (tearEffect != null)
            {
                tearEffect.PlayTearEffect();
            }
        });
    }

    public void StopAndReset()
    {
        if (posTween != null && posTween.IsActive()) posTween.Kill();
        if (rotTween != null && rotTween.IsActive()) rotTween.Kill();

        if (letterTarget != null && isInitialized)
        {
            letterTarget.anchoredPosition = originalAnchoredPosition;
            letterTarget.localRotation = originalRotation;
        }

        if (sparkParticle != null)
        {
            sparkParticle.Stop();
        }
    }
}