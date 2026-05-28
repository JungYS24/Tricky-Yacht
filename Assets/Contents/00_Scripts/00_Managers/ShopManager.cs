using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class ShopManager : MonoBehaviour
{
    public static bool IsShopOpen { get; private set; } = false;

    [Header("참조 설정")]
    public DiceManager diceManager;
    public List<BaseItemDataSO> allItemsPool;
    public ShopSlot[] shopSlots;
    public GameObject shopUI;

    [Header("설명창(Tooltip) UI")]
    public GameObject tooltipPanel;
    public RectTransform tooltipRect;
    public TextMeshProUGUI descText;

    [Header("리롤 및 재화 설정")]
    public int currentGold = 3000;
    public Button shopRerollButton;
    public TextMeshProUGUI rerollCostText;
    public int rerollCost = 100;

    [Header("상점 제어 버튼")]
    public Button nextStageButton;

    [Header("코팅 선택 UI")]
    public CoatingSelectionPanel coatingSelectionPanel;

    [Header("티켓 시스템 설정")]
    public GameObject ticketSelectionPanel;
    public List<TicketItemSO> allTicketsPool; // 8개의 티켓을 미리 넣어둘 리스트
    public TicketChoiceSlot[] ticketChoiceSlots; // 화면에 보일 3개의 버튼 슬롯

    private void Awake()
    {
        if (tooltipRect == null && tooltipPanel != null)
            tooltipRect = tooltipPanel.GetComponent<RectTransform>();

        HideTooltip();

        if (shopRerollButton != null)
            shopRerollButton.onClick.AddListener(RerollShop);

        if (rerollCostText != null)
            rerollCostText.text = "리롤 : " + rerollCost + " G";

        if (nextStageButton != null)
            nextStageButton.onClick.AddListener(CloseShopAndGoNext);

        if (ticketSelectionPanel != null)
            ticketSelectionPanel.SetActive(false);

        IsShopOpen = false;
    }

    private void Start()
    {
        if (diceManager?.ui != null) diceManager.ui.UpdateGoldUI(currentGold);
        if (GoldCounter.Instance != null) GoldCounter.Instance.SetGold(currentGold);
    }

    public void OpenShop()
    {
        IsShopOpen = true;
        if (shopUI != null) shopUI.SetActive(true);

        // 정적 UI 업데이트를 먼저 처리하여 데이터 싱크를 맞추기
        if (diceManager?.ui != null) diceManager.ui.UpdateGoldUI(currentGold);

        // 그 후 DoTween 연출을 실행해야 숫자가 꼬임 없이 부드럽게 표현
        if (GoldCounter.Instance != null) GoldCounter.Instance.SetGold(currentGold);

        RefreshShop(false);
    }

    public void RefreshShop(bool isReroll)
    {
        // 중복 획득 방지용 리스트 구성
        List<BaseItemDataSO> validPool = new List<BaseItemDataSO>();
        foreach (var item in allItemsPool)
        {
            // 이미 가지고 있는 피규어면 상점 풀에서 제외
            if (item is FigureItemSO figure && InventoryManager.Instance.ownedFigures.Contains(figure))
            {
                continue;
            }
            validPool.Add(item);
        }

        // 1. 일반 상점을 위해 미리 모든 아이템을 섞어둡니다. (validPool 기준)
        List<BaseItemDataSO> shuffled = new List<BaseItemDataSO>(validPool);
        for (int i = 0; i < shuffled.Count; i++)
        {
            int rnd = Random.Range(i, shuffled.Count);
            (shuffled[i], shuffled[rnd]) = (shuffled[rnd], shuffled[i]);
        }

        int dataIndex = 0;

        // 2. 튜토리얼 중일 때의 상점 강제 진열 로직
        if (TutorialManager.Instance != null && TutorialManager.Instance.isTutorialActive)
        {
            int step = TutorialManager.Instance.currentStepIndex;

            // [첫 번째 상점] 10~17단계 사이
            if (step <= 17)
            {
                if (shopSlots.Length >= 6)
                {
                    shopSlots[0].SetupSlot(TutorialManager.Instance.tutFigure, this);
                    shopSlots[1].SetupSlot(TutorialManager.Instance.tutSnack, this);
                    shopSlots[2].SetupSlot(TutorialManager.Instance.tutCoating, this);
                    shopSlots[3].SetupSlot(TutorialManager.Instance.tutDice, this);
                    shopSlots[4].SetupSlot(TutorialManager.Instance.tutTicket, this);

                    if (TutorialManager.Instance.tutDummy != null)
                        shopSlots[5].SetupSlot(TutorialManager.Instance.tutDummy, this);
                }
                return; // 첫 번째 상점 세팅 끝! (아래 코드는 실행 안 함)
            }

            // [두 번째 상점] 19단계 이상
            if (step >= 19)
            {
                for (int i = 0; i < shopSlots.Length; i++)
                {
                    if (i == 0 && TutorialManager.Instance.tutorialPeppermint != null)
                    {
                        shopSlots[0].gameObject.SetActive(true);
                        shopSlots[0].SetupSlot(TutorialManager.Instance.tutorialPeppermint, this);
                    }
                    else if (i == 1 && TutorialManager.Instance.tutorialGarnish != null)
                    {
                        shopSlots[1].gameObject.SetActive(true);
                        shopSlots[1].SetupSlot(TutorialManager.Instance.tutorialGarnish, this);
                    }
                    else
                    {
                        // 0번, 1번 슬롯을 제외한 나머지 칸은 랜덤 아이템으로 채우기
                        if (dataIndex < shuffled.Count)
                        {
                            shopSlots[i].gameObject.SetActive(true);
                            shopSlots[i].SetupSlot(shuffled[dataIndex], this);
                            dataIndex++;
                        }
                    }
                }
                return; // 두 번째 상점 세팅 끝!
            }
        }

        // 3. 튜토리얼이 모두 끝났거나 일반 게임일 때 (완전 랜덤 상점)
        for (int i = 0; i < shopSlots.Length; i++)
        {
            if (isReroll && shopSlots[i].isPurchased) continue;

            if (dataIndex < shuffled.Count)
            {
                shopSlots[i].gameObject.SetActive(true);
                shopSlots[i].SetupSlot(shuffled[dataIndex], this);
                dataIndex++;
            }
        }
    }

    public void RerollShop()
    {
        if (coatingSelectionPanel != null && coatingSelectionPanel.gameObject.activeSelf) return;//코팅 선택 중이면 작동 불가

        if (currentGold >= rerollCost)
        {
            currentGold -= rerollCost;
            if (diceManager?.ui != null) diceManager.ui.UpdateGoldUI(currentGold);
            RefreshShop(true);
        }
        if (GoldCounter.Instance != null) GoldCounter.Instance.SetGold(currentGold);
    }

    public void CloseShopAndGoNext()
    {
        // 코팅 선택 중이면 다음 스테이지 넘어가기 불가
        if (coatingSelectionPanel != null && coatingSelectionPanel.gameObject.activeSelf) return;

        IsShopOpen = false;
        if (shopUI != null) shopUI.SetActive(false);

        if (diceManager != null) diceManager.NextStage();
    }

    public bool PurchaseItem(BaseItemDataSO item)
    {
        // 코팅 선택 중이면 리롤 불가
        if (coatingSelectionPanel != null && coatingSelectionPanel.gameObject.activeSelf) return false;

        if (currentGold >= item.price)
        {
            if (item is FigureItemSO || item is SnackItemSO)
            {
                if (InventoryManager.Instance.AddItem(item))
                {
                    currentGold -= item.price;
                    if (diceManager?.ui != null) diceManager.ui.UpdateGoldUI(currentGold);

                    // 피규어/간식 정상 구매 성공 시 부드럽게 돈 깎이는 연출 적용
                    if (GoldCounter.Instance != null) GoldCounter.Instance.SetGold(currentGold);
                    if (TutorialManager.Instance != null && TutorialManager.Instance.isTutorialActive)
                    {
                        TutorialManager.Instance.OnItemBought(item.itemName);
                    }
                    return true;
                }
                else
                {
                    Debug.Log("인벤토리가 꽉 차서 구매할 수 없습니다!");
                    return false;
                }
            }
            else
            {
                item.ApplyItemEffect(diceManager);
                currentGold -= item.price;
                if (diceManager?.ui != null) diceManager.ui.UpdateGoldUI(currentGold);

                // 그 외 소모품/티켓류 정상 구매 성공 시 부드럽게 돈 깎이는 연출 적용
                if (GoldCounter.Instance != null) GoldCounter.Instance.SetGold(currentGold);

                if (TutorialManager.Instance != null && TutorialManager.Instance.isTutorialActive)
                {
                    TutorialManager.Instance.OnItemBought(item.itemName);
                }
                return true;
            }
        }
        Debug.Log("골드가 부족합니다.");
        return false;
    }

    public void ShowTooltip(string desc, RectTransform slotRect)
    {
        descText.text = desc;
        tooltipPanel.SetActive(true);

        // [추가] 상점 툴팁도 최상단으로 강제 고정
        Canvas canvas = tooltipPanel.GetComponent<Canvas>();
        if (canvas == null) canvas = tooltipPanel.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = 101;

        tooltipRect.SetAsLastSibling();
        tooltipRect.pivot = new Vector2(0f, 0.5f);

        tooltipRect.position = slotRect.position;
        tooltipRect.localPosition += new Vector3(20f, -50f, 0f);
    }
    // 티켓 선택창 열기 (티켓 아이템을 구매했을 때 호출됨)
    public void ShowTicketSelection()
    {
        if (allTicketsPool.Count < 3) return;

        if (ticketSelectionPanel != null)
            ticketSelectionPanel.SetActive(true);

        // 전체 티켓 풀을 셔플
        List<TicketItemSO> shuffledTickets = new List<TicketItemSO>(allTicketsPool);
        for (int i = 0; i < shuffledTickets.Count; i++)
        {
            int rnd = Random.Range(i, shuffledTickets.Count);
            var temp = shuffledTickets[i];
            shuffledTickets[i] = shuffledTickets[rnd];
            shuffledTickets[rnd] = temp;
        }

        // 섞인 리스트 중 앞의 3개를 슬롯에 배치
        for (int i = 0; i < ticketChoiceSlots.Length; i++)
        {
            ticketChoiceSlots[i].Setup(shuffledTickets[i], this);
        }
    }

    public void CloseTicketSelection()
    {
        if (ticketSelectionPanel != null)
            ticketSelectionPanel.SetActive(false);
    }

    public void ShowCoatingSelection(DiceType type, float mult, Color color)
    {
        if (coatingSelectionPanel != null && diceManager != null)
        {
            coatingSelectionPanel.OpenSelection(diceManager, type, mult, color);
        }
        else
        {
            Debug.LogWarning("CoatingSelectionPanel 또는 DiceManager 연결이 누락되었습니다.");
        }
    }

    public void HideTooltip() => tooltipPanel.SetActive(false);
}