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

            //피규어 획득 즉시(OnAcquired) 발동하는 효과 적용
            // (거북 등껍질, 바나나 왕관 등의 최대 체력 증가)
            var acquiredNode = figure.figureNodes.Find(n => n.triggerType == FigureTriggerType.OnAcquired);
            if (acquiredNode != null && acquiredNode.effects.Count > 0)
            {
                StartCoroutine(ApplyFigureEffectsCoroutine(acquiredNode.effects, 0, diceManager, diceManager.shopManager));
            }

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

        
    // 스테이지가 끝났을 때 패시브 피규어들을 발동시킵니다.
    public void EvaluateStageClearTriggers(DiceManager diceManager, ShopManager shopManager)
    {
        foreach (var figure in ownedFigures)
        {
            foreach (var node in figure.figureNodes)
            {
                if (node.triggerType == FigureTriggerType.OnCombatEnd)
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
    //주사위 결산 시 피규어 트리거를 확인하고 코루틴으로 대기합니다.
    public IEnumerator EvaluateTurnEndTriggersCoroutine(List<int> finalDiceValues, string handName, int currentBaseChips, DiceManager diceManager, ShopManager shopManager)
    {
        int[] diceCounts = new int[7];
        foreach (int v in finalDiceValues)
        {
            if (v >= 0 && v <= 6) diceCounts[v]++;
        }

        foreach (var figure in ownedFigures)
        {
            foreach (var node in figure.figureNodes)
            {
                bool isTriggered = false;

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

                if (isTriggered)
                {
                    Debug.Log($"[피규어 발동] {figure.itemName}의 {node.triggerType} 조건 달성!");
                    yield return StartCoroutine(ApplyFigureEffectsCoroutine(node.effects, currentBaseChips, diceManager, shopManager));
                }
            }
        }
    }

    // 조건 만족 시 실질적인 효과를 주고, UI 창이 켜지면 닫힐 때까지 대기하는 코루틴
    private IEnumerator ApplyFigureEffectsCoroutine(List<FigureEffectNode> effects, int currentBaseChips, DiceManager diceManager, ShopManager shopManager)
    {
        foreach (var effect in effects)
        {
            // 1. 확률 검사
            float prob = effect.probability <= 0f ? 100f : effect.probability;
            if (Random.Range(0f, 100f) > prob) continue;

            //어떻게 계산할지(CalcType)에 따라 최종 값을 먼저 뽑아냄
  
            float actualValue = effect.effectValue;
            switch (effect.calcType)
            {
                case EffectCalcType.MissingHP:
                    int missingHP = diceManager.playerMaxHP - diceManager.currentPlayerHP;
                    actualValue = missingHP * (effect.effectValue / 100f);
                    break;
                case EffectCalcType.EnemyHP:
                    if (diceManager.enemy != null) actualValue = diceManager.enemy.CurrentHP * (effect.effectValue / 100f);
                    break;
                case EffectCalcType.CurrentChips:
                    actualValue = currentBaseChips * (effect.effectValue > 0 ? effect.effectValue : 1f);
                    break;
                case EffectCalcType.OwnedFigures:
                    actualValue = ownedFigures.Count * effect.effectValue;
                    break;
                case EffectCalcType.Flat:
                default:
                    actualValue = effect.effectValue; // 고정값 그대로 사용
                    break;
                case EffectCalcType.PlayerMaxHP:
                    actualValue = diceManager.playerMaxHP * (effect.effectValue / 100f);
                    break;
                case EffectCalcType.IncomingDamage:
                    actualValue = effect.effectValue; // 뎀감은 별도 함수에서 계산하므로 값만 그대로 넘김
                    break;
                //덱에 보유 중인 주사위 1개당 N(effectValue)만큼 곱해서 계산
                case EffectCalcType.DeckDiceCount:
                    actualValue = diceManager.masterDeck.Count * effect.effectValue;
                    break;
                case EffectCalcType.SnackCount:
                    int snackCount = 0;
                    foreach (var slot in snackSlots)
                    {
                        if (!slot.isEmpty) snackCount++;
                    }
                    actualValue = snackCount * effect.effectValue;
                    break;

                case EffectCalcType.CurrentGold:
                    if (shopManager != null)
                    {
                        actualValue = shopManager.currentGold * (effect.effectValue / 100f);
                    }
                    break;
                // 소용돌이 트로피: 적 최대 체력 기준 퍼센트 계산
                case EffectCalcType.EnemyMaxHP:
                    if (diceManager.enemy != null)
                    {
                        actualValue = diceManager.enemy.MaxHP * (effect.effectValue / 100f);
                    }
                    break;
            }

            // 뽑아낸 actualValue를 가지고 행동(Action)을 수행

            switch (effect.effectType)
            {
                //회복할 때 힐량 배수를 곱해줌
                case FigureEffectType.HealHP:
                    float healMult = GetHealMultiplier();
                    diceManager.currentPlayerHP += Mathf.FloorToInt(actualValue * healMult);
                    if (diceManager.currentPlayerHP > diceManager.playerMaxHP) diceManager.currentPlayerHP = diceManager.playerMaxHP;
                    break;
                case FigureEffectType.AddGold:
                    if (shopManager != null) { shopManager.currentGold += Mathf.FloorToInt(actualValue); diceManager.ui?.UpdateGoldUI(shopManager.currentGold); if (GoldCounter.Instance != null) GoldCounter.Instance.SetGold(shopManager.currentGold); }
                    break;
                case FigureEffectType.AddChips: diceManager.snackBonusChips += Mathf.FloorToInt(actualValue); break;
                case FigureEffectType.AddMultiplier: diceManager.snackBonusMult += actualValue; break; // 배수는 float 그대로
                case FigureEffectType.DamageEnemy:
                    if (diceManager.enemy != null)
                    {
                        // 리롤 중에 돌연사하면 즉시 문지기(OnEnemyKilled)를 부르고,
                        // 결산 중에 돌연사하면 아까 짠 방어 코드가 부르도록 null을 줌
                        System.Action deathCallback = diceManager.isCalculating ? null : (System.Action)diceManager.OnEnemyKilled;

                        diceManager.enemy.TakeDamage(Mathf.FloorToInt(actualValue), deathCallback);
                    }
                    break;
                case FigureEffectType.AddReroll: diceManager.figureBonusRerolls += Mathf.FloorToInt(actualValue); break;
                case FigureEffectType.GetSnack: if (effect.optionalItem != null) AddItem(effect.optionalItem); break;   

                //1번 카테고리 특수 효과들
                case FigureEffectType.MultiplyCombatEndGold: diceManager.combatWinGoldMultiplier *= actualValue; break;
                case FigureEffectType.IncreaseMaxHP: diceManager.playerMaxHP += Mathf.FloorToInt(actualValue); diceManager.currentPlayerHP += Mathf.FloorToInt(actualValue); break;
                case FigureEffectType.AddExtraAttack: diceManager.extraAttackCount += Mathf.FloorToInt(actualValue); break;
                case FigureEffectType.NullifyEnemySkill:
                    diceManager.isEnemySkillNullified = true;
                    if (diceManager.enemy != null && diceManager.enemy.CurrentBossAbility == BossAbilityType.FakeDice) diceManager.RestoreFakeDice();
                    break;
                case FigureEffectType.FixEnemyAttackToOne: diceManager.isNextEnemyAttackFixedToOne = true; break;
                case FigureEffectType.DestroyDebuffDice: diceManager.RestoreFakeDice(); break;
                case FigureEffectType.AddFlameDamage:
                    diceManager.figureBonusFlameDamage += Mathf.FloorToInt(actualValue);
                    break;
                //5번 상점 복고양이 효과
                case FigureEffectType.MakeRandomShopItemFree:
                    if (shopManager != null)
                    {
                        shopManager.MakeRandomItemFree();
                    }
                    break;

                // --- UI 선택창 호출 ---
                case FigureEffectType.OpenTicketSelection:
                    if (shopManager != null) { shopManager.ShowTicketSelection(); while (shopManager.ticketSelectionPanel != null && shopManager.ticketSelectionPanel.activeSelf) yield return null; }
                    break;
                case FigureEffectType.OpenCoatingSelection:
                    if (shopManager != null) {
                        DiceType randType = (DiceType)Random.Range(1, 5);
                        Color randColor = randType == DiceType.Gold ? Color.yellow : randType == DiceType.Ice ? Color.cyan : randType == DiceType.Dark ? new Color32(43, 42, 26, 255) : Color.white;
                        shopManager.ShowCoatingSelection(randType, 1.0f, randColor);
                        while (CoatingSelectionPanel.IsPanelOpen) yield return null;
                    }
                    break;
                case FigureEffectType.OpenSatelliteSelection:
                    if (shopManager != null) { shopManager.ShowSatelliteSelection((SatelliteType)Random.Range(0, 4)); while (shopManager.satelliteSelectionPanel != null && shopManager.satelliteSelectionPanel.gameObject.activeSelf) yield return null; }
                    break;
            }
        }
    }

    // 적에게 맞기 직전에 모든 뎀감(ReduceDamageTaken) 효과를 긁어와서 합산해 주는 함수
    public int GetTotalDamageReduction(int incomingDamage, DiceManager diceManager)
    {
        float totalReduction = 0;
        foreach (var figure in ownedFigures)
        {
            foreach (var node in figure.figureNodes)
            {
                // 항시 발동이거나, 피격 시 발동하는 조건일 때
                if (node.triggerType == FigureTriggerType.Always || node.triggerType == FigureTriggerType.OnDamaged)
                {
                    foreach (var effect in node.effects)
                    {
                        if (effect.effectType == FigureEffectType.ReduceDamageTaken)
                        {
                            if (effect.calcType == EffectCalcType.Flat)
                                totalReduction += effect.effectValue; // 고정 뎀감 (거북 등껍질 -3)
                            else if (effect.calcType == EffectCalcType.IncomingDamage)
                                totalReduction += incomingDamage * (effect.effectValue / 100f); // 퍼센트 뎀감 (울퉁불퉁 헬멧 10%)
                        }
                        //안전모: 내 체력이 최대 체력의 30% 이하일 때만 발동
                        else if (effect.effectType == FigureEffectType.ReduceDamageTakenLowHP)
                        {
                            if (diceManager.currentPlayerHP <= diceManager.playerMaxHP * 0.3f)
                            {
                                if (effect.calcType == EffectCalcType.IncomingDamage) totalReduction += incomingDamage * (effect.effectValue / 100f);
                                else if (effect.calcType == EffectCalcType.Flat) totalReduction += effect.effectValue;
                            }
                        }
                    }
                }
            }
        }
        return Mathf.FloorToInt(totalReduction);
    }

    // 도도새 모자 등의 힐량 증가(%) 수치를 가져오는 함수 (기본 1배 = 1.0f)
    public float GetHealMultiplier()
    {
        float mult = 1.0f;
        foreach (var figure in ownedFigures)
        {
            foreach (var node in figure.figureNodes)
            {
                if (node.triggerType == FigureTriggerType.Always)
                {
                    foreach (var effect in node.effects)
                    {
                        if (effect.effectType == FigureEffectType.IncreaseHealMultiplier)
                        {
                            mult += (effect.effectValue / 100f);
                        }
                    }
                }
            }
        }
        return mult;
    }

    // 할인율(%)을 긁어와서 합산해 주는 함수 (나비 반지, 모래 목걸이, 고양이 눈)
    public int GetShopDiscountRate(FigureEffectType discountType)
    {
        float totalDiscount = 0;
        foreach (var figure in ownedFigures)
        {
            foreach (var node in figure.figureNodes)
            {
                // 할인은 상시(Always) 발동 패시브
                if (node.triggerType == FigureTriggerType.Always)
                {
                    foreach (var effect in node.effects)
                    {
                        if (effect.effectType == discountType)
                        {
                            totalDiscount += effect.effectValue;
                        }
                    }
                }
            }
        }
        return Mathf.FloorToInt(totalDiscount);
    }

    // 상점 문을 열었을 때(OnShopEntered) 발동하는 피규어 처리 (복고양이용)
    public void EvaluateShopEnteredTriggers(DiceManager diceManager, ShopManager shopManager)
    {
        foreach (var figure in ownedFigures)
        {
            foreach (var node in figure.figureNodes)
            {
                if (node.triggerType == FigureTriggerType.OnShopEntered)
                {
                    Debug.Log($"[상점 진입 발동] {figure.itemName} 효과 달성!");
                    ApplyFigureEffects(node.effects, diceManager, shopManager);
                }
            }
        }
    }

    // 리롤 버튼을 눌렀을 때(OnDiceReroll) 발동하는 피규어 처리 (6번 카테고리)
    public void EvaluateRerollTriggers(DiceManager diceManager, ShopManager shopManager)
    {
        foreach (var figure in ownedFigures)
        {
            foreach (var node in figure.figureNodes)
            {
                if (node.triggerType == FigureTriggerType.OnDiceReroll)
                {
                    Debug.Log($"[리롤 발동] {figure.itemName} 기믹 발동!");
                    ApplyFigureEffects(node.effects, diceManager, shopManager);
                }
            }
        }
    }

    // 다른 스크립트(스테이지 클리어, 스낵 사용)에서 에러가 안 나도록 기존 동기형 이름도 남겨둠
    public void ApplyFigureEffects(List<FigureEffectNode> effects, DiceManager diceManager, ShopManager shopManager)
    {
        StartCoroutine(ApplyFigureEffectsCoroutine(effects, 0, diceManager, shopManager));
    }


}