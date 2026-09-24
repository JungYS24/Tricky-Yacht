using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// 캐싱을 위한 데이터 구조체: '어떤 피규어'의 '어떤 노드'인지 함께 기억합니다.
public class FigureCacheItem
{
    public FigureItemSO sourceFigure;
    public FigureNode node;
}

public class FigureEffectManager : MonoBehaviour
{
    public static FigureEffectManager Instance { get; private set; }

    // 트리거 타입을 키(Key)로 사용하여 빠르게 대상을 찾을 수 있는 캐시 딕셔너리
    private Dictionary<FigureTriggerType, List<FigureCacheItem>> effectCache = new Dictionary<FigureTriggerType, List<FigureCacheItem>>();
    // 인벤토리가 바뀔 때 계산해두는 상시 효과 합계
    private float cachedHealMultiplier = 1f;
    private float cachedSnackPreserveChance = 0f;
    private float cachedSnackMultiplier = 1f;
    private float cachedGoldGainMultiplier = 1f;
    private float cachedIceMultiplierBonus = 0f;
    private float cachedIceChipsBonus = 0f;

    private float cachedCoatingDiscount = 0f;
    private float cachedSatelliteDiscount = 0f;
    private float cachedShopRerollDiscount = 0f;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // 인벤토리에 변동(획득, 판매, 초기화)이 생길 때마다 InventoryManager가 호출하여 캐시를 갱신합니다.
    public void RebuildCache()
    {
        effectCache.Clear();
        cachedHealMultiplier = 1f;
        cachedSnackPreserveChance = 0f;

        cachedCoatingDiscount = 0f;
        cachedSatelliteDiscount = 0f;
        cachedShopRerollDiscount = 0f;
        cachedSnackMultiplier = 1f;
        cachedIceMultiplierBonus = 0f;
        cachedIceChipsBonus = 0f;

        if (InventoryManager.Instance == null) return;

        foreach (var figure in InventoryManager.Instance.ownedFigures)
        {
            foreach (var node in figure.figureNodes)
            {
                if (!effectCache.ContainsKey(node.triggerType))
                {
                    effectCache[node.triggerType] = new List<FigureCacheItem>();
                }

                effectCache[node.triggerType].Add(new FigureCacheItem { sourceFigure = figure, node = node });
                // 상시 효과는 인벤토리 변경 시 합계를 계산
                if (node.triggerType == FigureTriggerType.Always)
                {
                    foreach (var effect in node.effects)
                    {
                        switch (effect.effectType)
                        {
                            case FigureEffectType.IncreaseHealMultiplier:
                                cachedHealMultiplier += effect.effectValue / 100f;
                                break;

                            case FigureEffectType.PreserveSnackChance:
                                cachedSnackPreserveChance += effect.effectValue / 100f;
                                break;

                            case FigureEffectType.DiscountCoating:
                                cachedCoatingDiscount += effect.effectValue;
                                break;

                            case FigureEffectType.DiscountSatellite:
                                cachedSatelliteDiscount += effect.effectValue;
                                break;

                            case FigureEffectType.DiscountShopReroll:
                                cachedShopRerollDiscount += effect.effectValue;
                                break;
                            case FigureEffectType.MultiplySnackEffects:
                                cachedSnackMultiplier *= Mathf.Max(0f, effect.effectValue);
                                break;
                            case FigureEffectType.IncreaseGoldGainPercent:
                                cachedGoldGainMultiplier += effect.effectValue / 100f;
                                break;
                            case FigureEffectType.AddIceMultiplier:
                                cachedIceMultiplierBonus += effect.effectValue;
                                break;

                            case FigureEffectType.AddIceChips:
                                cachedIceChipsBonus += effect.effectValue;
                                break;
                        }
                    }
                }
            }
        }
        Debug.Log("[FigureEffectManager] 피규어 효과 캐시 갱신 완료!");
    }



