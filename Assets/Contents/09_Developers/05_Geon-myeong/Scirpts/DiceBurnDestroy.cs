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

        // 1. 불꽃 파티클 재생
        if (fireParticle != null)
        {
            fireParticle.gameObject.SetActive(true);
            fireParticle.Play();
        }

        Sequence burnSeq = DOTween.Sequence();

        // 2. 부들부들 떨리는 흔들림
        if (diceRect != null)
        {
            burnSeq.Join(diceRect.DOShakeAnchorPos(burnDuration, 12f, 25, 90f, false));
        }

        // 3. ⭐ [핵심] 셰이더의 불타는 수치(0 -> 1)를 올려서 불타며 타들어가는 연출
        if (instancedMaterial != null)
        {
            burnSeq.Join(
                DOTween.To(() => 0f, x => instancedMaterial.SetFloat(BurnAmountID, x), 1f, burnDuration)
                       .SetEase(Ease.InQuad)
            );
        }

        // 4. 완료 후 비활성화
        burnSeq.OnComplete(() =>
        {
            if (fireParticle != null) fireParticle.Stop();
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