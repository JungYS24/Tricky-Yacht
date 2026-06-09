using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class LobbyTitleJuice : MonoBehaviour
{
    private RectTransform rectTransform;
    private Image titleImage;

    [Header("Punch Scale Settings")]
    public float punchDuration = 1.2f;   
    public Vector3 punchScale = new Vector3(0.1f, 0.1f, 0f); 
    public int vibrato = 2;                 
    public float elasticity = 0.5f;        

    [Header("Blink Alpha Settings")]
    public float minAlpha = 0.4f;           
    public float blinkDuration = 0.6f;      

    void Start()
    {
        rectTransform = GetComponent<RectTransform>();
        titleImage = GetComponent<Image>();

        rectTransform.DOPunchScale(punchScale, punchDuration, vibrato, elasticity)
            .SetLoops(-1, LoopType.Restart)
            .SetEase(Ease.InOutQuad);

        if (titleImage != null)
        {
            titleImage.DOFade(minAlpha, blinkDuration)
                .SetLoops(-1, LoopType.Yoyo) // 요요 모드로 리버스 재생되게 설정
                .SetEase(Ease.InOutSine);
        }
    }

    void OnDestroy()
    {
        transform.DOKill();
    }
}