    // 스테이지가 끝났을 때 패시브 피규어들을 발동시킵니다.
    public void EvaluateStageClearTriggers(DiceManager diceManager, ShopManager shopManager)
    {
        if (effectCache.ContainsKey(FigureTriggerType.OnCombatEnd))
        {
            foreach (var cacheItem in effectCache[FigureTriggerType.OnCombatEnd])
            {
                Debug.Log($"[피규어 스테이지 클리어 발동] {cacheItem.sourceFigure.itemName} 패시브 효과 달성!");
                StartCoroutine(ExecuteFigureNode(cacheItem, 0, diceManager, shopManager));
            }
        }
    }

    // 스낵을 사용했을 때 OnSnackUsed 피규어들을 발동
    public void EvaluateSnackUsedTriggers(DiceManager diceManager, ShopManager shopManager)
    {
        if (effectCache.ContainsKey(FigureTriggerType.OnSnackUsed))
        {
            foreach (var cacheItem in effectCache[FigureTriggerType.OnSnackUsed])
            {
                Debug.Log($"[피규어 스낵 사용 발동] {cacheItem.sourceFigure.itemName} 효과 달성!");
                StartCoroutine(ExecuteFigureNode(cacheItem, 0, diceManager, shopManager));
            }
        }

        // 리롤 횟수 등 UI에 즉각적인 변화가 생겼으므로 화면을 강제 갱신
        diceManager.ForceUpdateUI();
    }

    // 주사위 결산 시 피규어 트리거를 확인하고 코루틴으로 대기합니다.
    public IEnumerator EvaluateTurnEndTriggersCoroutine(List<int> finalDiceValues, HandRank handRank, int currentBaseChips, DiceManager diceManager, ShopManager shopManager)
    {
        int[] diceCounts = new int[7];
        foreach (int v in finalDiceValues)
        {
            if (v >= 0 && v <= 6) diceCounts[v]++;
        }

        // 이번 결산에서 만족한 트리거 타입들만 수집
        List<FigureTriggerType> activeTriggers = new List<FigureTriggerType>();

        if (diceCounts[1] >= 3) activeTriggers.Add(FigureTriggerType.ThreeOf1);
        if (diceCounts[2] >= 3) activeTriggers.Add(FigureTriggerType.ThreeOf2);
        if (diceCounts[3] >= 3) activeTriggers.Add(FigureTriggerType.ThreeOf3);
        if (diceCounts[4] >= 3) activeTriggers.Add(FigureTriggerType.ThreeOf4);
        if (diceCounts[5] >= 3) activeTriggers.Add(FigureTriggerType.ThreeOf5);
        if (diceCounts[6] >= 3) activeTriggers.Add(FigureTriggerType.ThreeOf6);

        HandRankUtil.AddHandTriggers(handRank, activeTriggers);

        // 수집된 트리거에 해당하는 피규어 효과만 꺼내서 바로 실행 (불필요한 반복 탐색 제거)
        foreach (var tType in activeTriggers)
        {
            if (effectCache.ContainsKey(tType))
            {
                foreach (var cacheItem in effectCache[tType])
                {
                    Debug.Log($"[피규어 발동] {cacheItem.sourceFigure.itemName}의 {tType} 조건 달성!");
                    yield return StartCoroutine(ExecuteFigureNode(cacheItem,currentBaseChips,diceManager,shopManager));
                }
            }
        }
    }

