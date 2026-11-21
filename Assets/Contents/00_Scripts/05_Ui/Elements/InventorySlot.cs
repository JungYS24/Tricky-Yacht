using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections; // 코루틴 사용을 위해 추가
using TMPro;

// 마우스 오버 이벤트를 받기 위해 IPointerEnterHandler, IPointerExitHandler 인터페이스 추가
public class InventorySlot : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    public Image itemIcon;
    public bool isEmpty = true;
    public BaseItemDataSO currentItem;

    // 같은 스낵이 몇 개 겹쳐 있는지 표시. 씬에서 연결하지 않으면 처음 2개가 될 때 코드가 만든다.
    public TextMeshProUGUI countText;

    private InventoryManager manager;
    public SnackUseEntry PendingSnack { get; private set; }

    // 이 칸에 들어 있는 개수. 스낵은 같은 것끼리 한 칸에 쌓이고, 피규어는 항상 1개.
    public int Count { get; private set; }

    // 지금 가지고 있다고 볼 개수. 이번 턴에 먹기로 확정됐지만 아직 사라지지 않은 1개는 뺀다(소모 방지 피규어로 지킨 것은 포함).
    public int OwnedSnackCount
    {
        get
        {
            if (isEmpty) return 0;
            bool eating = PendingSnack != null && PendingSnack.committed && !PendingSnack.preserved && !PendingSnack.settled;
            return eating ? Count - 1 : Count;
        }
    }

    public bool CountsAsOwnedSnack => OwnedSnackCount > 0;

    public void MarkSnackUsed(SnackUseEntry entry, Color tint)
    {
        PendingSnack = entry;
        SnackUseFeedback.Get(this).ShowUsed(tint);
    }

    public void ReleaseSnackReservation()
    {
        PendingSnack = null;
        var feedback = GetComponent<SnackUseFeedback>();
        if (feedback != null) feedback.ResetVisual();
    }

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

    public void SetItem(BaseItemDataSO item) => SetItem(item, 1);

    public void SetItem(BaseItemDataSO item, int count)
    {
        ReleaseSnackReservation();
        currentItem = item;
        isEmpty = false;
        itemIcon.sprite = item.icon;
        itemIcon.color = Color.white;
        itemIcon.gameObject.SetActive(true);
        FigureFeedback.Bind(this, item as FigureItemSO);
        Count = Mathf.Max(1, count);
        RefreshCount();
    }

    // 같은 스낵을 더 얻었을 때. 이번 턴 사용 예약·연출은 건드리지 않는다.
    public void AddCount(int amount)
    {
        if (isEmpty || amount <= 0) return;
        Count += amount;
        RefreshCount();
    }

    // 1개를 뺀다. 마지막 1개였으면 칸을 비운다.
    public void RemoveOne()
    {
        if (isEmpty) return;
        if (Count <= 1) { ClearSlot(); return; }
        Count--;
        RefreshCount();
    }

    public void ClearSlot()
    {
        ReleaseSnackReservation();
        FigureFeedback.Bind(this, null);
        currentItem = null;
        isEmpty = true;
        Count = 0;
        itemIcon.sprite = null;
        itemIcon.gameObject.SetActive(false);

        if (countText != null) countText.gameObject.SetActive(false);

        // 슬롯이 비워질 때 크기 연출 중이었다면 초기화
        if (scaleCoroutine != null) StopCoroutine(scaleCoroutine);
        transform.localScale = originalScale;
    }

    // 2개 이상일 때만 칸 오른쪽 아래에 "×3"처럼 보여 준다.
    private void RefreshCount()
    {
        if (Count < 2)
        {
            if (countText != null) countText.gameObject.SetActive(false);
            return;
        }
        EnsureCountText();
        LayoutCountText();
        countText.SetText("×{0}", Count);
        countText.gameObject.SetActive(true);
    }

    // 디버프 개수 표시(DebuffTrackerHud)와 같은 숫자 글꼴·외곽선을 쓴다.
    private void EnsureCountText()
    {
        if (countText != null) return;
        var go = new GameObject("Snack Count", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.layer = gameObject.layer;
        var rect = (RectTransform)go.transform;
        rect.SetParent(transform, false);
        rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        countText = go.GetComponent<TextMeshProUGUI>();
        TMP_FontAsset font = DebuffFeedback.NumberFont();
        if (font != null) countText.font = font;
        Material outline = DebuffFeedback.NumberMaterial(countText.font);
        if (outline != null) countText.fontSharedMaterial = outline;
        countText.alignment = TextAlignmentOptions.BottomRight;
        countText.textWrappingMode = TextWrappingModes.NoWrap;
        countText.color = Color.white;
        countText.raycastTarget = false;
        createdCountText = true;
    }

    private bool createdCountText;

    // 코드가 만든 글자만 칸 크기에 맞춰 배치한다(씬에서 직접 연결한 글자는 그대로 둔다).
    private void LayoutCountText()
    {
        if (!createdCountText) return;
        var own = (RectTransform)transform;
        float size = Mathf.Min(own.rect.width, own.rect.height);
        float fontRatio = manager != null ? manager.snackCountFontScale : 0.26f;
        Vector2 offset = manager != null ? manager.snackCountPos : new Vector2(0.08f, -0.18f);
        RectTransform rect = countText.rectTransform;
        rect.sizeDelta = new Vector2(size, size * 0.45f);
        rect.anchoredPosition = new Vector2(size * offset.x, size * offset.y);
        countText.fontSize = size * fontRatio;
        rect.SetAsLastSibling(); // 스낵이 사라질 때 생기는 빛 알갱이보다 위에 보이게
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (isEmpty) return;
        if (PendingSnack != null)
        {
            if (eventData.button == PointerEventData.InputButton.Left) manager.SnackUses.TryCancel(this);
            return;
        }

        //몬스터가 죽어있는 상태(전투 종료 및 연출 대기 중)라면 인벤토리 상호작용 차단!
        // 지도 화면에서는 직전 전투의 몬스터가 다음 전투 전까지 죽은 상태로 남아 있으므로 막지 않는다.
        DiceManager dm = manager.diceManager;
        bool onMap = dm != null && dm.mapPanel != null && dm.mapPanel.gameObject.activeInHierarchy;
        if (!onMap && dm != null && dm.enemy != null && dm.enemy.IsDead)
        {
            return;
        }

        // 좌클릭: 스낵 먹기 또는 피규어 상세 보기
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            if (!ShopManager.IsShopOpen && currentItem is SnackItemSO snack)
            {
                // 결산 시작부터 다음 라운드 시작 전까지 스낵 사용 차단
                if (manager.diceManager == null ||
                    manager.diceManager.isCalculating)
                {
                    return;
                }
                // 현재 먹으려는 스낵이 페퍼민트인데, 이미 DiceManager에서 효과가 활성 상태라면
                if (snack.snackType == SnackType.Peppermint && manager.diceManager.isPeppermintActive)
                {
                    Debug.Log("이미 페퍼민트 효과가 활성화되어 있어 다시 먹을 수 없습니다!");
                    return; // 여기서 함수를 종료하면 아래의 snack.ApplyItemEffect와 ClearSlot이 실행되지 않습니다.
                }

                // 실제 효과와 사용 후 자리 예약은 스낵 담당에 위임합니다.
                // 소모 방지 판정, OnSnackUsed 피규어 알림, 튜토리얼 알림도 한 번만 처리합니다.
                manager.SnackUses.TryUse(this, snack);
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
            //피규어일 때 호버(눌림) 연출 실행
            if (currentItem is FigureItemSO)
            {
                // 상세 패널이 열려있으면 꿀렁이는 연출 방지
                if (!FigureDetailPanel.IsPanelOpen)
                {
                    StartScaleTransition(originalScale * hoverScaleFactor);
                }
                return;
            }

            // 인벤토리 매니저에게 내 위치(RectTransform)와 설명을 전달하여 툴팁 띄우기
            manager.ShowTooltip(LocalizationManager.GetItemDescription(currentItem), GetComponent<RectTransform>());
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        //마우스가 나갈 때 원래 크기로 복구 (피규어)
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
