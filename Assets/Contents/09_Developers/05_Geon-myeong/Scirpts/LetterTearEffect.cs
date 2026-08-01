using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class LetterTearEffect : MonoBehaviour
{
    [Header("오브젝트 참조")]
    [SerializeField] private GameObject originalLetter; // 원본 온전한 편지 (Image)
    [SerializeField] private GameObject piecesGroup;    // 찢어진 조각들 부모 (Letter_Pieces)
    [SerializeField] private RectTransform leftPiece;   // 왼쪽 조각 (Image_Left)
    [SerializeField] private RectTransform rightPiece;  // 오른쪽 조각 (Image_Right)

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

    // Awake는 단 하나만 존재해야 합니다!
    private void Awake()
    {
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
    }

    [ContextMenu("Play Tear Effect")]
    public void PlayTearEffect()
    {
        // 1. 상태 완전 리셋
        ResetTear();

        // 2. 원본 편지는 끄고, 찢어진 조각들 켜기
        if (originalLetter != null) originalLetter.SetActive(false);
        if (piecesGroup != null) piecesGroup.SetActive(true);

        // 3. 컨페티 종이 가루 팡!
        if (confettiParticle != null)
        {
            confettiParticle.Play();
        }

        // 4. DOTween 연출 진행
        Sequence seq = DOTween.Sequence();

        // [왼쪽 조각] 대각선 왼쪽 위로 날아가며 + 확대 + Fade Out
        if (leftPiece != null)
        {
            seq.Join(leftPiece.DOAnchorPos(leftOriginPos + new Vector2(-moveDistance, 40f), duration).SetEase(Ease.OutCubic));
            seq.Join(leftPiece.DOScale(Vector3.one * targetScale, duration));
            if (leftImage != null) seq.Join(leftImage.DOFade(0f, duration));
        }

        // [오른쪽 조각] 대각선 오른쪽 아래로 날아가며 + 확대 + Fade Out
        if (rightPiece != null)
        {
            seq.Join(rightPiece.DOAnchorPos(rightOriginPos + new Vector2(moveDistance, -40f), duration).SetEase(Ease.OutCubic));
            seq.Join(rightPiece.DOScale(Vector3.one * targetScale, duration));
            if (rightImage != null) seq.Join(rightImage.DOFade(0f, duration));
        }

        // 5. 연출 끝난 후 조각 그룹 숨기기
        seq.OnComplete(() =>
        {
            if (piecesGroup != null) piecesGroup.SetActive(false);
        });
    }

    public void ResetTear()
    {
        if (originalLetter != null) originalLetter.SetActive(true);
        if (piecesGroup != null) piecesGroup.SetActive(false);

        // 위치, 스케일, 투명도 원복
        if (leftPiece != null)
        {
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
            rightPiece.anchoredPosition = rightOriginPos;
            rightPiece.localScale = Vector3.one;
            if (rightImage != null)
            {
                Color c = rightImage.color;
                rightImage.color = new Color(c.r, c.g, c.b, 1f);
            }
        }

        if (confettiParticle != null)
        {
            confettiParticle.Stop();
        }
    }
}