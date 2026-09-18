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

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // 인벤토리에 변동(획득, 판매, 초기화)이 생길 때마다 InventoryManager가 호출하여 캐시를 갱신합니다.
    public void RebuildCache()
    {
        effectCache.Clear();

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
                ApplyFigureEffects(cacheItem.node.effects, diceManager, shopManager, cacheItem.sourceFigure);
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
                ApplyFigureEffects(cacheItem.node.effects, diceManager, shopManager, cacheItem.sourceFigure);
            }
        }

        // 리롤 횟수 등 UI에 즉각적인 변화가 생겼으므로 화면을 강제 갱신
        diceManager.ForceUpdateUI();
    }

    // 주사위 결산 시 피규어 트리거를 확인하고 코루틴으로 대기합니다.
    public IEnumerator EvaluateTurnEndTriggersCoroutine(List<int> finalDiceValues, string handName, int currentBaseChips, DiceManager diceManager, ShopManager shopManager)
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

        if (handName == "원 페어") activeTriggers.Add(FigureTriggerType.OnePair);
        if (handName == "투 페어") activeTriggers.Add(FigureTriggerType.TwoPair);
        if (handName == "트리플") activeTriggers.Add(FigureTriggerType.Triple);
        if (handName == "스트레이트") activeTriggers.Add(FigureTriggerType.Straight);
        if (handName == "풀하우스") activeTriggers.Add(FigureTriggerType.FullHouse);
        if (handName == "포카드") activeTriggers.Add(FigureTriggerType.FourOfAKind);
        if (handName == "Yacht" || handName == "요트" || handName == "파이브 카드") activeTriggers.Add(FigureTriggerType.Yacht);

        // 수집된 트리거에 해당하는 피규어 효과만 꺼내서 바로 실행 (불필요한 반복 탐색 제거)
        foreach (var tType in activeTriggers)
        {
            if (effectCache.ContainsKey(tType))
            {
                foreach (var cacheItem in effectCache[tType])
                {
                    Debug.Log($"[피규어 발동] {cacheItem.sourceFigure.itemName}의 {tType} 조건 달성!");
                    yield return StartCoroutine(ApplyFigureEffectsCoroutine(cacheItem.node.effects, currentBaseChips, diceManager, shopManager, cacheItem.sourceFigure));
                }
            }
        }
    }

    // 조건 만족 시 실질적인 효과를 주고, UI 창이 켜지면 닫힐 때까지 대기하는 코루틴
    private IEnumerator ApplyFigureEffectsCoroutine(List<FigureEffectNode> effects, int currentBaseChips, DiceManager diceManager, ShopManager shopManager, FigureItemSO sourceFigure)
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
        float mult = 1.0f;

        if (effectCache.ContainsKey(FigureTriggerType.Always))
        {
            foreach (var cacheItem in effectCache[FigureTriggerType.Always])
            {
                foreach (var effect in cacheItem.node.effects)
                {
                    if (effect.effectType == FigureEffectType.IncreaseHealMultiplier)
                    {
                        mult += (effect.effectValue / 100f);
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

        if (effectCache.ContainsKey(FigureTriggerType.Always))
        {
            foreach (var cacheItem in effectCache[FigureTriggerType.Always])
            {
                foreach (var effect in cacheItem.node.effects)
                {
                    if (effect.effectType == discountType)
                    {
                        totalDiscount += effect.effectValue;
                    }
                }
            }
        }
        return Mathf.FloorToInt(totalDiscount);
    }

    // 상점 문을 열었을 때(OnShopEntered) 발동하는 피규어 처리 (복고양이용)
    public void EvaluateShopEnteredTriggers(DiceManager diceManager, ShopManager shopManager)
    {
        if (effectCache.ContainsKey(FigureTriggerType.OnShopEntered))
        {
            foreach (var cacheItem in effectCache[FigureTriggerType.OnShopEntered])
            {
                Debug.Log($"[상점 진입 발동] {cacheItem.sourceFigure.itemName} 효과 달성!");
                ApplyFigureEffects(cacheItem.node.effects, diceManager, shopManager, cacheItem.sourceFigure);
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
                ApplyFigureEffects(cacheItem.node.effects, diceManager, shopManager, cacheItem.sourceFigure);
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
                ApplyFigureEffects(cacheItem.node.effects, diceManager, shopManager, cacheItem.sourceFigure);
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
                ApplyFigureEffects(cacheItem.node.effects, diceManager, shopManager, cacheItem.sourceFigure);
            }
        }
        // UI 반영
        diceManager.ForceUpdateUI();
    }

    // 사망 시 발동하는 피규어 처리 (부활)
    public bool EvaluateDeathTriggers(DiceManager diceManager, ShopManager shopManager)
    {
        if (effectCache.ContainsKey(FigureTriggerType.OnDeath))
        {
            // 부활 피규어가 여러 개 있어도 회차당 1회씩 소모하도록 첫 번째 것만 사용
            var cacheItem = effectCache[FigureTriggerType.OnDeath][0];
            Debug.Log($"[부활 발동] {cacheItem.sourceFigure.itemName} 기믹으로 부활합니다!");

            foreach (var effect in cacheItem.node.effects)
            {
                float actualValue = effect.effectValue;
                if (effect.calcType == EffectCalcType.PlayerMaxHP)
                {
                    actualValue = diceManager.playerMaxHP * (effect.effectValue / 100f);
                }
                else if (effect.calcType == EffectCalcType.Flat)
                {
                    actualValue = effect.effectValue;
                }

                if (effect.effectType == FigureEffectType.HealHP)
                {
                    // 부활 시에는 힐량 증폭(도도새 등)을 무시하고 명시된 체력(예: 1%)만 채워줌
                    diceManager.currentPlayerHP = Mathf.Max(1, Mathf.FloorToInt(actualValue));
                }
                else if (effect.effectType == FigureEffectType.DestroySelf)
                {
                    // 효과 발동 후 피규어 영구 파괴
                    InventoryManager.Instance.RemoveItem(cacheItem.sourceFigure);
                }
            }

            // 부활 후 체력 UI 즉시 갱신
            diceManager.ForceUpdateUI();
            return true; // 부활 성공!
        }
        return false; // 부활 수단 없음
    }

    // 적에게 피해를 입었을 때(피격 시) 발동하는 피규어 처리 (예: 광대의 눈물)
    public void EvaluateDamagedTriggers(DiceManager diceManager, ShopManager shopManager)
    {
        if (effectCache.ContainsKey(FigureTriggerType.OnDamaged))
        {
            foreach (var cacheItem in effectCache[FigureTriggerType.OnDamaged])
            {
                Debug.Log($"[피격 시 발동] {cacheItem.sourceFigure.itemName} 기믹 발동!");
                ApplyFigureEffects(cacheItem.node.effects, diceManager, shopManager, cacheItem.sourceFigure);
            }
        }
        // 피격 시 배수가 오르는 등 UI 변화가 생길 수 있으므로 갱신
        diceManager.ForceUpdateUI();
    }


    // 다른 스크립트(스테이지 클리어, 스낵 사용)에서 에러가 안 나도록 기존 동기형 이름도 남겨둠
    public void ApplyFigureEffects(List<FigureEffectNode> effects, DiceManager diceManager, ShopManager shopManager, FigureItemSO sourceFigure)
    {
        StartCoroutine(ApplyFigureEffectsCoroutine(effects, 0, diceManager, shopManager, sourceFigure));
    }
}