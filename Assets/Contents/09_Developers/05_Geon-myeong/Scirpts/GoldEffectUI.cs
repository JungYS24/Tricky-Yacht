using UnityEngine;
using TMPro;
using DG.Tweening;
public class GoldEffectUI : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    [SerializeField] private TextMeshProUGUI effectText;

    [Header("연출 설정")]
    [SerializeField] private float moveDistance = 70f;
    [SerializeField] private float duration = 0.7f;

    private RectTransform rect;
    private CanvasGroup canvasGroup;
    private Vector2 startPos;

    private void Awake()
    {
        rect = effectText.GetComponent<RectTransform>();

        canvasGroup = effectText.GetComponent<CanvasGroup>();

        if (canvasGroup == null)
            canvasGroup = effectText.gameObject.AddComponent<CanvasGroup>();

        startPos = rect.anchoredPosition;

        // 평소에는 숨김
        canvasGroup.alpha = 0f;
    }


    // 
    public void PlayGain(int amount)
    {
        PlayEffect("+" + amount, true);
    }

    public void PlaySpend(int amount)
    {
        PlayEffect("-" + amount, false);
    }

    private void PlayEffect(string text, bool isGain)
    {
        rect.DOKill();
        canvasGroup.DOKill();

        rect.anchoredPosition = startPos;
        rect.localScale = Vector3.one;

        // ⭐ 이펙트 시작할 때 반드시 보여줌
        canvasGroup.alpha = 1f;

        effectText.text = text;

        Sequence seq = DOTween.Sequence();

        seq.Append(
            rect.DOScale(1.3f, 0.12f)
                .SetEase(Ease.OutBack)
        );

        seq.Append(
            rect.DOScale(1f, 0.12f)
        );

        float direction = isGain ? 1f : -1f;

        seq.Join(
            rect.DOAnchorPosY(
                startPos.y + (moveDistance * direction),
                duration
            )
            .SetEase(Ease.OutCubic)
        );

        seq.Join(
            canvasGroup.DOFade(0f, duration)
        );
    }
}
