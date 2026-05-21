using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections; // 코루틴 사용을 위해 추가

// 마우스 오버 이벤트를 받기 위해 IPointerEnterHandler, IPointerExitHandler 인터페이스 추가
public class InventorySlot : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    public Image itemIcon;
    public bool isEmpty = true;
    public BaseItemDataSO currentItem;

    private InventoryManager manager;

    [Header("호버 효과 설정")]
    [SerializeField] private float hoverScaleFactor = 0.93f; // 살짝 눌린 크기
    [SerializeField] private float transitionDuration = 0.07f; // 변하는 시간
    private Vector3 originalScale = Vector3.one; // 안전하게 기본값 1로 세팅
    private Coroutine scaleCoroutine;


    public void Initialize(InventoryManager invManager)
    {
        manager = invManager;

        // 가장 안전한 시점: 매니저가 초기화하라고 명령할 때 내 원래 크기를 저장합니다.
        originalScale = transform.localScale;

        ClearSlot();
    }

    public void SetItem(BaseItemDataSO item)
    {
        currentItem = item;
        isEmpty = false;
        itemIcon.sprite = item.icon;
        itemIcon.color = Color.white;
        itemIcon.gameObject.SetActive(true);
    }

    public void ClearSlot()
    {
        currentItem = null;
        isEmpty = true;
        itemIcon.sprite = null;
        itemIcon.gameObject.SetActive(false);

        // 슬롯이 비워질 때 크기 연출 중이었다면 초기화
        if (scaleCoroutine != null) StopCoroutine(scaleCoroutine);
        transform.localScale = originalScale;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (isEmpty) return;

        // 좌클릭: 스낵 먹기 또는 피규어 상세 보기
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            if (!ShopManager.IsShopOpen && currentItem is SnackItemSO snack)
            {
                // 현재 먹으려는 스낵이 페퍼민트인데, 이미 DiceManager에서 효과가 활성 상태라면
                if (snack.snackType == SnackType.Peppermint && manager.diceManager.isPeppermintActive)
                {
                    Debug.Log("이미 페퍼민트 효과가 활성화되어 있어 다시 먹을 수 없습니다!");
                    return; // 여기서 함수를 종료하면 아래의 snack.ApplyItemEffect와 ClearSlot이 실행되지 않습니다.
                }

                snack.ApplyItemEffect(manager.diceManager);
                if (TutorialManager.Instance != null && TutorialManager.Instance.isTutorialActive)
                {
                    TutorialManager.Instance.OnItemUsed(currentItem.itemName);
                }
                ClearSlot(); // 효과가 적용된 후에만 슬롯을 비웁니다.
                manager.HideSellPopup();
                manager.HideTooltip();
            }
            else if (currentItem is FigureItemSO figure)
            {
                // 피규어 클릭 시 상세 패널 열기
                if (manager.figureDetailPanel != null)
                {
                    manager.figureDetailPanel.OpenPanel(manager.ownedFigures, figure);
                }
            }
        }
        // 우클릭: 피규어 판매 팝업 띄우기
        else if (eventData.button == PointerEventData.InputButton.Right)
        {
            if (currentItem is FigureItemSO)
            {
                manager.ShowSellPopup(this);
            }
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!isEmpty && currentItem != null)
        {
            // 피규어일 때만 눌리는 연출 실행 (상세 창이 열려있지 않을 때만)
            if (currentItem is FigureItemSO)
            {
                if (!FigureDetailPanel.IsPanelOpen)
                {
                    StartScaleTransition(originalScale * hoverScaleFactor);
                }
                return;
            }

            // 인벤토리 매니저에게 내 위치(RectTransform)와 설명을 전달하여 툴팁 띄우기
            manager.ShowTooltip(currentItem.description, GetComponent<RectTransform>());
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // 피규어였다면 마우스가 나갈 때 원래 크기로 복구
        if (!isEmpty && currentItem is FigureItemSO)
        {
            StartScaleTransition(originalScale);
        }

        manager.HideTooltip();
    }

    // --- Smooth한 크기 변화를 위한 Coroutine 제어 로직 ---
    private void StartScaleTransition(Vector3 targetScale)
    {
        if (scaleCoroutine != null) StopCoroutine(scaleCoroutine);
        scaleCoroutine = StartCoroutine(ScaleRoutine(targetScale));
    }

    private IEnumerator ScaleRoutine(Vector3 target)
    {
        Vector3 start = transform.localScale;
        float elapsed = 0f;

        while (elapsed < transitionDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / transitionDuration;

            t = Mathf.Sin(t * Mathf.PI * 0.5f);

            transform.localScale = Vector3.Lerp(start, target, t);
            yield return null;
        }

        transform.localScale = target;
        scaleCoroutine = null;
    }
}