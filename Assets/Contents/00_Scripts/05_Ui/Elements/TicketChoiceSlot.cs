using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems; 
using DG.Tweening;

//마우스 감지
public class TicketChoiceSlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public Image ticketIcon;
    public TextMeshProUGUI handNameText;
    public Button selectButton;

    private TicketItemSO currentTicketData;
    private ShopManager shopManager;

    private Vector2 startPosition;
    private bool hasStarPosition = false;



    public void Setup(TicketItemSO data, ShopManager manager)
    {
        RectTransform rect = GetComponent<RectTransform>();

        //기존 애니메이션 정지
        StopIdleAnimation();
        rect.DOKill();

        CanvasGroup canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        canvasGroup.DOKill();

        //처음 실행미녀 현재위치를 저장하고,두 번째부터는 저장했던 원래 위치로 복구
        if (hasStarPosition)
        {
            rect.anchoredPosition = startPosition;
        }
        else
        {
            startPosition = rect.anchoredPosition;
            hasStarPosition = true;
        }

        //크기 회전 투명도 초기화
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        canvasGroup.alpha = 1f;

        //현재 정상 위치 저장
        startPosition = rect.anchoredPosition;

        currentTicketData = data;
        shopManager = manager;

        ticketIcon.sprite = data.icon;
        handNameText.text = data.itemName;

        selectButton.onClick.RemoveAllListeners();
        selectButton.onClick.AddListener(OnClicked);
    }

    private void OnClicked()
    {
        // ⭐ ShopManager에게 "이 티켓 선택했어!"라고 전달
        shopManager.SelectTicketWithVFX(this, currentTicketData);
    }

    //마우스가 버튼 위에 올라왔을 때
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (currentTicketData != null && shopManager != null)
        {
            shopManager.ShowTooltip(currentTicketData.description, GetComponent<RectTransform>());
        }
    }

    //마우스가 버튼에서 빠져나갔을 때
    public void OnPointerExit(PointerEventData eventData)
    {
        if (shopManager != null)
        {
            // 툴팁 숨기기
            shopManager.HideTooltip();
        }
    }

    public void PlayAppearEffect(float delay)
    {

        RectTransform rect = GetComponent<RectTransform>();

        // 기존 rect 제거
        rect.DOKill();

        //처음에는 안 보이게
        rect.localScale = Vector3.zero;

        //카드가 옆으로 돌아가 있는 상태
        rect.localRotation = Quaternion.Euler(0f, -90f, 0f);

        Sequence seq = DOTween.Sequence();

        seq.SetDelay(delay);

        //카드가 쾅 하고 커지면서 등장
        seq.Append(
            rect.DOScale(Vector3.one, 0.5f)
            .SetEase(Ease.OutBack)
            );


        //동시에 뒤집히면서 정면을 바라봄
        seq.Join(
            rect.DOLocalRotate(
                Vector3.zero,
                0.5f,
                RotateMode.FastBeyond360
            )
                .SetEase(Ease.OutCubic)
                );

        //등장 완료 후 둥실둥실 시작
        seq.OnComplete(() =>
        {
            StartIdleAnimation();
        });
    }

    private Tween idleTween;

    public void StartIdleAnimation()
    {
        StopIdleAnimation();

        RectTransform rect = GetComponent<RectTransform>();

        Sequence idleSeq = DOTween.Sequence();

        // ⭐ 위로 살짝 이동 + 살짝 기울이기
        idleSeq.Append(
            rect.DOAnchorPosY(
                rect.anchoredPosition.y + 12f,
                0.8f
            )
            .SetEase(Ease.InOutSine)
        );

        idleSeq.Join(
            rect.DOLocalRotate(
                new Vector3(0f, 0f, 3f),
                0.8f
            )
            .SetEase(Ease.InOutSine)
        );

        // ⭐ 다시 원래 위치 + 반대쪽으로 살짝 기울이기
        idleSeq.Append(
            rect.DOAnchorPosY(
                rect.anchoredPosition.y,
                0.8f
            )
            .SetEase(Ease.InOutSine)
        );

        idleSeq.Join(
            rect.DOLocalRotate(
                new Vector3(0f, 0f, -3f),
                0.8f
            )
            .SetEase(Ease.InOutSine)
        );

        // ⭐ 개별 Tween이 아니라 Sequence 전체를 무한 반복
        idleSeq.SetLoops(-1, LoopType.Yoyo);

        idleTween = idleSeq;
    }

    public void StopIdleAnimation()
    {
        if (idleTween != null && idleTween.IsActive())
        {
            idleTween.Kill();
            idleTween = null;
        }
    }

    public void ResetTicketAnimation()
    {
        StopIdleAnimation();

        RectTransform rect = GetComponent<RectTransform>();

        rect.DOKill();

        CanvasGroup canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        canvasGroup.DOKill();
        canvasGroup.alpha = 1f;
    }
}