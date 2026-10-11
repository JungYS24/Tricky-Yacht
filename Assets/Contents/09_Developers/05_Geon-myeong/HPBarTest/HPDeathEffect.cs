
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class HPDeathEffect : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform hpBar;
    [SerializeField] private Image frame;
    [SerializeField] private Image flash;

    [Header("Death Effect")]
    [SerializeField] private float shakeDuration = 0.4f;
    [SerializeField] private float shakeStrength = 12f;
    [SerializeField] private float darkenDuration = 0.5f;

    private Sequence deathSequence;
    private Vector2 originalPosition;
    private Color originalFrameColor;

    private void Awake()
    {
        if (hpBar != null)
            originalPosition = hpBar.anchoredPosition;

        if (frame != null)
            originalFrameColor = frame.color;
    }

    public void Play()
    {
        if (hpBar == null || frame == null || flash == null)
            return;

        deathSequence?.Kill();

        hpBar.anchoredPosition = originalPosition;
        frame.color = originalFrameColor;

        // HP가 0이어도 번쩍임이 보이도록 전체 영역 표시
        flash.DOKill();
        flash.fillAmount = 1f;
        flash.color = new Color(1f, 0.15f, 0.15f, 1f);

        deathSequence = DOTween.Sequence();

        // 붉은색 Flash
        deathSequence.Append(
            flash.DOFade(0f, 0.25f)
        );

        // HP바 흔들림
        deathSequence.Join(
            hpBar.DOShakeAnchorPos(
                shakeDuration,
                shakeStrength,
                20,
                90f,
                false,
                true
            )
        );

        // HP바 어두워짐
        deathSequence.Append(
            frame.DOColor(
                new Color(0.12f, 0.08f, 0.08f, 1f),
                darkenDuration
            )
        );
    }


    public void ResetEffect()
    {
        // 사망 이펙트 중지
        deathSequence?.Kill();

        // HP바 위치 복구
        if (hpBar != null)
        {
            hpBar.DOKill();
            hpBar.anchoredPosition = originalPosition;
        }

        // HP바 테두리 색상 복구
        if (frame != null)
        {
            frame.DOKill();
            frame.color = originalFrameColor;
        }

        // Flash 원래 상태 복구
        if (flash != null)
        {
            flash.DOKill();

            flash.color = new Color(1f, 1f, 1f, 0f);
            flash.fillAmount = 0f;
        }
    }


    private void OnDestroy()
    {
        deathSequence?.Kill();
    }
}