    // 조건 만족 시 실질적인 효과를 주고, UI 창이 켜지면 닫힐 때까지 대기하는 코루틴
    private IEnumerator ApplyFigureEffectsCoroutine(List<FigureEffectNode> effects, int currentBaseChips, DiceManager diceManager, ShopManager shopManager, FigureItemSO sourceFigure, int actualDamage = 0)
    {
        foreach (var effect in effects)
        {
            //확률 검사
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
                    actualValue = InventoryManager.Instance.ownedFigures.Count * effect.effectValue;
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
                    foreach (var slot in InventoryManager.Instance.snackSlots)
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
                case EffectCalcType.ActualDamage:
                    actualValue = actualDamage * (effect.effectValue / 100f);
                    break;

            }

            // 뽑아낸 actualValue를 가지고 행동(Action)을 수행
            switch (effect.effectType)
            {
                case FigureEffectType.HealHP:
                    {
                        //회복할 때 힐량 배수를 곱해줌
                        float healMult = GetHealMultiplier();
                        int healAmount = Mathf.FloorToInt(actualValue * healMult);

                        diceManager.playerStatus.Heal(healAmount);
                        break;
                    }
                case FigureEffectType.AddGold:
                    if (shopManager != null)
                    {
                        shopManager.GrantGold(Mathf.FloorToInt(actualValue));
                    }
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
                case FigureEffectType.GetSnack: if (effect.optionalItem != null) InventoryManager.Instance.AddItem(effect.optionalItem); break;

                case FigureEffectType.DestroySelf:
                    // 피규어 발동 후 영구 파괴 (일회성 아이템용)
                    InventoryManager.Instance.RemoveItem(sourceFigure);
                    break;
                    break;
                //보호막 추가 (이번 전투 동안 유지)
                case FigureEffectType.AddShield:
                    diceManager.currentShield += Mathf.FloorToInt(actualValue);
                    // 보호막 UI가 있다면 즉시 갱신
                    if (diceManager.ui != null) diceManager.ui.UpdateShieldUI(diceManager.currentShield);
                    break;
                //전투 누적 보너스 (스테이지 한정)
                case FigureEffectType.AddCombatMultiplier:
                    diceManager.stageBonusMult += actualValue;
                    break;
                case FigureEffectType.AddCombatChips:
                    diceManager.stageBonusChips += Mathf.FloorToInt(actualValue);
                    break;
                case FigureEffectType.ReduceEnemyMaxHP:
                    if (diceManager.enemy != null)
                    {
                        diceManager.enemy.ReduceMaxHP(
                            Mathf.FloorToInt(actualValue));
                    }
                    break;
                case FigureEffectType.DamageEnemyOrPlayer:
                    {
                        // 두 효과를 각각 추첨하지 않고, 한 번의 추첨으로 한쪽만 실행
                    if (Random.value < 0.5f)
                        {
                    if (diceManager.enemy != null && !diceManager.enemy.IsDead)
                            {
                                int enemyDamage = Mathf.Max(0, Mathf.FloorToInt(actualValue));
                                diceManager.enemy.TakeDamage(enemyDamage, diceManager.OnEnemyKilled);
                            }
                        }
                    else
                        {
                            int playerDamage = Mathf.Max(0, Mathf.FloorToInt(effect.secondaryEffectValue));
                            // 기존 피해 규칙에 따라 보호막부터 차감
                            diceManager.playerStatus.TakeDamage(playerDamage);
                            diceManager.ui?.UpdateShieldUI(diceManager.currentShield);
                            // 살아 있다면 용암 가면 등의 체력 조건 검사
                            EvaluateLowHPTriggers(diceManager, shopManager);

                    if (diceManager.currentPlayerHP <= 0)
                            {
                                bool revived = EvaluateDeathTriggers(diceManager, shopManager);
                                if (!revived)
                                {
                                    // 현재 라운드의 입력을 막고 게임 오버 처리
                                    diceManager.isCalculating = true;

                                    GameSaveManager.Instance?.DeleteSave();

                            string gameOverText = LocalizationManager.GetUi("UI_GAME_OVER", "게임 오버");
                                    diceManager.ui?.ShowResult("#FF0000", gameOverText);
                                    diceManager.StartCoroutine(diceManager.ShowGameOverPanelDelayed());
                                }
                            }
                        }
                        diceManager.ForceUpdateUI();
                        break;
                    }
                case FigureEffectType.AddPermanentMultiplier:
                    diceManager.permanentFigureMultiplier += actualValue;
                    break;

                //1번 카테고리 특수 효과들
                case FigureEffectType.MultiplyCombatEndGold: diceManager.combatWinGoldMultiplier *= actualValue; break;
                case FigureEffectType.IncreaseMaxHP: diceManager.playerMaxHP += Mathf.FloorToInt(actualValue); diceManager.currentPlayerHP += Mathf.FloorToInt(actualValue); break;
                case FigureEffectType.AddExtraAttack: diceManager.extraAttackCount += Mathf.FloorToInt(actualValue); break;
                case FigureEffectType.NullifyEnemySkill:
                    diceManager.isEnemySkillNullified = true;
                    if (diceManager.enemy != null && diceManager.enemy.CurrentBossAbility == BossAbilityType.FakeDice) diceManager.deckManager.RestoreFakeDice(ref diceManager.originalBossDice, ref diceManager.fakeDiceIndex);    
                    break;
                case FigureEffectType.FixEnemyAttackToOne: diceManager.isNextEnemyAttackFixedToOne = true; break;
                case FigureEffectType.DestroyDebuffDice: diceManager.deckManager.RestoreFakeDice(ref diceManager.originalBossDice, ref diceManager.fakeDiceIndex); break;
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
                    if (shopManager != null)
                    {
                        DiceType randType = (DiceType)Random.Range(1, 5);
                        Color randColor = randType == DiceType.Gold ? Color.yellow : randType == DiceType.Ice ? Color.cyan : randType == DiceType.Dark ? new Color32(43, 42, 26, 255) : Color.white;
                        float coatingMultiplier = randType == DiceType.Prism ? 1.2f : 1.0f;

                        shopManager.ShowCoatingSelection(randType, coatingMultiplier, randColor);
                        while (CoatingSelectionPanel.IsPanelOpen) yield return null;
                    }
                    break;

                case FigureEffectType.OpenSatelliteSelection:
                    if (shopManager != null)
                    {
                        shopManager.ShowSatelliteSelection(
                            (SatelliteType)Random.Range(0, 4));

                        // 위성 선택이 끝날 때까지 다음 효과 진행 대기
                        while (SatelliteSelectionPanel.IsPanelOpen)
                        {
                            yield return null;
                        }
                    }
                    break;
            }
        }
    }

    // 적에게 맞기 직전에 모든 뎀감(ReduceDamageTaken) 효과를 긁어와서 합산해 주는 함수
    public int GetTotalDamageReduction(int incomingDamage, DiceManager diceManager)
    {
        float totalReduction = 0;

        // 보조 함수: 특정 트리거 캐시를 확인해 데미지 계산
        void CalculateReduction(FigureTriggerType type)
        {
            if (effectCache.ContainsKey(type))
            {
                foreach (var cacheItem in effectCache[type])
                {
                    foreach (var effect in cacheItem.node.effects)
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

        // 항시 발동이거나, 피격 시 발동하는 조건일 때 캐시 조회
        CalculateReduction(FigureTriggerType.Always);
        CalculateReduction(FigureTriggerType.OnDamaged);

        return Mathf.FloorToInt(totalReduction);
    }

    // 도도새 모자 등의 힐량 증가(%) 수치를 가져오는 함수 (기본 1배 = 1.0f)
    public float GetHealMultiplier()
    {
        return Mathf.Max(0f, cachedHealMultiplier);
    }

    // 할인율(%)을 긁어와서 합산해 주는 함수 (나비 반지, 모래 목걸이, 고양이 눈)
    public int GetShopDiscountRate(FigureEffectType discountType)
    {
        float discount;

        switch (discountType)
        {
            case FigureEffectType.DiscountCoating:
                discount = cachedCoatingDiscount;
                break;

            case FigureEffectType.DiscountSatellite:
                discount = cachedSatelliteDiscount;
                break;

            case FigureEffectType.DiscountShopReroll:
                discount = cachedShopRerollDiscount;
                break;

            default:
                return 0;
        }

        return Mathf.Clamp(Mathf.FloorToInt(discount), 0, 100);
    }
    //스낵 소모 방지 판정
    public bool ShouldPreserveSnack()
    {
        float chance = Mathf.Clamp01(cachedSnackPreserveChance);

        if (chance <= 0f) return false;
        if (chance >= 1f) return true;

        return Random.value < chance;
    }

    // 상점 문을 열었을 때(OnShopEntered) 발동하는 피규어 처리 (복고양이용)
    public void EvaluateShopEnteredTriggers(DiceManager diceManager, ShopManager shopManager)
    {
        if (effectCache.ContainsKey(FigureTriggerType.OnShopEntered))
        {
            foreach (var cacheItem in effectCache[FigureTriggerType.OnShopEntered])
            {
                Debug.Log($"[상점 진입 발동] {cacheItem.sourceFigure.itemName} 효과 달성!");
                StartCoroutine(ExecuteFigureNode(cacheItem, 0, diceManager, shopManager));
            }
        }
    }

    // 리롤 버튼을 눌렀을 때(OnDiceReroll) 발동하는 피규어 처리 (6번 카테고리)
    public void EvaluateRerollTriggers(DiceManager diceManager, ShopManager shopManager)
    {
        if (effectCache.ContainsKey(FigureTriggerType.OnDiceReroll))
        {
            foreach (var cacheItem in effectCache[FigureTriggerType.OnDiceReroll])
            {
                Debug.Log($"[리롤 발동] {cacheItem.sourceFigure.itemName} 기믹 발동!");
                StartCoroutine(ExecuteFigureNode(cacheItem, 0, diceManager, shopManager));
            }
        }
    }

    // 전투(스테이지) 시작 시 딱 한 번 발동하는 피규어 처리 (예: 연, 아귀 인형)
    public void EvaluateCombatStartTriggers(DiceManager diceManager, ShopManager shopManager)
    {
        if (effectCache.ContainsKey(FigureTriggerType.OnCombatStart))
        {
            foreach (var cacheItem in effectCache[FigureTriggerType.OnCombatStart])
            {
                Debug.Log($"[전투 시작 발동] {cacheItem.sourceFigure.itemName} 기믹 발동!");
                StartCoroutine(ExecuteFigureNode(cacheItem, 0, diceManager, shopManager));
            }
        }
        // 리롤 횟수 증가 등 UI 변동이 생길 수 있으므로 즉각 반영
        diceManager.ForceUpdateUI();
    }

    // 매 라운드(턴) 시작 시 발동하는 피규어 처리 (예: 깜짝 상자)
    public void EvaluateRoundStartTriggers(DiceManager diceManager, ShopManager shopManager)
    {
        if (effectCache.ContainsKey(FigureTriggerType.OnRoundStart))
        {
            foreach (var cacheItem in effectCache[FigureTriggerType.OnRoundStart])
            {
                Debug.Log($"[라운드 시작 발동] {cacheItem.sourceFigure.itemName} 기믹 발동!");
                StartCoroutine(ExecuteFigureNode(cacheItem, 0, diceManager, shopManager));
            }
        }
        // UI 반영
        diceManager.ForceUpdateUI();
    }

    // 사망 시 발동하는 피규어 처리 (부활)
    public bool EvaluateDeathTriggers(
     DiceManager diceManager,
     ShopManager shopManager)
    {
        if (diceManager == null ||
            diceManager.currentPlayerHP > 0 ||
            InventoryManager.Instance == null)
        {
            return false;
        }

        if (!effectCache.TryGetValue(
            FigureTriggerType.OnDeath, out var candidates))
        {
            return false;
        }

        // 부활 피규어 소멸로 캐시가 변경될 수 있으므로 복사
        FigureCacheItem[] snapshot = candidates.ToArray();

        foreach (var cacheItem in snapshot)
        {
            FigureItemSO figure = cacheItem.sourceFigure;
            FigureNode node = cacheItem.node;

            if (figure == null || node == null ||
                !InventoryManager.Instance.ownedFigures.Contains(figure))
            {
                continue;
            }

            if (node.requiredKills > 0)
            {
                diceManager.figureKillCounts.TryGetValue(
                    figure.itemName, out int kills);

                if (kills < node.requiredKills)
                    continue;
            }

            bool hasHealEffect = false;
            bool destroyAfterRevive = false;
            int reviveHP = 0;

            foreach (var effect in node.effects)
            {
                if (effect.effectType == FigureEffectType.HealHP)
                {
                    float value;

                    if (effect.calcType == EffectCalcType.PlayerMaxHP)
                    {
                        value = diceManager.playerMaxHP
                            * effect.effectValue / 100f;
                    }
                    else if (effect.calcType == EffectCalcType.Flat)
                    {
                        value = effect.effectValue;
                    }
                    else
                    {
                        continue;
                    }

                    hasHealEffect = true;
                    reviveHP = Mathf.Max(
                        reviveHP, Mathf.FloorToInt(value));
                }
                else if (effect.effectType == FigureEffectType.DestroySelf)
                {
                    destroyAfterRevive = true;
                }
            }

            if (!hasHealEffect) continue;

            // 부활은 도도새 회복 증가를 적용하지 않음
            diceManager.currentPlayerHP = Mathf.Clamp(
                reviveHP, 1, Mathf.Max(1, diceManager.playerMaxHP));

            if (destroyAfterRevive)
            {
                InventoryManager.Instance.RemoveItem(figure);
            }

            diceManager.ForceUpdateUI();
            return true; // 조건을 만족한 피규어 하나만 사용
        }

        return false;
    }

    // 적에게 피해를 입었을 때(피격 시) 발동하는 피규어 처리 (예: 광대의 눈물)
    public void EvaluateDamagedTriggers(DiceManager diceManager, ShopManager shopManager)
    {
        if (effectCache.ContainsKey(FigureTriggerType.OnDamaged))
        {
            foreach (var cacheItem in effectCache[FigureTriggerType.OnDamaged])
            {
                Debug.Log($"[피격 시 발동] {cacheItem.sourceFigure.itemName} 기믹 발동!");
                StartCoroutine(ExecuteFigureNode(cacheItem, 0, diceManager, shopManager));
            }
        }
        // 피격 시 배수가 오르는 등 UI 변화가 생길 수 있으므로 갱신
        diceManager.ForceUpdateUI();
    }
    // 조건 및 스테이지당 사용 제한을 확인한 뒤 기존 효과 실행기로 전달
    private IEnumerator ExecuteFigureNode( FigureCacheItem cacheItem,int currentBaseChips,DiceManager diceManager,ShopManager shopManager,int actualDamage = 0)
    { 
        if (cacheItem == null ||cacheItem.sourceFigure == null ||cacheItem.node == null ||diceManager == null)
        {
            yield break;
        }
        // 사망 후에는 일반 트리거 실행 중단
        // 부활은 EvaluateDeathTriggers에서 별도로 처리
        if (diceManager.currentPlayerHP <= 0)
            yield break;

        FigureNode node = cacheItem.node;
        if (node.requiredKills > 0)
        {
            diceManager.figureKillCounts.TryGetValue(
                cacheItem.sourceFigure.itemName, out int kills);

            if (kills < node.requiredKills)
                yield break;
        }


        if (node.effects == null || node.effects.Count == 0)
            yield break;

        // 낮은 체력 조건은 살아 있을 때만 검사
        if (node.triggerType == FigureTriggerType.OnLowHP)
        {
            if (diceManager.currentPlayerHP <= 0 ||
                diceManager.playerMaxHP <= 0)
            {
                yield break;
            }

            float thresholdHP = diceManager.playerMaxHP
                * Mathf.Clamp(node.healthThresholdPercent, 0f, 100f)
                / 100f;

            if (diceManager.currentPlayerHP > thresholdHP)
                yield break;
        }

        if (node.oncePerStage)
        {
            int nodeIndex = cacheItem.sourceFigure.figureNodes.IndexOf(node);
            if (nodeIndex < 0) yield break;

            // 현재 저장 방식과 동일하게 고유한 itemName을 사용
            string nodeKey = $"{cacheItem.sourceFigure.itemName}:{nodeIndex}";

            // 이미 사용했다면 종료. 처음이면 실행 전에 기록해 중복 진입 방지
            if (!diceManager.stageContext.usedFigureNodes.Add(nodeKey))
                yield break;
        }

        yield return ApplyFigureEffectsCoroutine(node.effects,currentBaseChips,diceManager,shopManager,cacheItem.sourceFigure,actualDamage);
    }

    public void EvaluateLowHPTriggers(
    DiceManager diceManager,
    ShopManager shopManager)
    {
        if (diceManager == null ||
            diceManager.enemy == null ||
            diceManager.enemy.IsDead ||
            diceManager.isStageClearing)
        {
            return;
        }

        if (!effectCache.TryGetValue(
            FigureTriggerType.OnLowHP, out var candidates))
        {
            return;
        }

        foreach (var cacheItem in candidates)
        {
            StartCoroutine(
                ExecuteFigureNode(cacheItem, 0, diceManager, shopManager));
        }
    }

    public void EvaluateEnemyDamageTriggers(
    DiceManager diceManager,
    int actualDamage,
    bool isFirstNormalAttack)
    {
        if (diceManager == null || actualDamage <= 0)
            return;

        ExecuteDamageTrigger(
            FigureTriggerType.OnEnemyDamaged,
            diceManager,
            actualDamage);

        if (isFirstNormalAttack)
        {
            ExecuteDamageTrigger(
                FigureTriggerType.OnFirstNormalAttack,
                diceManager,
                actualDamage);
        }
    }

    private void ExecuteDamageTrigger(
        FigureTriggerType trigger,
        DiceManager diceManager,
        int actualDamage)
    {
        if (!effectCache.TryGetValue(trigger, out var candidates))
            return;

        // 효과 실행 중 피규어가 제거되어 캐시가 바뀌어도 안전하게 순회
        FigureCacheItem[] snapshot = candidates.ToArray();

        foreach (var cacheItem in snapshot)
        {
            if (InventoryManager.Instance == null ||
                !InventoryManager.Instance.ownedFigures.Contains(
                    cacheItem.sourceFigure))
            {
                continue;
            }

            StartCoroutine(
                ExecuteFigureNode(
                    cacheItem,
                    0,
                    diceManager,
                    diceManager.shopManager,
                    actualDamage));
        }
    }

    public void RecordEnemyKill(DiceManager diceManager)
    {
        if (diceManager == null || InventoryManager.Instance == null)
            return;

        foreach (var figure in InventoryManager.Instance.ownedFigures)
        {
            int requiredCount = 0;

            foreach (var node in figure.figureNodes)
            {
                requiredCount = Mathf.Max(
                    requiredCount, node.requiredKills);
            }

            // 처치 조건을 사용하지 않는 피규어는 기록하지 않음
            if (requiredCount <= 0) continue;

            diceManager.figureKillCounts.TryGetValue(
                figure.itemName, out int currentCount);

            // 조건 달성 이후에는 불필요하게 계속 증가시키지 않음
            diceManager.figureKillCounts[figure.itemName] =
                currentCount >= requiredCount
                    ? requiredCount
                    : currentCount + 1;
        }
    }
    public void EvaluateDiceDestroyedTriggers(
    DiceManager diceManager,
    ShopManager shopManager)
    {
        if (diceManager == null) return;

        if (!effectCache.TryGetValue(
            FigureTriggerType.OnDiceDestroyed, out var candidates))
        {
            return;
        }

        FigureCacheItem[] snapshot = candidates.ToArray();

        foreach (var cacheItem in snapshot)
        {
            StartCoroutine(
                ExecuteFigureNode(
                    cacheItem, 0, diceManager, shopManager));
        }

        diceManager.ForceUpdateUI();
    }


    public float GetSnackEffectMultiplier()
    {
        return cachedSnackMultiplier;
    }

    //공통 골드 지급
    public float GetGoldGainMultiplier()
    {
        return Mathf.Max(0f, cachedGoldGainMultiplier);
    }
    public float GetIceMultiplierBonus()
    {
        return cachedIceMultiplierBonus;
    }

    public int GetIceChipsBonus()
    {
        return Mathf.FloorToInt(cachedIceChipsBonus);
    }

    // 다른 스크립트(스테이지 클리어, 스낵 사용)에서 에러가 안 나도록 기존 동기형 이름도 남겨둠
    public void ApplyFigureEffects(List<FigureEffectNode> effects, DiceManager diceManager, ShopManager shopManager, FigureItemSO sourceFigure)
    {
        StartCoroutine(ApplyFigureEffectsCoroutine(effects, 0, diceManager, shopManager, sourceFigure));
    }
}