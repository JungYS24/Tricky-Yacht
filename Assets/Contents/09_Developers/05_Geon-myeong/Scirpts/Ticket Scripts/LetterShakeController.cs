using UnityEngine;
using DG.Tweening;

public class LetterShakeController : MonoBehaviour
{
    [Header("흔들 UI 대상 (Canvas 안의 Image)")]
    [SerializeField] private RectTransform letterTarget;

    [Header("다음 연출 스크립트 연결 (LetterTearEffect)")]
    [SerializeField] private LetterTearEffect tearEffect;

    [Header("스파크 파티클 (선택사항)")]
    [SerializeField] private ParticleSystem sparkParticle;

    [Header("Shake 상세 설정")]
    [SerializeField] private float duration = 0.6f;
    [SerializeField] private float posStrength = 20.0f;
    [SerializeField] private float rotStrength = 12.0f;
    [SerializeField] private int vibrato = 20;

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
            letterTarget.gameObject.SetActive(false);
        }

        if (tearEffect == null)
        {
            // typeof() 방식으로 수정하여 꺾쇠괄호 소실 방지
            tearEffect = (LetterTearEffect)GetComponent(typeof(LetterTearEffect));
        }
    }

    [ContextMenu("Play Shake")]
    public void StartLetterShake()
    {
        if (letterTarget == null)
        {
            OnShakeComplete();
            return;
        }

        letterTarget.gameObject.SetActive(true);
        StopAndReset();

        if (sparkParticle != null)
        {
            sparkParticle.gameObject.SetActive(true);
            sparkParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            sparkParticle.Play();
        }

        Sequence shakeSeq = DOTween.Sequence();

        shakeSeq.Join(letterTarget.DOShakeAnchorPos(
            duration: duration,
            strength: new Vector2(posStrength, posStrength),
            vibrato: vibrato,
            randomness: 90f,
            fadeOut: true
        ));

        shakeSeq.Join(letterTarget.DOShakeRotation(
            duration: duration,
            strength: new Vector3(0f, 0f, rotStrength),
            vibrato: vibrato,
            randomness: 90f,
            fadeOut: true
        ));

        shakeSeq.OnComplete(OnShakeComplete);
    }

    private void OnShakeComplete()
    {
        if (tearEffect == null)
        {
            // typeof() 방식으로 수정하여 꺾쇠괄호 소실 방지
            tearEffect = (LetterTearEffect)GetComponent(typeof(LetterTearEffect));
            if (tearEffect == null)
            {
                tearEffect = (LetterTearEffect)Object.FindFirstObjectByType(typeof(LetterTearEffect));
            }
        }

        if (tearEffect != null)
        {
            tearEffect.PlayTearEffect();
        }
    }

    public void StopAndReset()
    {
        if (letterTarget != null && isInitialized)
        {
            letterTarget.DOKill();
            letterTarget.anchoredPosition = originalAnchoredPosition;
            letterTarget.localRotation = originalRotation;
            letterTarget.gameObject.SetActive(true);
        }

        if (sparkParticle != null)
        {
            sparkParticle.Stop();
        }
    }
}