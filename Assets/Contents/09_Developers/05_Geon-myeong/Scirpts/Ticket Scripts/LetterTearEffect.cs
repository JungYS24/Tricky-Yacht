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

    [Header("티켓 선택 시스템 연결")]
    [SerializeField] private ShopManager shopManager;

    [Header("파티클 이펙트")]
    [SerializeField] private ParticleSystem confettiParticle; // 컨페티 파티클 (ConfettiFX)

    [Header("카드팩 개봉 빛 연출")]
    [SerializeField] private Image tearGlow;   // 원형 후광
    [SerializeField] private Image tearRays;   // 방사형 빛
    [SerializeField] private Image tearFlash;  // 순간 플래시

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

        if (shopManager == null)
        {
            shopManager = FindFirstObjectByType<ShopManager>();
        }

        // ⭐ 게임 시작 시 빛 이펙트 완전히 숨기기
        HideLightEffects();
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

        // ⭐ 빛 이펙트 초기화
        SetupLightEffect(tearGlow);
        SetupLightEffect(tearRays);
        SetupLightEffect(tearFlash);

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

        // 4. 조각 + 빛 애니메이션
        Sequence seq = DOTween.Sequence();


        // ⭐ 여기에 넣기!
        if (tearRays != null)
        {
            tearRays.gameObject.SetActive(true);

            tearRays.transform.localScale = Vector3.one * 0.7f;

            Color color = tearRays.color;
            color.a = 0f;
            tearRays.color = color;

            seq.Append(
                tearRays.DOFade(0.55f, 0.08f)
            );

            seq.Join(
                tearRays.transform
                    .DOScale(1.15f, 0.25f)
                    .SetEase(Ease.OutCubic)
            );

            seq.Append(
                tearRays.DOFade(0f, 0.18f)
                    .SetEase(Ease.OutQuad)
            );
        }


        // ↓ 그 아래에 기존 봉투 찢어지는 코드
        if (leftPiece != null)
        {
            // 왼쪽 조각 코드
        }

        if (rightPiece != null)
        {
            // 오른쪽 조각 코드
        }


        // ↓ 마지막
        seq.OnComplete(() =>
        {
            if (piecesGroup != null)
                piecesGroup.SetActive(false);

            if (tearRays != null)
                tearRays.gameObject.SetActive(false);

            if (shopManager != null)
                shopManager.PlayTicketAppearEffects();
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

    private void SetupLightEffect(Image image)
    {
        if (image == null)
            return;

        image.DOKill();
        image.transform.DOKill();

        Color color = image.color;
        color.a = 0f;
        image.color = color;

        image.transform.localScale = Vector3.one;
        image.transform.localRotation = Quaternion.identity;

        image.gameObject.SetActive(false);
    }

    private void HideLightEffects()
    {
        if (tearGlow != null)
            tearGlow.gameObject.SetActive(false);

        if (tearRays != null)
            tearRays.gameObject.SetActive(false);

        if (tearFlash != null)
            tearFlash.gameObject.SetActive(false);
    }
}