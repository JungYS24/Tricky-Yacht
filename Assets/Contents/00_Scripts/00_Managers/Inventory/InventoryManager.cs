using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance { get; private set; }

    [Header("참조")]
    public DiceManager diceManager;
    public FigureDetailPanel figureDetailPanel; // 피규어 상세 정보 패널
    public TicketDetailPanel ticketDetailPanel; //티켓 상세 정보 패널

    [Header("슬롯 배열")]
    public Transform figureSlotParent; // 피규어가 생성될 부모 위치 (FigureSlotArea)
    public GameObject figureSlotPrefab; // 피규어 슬롯 프리팹
    public InventorySlot[] snackSlots;

    [Header("티켓 슬롯 설정")]
    public Transform ticketSlotParent;
    public GameObject ticketSlotPrefab;

    //보유 중인 티켓 리스트
    public List<TicketItemSO> ownedTickets = new List<TicketItemSO>();
    private List<GameObject> activeTicketSlots = new List<GameObject>();

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
        // 피규어 슬롯 파괴 및 리스트 초기화
        foreach (var slotGo in activeFigureSlots)
        {
            Destroy(slotGo);
        }
        activeFigureSlots.Clear();
        ownedFigures.Clear();

        //티켓 슬롯도 같이 비워줌
        foreach (var slotGo in activeTicketSlots) Destroy(slotGo);
        activeTicketSlots.Clear();
        ownedTickets.Clear();

        foreach (var slot in snackSlots) slot.ClearSlot();
        Debug.Log("인벤토리의 모든 아이템이 초기화되었습니다.");
    }

    public bool AddItem(BaseItemDataSO item)
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

            // 도감 영구 해금 기록
            PlayerPrefs.SetInt("Collection_Unlocked_" + figure.itemName, 1);
            PlayerPrefs.Save();

            // 새 슬롯 생성
            GameObject newSlotGo = Instantiate(figureSlotPrefab, figureSlotParent);
            InventorySlot newSlot = newSlotGo.GetComponent<InventorySlot>();
            newSlot.Initialize(this);
            newSlot.SetItem(figure);    
            activeFigureSlots.Add(newSlotGo);
            return true;
        }
        //티켓 아이템이 들어올 경우 처리
        else if (item is TicketItemSO ticket)
        {
            foreach (var slotGo in activeTicketSlots)
            {
                InventorySlot slot = slotGo.GetComponent<InventorySlot>();
                if (slot.currentItem == ticket)
                {
                    slot.AddStack();
                    return true;
                }
            }

            ownedTickets.Add(ticket);
            GameObject newSlotGo = Instantiate(ticketSlotPrefab, ticketSlotParent);
            InventorySlot newSlot = newSlotGo.GetComponent<InventorySlot>();
            newSlot.Initialize(this);
            newSlot.SetItem(ticket);
            newSlot.AddStack();
            activeTicketSlots.Add(newSlotGo);
            return true;
        }
        else if (item is SnackItemSO)
        {
            return PlaceIntoEmptySlot(item, snackSlots, maxSnackSlots);
        }
        return false;
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
        if (sellPriceText != null) sellPriceText.text = $"판매: {sellPrice} G";

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
            diceManager.shopManager.currentGold += sellPrice;
            diceManager.ui?.UpdateGoldUI(diceManager.shopManager.currentGold);
        }

        Debug.Log($"피규어 [{targetSellSlot.currentItem.itemName}] 판매 완료! +{sellPrice} G");

        if (targetSellSlot.currentItem is FigureItemSO figure)
        {
            // 판매 시 리스트와 씬에서 삭제
            ownedFigures.Remove(figure);
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
        if (descText == null || tooltipPanel == null) return;

        descText.text = desc;
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
        tooltipRect.position = slotRect.position;
        // x, y 값을 조절하여 마우스/슬롯을 가리지 않게 오프셋 부여
        tooltipRect.localPosition += new Vector3(0f, -50f, 0f);
    }

    //툴팁 숨김 함수
    public void HideTooltip()
    {
        if (tooltipPanel != null) tooltipPanel.SetActive(false);
    }

    // 심연의 딜러 조우자 이벤트를 위한 티켓 완전 초기화 함수
    public void ClearAllTickets()
    {
        foreach (var slotGo in activeTicketSlots)
        {
            if (slotGo != null) Destroy(slotGo);
        }
        activeTicketSlots.Clear();
        ownedTickets.Clear();
    }

    // 주사위 결산
    // 주사위 결산
    public void EvaluateTurnEndTriggers(List<int> finalDiceValues, string handName, DiceManager diceManager, ShopManager shopManager)
    {
        //주사위 눈금 개수 카운팅
        int[] diceCounts = new int[7];
        foreach (int v in finalDiceValues)
        {
            if (v >= 0 && v <= 6)
            {
                diceCounts[v]++;
            }
        }

        foreach (var figure in ownedFigures)
        {
            foreach (var node in figure.figureNodes)
            {
                bool isTriggered = false;

                // 족보 및 주사위 개수 감지
                switch (node.triggerType)
                {
                    case FigureTriggerType.ThreeOf1: if (diceCounts[1] >= 3) isTriggered = true; break;
                    case FigureTriggerType.ThreeOf2: if (diceCounts[2] >= 3) isTriggered = true; break;
                    case FigureTriggerType.ThreeOf3: if (diceCounts[3] >= 3) isTriggered = true; break;
                    case FigureTriggerType.ThreeOf4: if (diceCounts[4] >= 3) isTriggered = true; break;
                    case FigureTriggerType.ThreeOf5: if (diceCounts[5] >= 3) isTriggered = true; break;
                    case FigureTriggerType.ThreeOf6: if (diceCounts[6] >= 3) isTriggered = true; break;

                    case FigureTriggerType.OnePair: if (handName == "원 페어") isTriggered = true; break;
                    case FigureTriggerType.TwoPair: if (handName == "투 페어") isTriggered = true; break;
                    case FigureTriggerType.Triple: if (handName == "트리플") isTriggered = true; break;
                    case FigureTriggerType.Straight: if (handName == "스트레이트") isTriggered = true; break;
                    case FigureTriggerType.FullHouse: if (handName == "풀하우스") isTriggered = true; break;
                    case FigureTriggerType.FourOfAKind: if (handName == "포카드") isTriggered = true; break;
                    case FigureTriggerType.Yacht: if (handName == "Yacht" || handName == "요트" || handName == "파이브 카드") isTriggered = true; break;
                }

                // 조건이 만족되었다면 연결된 복수의 보상 리스트를 모두 실행
                if (isTriggered)
                {
                    Debug.Log($"[피규어 발동] {figure.itemName}의 {node.triggerType} 조건 달성!");
                    ApplyFigureEffects(node.effects, diceManager, shopManager);
                }
            }
        }
    }
    // 스테이지가 끝났을 때 패시브 피규어들을 발동시킵니다.
    public void EvaluateStageClearTriggers(DiceManager diceManager, ShopManager shopManager)
    {
        foreach (var figure in ownedFigures)
        {
            foreach (var node in figure.figureNodes)
            {
                if (node.triggerType == FigureTriggerType.Passive)
                {
                    Debug.Log($"[피규어 스테이지 클리어 발동] {figure.itemName} 패시브 효과 달성!");
                    ApplyFigureEffects(node.effects, diceManager, shopManager);
                }
            }
        }
    }

    // 스낵을 사용했을 때 OnSnackUsed 피규어들을 발동
    public void EvaluateSnackUsedTriggers(DiceManager diceManager, ShopManager shopManager)
    {
        foreach (var figure in ownedFigures)
        {
            foreach (var node in figure.figureNodes)
            {
                if (node.triggerType == FigureTriggerType.OnSnackUsed)
                {
                    Debug.Log($"[피규어 스낵 사용 발동] {figure.itemName} 효과 달성!");
                    ApplyFigureEffects(node.effects, diceManager, shopManager);
                }
            }
        }

        // 리롤 횟수 등 UI에 즉각적인 변화가 생겼으므로 화면을 강제 갱신
        diceManager.ForceUpdateUI();
    }

    // 조건 만족 시 실질적인 인게임 변화(보상)를 주는 함수
    private void ApplyFigureEffects(List<FigureEffectNode> effects, DiceManager diceManager, ShopManager shopManager)
    {
        foreach (var effect in effects)
        {
            switch (effect.effectType)
            {
                case FigureEffectType.HealHP:
                    diceManager.currentPlayerHP += (int)effect.effectValue;
                    if (diceManager.currentPlayerHP > diceManager.playerMaxHP)
                        diceManager.currentPlayerHP = diceManager.playerMaxHP; // 오버힐 방지[cite: 1]
                    break;

                case FigureEffectType.AddGold:
                    if (shopManager != null)
                    {
                        shopManager.currentGold += (int)effect.effectValue;
                        diceManager.ui?.UpdateGoldUI(shopManager.currentGold);
                        if (GoldCounter.Instance != null) GoldCounter.Instance.SetGold(shopManager.currentGold); // GoldCounter 연출 실행
                    }
                    break;

                case FigureEffectType.AddReroll:
                    diceManager.figureBonusRerolls += (int)effect.effectValue; // 리롤 횟수 가산
                    break;

                case FigureEffectType.AddChips:
                    diceManager.snackBonusChips += (int)effect.effectValue; // 최종 플랫 칩 추가
                    break;

                case FigureEffectType.AddMultiplier:
                    diceManager.snackBonusMult += effect.effectValue; // 차례 정산 시 배수 가산
                    break;

                case FigureEffectType.DamageEnemy:
                    if (diceManager.enemy != null)
                        diceManager.enemy.TakeDamage((int)effect.effectValue, null); // 고정 데미지 부여
                    break;

                case FigureEffectType.GetSnack:
                    if (effect.optionalItem != null)
                        AddItem(effect.optionalItem); // 지정된 스낵 프리팹 인벤토리에 추가[cite: 1]
                    break;
            }
        }
    }
}