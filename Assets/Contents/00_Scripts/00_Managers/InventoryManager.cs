using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Collections;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance { get; private set; }

    [Header("참조")]
    public DiceManager diceManager;
    public FigureDetailPanel figureDetailPanel; // 피규어 상세 정보 패널

    [Header("슬롯 배열")]
    public Transform figureSlotParent; // 피규어가 생성될 부모 위치 (FigureSlotArea)
    public GameObject figureSlotPrefab; // 피규어 슬롯 프리팹
    public InventorySlot[] snackSlots;

    [Header("스낵 사용 연출")]
    public Color snackUsedTint = new Color(0.55f, 0.55f, 0.55f, 0.85f);
    [Min(0.05f)] public float snackDissolveDuration = 0.65f;
    [Min(0.05f)] public float snackRestoreDuration = 0.18f;
    private SnackUseController snackUses;
    public SnackUseController SnackUses => snackUses ?? (snackUses = new SnackUseController(this));

    //보유 중인 티켓 리스트 (먹은 장수만큼 같은 티켓이 여러 번 들어감, 화면에는 도감 티켓 탭에서 보여줌)
    public List<TicketItemSO> ownedTickets = new List<TicketItemSO>();

    [Header("인벤토리 용량 제한")]
    public int maxSnackSlots = 5;   // 스낵칸 최대 5개로 제한

    // 보유 중인 피규어 리스트 (무한 소지)
    public List<FigureItemSO> ownedFigures = new List<FigureItemSO>();
    private List<GameObject> activeFigureSlots = new List<GameObject>();

    [Header("판매 팝업 UI")]
    public GameObject sellPopupRoot;
    public GameObject sellPopupPanel;     // 실제 그래픽이 있는 팝업창 (마우스 따라다닐 부분)
    public Button sellButton;             // 판매 확인 버튼
    public Button backgroundCloseButton;  // 팝업 뒤에 깔린 투명한 전체화면 닫기 버튼
    public TextMeshProUGUI sellPriceText;

    // [추가] 툴팁 UI
    [Header("설명창(Tooltip) UI")]
    public GameObject tooltipPanel;
    public RectTransform tooltipRect;
    public TextMeshProUGUI descText;
    private ItemTooltipLayout tooltipLayout;

    private InventorySlot targetSellSlot;

    private void Awake()
    {
        Instance = this;

        // 에디터에서 실수로 꺼두었더라도 시작 시 자동으로 피규어 영역을 켜줌
        if (figureSlotParent != null)
        {
            figureSlotParent.gameObject.SetActive(true);
        }

        // 스낵 슬롯 초기화
        foreach (var slot in snackSlots) slot.Initialize(this);

        if (sellButton != null) sellButton.onClick.AddListener(SellTargetItem);

        // 취소 버튼 대신 투명한 배경을 누르면 팝업이 닫히도록 연결
        if (backgroundCloseButton != null) backgroundCloseButton.onClick.AddListener(HideSellPopup);

        // 툴팁 RectTransform 자동 연결
        if (tooltipRect == null && tooltipPanel != null)
            tooltipRect = tooltipPanel.GetComponent<RectTransform>();

        HideSellPopup();
        HideTooltip(); // 시작할 때 툴팁 숨기기
    }

    public void ClearAllSlots()
    {
        snackUses?.Clear();
        // 피규어 슬롯 파괴 및 리스트 초기화
        foreach (var slotGo in activeFigureSlots)
        {
            Destroy(slotGo);
        }
        activeFigureSlots.Clear();
        ownedFigures.Clear();

        if (diceManager != null)
        {
            diceManager.figureKillCounts.Clear();
        }

        FigureEffectManager.Instance?.RebuildCache(); // 초기화 시 캐시 갱신

        //티켓도 같이 비워줌
        ownedTickets.Clear();

        foreach (var slot in snackSlots) slot.ClearSlot();
        Debug.Log("인벤토리의 모든 아이템이 초기화되었습니다.");
    }

    public bool AddItem(BaseItemDataSO item)
    {
        return AddItemInternal(item, true);
    }

    private bool AddItemInternal(BaseItemDataSO item, bool applyAcquiredEffects)
    {
        if (item is FigureItemSO figure)
        {
            // 중복 획득 방지
            if (ownedFigures.Contains(figure))
            {
                Debug.Log("이미 보유한 피규어입니다.");
                return false;
            }

            ownedFigures.Add(figure);

            // 새로 획득한 피규어는 처치 기록을 처음부터 시작
            // 이어하기 RestoreItem에서는 초기화하지 않음
            if (applyAcquiredEffects && diceManager != null)
            {
                diceManager.figureKillCounts.Remove(figure.itemName);
            }

            FigureEffectManager.Instance?.RebuildCache(); // 피규어 추가 시 캐시 갱신

            // 새 슬롯 생성
            GameObject newSlotGo = Instantiate(figureSlotPrefab, figureSlotParent);
            InventorySlot newSlot = newSlotGo.GetComponent<InventorySlot>();
            newSlot.Initialize(this);
            newSlot.SetItem(figure);
            activeFigureSlots.Add(newSlotGo);

            //피규어 획득 즉시(OnAcquired) 발동하는 효과 적용
            // (거북 등껍질, 바나나 왕관 등의 최대 체력 증가)
            var acquiredNode = figure.figureNodes.Find(n => n.triggerType == FigureTriggerType.OnAcquired);

            if (applyAcquiredEffects &&
                 acquiredNode != null &&
                    acquiredNode.effects.Count > 0)
            {
                if (FigureEffectManager.Instance != null)
                {
                    FigureEffectManager.Instance.ApplyFigureEffects(acquiredNode.effects, diceManager, diceManager.shopManager, figure);
                }
            }

            // 도감 영구 해금 기록
            // PlayerPrefs 대신 새로운 매니저를 통해 메모리에 즉시 반영하고 자동 압축 저장
            CollectionDataManager.Instance.UnlockFigure(figure);

            if (diceManager != null && diceManager.enemy != null) diceManager.ForceUpdateUI();
            return true;
        }
        //티켓 아이템이 들어올 경우 처리
        else if (item is TicketItemSO ticket)
        {
            ownedTickets.Add(ticket);
            return true;
        }
        else if (item is SnackItemSO)
        {
            return PlaceIntoEmptySlot(item, snackSlots, maxSnackSlots);
        }
        return false;
    }

    public void CollectTicketNamesForSave(List<string> destination)
    {
        destination.Clear();

        foreach (var ticket in ownedTickets)
        {
            if (ticket != null) destination.Add(ticket.itemName);
        }
    }

    // 해당 족보 티켓을 몇 장 먹었는지 (도감 숫자 = 이 값 + 1)
    public int GetTicketCount(HandType handType)
    {
        int count = 0;
        foreach (var ticket in ownedTickets)
        {
            if (ticket != null && ticket.targetHand == handType) count++;
        }
        return count;
    }

    private bool PlaceIntoEmptySlot(BaseItemDataSO item, InventorySlot[] slots, int maxLimit)
    {
        // 슬롯 배열의 실제 길이와 기획상 최대 길이 중 더 작은 값을 기준으로 삼습니다.
        int limit = Mathf.Min(slots.Length, maxLimit);

        for (int i = 0; i < limit; i++)
        {
            if (slots[i].isEmpty)
            {
                slots[i].SetItem(item);
                return true;
            }
        }
        return false;
    }

    public void ShowSellPopup(InventorySlot slot)
    {
        targetSellSlot = slot;

        int sellPrice = Mathf.FloorToInt(slot.currentItem.price * 0.5f);
        if (sellPriceText != null) sellPriceText.text = LocalizationManager.GetSys("SYS_SELL_PRICE", "판매: {0} G", sellPrice);

        if (sellPopupRoot != null)
        {
            // 전체 팝업 루트를 켭니다 (투명 배경 활성화)
            sellPopupRoot.SetActive(true);

            // 실제 내용물이 있는 작은 팝업창만 마우스(슬롯) 위치 근처로 이동시킵니다
            if (sellPopupPanel != null)
            {
                sellPopupPanel.transform.position = slot.transform.position;
                sellPopupPanel.transform.localPosition += new Vector3(0f, 100f, 0f);

                Vector3 localPos = sellPopupPanel.transform.localPosition;
                localPos.z = 0f;
                sellPopupPanel.transform.localPosition = localPos;
            }
        }
    }

    // 일회성 피규어 파괴(소모)를 위한 함수
    public void RemoveItem(BaseItemDataSO item)
    {
        if (item is FigureItemSO figure && ownedFigures.Contains(figure))
        {
            // 리스트에서 제거
            ownedFigures.Remove(figure);

            if (diceManager != null)
            {
                diceManager.figureKillCounts.Remove(figure.itemName);
            }

            // 해당 피규어가 들어있던 UI 슬롯 찾아 삭제
            for (int i = 0; i < activeFigureSlots.Count; i++)
            {
                InventorySlot slot = activeFigureSlots[i].GetComponent<InventorySlot>();
                if (slot != null && slot.currentItem == figure)
                {
                    FigureFeedback.RemoveSlot(activeFigureSlots[i]);
                    activeFigureSlots.RemoveAt(i);
                    break;
                }
            }

            //인벤토리 변동이 생겼으므로 효과 매니저 캐시 갱신
            FigureEffectManager.Instance?.RebuildCache();
            Debug.Log($"[아이템 소모] {figure.itemName} 파괴 완료");
        }
    }

    public void HideSellPopup()
    {
        if (sellPopupRoot != null) sellPopupRoot.SetActive(false);
        targetSellSlot = null;
    }

    private void SellTargetItem()
    {
        if (targetSellSlot == null || targetSellSlot.isEmpty) return;

        int sellPrice = Mathf.FloorToInt(targetSellSlot.currentItem.price * 0.5f);

        if (diceManager != null && diceManager.shopManager != null)
        {
            diceManager.shopManager.GrantGold(sellPrice);
        }

        Debug.Log($"피규어 [{targetSellSlot.currentItem.itemName}] 판매 완료! +{sellPrice} G");

        if (targetSellSlot.currentItem is FigureItemSO figure)
        {
            // 판매 시 리스트와 씬에서 삭제
            ownedFigures.Remove(figure);
            if (diceManager != null)
            {
                diceManager.figureKillCounts.Remove(figure.itemName);
            }
            FigureEffectManager.Instance?.RebuildCache(); // 피규어 판매 시 캐시 갱신

            activeFigureSlots.Remove(targetSellSlot.gameObject);
            Destroy(targetSellSlot.gameObject);
        }
        else
        {
            targetSellSlot.ClearSlot();
        }

        HideSellPopup();
        HideTooltip(); //판매 후 툴팁 가리기
    }

    // 툴팁 표시 함수
    public void ShowTooltip(string desc, RectTransform slotRect)
    {
        if (descText == null || tooltipPanel == null || slotRect == null) return;
        if (tooltipRect == null) tooltipRect = tooltipPanel.GetComponent<RectTransform>();
        if (tooltipRect == null) return;
        if (tooltipLayout == null) tooltipLayout = new ItemTooltipLayout(tooltipRect, descText);

        descText.text = desc ?? string.Empty;
        tooltipPanel.SetActive(true);

        //툴팁이 다른 모든 UI(튜토리얼 가림막 포함)보다 앞에 오도록 설정
        Canvas canvas = tooltipPanel.GetComponent<Canvas>();
        if (canvas == null) canvas = tooltipPanel.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = 101; // 아주 높은 숫자를 주어 최상단으로 올림

        // 툴팁 패널을 계층 구조의 맨 아래로 보내서 화면상 가장 앞에 오게 합니다.
        tooltipRect.SetAsLastSibling();

        // 툴팁 위치를 슬롯 근처로 조정 (상점과 동일한 방식)
        tooltipRect.pivot = new Vector2(0f, 0.5f);
        tooltipLayout.Fit();
        tooltipRect.position = slotRect.position;
        // x, y 값을 조절하여 마우스/슬롯을 가리지 않게 오프셋 부여
        tooltipRect.localPosition += new Vector3(0f, -50f, 0f);
        tooltipLayout.ClampToCanvas();
    }

    public bool RestoreItem(BaseItemDataSO item)
    {
        return AddItemInternal(item, false);
    }

    //툴팁 숨김 함수
    public void HideTooltip()
    {
        if (tooltipPanel != null) tooltipPanel.SetActive(false);
    }

    // 심연의 딜러 조우자 이벤트를 위한 티켓 완전 초기화 함수
    public void ClearAllTickets()
    {
        ownedTickets.Clear();
    }
}

