using UnityEngine;
using TMPro;
using DG.Tweening;

public class ToastPopupController : MonoBehaviour
{
    public static ToastPopupController Instance { get; private set; }

    [Header("UI 컴포넌트 연결")]
    [SerializeField] private CanvasGroup canvasGroup;     
    [SerializeField] private TextMeshProUGUI messageText;   
    [SerializeField] private RectTransform rectTransform;  

    [Header("연출 세부 설정")]
    [SerializeField] private float fadeDuration = 0.25f;   
    [SerializeField] private float delayDuration = 1.2f;   
    [SerializeField] private float moveOffset = 35f;     

    private Sequence toastSequence;
    private Vector2 originPosition;

    void Awake()
    {
        // 1. 싱글톤 가드 및 인스턴스 할당
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); 
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        DOTween.Init(true, true, LogBehaviour.ErrorsOnly);

        // 2. 컴포넌트 자동 찾기 안전장치
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (rectTransform == null) rectTransform = GetComponent<RectTransform>();

        // 3. 초기 위치 기억 및 투명도 리셋
        if (rectTransform != null) originPosition = rectTransform.anchoredPosition;
        if (canvasGroup != null) canvasGroup.alpha = 0f;
    }

    public void ShowToast(string message)
    {
        // 1. 혹시 누락된 레퍼런스가 있다면 연출을 실행하지 않고 리턴 (에러 방지)
        if (canvasGroup == null || rectTransform == null || messageText == null)
        {
            Debug.LogWarning("[ToastPopup] 필수 UI 컴포넌트가 슬롯에 연결되지 않았습니다!");
            return;
        }

        // 2. 팝업 오브젝트 활성화
        gameObject.SetActive(true);

        // 3. 매개변수로 들어온 문구 텍스트 꽂기
        messageText.text = message;

        // 4. 연타로 버튼을 눌렀을 때 기존 연출 강제 종료 (트윈 꼬임 방지 치트키)
        if (toastSequence != null && toastSequence.IsActive())
        {
            toastSequence.Kill();
        }

        // 5. 연출 시작 지점으로 상태값 리셋
        canvasGroup.alpha = 0f;
        rectTransform.anchoredPosition = originPosition;

        toastSequence = DOTween.Sequence();

        toastSequence

            .Append(canvasGroup.DOFade(1f, fadeDuration))
            .Join(rectTransform.DOAnchorPos(originPosition + new Vector2(0, moveOffset), fadeDuration).SetEase(Ease.OutQuad))

            .AppendInterval(delayDuration)

            .Append(canvasGroup.DOFade(0f, fadeDuration))

            .OnComplete(() =>
            {
                gameObject.SetActive(false);
                rectTransform.anchoredPosition = originPosition; // 위치 완벽 리셋
            })
            // 타임스케일이 0(일시정지)이 되어도 팝업 알림은 정상 재생되도록 설정
            .SetUpdate(true);
    }

    private void OnDestroy()
    {
        if (toastSequence != null && toastSequence.IsActive())
        {
            toastSequence.Kill();
        }
    }
}