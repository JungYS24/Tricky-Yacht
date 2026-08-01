using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class CoreEvolutionController : MonoBehaviour
{
    [Header("오브젝트 참조 (UI Image)")]
    [SerializeField] private RectTransform coreTarget; // 빛나는 구체 (Core)

    [Header("연출 상세 설정 (3단계: 등장 & 확대)")]
    [SerializeField] private float appearDuration = 0.5f;     // 등장 시간
    [SerializeField] private float pulseStrength = 1.1f;       // 심장 박동 강조 비율
    [SerializeField] private float expansionDuration = 0.8f;  // 거대화 확장 시간
    [SerializeField] private float expandedScale = 15f;       // 거대해질 크기 배율
    [SerializeField] private float fadeOutDuration = 0.4f;   // 사라지는 시간 (추가됨!)

    private Vector3 originalScale = Vector3.one;
    private Tween pulseTween;
    private CanvasGroup canvasGroup;

    private void Awake()
    {
        DOTween.Init();

        if (coreTarget != null)
        {
            coreTarget.localScale = Vector3.zero;

            // CanvasGroup 컴포넌트 가져오기 (없으면 자동으로 붙여줌)
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

        // 1. 등장 (Scale 0 -> 1)
        evolutionSeq.Append(coreTarget.DOScale(originalScale, appearDuration).SetEase(Ease.OutBack));

        // 2. 강조 (맥동)
        pulseTween = coreTarget.DOScale(originalScale * pulseStrength, 0.2f)
            .SetEase(Ease.InOutSine)
            .SetLoops(2, LoopType.Yoyo);
        evolutionSeq.Append(pulseTween);

        // 3. 거대화 확장 (Scale 1 -> 15)
        evolutionSeq.Append(coreTarget.DOScale(Vector3.one * expandedScale, expansionDuration).SetEase(Ease.InExpo));

        // 4. [신규 추가] 거대해지면서 스르륵 사라지기 (Fade Out)
        if (canvasGroup != null)
        {
            evolutionSeq.Append(canvasGroup.DOFade(0f, fadeOutDuration).SetEase(Ease.OutQuad));
        }

        // 5. 연출 끝난 후 오브젝트 완전히 끄기
        evolutionSeq.OnComplete(() =>
        {
            coreTarget.gameObject.SetActive(false);
            Debug.Log("<color=green>[Core] 연출 완료 및 사라짐!</color>");
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
            canvasGroup.alpha = 1f; // 투명도 원복
        }
    }
}