// 상점과 인벤토리 툴팁에만 사용한다. 원래 폭·글자 크기·배율은 유지하고 높이를 맞춘다.
internal sealed class ItemTooltipLayout
{
    private const float ScreenPadding = 12f;
    private readonly RectTransform panel;
    private readonly TextMeshProUGUI text;
    private readonly float baseWidth;
    private readonly float minHeight;
    private readonly float fontSize;
    private readonly float left, right, top, bottom;
    private readonly Vector4 imageInsets;

    public ItemTooltipLayout(RectTransform panel, TextMeshProUGUI text)
    {
        this.panel = panel;
        this.text = text;
        baseWidth = Mathf.Max(1f, panel.rect.width);
        minHeight = Mathf.Max(1f, panel.rect.height);
        fontSize = text.fontSize;

        // 말풍선의 위쪽 꼬리와 테두리는 글을 넣는 공간이 아니다.
        // Image가 Simple이면 이 여백도 높이에 비례해 늘어나므로 비율로 계산한다.
        Image background = panel.GetComponent<Image>();
        bool speechBubble = background != null && background.sprite != null && background.sprite.name.StartsWith("Dialog_TextUi");
        imageInsets = speechBubble
            ? new Vector4(0.10f, 0.30f, 0.10f, 0.12f)
            : new Vector4(0.08f, 0.18f, 0.08f, 0.14f);

        Bounds content = RectTransformUtility.CalculateRelativeRectTransformBounds(panel, text.rectTransform);
        left = Mathf.Max(12f, content.min.x - panel.rect.xMin);
        right = Mathf.Max(12f, panel.rect.xMax - content.max.x);
        top = Mathf.Max(12f, panel.rect.yMax - content.max.y);
        bottom = Mathf.Max(12f, content.min.y - panel.rect.yMin);

        // 상점의 기존 가로 자동 확장과 직접 크기 계산이 서로 덮어쓰지 않게 한다.
        LayoutGroup layout = panel.GetComponent<LayoutGroup>();
        if (layout != null)
        {
            left = Mathf.Max(12f, layout.padding.left);
            right = Mathf.Max(12f, layout.padding.right);
            top = Mathf.Max(12f, layout.padding.top);
            bottom = Mathf.Max(12f, layout.padding.bottom);
            layout.enabled = false;
        }
        ContentSizeFitter panelFitter = panel.GetComponent<ContentSizeFitter>();
        if (panelFitter != null) panelFitter.enabled = false;
        ContentSizeFitter textFitter = text.GetComponent<ContentSizeFitter>();
        if (textFitter != null) textFitter.enabled = false;
    }

