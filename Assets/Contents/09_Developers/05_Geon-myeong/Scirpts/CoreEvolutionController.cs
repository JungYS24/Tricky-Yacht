using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class CoreEvolutionController : MonoBehaviour
{
    [Header("오브젝트 참조 (UI Image)")]
    [SerializeField] private RectTransform coreTarget;

    [Header("연출 상세 설정 (3단계: 등장 & 확대)")]
    [SerializeField] private float appearDuration = 0.5f;
    [SerializeField] private float pulseStrength = 1.1f;
    [SerializeField] private float expansionDuration = 0.8f;
    [SerializeField] private float expandedScale = 15f;
    [SerializeField] private float fadeOutDuration = 0.4f;

    private Vector3 originalScale = Vector3.one;
    private Tween pulseTween;
    private CanvasGroup canvasGroup;

    private void Awake()
    {
        DOTween.Init();

        if (coreTarget != null)
        {
            coreTarget.localScale = Vector3.zero;

            canvasGroup = coreTarget.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = coreTarget.gameObject.AddComponent<CanvasGroup>();
            }
        }
    }

    private void Start()
    {
        StartCoreEvolution();
    }

    [ContextMenu("Play Core Evolution")]
    public void StartCoreEvolution()
    {
        if (coreTarget == null) return;

        ResetCore();

        Sequence evolutionSeq = DOTween.Sequence();

        // 1. 등장
        evolutionSeq.Append(coreTarget.DOScale(originalScale, appearDuration).SetEase(Ease.OutBack));

        // 2. 강조 (맥동)
        pulseTween = coreTarget.DOScale(originalScale * pulseStrength, 0.2f)
            .SetEase(Ease.InOutSine)
            .SetLoops(2, LoopType.Yoyo);
        evolutionSeq.Append(pulseTween);

        // 3. 거대화 확장
        evolutionSeq.Append(coreTarget.DOScale(Vector3.one * expandedScale, expansionDuration).SetEase(Ease.InExpo));

        // 4. 사라지기 (Fade Out)
        if (canvasGroup != null)
        {
            evolutionSeq.Append(canvasGroup.DOFade(0f, fadeOutDuration).SetEase(Ease.OutQuad));
        }

        // ★ [핵심] 구체 연출이 완료되면 카드 3장 플립 연출 시작! ★
        evolutionSeq.OnComplete(() =>
        {
            coreTarget.gameObject.SetActive(false);
            Debug.Log("<color=green>[Core] 구체 연출 완료 -> 카드 플립 연출 시작!</color>");

            // 카드 매니저를 찾아서 4단계 실행
            CardFlipManager flipManager = FindObjectOfType<CardFlipManager>();
            if (flipManager != null)
            {
                flipManager.PlayCardFlipSequence();
            }
        });
    }

    public void ResetCore()
    {
        if (pulseTween != null && pulseTween.IsActive()) pulseTween.Kill();

        if (coreTarget != null)
        {
            coreTarget.gameObject.SetActive(true);
            coreTarget.localScale = Vector3.zero;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
        }
    }
}