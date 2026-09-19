using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using DG.Tweening;
using UnityEditor.Experimental.GraphView;

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

    [Header("골드 이펙트")]
    public GoldEffectUI goldEffectUI;

    [Header("상점 제어 버튼")]
    public Button nextStageButton;

    [Header("코팅 선택 UI")]
    public CoatingSelectionPanel coatingSelectionPanel;

    [Header("티켓 시스템 설정")]
    public GameObject ticketSelectionPanel;
    public List<TicketItemSO> allTicketsPool; // 8개의 티켓을 미리 넣어둘 리스트
    public TicketChoiceSlot[] ticketChoiceSlots; // 화면에 보일 3개의 버튼 슬롯

    [Header("티켓 VFX 연출")]
    public CardFlipManager cardFlipManager;
    [Header("티켓 봉투 연출")]
    [SerializeField] private LetterShakeController letterShakeController;

    [Header("주사위 파괴 선택 UI")]
    public DiceDestructionPanel diceDestructionPanel;

    [Header("위성 선택 UI")]
    public SatelliteSelectionPanel satelliteSelectionPanel;

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

        // 2. 튜토리얼 강제 진열 로직 분기
        if (TutorialManager.Instance != null && TutorialManager.Instance.isTutorialActive)
        {
            int step = TutorialManager.Instance.currentStepIndex;

            // [첫 번째 상점] 13~20단계 사이
            if (step <= 20)
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
                return; // 튜토리얼이면 여기서 함수 종료!
            }

            // [두 번째 상점] 23단계
            if (step == 22 || step == 23)
            {
                for (int i = 0; i < shopSlots.Length; i++)
                {
                    shopSlots[i].gameObject.SetActive(true);

                    if (i == 0 && TutorialManager.Instance.tutorialPeppermint != null)
                        shopSlots[0].SetupSlot(TutorialManager.Instance.tutorialPeppermint, this);
                    else if (i == 1 && TutorialManager.Instance.tutorialGarnish != null)
                        shopSlots[1].SetupSlot(TutorialManager.Instance.tutorialGarnish, this);
                    else if (i == 2 && TutorialManager.Instance.tutorialHeartDice != null)
                        shopSlots[2].SetupSlot(TutorialManager.Instance.tutorialHeartDice, this);
                    else if (i == 3 && TutorialManager.Instance.tutorialCoating != null)
                        shopSlots[3].SetupSlot(TutorialManager.Instance.tutorialCoating, this);
                    else
                    {
                        // 남는 슬롯이 있다면 랜덤으로 채우기
                        if (dataIndex < shuffled.Count)
                        {
                            shopSlots[i].SetupSlot(shuffled[dataIndex], this);
                            dataIndex++;
                        }
                    }
                }
                return; // 튜토리얼이면 여기서 함수 종료!
            }
        }

        // 3. 튜토리얼이 모두 끝났거나 일반 게임일 때 (완전 랜덤 상점)



        // 스테이지에 따른 슬롯 해금 개수 계산 (기본 2개 + 2스테이지마다 1개씩 추가)
        int unlockedCount = 6 + (diceManager.currentStage - 1) / 2;
        unlockedCount = Mathf.Clamp(unlockedCount, 2, shopSlots.Length); // 최소 2개, 최대 6개(Length)로 고정

        // 주의: 이 for문 아래에 기존 for문이 또 남아있으면 안 됩니다!
        for (int i = 0; i < shopSlots.Length; i++)
        {
            // 일단 슬롯 자체는 무조건 켭니다 (자물쇠 UI를 보여줘야 하므로)
            shopSlots[i].gameObject.SetActive(true);

            // 1. 아직 해금되지 않은 칸은 '자물쇠 모드'로 만듦
            if (i >= unlockedCount)
            {
                shopSlots[i].SetLockedSlot();
                continue; // 자물쇠로 잠갔으니 이번 칸은 여기서 끝내고 다음 칸으로 넘어감
            }

            // 2. 리롤을 눌렀을 때, 이미 구매한 슬롯은 상품을 바꾸지 않고 건너뜁니다.
            if (isReroll && shopSlots[i].isPurchased) continue;

            // 3. 해금된 슬롯에 정상적으로 아이템 배치
            if (dataIndex < shuffled.Count)
            {
                shopSlots[i].SetupSlot(shuffled[dataIndex], this);
                dataIndex++;
            }
            else if (!isReroll || !shopSlots[i].isPurchased)
            {
                // 상점 풀의 아이템이 다 떨어졌을 때를 대비한 안전장치
                shopSlots[i].gameObject.SetActive(false);
            }
        }
    }

    // 위성 선택창 열기 
    public void ShowSatelliteSelection(SatelliteType type)
    {
        if (satelliteSelectionPanel != null && diceManager != null)
        {
            satelliteSelectionPanel.OpenSelection(diceManager, type);
        }
    }

    public void RerollShop()
    {
        // 코팅 선택 중이거나 파괴 선택 중이면 작동 불가
        if (coatingSelectionPanel != null && coatingSelectionPanel.gameObject.activeSelf) return;
        if (diceDestructionPanel != null && diceDestructionPanel.gameObject.activeSelf) return;

        if (currentGold >= rerollCost)
        {
            // ⭐ 리롤 골드 감소 이펙트
            goldEffectUI?.PlaySpend(rerollCost);

            currentGold -= rerollCost;
            if (diceManager?.ui != null) diceManager.ui.UpdateGoldUI(currentGold);
            RefreshShop(true);
        }
        else
        {
            // [추가] 리롤 비용이 부족할 때 토스트 팝업 강제 출력
            if (ToastPopupController.Instance != null)
            {
                ToastPopupController.Instance.ShowToast("골드가 부족합니다.");
            }
        }
        if (GoldCounter.Instance != null) GoldCounter.Instance.SetGold(currentGold);
    }

    public void CloseShopAndGoNext()
    {
        // 코팅 선택 중이거나 파괴 선택 중이면 다음 스테이지 넘어가기 불가
        if (coatingSelectionPanel != null && coatingSelectionPanel.gameObject.activeSelf) return;
        if (diceDestructionPanel != null && diceDestructionPanel.gameObject.activeSelf) return;

        IsShopOpen = false;
        if (shopUI != null) shopUI.SetActive(false);

        if (diceManager != null)
        {
            //무료 스위치 끄기
            diceManager.isNextShopFree = false;
            diceManager.NextStage();
        }
    }

    // 주사위 파괴 선택창 열기 (파괴 아이템을 구매했을 때 호출됨)
    public void ShowDiceDestructionSelection()
    {
        if (diceDestructionPanel != null && diceManager != null)
        {
            diceDestructionPanel.OpenSelection(diceManager);
        }
        else
        {
            Debug.LogWarning("DiceDestructionPanel 또는 DiceManager 연결이 누락되었습니다.");
        }
    }

    public bool PurchaseItem(BaseItemDataSO item)
    {
        // 코팅 선택 중이거나 파괴 선택 중이면 구매 불가
        if (coatingSelectionPanel != null && coatingSelectionPanel.gameObject.activeSelf) return false;
        if (diceDestructionPanel != null && diceDestructionPanel.gameObject.activeSelf) return false;

        //실제 계산될 가격 판별 로직 추가
        int actualPrice = diceManager.isNextShopFree ? 0 : item.price;

        if (currentGold >= actualPrice)
        {
            if (item is FigureItemSO || item is SnackItemSO)
            {
                if (InventoryManager.Instance.AddItem(item))
                {
                    currentGold -= actualPrice;


                    // ⭐ 구매 골드 감소 이펙트
                    if (actualPrice > 0)
                        goldEffectUI?.PlaySpend(actualPrice);

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
                    //인벤토리가 꽉 찼을 때도 토스트 팝업으로 피드백 제공
                    if (ToastPopupController.Instance != null)
                    {
                        ToastPopupController.Instance.ShowToast("인벤토리가 가득 찼습니다.");
                    }
                    return false;
                }
            }
            else
            {
                item.ApplyItemEffect(diceManager);
                currentGold -= actualPrice;

                // ⭐ 구매 골드 감소 이펙트
                if (actualPrice > 0)
                    goldEffectUI?.PlaySpend(actualPrice); 

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
        // 아이템 구매 비용이 부족할 때 토스트 팝업 띄우기
        if (ToastPopupController.Instance != null)
        {
            ToastPopupController.Instance.ShowToast("골드가 부족합니다.");
        }
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

            //봉투 찢어질 때까지 티켓 숨기기
            ticketChoiceSlots[i].gameObject.SetActive(false); 
        }
        
        if (letterShakeController != null)
        {
            letterShakeController.gameObject.SetActive(true);
            letterShakeController.StartLetterShake();
        }
        else
        {
            //봉투가 연결되지 않았으면 티켓은 그냥 등장
            PlayTicketAppearEffects();
        }

        // ⭐ 기존 창 안에서 티켓 등장 VFX 실행
        if (cardFlipManager != null)
        {
            cardFlipManager.OnClickStartButton();
        }
    }

    public void SelectTicketWithVFX(
     TicketChoiceSlot selectedSlot,
     TicketItemSO selectedTicket)
    {
        Sequence selectSeq = DOTween.Sequence();

        RectTransform selectedRect = null;

        foreach (TicketChoiceSlot slot in ticketChoiceSlots)
        {
            if (slot == null)
                continue;

            //  모든 티켓 둥실거림 정지
            slot.StopIdleAnimation();

            RectTransform rect = slot.GetComponent<RectTransform>();

            CanvasGroup canvasGroup = slot.GetComponent<CanvasGroup>();

            if (canvasGroup == null)
                canvasGroup = slot.gameObject.AddComponent<CanvasGroup>();

            rect.DOKill();
            canvasGroup.DOKill();

            if (slot == selectedSlot)
            {
                selectedRect = rect;

                //  선택한 티켓을 가장 앞으로
                rect.SetAsLastSibling();

                //  선택 티켓 확대
                selectSeq.Join(
                    rect.DOScale(Vector3.one * 1.25f, 0.3f)
                        .SetEase(Ease.OutBack)
                );

                // 기울어져 있으면 정면으로
                selectSeq.Join(
                    rect.DOLocalRotate(Vector3.zero, 0.2f)
                );
            }
            else
            {
                //  선택하지 않은 티켓은 작아지면서 사라짐
                selectSeq.Join(
                    rect.DOScale(Vector3.zero, 0.3f)
                        .SetEase(Ease.InBack)
                );

                selectSeq.Join(
                    canvasGroup.DOFade(0f, 0.3f)
                );
            }
        }

        // ⭐ 선택한 티켓 잠깐 강조
        selectSeq.AppendInterval(0.4f);

        // ⭐ 오른쪽 위로 슝
        if (selectedRect != null)
        {
            selectSeq.Append(
                selectedRect.DOAnchorPos(
                    selectedRect.anchoredPosition + new Vector2(750f, 200f),
                    0.6f
                )
                .SetEase(Ease.InCubic)
            );

            // 동시에 작아짐
            selectSeq.Join(
                selectedRect.DOScale(Vector3.zero, 0.6f)
                    .SetEase(Ease.InBack)
            );
        }

        // 모든 연출이 끝난 다음 실제 티켓 적용
        selectSeq.OnComplete(() =>
        {
            HideTooltip();

            selectedTicket.ApplyItemEffect(diceManager);

            InventoryManager.Instance.AddItem(selectedTicket);

            CloseTicketSelection();
        });
    }

    public void PlayTicketAppearEffects()
    {
        Debug.Log("⭐ PlayTicketAppearEffects 실행됨!");

        for (int i = 0; i < ticketChoiceSlots.Length; i++)
        {
            if (ticketChoiceSlots[i] == null)
            {
                Debug.LogError($"❌ TicketChoiceSlot {i}가 비어있음!");
                continue;
            }

            Debug.Log($"⭐ 티켓 {i} 등장!");

            // 숨겨놨던 티켓 다시 활성화
            ticketChoiceSlots[i].gameObject.SetActive(true);

            // 등장 애니메이션
            ticketChoiceSlots[i].PlayAppearEffect(i * 0.18f);
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
            Debug.Log("123123");
        }
        else
        {
            Debug.LogWarning("CoatingSelectionPanel 또는 DiceManager 연결이 누락되었습니다.");
        }
    }

    public void HideTooltip() => tooltipPanel.SetActive(false);
}