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
    }

    public void OpenShop()
    {
        IsShopOpen = true;
        if (shopUI != null) shopUI.SetActive(true);
        if (diceManager?.ui != null) diceManager.ui.UpdateGoldUI(currentGold);

        RefreshShop(false);
    }

    public void RefreshShop(bool isReroll)
    {
        List<BaseItemDataSO> shuffled = new List<BaseItemDataSO>(allItemsPool);

        // 튜토리얼 중이고, 첫 번째 상점 방문(10~17단계 사이)일 때
        if (TutorialManager.Instance != null && TutorialManager.Instance.isTutorialActive && TutorialManager.Instance.currentStepIndex <= 17)
        {
            // 상점 슬롯이 5개 이상이라고 가정하고 순서대로 꽂아 넣습니다.
            if (shopSlots.Length >= 5)
            {
                shopSlots[0].SetupSlot(TutorialManager.Instance.tutFigure, this);
                shopSlots[1].SetupSlot(TutorialManager.Instance.tutSnack, this);
                shopSlots[2].SetupSlot(TutorialManager.Instance.tutCoating, this);
                shopSlots[3].SetupSlot(TutorialManager.Instance.tutDice, this);
                shopSlots[4].SetupSlot(TutorialManager.Instance.tutTicket, this);
            }
            return; // 튜토리얼 강제 진열 후 함수 종료          
        }
        else
        {
            for (int i = 0; i < shuffled.Count; i++)
            {
                int rnd = Random.Range(i, shuffled.Count);
                (shuffled[i], shuffled[rnd]) = (shuffled[rnd], shuffled[i]);
            }

            int dataIndex = 0;

            // [두 번째 상점: 20단계 이후] 페퍼민트와 가니쉬만 진열 -> 고정 + 나머지 랜덤으로 로직 수정
            // (주의: 19단계에서 상점이 열리며 세팅되므로 조건을 19단계 이상으로 수정했습니다)
            if (TutorialManager.Instance != null && TutorialManager.Instance.isTutorialActive && TutorialManager.Instance.currentStepIndex >= 19)
            {
                for (int i = 0; i < shopSlots.Length; i++)
                {
                    if (i == 0)
                    {
                        shopSlots[0].gameObject.SetActive(true);
                        // .tutorialPeppermint 이름 확인!
                        shopSlots[0].SetupSlot(TutorialManager.Instance.tutorialPeppermint, this);
                    }
                    else if (i == 1)
                    {
                        shopSlots[1].gameObject.SetActive(true);
                        // .tutorialGarnish 이름 확인!
                        shopSlots[1].SetupSlot(TutorialManager.Instance.tutorialGarnish, this);
                    }
                    else
                    {
                        // 남은 슬롯은 셔플된 아이템으로 채움
                        if (dataIndex < shuffled.Count)
                        {
                            shopSlots[i].gameObject.SetActive(true);
                            shopSlots[i].SetupSlot(shuffled[dataIndex], this);
                            dataIndex++;
                        }
                    }
                }
                return; // 튜토리얼 강제 진열 후 함수 종료
            }

            // --- 일반적인 상점 리프레시 (튜토리얼이 아닐 때) ---
            for (int i = 0; i < shopSlots.Length; i++)
            {
                if (isReroll && shopSlots[i].isPurchased) continue;

                if (dataIndex < shuffled.Count)
                {
                    shopSlots[i].SetupSlot(shuffled[dataIndex], this);
                    dataIndex++;
                }
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