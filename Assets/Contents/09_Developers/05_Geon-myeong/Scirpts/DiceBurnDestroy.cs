using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;

public class DiceBurnDestroy : MonoBehaviour, IPointerClickHandler
{
    [Header("타겟 설정")]
    [SerializeField] private Image diceImage;

    [Header("불타는 연출 셰이더 매터리얼")]
    [SerializeField] private Material burnMaterial; // ⭐ 불타는 셰이더가 적용된 Material

    [Header("파티클 이펙트")]
    [SerializeField] private ParticleSystem fireParticle;

    [Header("연출 시간")]
    [SerializeField] private float burnDuration = 1.2f;

    private Material instancedMaterial;
    private RectTransform diceRect;
    private bool isBurning = false;

    // 셰이더 내부 프로퍼티 이름 (보통 _DissolveAmount, _BurnAmount, _Cutoff 등)
    private static readonly int BurnAmountID = Shader.PropertyToID("_BurnAmount");

    private void Awake()
    {
        if (diceImage == null) diceImage = GetComponent<Image>();
        if (diceImage != null)
        {
            diceRect = diceImage.GetComponent<RectTransform>();

            // 매터리얼 복사본 생성 (다른 주사위에 영향 주지 않도록)
            if (burnMaterial != null)
            {
                instancedMaterial = new Material(burnMaterial);
                diceImage.material = instancedMaterial;
                instancedMaterial.SetFloat(BurnAmountID, 0f); // 처음엔 안 탄 상태 (0)
            }
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        PlayBurnSequence();
    }

    public void PlayBurnSequence()
    {
        if (isBurning) return;
        isBurning = true;

        if (fireParticle != null)
        {
            fireParticle.gameObject.SetActive(true);
            fireParticle.Play();
        }

        Sequence burnSeq = DOTween.Sequence();

        if (diceRect != null)
        {
            // 1. 불붙기 직전 살짝 팽창
            burnSeq.Append(
                diceRect.DOScale(1.12f, 0.12f)
                    .SetEase(Ease.OutBack)
            );

            // 2. 흔들리면서 불타기
            burnSeq.Join(
                diceRect.DOShakeAnchorPos(
                    burnDuration,
                    12f,
                    25,
                    90f,
                    false
                )
            );

            // 3. 타면서 점점 작아짐
            burnSeq.Join(
                diceRect.DOScale(
                    Vector3.zero,
                    burnDuration
                )
                .SetEase(Ease.InBack)
            );

            // 4. 살짝 회전
            burnSeq.Join(
                diceRect.DOLocalRotate(
                    new Vector3(0f, 0f, 15f),
                    burnDuration
                )
                .SetEase(Ease.InQuad)
            );
        }

        // 기존 Burn Shader
        if (instancedMaterial != null)
        {
            burnSeq.Join(
                DOTween.To(
                    () => 0f,
                    x => instancedMaterial.SetFloat(BurnAmountID, x),
                    1f,
                    burnDuration
                )
                .SetEase(Ease.InQuad)
            );
        }

        burnSeq.OnComplete(() =>
        {
            if (fireParticle != null)
                fireParticle.Stop();

            gameObject.SetActive(false);
        });
    }

    public void ResetDice()
    {
        isBurning = false;
        if (instancedMaterial != null)
        {
            instancedMaterial.SetFloat(BurnAmountID, 0f);
        }
        gameObject.SetActive(true);
    }
}