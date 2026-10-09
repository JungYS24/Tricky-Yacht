using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;

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

    [Header("주사위 파괴 선택 UI")]
    public DiceDestructionPanel diceDestructionPanel;

    [Header("위성 선택 UI")]
    public SatelliteSelectionPanel satelliteSelectionPanel;

    public enum MapMerchant
    {
        Louis,
        Ronan,
        Opel
    }

    public MapMerchant currentMerchant = MapMerchant.Louis;
    bool mapMerchantActive;
    TextMeshProUGUI merchantNameText;

    private void Awake()
    {
        if (tooltipRect == null && tooltipPanel != null)
            tooltipRect = tooltipPanel.GetComponent<RectTransform>();

        HideTooltip();
        if (allItemsPool == null) allItemsPool = new List<BaseItemDataSO>();
        SnackItemSO.RegisterResourceSnacks(allItemsPool);

        if (shopRerollButton != null)
            shopRerollButton.onClick.AddListener(RerollShop);

        if (rerollCostText != null)
            rerollCostText.text = LocalizationManager.GetUi("UI_REROLL_COST", "리롤 : {0} G", rerollCost);

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
        OpenShopInternal(false);
    }

    public void OpenMapShop()
    {
        currentMerchant = (MapMerchant)UnityEngine.Random.Range(0, 3);
        OpenShopInternal(true);
    }

    void OpenShopInternal(bool fromMap)
    {
        mapMerchantActive = fromMap;
        IsShopOpen = true;
        RevealShop();
        EnsureMerchantLabel();
        if (merchantNameText != null)
        {
            merchantNameText.gameObject.SetActive(fromMap);
            if (fromMap) merchantNameText.text = MerchantName(currentMerchant);
        }

        if (diceManager?.ui != null) diceManager.ui.UpdateGoldUI(currentGold);
        if (GoldCounter.Instance != null) GoldCounter.Instance.SetGold(currentGold);

        RefreshShop(false);
        UpdateRerollUI();
        if (FigureEffectManager.Instance != null)
            FigureEffectManager.Instance.EvaluateShopEnteredTriggers(diceManager, this);

        Debug.Log($"[Map Shop] merchant={(fromMap ? currentMerchant.ToString() : "tutorial")} shopUI={(shopUI != null ? shopUI.name : "null")} active={shopUI != null && shopUI.activeInHierarchy}");
    }

    void RevealShop()
    {
        if (shopUI == null) return;
        Transform cursor = shopUI.transform;
        while (cursor != null)
        {
            if (!cursor.gameObject.activeSelf)
                cursor.gameObject.SetActive(true);
            if (cursor.GetComponent<Canvas>() != null) break;
            cursor = cursor.parent;
        }
        shopUI.transform.SetAsLastSibling();
    }

    void EnsureMerchantLabel()
    {
        if (shopUI == null) return;
        Transform found = shopUI.transform.Find("MerchantName");
        if (found == null)
        {
            var go = new GameObject("MerchantName", typeof(RectTransform));
            go.transform.SetParent(shopUI.transform, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -12f);
            rect.sizeDelta = new Vector2(640f, 72f);
            merchantNameText = go.AddComponent<TextMeshProUGUI>();
            merchantNameText.fontSize = 40;
            merchantNameText.alignment = TextAlignmentOptions.Center;
            merchantNameText.color = Color.white;
            if (rerollCostText != null) merchantNameText.font = rerollCostText.font;
        }
        else
        {
            merchantNameText = found.GetComponent<TextMeshProUGUI>();
        }
    }

    static string MerchantName(MapMerchant merchant)
    {
        switch (merchant)
        {
            case MapMerchant.Louis: return "루이";
            case MapMerchant.Ronan: return "로난";
            default: return "오펠";
        }
    }

    bool MerchantSells(BaseItemDataSO item)
    {
        if (item == null) return false;
        switch (currentMerchant)
        {
            case MapMerchant.Louis:
                return item is FigureItemSO || item is SnackItemSO || item is TicketItemSO;
            case MapMerchant.Ronan:
                return item is DiceItemSO || item is CoatingItemSO || item is DiceDestroyItemSO || item is MaxHPItemSO || item is CoinItemSO;
            default:
                return item is SatelliteItemSO;
        }
    }

    List<BaseItemDataSO> FilterMerchantStock(List<BaseItemDataSO> source)
    {
        var filtered = new List<BaseItemDataSO>();
        for (int i = 0; i < source.Count; i++)
        {
            if (MerchantSells(source[i])) filtered.Add(source[i]);
        }
        if (currentMerchant == MapMerchant.Opel && filtered.Count == 0)
            filtered.AddRange(SatelliteStock());
        if (filtered.Count == 0)
        {
            Debug.LogWarning($"[Map Shop] {currentMerchant} 상품이 없어 전체 진열을 사용합니다.");
            return source;
        }
        return filtered;
    }

    List<SatelliteItemSO> satelliteStock;

    List<BaseItemDataSO> SatelliteStock()
    {
        if (satelliteStock == null)
        {
            satelliteStock = new List<SatelliteItemSO>();
            SatelliteType[] types = { SatelliteType.Jupiter, SatelliteType.Mars, SatelliteType.Mercury, SatelliteType.Venus };
            string[] names = { "목성", "화성", "수성", "금성" };
            for (int i = 0; i < types.Length; i++)
            {
                var item = ScriptableObject.CreateInstance<SatelliteItemSO>();
                item.satelliteType = types[i];
                item.itemName = names[i];
                item.price = 200;
                satelliteStock.Add(item);
            }
        }
        return new List<BaseItemDataSO>(satelliteStock);
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

        if (mapMerchantActive)
            validPool = FilterMerchantStock(validPool);

        // 일반 상점을 위해 미리 모든 아이템을 섞어둡니다. (validPool 기준)
        List<BaseItemDataSO> shuffled = new List<BaseItemDataSO>(validPool);
        for (int i = 0; i < shuffled.Count; i++)
        {
            int rnd = Random.Range(i, shuffled.Count);
            (shuffled[i], shuffled[rnd]) = (shuffled[rnd], shuffled[i]);
        }

        int dataIndex = 0;

        // 튜토리얼 강제 진열 로직 분기
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

        // 튜토리얼이 모두 끝났거나 일반 게임일 때 (완전 랜덤 상점)



        // 스테이지에 따른 슬롯 해금 개수 계산 (기본 2개 + 2스테이지마다 1개씩 추가)
        int unlockedCount = 6 + (diceManager.currentStage - 1) / 2;
        unlockedCount = Mathf.Clamp(unlockedCount, 2, shopSlots.Length); // 최소 2개, 최대 6개(Length)로 고정

        // 주의: 이 for문 아래에 기존 for문이 또 남아있으면 안 됩니다!
        for (int i = 0; i < shopSlots.Length; i++)
        {
            // 일단 슬롯 자체는 무조건 켭니다 (자물쇠 UI를 보여줘야 하므로)
            shopSlots[i].gameObject.SetActive(true);

            // 아직 해금되지 않은 칸은 '자물쇠 모드'로 만듦
            if (i >= unlockedCount)
            {
                shopSlots[i].SetLockedSlot();
                continue; // 자물쇠로 잠갔으니 이번 칸은 여기서 끝내고 다음 칸으로 넘어감
            }

            // 리롤을 눌렀을 때, 이미 구매한 슬롯은 상품을 바꾸지 않고 건너뜁니다.
            if (isReroll && shopSlots[i].isPurchased) continue;

            // 해금된 슬롯에 정상적으로 아이템 배치
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
        if ((ticketSelectionPanel != null && ticketSelectionPanel.activeSelf) || SatelliteSelectionPanel.IsPanelOpen)
        {
            return;
        }

        if (coatingSelectionPanel != null && coatingSelectionPanel.gameObject.activeSelf) return;
        if (diceDestructionPanel != null && diceDestructionPanel.gameObject.activeSelf) return;

        //기본 rerollCost 대신 할인이 적용된 최종 비용 사용
        int finalCost = GetFinalRerollCost();
        if (currentGold >= finalCost)
        {
            currentGold -= finalCost;
            if (finalCost < rerollCost) FigureEffectManager.Instance?.NotifyPassiveApplied(FigureEffectType.DiscountShopReroll);
            if (diceManager?.ui != null) diceManager.ui.UpdateGoldUI(currentGold);
            RefreshShop(true);
        }
        else
        {
            if (ToastPopupController.Instance != null)
                ToastPopupController.Instance.ShowToast(LocalizationManager.GetSys("SYS_GOLD_NOT_ENOUGH", "골드가 부족합니다."));
        }
        if (GoldCounter.Instance != null) GoldCounter.Instance.SetGold(currentGold);
    }

    public void CloseShopAndGoNext()
    {
        if ((ticketSelectionPanel != null && ticketSelectionPanel.activeSelf) || SatelliteSelectionPanel.IsPanelOpen)
        {
            return;
        }
        // 코팅 선택 중이거나 파괴 선택 중이면 다음 스테이지 넘어가기 불가
        if (coatingSelectionPanel != null && coatingSelectionPanel.gameObject.activeSelf) return;
        if (diceDestructionPanel != null && diceDestructionPanel.gameObject.activeSelf) return;

        IsShopOpen = false;
        mapMerchantActive = false;
        if (shopUI != null) shopUI.SetActive(false);

        if (diceManager != null)
        {
            //무료 스위치 끄기
            diceManager.isNextShopFree = false;
            if (diceManager.UsesMapFlow)
                diceManager.OnShopClosedDuringMap();
            else
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

    public bool PurchaseItem(BaseItemDataSO item, int actualPrice)
    {
        if ((ticketSelectionPanel != null && ticketSelectionPanel.activeSelf) || SatelliteSelectionPanel.IsPanelOpen)
        {
            return false;
        }

        // 코팅 선택 중이거나 파괴 선택 중이면 구매 불가
        if (coatingSelectionPanel != null && coatingSelectionPanel.gameObject.activeSelf) return false;
        if (diceDestructionPanel != null && diceDestructionPanel.gameObject.activeSelf) return false;

        if (currentGold >= actualPrice)
        {
            if (item is FigureItemSO || item is SnackItemSO)
            {
                if (InventoryManager.Instance.AddItem(item))
                {
                    currentGold -= actualPrice;
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
                        ToastPopupController.Instance.ShowToast(LocalizationManager.GetSys("SYS_INVENTORY_FULL", "인벤토리가 가득 찼습니다."));
                    }
                    return false;
                }
            }
            else
            {
                item.ApplyItemEffect(diceManager);
                currentGold -= actualPrice;
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
            ToastPopupController.Instance.ShowToast(LocalizationManager.GetSys("SYS_GOLD_NOT_ENOUGH", "골드가 부족합니다."));
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
        {
            ticketSelectionPanel.SetActive(true);
            Transform dim = ticketSelectionPanel.transform.Find("DimPanel");
            if (dim != null) dim.SetAsFirstSibling();
            ticketSelectionPanel.transform.SetAsLastSibling();
        }
        SetTicketCombatButtonsHidden(true);

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
        SetTicketCombatButtonsHidden(false);
    }

    bool ticketHidRoll;
    bool ticketHidFinish;
    bool ticketRollWasActive;
    bool ticketFinishWasActive;

    void SetTicketCombatButtonsHidden(bool hidden)
    {
        if (diceManager == null || diceManager.ui == null) return;
        if (hidden)
        {
            if (!ticketHidRoll && diceManager.ui.rollButton != null)
            {
                ticketRollWasActive = diceManager.ui.rollButton.gameObject.activeSelf;
                diceManager.ui.rollButton.gameObject.SetActive(false);
                ticketHidRoll = true;
            }
            if (!ticketHidFinish && diceManager.ui.finishButton != null)
            {
                ticketFinishWasActive = diceManager.ui.finishButton.gameObject.activeSelf;
                diceManager.ui.finishButton.gameObject.SetActive(false);
                ticketHidFinish = true;
            }
            return;
        }
        if (ticketHidRoll && diceManager.ui.rollButton != null && ticketRollWasActive)
            diceManager.ui.rollButton.gameObject.SetActive(true);
        if (ticketHidFinish && diceManager.ui.finishButton != null && ticketFinishWasActive)
            diceManager.ui.finishButton.gameObject.SetActive(true);
        ticketHidRoll = false;
        ticketHidFinish = false;
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
    // 리롤(고양이 눈) 할인율을 적용한 최종 리롤 비용 계산
    public int GetFinalRerollCost()
    {
        int discount = FigureEffectManager.Instance != null ? FigureEffectManager.Instance.GetShopDiscountRate(FigureEffectType.DiscountShopReroll) : 0;
        int finalCost = Mathf.FloorToInt(rerollCost * (1f - discount / 100f));
        return Mathf.Max(0, finalCost);
    }

    public void UpdateRerollUI()
    {
        int finalCost = GetFinalRerollCost();
        if (rerollCostText != null) rerollCostText.text = LocalizationManager.GetUi("UI_REROLL_COST", "리롤 : {0} G", finalCost);
    }

    // 복고양이 효과: 진열된 아이템 중 하나를 무작위로 0원 처리
    public void MakeRandomItemFree()
    {
        var validSlots = shopSlots.Where(s => s.gameObject.activeSelf && !s.isLocked && !s.isPurchased && s.currentData != null).ToList();
        if (validSlots.Count > 0)
        {
            var randSlot = validSlots[Random.Range(0, validSlots.Count)];
            randSlot.ApplyLuckyCatFree();
        }
    }

    // 골드 획득 시 사용. 증가 효과 적용과 UI 갱신을 한 곳에서 처리
    // 반환값은 보너스까지 반영해 실제로 지급한 골드
    public int GrantGold(int baseAmount)
    {
        if (baseAmount <= 0) return 0;

        float multiplier = FigureEffectManager.Instance != null
            ? FigureEffectManager.Instance.GetGoldGainMultiplier()
            : 1f;

        // 획득 건별로 소수점 아래는 버림
        int grantedAmount = Mathf.FloorToInt(baseAmount * multiplier);

        currentGold += grantedAmount;
        if (grantedAmount > baseAmount) FigureEffectManager.Instance?.NotifyPassiveApplied(FigureEffectType.IncreaseGoldGainPercent);

        diceManager?.ui?.UpdateGoldUI(currentGold);

        if (GoldCounter.Instance != null)
        {
            GoldCounter.Instance.SetGold(currentGold);
        }

        return grantedAmount;
    }

    public void HideTooltip() => tooltipPanel.SetActive(false);
}
