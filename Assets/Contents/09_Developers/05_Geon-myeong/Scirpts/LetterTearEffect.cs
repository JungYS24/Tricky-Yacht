using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class LetterTearEffect : MonoBehaviour
{
    [Header("오브젝트 참조 (미연결 시 콘솔에 경고 표시)")]
    [SerializeField] private GameObject originalLetter; // 원본 온전한 편지 (Image)
    [SerializeField] private GameObject piecesGroup;    // 찢어진 조각들 부모 (Letter_Pieces)
    [SerializeField] private RectTransform leftPiece;   // 왼쪽 조각 (Image_Left)
    [SerializeField] private RectTransform rightPiece;  // 오른쪽 조각 (Image_Right)

    [Header("다음 연출 스크립트 연결 (CardFlipManager)")]
    [SerializeField] private CardFlipManager flipManager;

    [Header("파티클 이펙트")]
    [SerializeField] private ParticleSystem confettiParticle; // 컨페티 파티클 (ConfettiFX)

    [Header("연출 상세 설정")]
    [SerializeField] private float duration = 0.6f;     // 찢어지며 퍼지는 시간
    [SerializeField] private float moveDistance = 120f; // 바깥쪽으로 날아갈 거리
    [SerializeField] private float targetScale = 1.25f; // 커질 크기 비율

    private Image leftImage;
    private Image rightImage;
    private Vector2 leftOriginPos;
    private Vector2 rightOriginPos;

    private void Awake()
    {
        CheckReferences();

        if (leftPiece != null)
        {
            leftImage = leftPiece.GetComponent<Image>();
            leftOriginPos = leftPiece.anchoredPosition;
        }

        if (rightPiece != null)
        {
            rightImage = rightPiece.GetComponent<Image>();
            rightOriginPos = rightPiece.anchoredPosition;
        }

        if (flipManager == null)
        {
            flipManager = FindObjectOfType<CardFlipManager>();
        }
    }

    private void CheckReferences()
    {
        if (originalLetter == null) Debug.LogWarning("⚠️ [LetterTear] 'originalLetter'가 연결되지 않았습니다! 편지가 안 숨겨질 수 있습니다.");
        if (piecesGroup == null) Debug.LogWarning("⚠️ [LetterTear] 'piecesGroup'이 연결되지 않았습니다!");
        if (leftPiece == null) Debug.LogWarning("⚠️ [LetterTear] 'leftPiece'가 연결되지 않았습니다!");
        if (rightPiece == null) Debug.LogWarning("⚠️ [LetterTear] 'rightPiece'가 연결되지 않았습니다!");
        if (confettiParticle == null) Debug.LogWarning("⚠️ [LetterTear] 'confettiParticle'이 연결되지 않았습니다! 파티클이 안 나옵니다.");
    }

    [ContextMenu("Play Tear Effect")]
    public void PlayTearEffect()
    {
        ResetTear();

        // 1. 원본 편지는 숨기기
        if (originalLetter != null)
        {
            originalLetter.SetActive(false);
        }

        // 2. 조각 그룹 켜기
        if (piecesGroup != null)
        {
            piecesGroup.SetActive(true);
        }

        // 3. 파티클 재생
        if (confettiParticle != null)
        {
            confettiParticle.gameObject.SetActive(true);
            confettiParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            confettiParticle.Play(true);
        }

        // 4. 조각 애니메이션
        Sequence seq = DOTween.Sequence();

        if (leftPiece != null)
        {
            seq.Join(leftPiece.DOAnchorPos(leftOriginPos + new Vector2(-moveDistance, 40f), duration).SetEase(Ease.OutCubic));
            seq.Join(leftPiece.DOScale(Vector3.one * targetScale, duration));
            if (leftImage != null) seq.Join(leftImage.DOFade(0f, duration));
        }

        if (rightPiece != null)
        {
            seq.Join(rightPiece.DOAnchorPos(rightOriginPos + new Vector2(moveDistance, -40f), duration).SetEase(Ease.OutCubic));
            seq.Join(rightPiece.DOScale(Vector3.one * targetScale, duration));
            if (rightImage != null) seq.Join(rightImage.DOFade(0f, duration));
        }

        // 5. 완료 후 카드 등장
        seq.OnComplete(() =>
        {
            if (piecesGroup != null) piecesGroup.SetActive(false);

            if (flipManager == null) flipManager = FindObjectOfType<CardFlipManager>();

            if (flipManager != null)
            {
                flipManager.PlayCardFlipSequence();
            }
        });
    }

    public void ResetTear()
    {
        if (leftPiece != null)
        {
            leftPiece.DOKill();
            leftPiece.anchoredPosition = leftOriginPos;
            leftPiece.localScale = Vector3.one;
            if (leftImage != null)
            {
                Color c = leftImage.color;
                leftImage.color = new Color(c.r, c.g, c.b, 1f);
            }
        }

        if (rightPiece != null)
        {
            rightPiece.DOKill();
            rightPiece.anchoredPosition = rightOriginPos;
            rightPiece.localScale = Vector3.one;
            if (rightImage != null)
            {
                Color c = rightImage.color;
                rightImage.color = new Color(c.r, c.g, c.b, 1f);
            }
        }
    }
}