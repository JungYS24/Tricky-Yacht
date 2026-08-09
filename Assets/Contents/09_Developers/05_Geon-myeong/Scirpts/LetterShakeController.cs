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

            // ⭐ [핵심] 게임 시작 시 화면에 안 보이도록 즉시 숨김!
            letterTarget.gameObject.SetActive(false);
        }

        if (tearEffect == null)
        {
            tearEffect = GetComponent<LetterTearEffect>();
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

        // ⭐ 버튼을 눌러 흔들기가 시작될 때만 짠! 하고 켜기
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
            tearEffect = GetComponent<LetterTearEffect>();
            if (tearEffect == null) tearEffect = FindObjectOfType<LetterTearEffect>();
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