    public void Fit()
    {
        RectTransform viewport = GetViewport();
        float maxWidth = baseWidth;
        float maxHeight = float.PositiveInfinity;
        if (viewport != null)
        {
            float scaleX = Mathf.Max(0.0001f, viewport.InverseTransformVector(panel.TransformVector(Vector3.right)).magnitude);
            float scaleY = Mathf.Max(0.0001f, viewport.InverseTransformVector(panel.TransformVector(Vector3.up)).magnitude);
            maxWidth = Mathf.Max(1f, (viewport.rect.width - ScreenPadding * 2f) / scaleX);
            maxHeight = Mathf.Max(1f, (viewport.rect.height - ScreenPadding * 2f) / scaleY);
        }

        float width = Mathf.Min(baseWidth, maxWidth);
        panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        RectTransform content = text.rectTransform;
        float textScaleX = Mathf.Max(0.0001f, Mathf.Abs(content.localScale.x));
        float textScaleY = Mathf.Max(0.0001f, Mathf.Abs(content.localScale.y));
        float leftPadding = Mathf.Max(left, width * imageInsets.x);
        float rightPadding = Mathf.Max(right, width * imageInsets.z);
        float textWidth = Mathf.Max(1f, width - leftPadding - rightPadding) / textScaleX;
        content.anchorMin = content.anchorMax = new Vector2(0.5f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, textWidth);

        // 음수 여백은 글자를 사각형 밖으로 밀어내므로 제거한다.
        Vector4 margin = text.margin;
        text.margin = new Vector4(Mathf.Max(0f, margin.x), Mathf.Max(0f, margin.y), Mathf.Max(0f, margin.z), Mathf.Max(0f, margin.w));
        text.textWrappingMode = TextWrappingModes.Normal;
        text.verticalAlignment = VerticalAlignmentOptions.Top;
        text.enableAutoSizing = false;
        text.fontSize = fontSize;
        text.overflowMode = TextOverflowModes.Overflow;
        float textHeight = Mathf.Ceil(text.GetPreferredValues(text.text, textWidth, Mathf.Infinity).y + 2f);
        float contentRatio = 1f - imageInsets.y - imageInsets.w;
        float desiredHeight = Mathf.Max(minHeight, (textHeight * textScaleY + top + bottom) / contentRatio);
        float height = Mathf.Min(desiredHeight, maxHeight);
        panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        float topPadding = height * imageInsets.y + top;
        float bottomPadding = height * imageInsets.w + bottom;
        content.anchoredPosition = new Vector2((leftPadding - rightPadding) * 0.5f, -topPadding);
        content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(1f, height - topPadding - bottomPadding) / textScaleY);

        // 화면 한 장보다 긴 설명에만 글자 자동 축소를 허용한다.
        if (desiredHeight > maxHeight)
        {
            text.fontSizeMin = 1f;
            text.fontSizeMax = fontSize;
            text.enableAutoSizing = true;
        }
        text.ForceMeshUpdate();
    }

    public void ClampToCanvas()
    {
        RectTransform viewport = GetViewport();
        if (viewport == null) return;
        Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, panel);
        Rect screen = viewport.rect;
        Vector3 shift = Vector3.zero;
        if (bounds.max.x > screen.xMax - ScreenPadding)
            shift.x = screen.xMax - ScreenPadding - bounds.max.x;
        else if (bounds.min.x < screen.xMin + ScreenPadding)
            shift.x = screen.xMin + ScreenPadding - bounds.min.x;
        if (bounds.max.y > screen.yMax - ScreenPadding)
            shift.y = screen.yMax - ScreenPadding - bounds.max.y;
        else if (bounds.min.y < screen.yMin + ScreenPadding)
            shift.y = screen.yMin + ScreenPadding - bounds.min.y;
        panel.position += viewport.TransformVector(shift);
    }

    private RectTransform GetViewport()
    {
        Canvas canvas = panel.GetComponentInParent<Canvas>();
        return canvas != null ? canvas.rootCanvas.transform as RectTransform : null;
    }
}
