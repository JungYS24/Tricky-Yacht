using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class DiceManager : MonoBehaviour
{
    [Header("덱 시스템 ")]
    public List<DiceData1> masterDeck = new List<DiceData1>();
    public List<DiceData1> drawPile = new List<DiceData1>();
    private List<DiceData1> discardPile = new List<DiceData1>();

    [Header("프리팹 및 슬롯 설정")]
    public GameObject dicePrefab;
    public Transform keepSlotParent;
    public Transform rollSlotParent;
    private Transform[] keepSlots;
    private Transform[] rollSlots;

    [Header("참조 설정")]
    public UIManager ui;
    public ShopManager shopManager;
    public Enemy enemy;
    public HandVFXManager handVFXManager;

    [Header("게임 데이터")]
    public int enemyMaxHP = 40;
    public int currentStage = 1;
    public int maxPlays = 3;
    private int currentPlayNum;
    public int maxRerolls = 2;
    public int currentRerolls;

    // --- 스낵 시스템용 변수 ---
    private int defaultMaxPlays;
    private int defaultMaxRerolls;
    [HideInInspector] public float snackBonusMult = 0f;
    [HideInInspector] public int snackBonusChips = 0;
    [HideInInspector] public int snackBonusRerolls = 0;
    [HideInInspector] public float snackBonusFigureDropRate = 0f;

    private List<Dice> activeDiceList = new List<Dice>();
    private Dice[] keepSlotOccupants;

    void Awake()
    {
        if (ui == null) ui = FindFirstObjectByType<UIManager>();
        InitializeSlots();
        keepSlotOccupants = new Dice[keepSlots.Length];
        Dice.OnDiceStateChanged += HandleDiceChanged;

        defaultMaxPlays = maxPlays;
        defaultMaxRerolls = maxRerolls;

        if (ui != null)
        {
            ui.goShopButton?.onClick.AddListener(GoToShop);
            ui.nextStageButton?.onClick.AddListener(SkipShopAndNextStage);
        }
    }

    void Start()
    {
        InitializeMasterDeck();
        StartNewStage();
    }

    void OnDestroy() => Dice.OnDiceStateChanged -= HandleDiceChanged;

    void InitializeMasterDeck()
    {
        masterDeck.Clear();
        for (int i = 0; i < 20; i++) masterDeck.Add(new DiceData1());
    }

    public void ApplyRandomCoating(DiceType coatingType, float mult, Color color)
    {
        var nonCoatedDice = masterDeck.Where(d => !d.isCoated).ToList();
        if (nonCoatedDice.Count > 0)
        {
            DiceData1 selected = nonCoatedDice[UnityEngine.Random.Range(0, nonCoatedDice.Count)];
            selected.isCoated = true;
            selected.multiplier = mult;
            selected.diceColor = color;
            selected.type = coatingType;
        }
    }

    void StartNewStage()
    {
        currentPlayNum = 1;
        currentRerolls = 0;
        maxPlays = defaultMaxPlays;
        maxRerolls = defaultMaxRerolls;

        enemy.Initialize(enemyMaxHP);
        drawPile = new List<DiceData1>(masterDeck);
        discardPile.Clear();
        ShufflePile(drawPile);
        StartNewRound();
    }

    void ShufflePile(List<DiceData1> pile)
    {
        for (int i = 0; i < pile.Count; i++)
        {
            int rnd = UnityEngine.Random.Range(i, pile.Count);
            var temp = pile[i];
            pile[i] = pile[rnd];
            pile[rnd] = temp;
        }
    }

    void StartNewRound()
    {
        ui?.HideResult();
        currentRerolls = 0;
        snackBonusMult = 0f;
        snackBonusChips = 0;
        snackBonusRerolls = 0;
        snackBonusFigureDropRate = 0f;

        SpawnDice();
        HandleDiceChanged();
    }

    public void ForceUpdateUI() => HandleDiceChanged();

    void SpawnDice()
    {
        foreach (var d in activeDiceList) if (d != null) Destroy(d.gameObject);
        activeDiceList.Clear();
        Array.Clear(keepSlotOccupants, 0, keepSlotOccupants.Length);

        for (int i = 0; i < rollSlots.Length; i++)
        {
            if (drawPile.Count == 0)
            {
                drawPile = new List<DiceData1>(discardPile);
                discardPile.Clear();
                ShufflePile(drawPile);
                if (drawPile.Count == 0) break;
            }
            DiceData1 drawnData = drawPile[0];
            drawPile.RemoveAt(0);
            discardPile.Add(drawnData);

            GameObject go = Instantiate(dicePrefab, rollSlots[i].position, Quaternion.identity);
            Dice d = go.GetComponent<Dice>();
            d.rollPos = rollSlots[i].position;
            d.SetData(drawnData, UnityEngine.Random.Range(1, 7));
            activeDiceList.Add(d);
        }
    }

    public void OnRollButtonClick()
    {
        if (currentRerolls >= (maxRerolls + snackBonusRerolls) || ShopManager.IsShopOpen) return;
        CameraShake.Instance.Shake(0.1f, 0.1f);
        foreach (var d in activeDiceList.Where(d => d != null && !d.isKept))
            d.PlayRollEffect(UnityEngine.Random.Range(1, 7));
        currentRerolls++;
        StartCoroutine(HandleDiceChangedDelayed());
    }

    public void OnFinishButtonClick()
    {
        if (ShopManager.IsShopOpen || enemy.IsDead) return;
        CameraShake.Instance.Shake(0.2f, 0.15f);

        var keptDice = activeDiceList.Where(d => d != null && d.isKept).ToList();
        int baseSum = keptDice.Sum(d => d.currentValue);

        CalculateHandData(keptDice.Select(d => d.currentValue).ToList(), out float comboMultiplier, out string handName);
        handVFXManager?.PlayHandVFX(handName);

        if (comboMultiplier >= 2.0f) SlowMotion.Instance.PlaySlowMotion(0.2f, 0.2f);

        float finalMultiplier = comboMultiplier + snackBonusMult;
        int currentSimulatedHP = enemy.CurrentHP;
        int darkDamageTotal = 0, iceBonusChips = 0;

        foreach (var d in keptDice)
        {
            if (d.myData.isCoated)
            {
                switch (d.myData.type)
                {
                    case DiceType.Prism: finalMultiplier += (d.myData.multiplier - 1.0f); break;
                    case DiceType.Gold: if (shopManager != null) shopManager.currentGold += d.currentValue; break;
                    case DiceType.Dark:
                        int drop = Mathf.FloorToInt(currentSimulatedHP * 0.1f);
                        darkDamageTotal += drop; currentSimulatedHP -= drop; break;
                    case DiceType.Ice: iceBonusChips += 10; break;
                }
            }
        }

        if (darkDamageTotal > 0) enemy.TakeDamage(darkDamageTotal, null);

        int damage = Mathf.FloorToInt((baseSum + iceBonusChips + snackBonusChips) * finalMultiplier);
        enemy.TakeDamage(damage, () => ProcessStageClear(false));

        StartCoroutine(ProcessTurnResult(handName));
    }

    // --- 스테이지 클리어 공통 시스템 ---
    private void ProcessStageClear(bool fromPeppermint)
    {
        int baseClearReward = 500;
        // 인벤토리 피규어 패시브 전체 적용 및 보너스 골드 합산
        int figureBonusGold = InventoryManager.Instance.ApplyAllFigurePassives(this, shopManager);

        if (shopManager != null)
        {
            shopManager.currentGold += baseClearReward;
            ui?.UpdateGoldUI(shopManager.currentGold);
        }

        string clearMessage = $"스테이지 클리어!\n<size=80%><color=#FFD700>+{baseClearReward} 코인 획득!</color></size>";
        if (figureBonusGold > 0) clearMessage += $"\n<size=60%><color=#FFA500>피규어 보너스 +{figureBonusGold}G</color></size>";

        // 적 프리팹에 설정된 고유 데이터 활용
        if (fromPeppermint)
        {
            if (enemy.dropFigureData != null && InventoryManager.Instance.HasEmptyFigureSlot())
            {
                InventoryManager.Instance.AddItem(enemy.dropFigureData);
                clearMessage += $"\n<size=70%><color=#00FFFF>전리품: {enemy.dropFigureData.itemName} 획득 (페퍼민트)</color></size>";
            }
        }
        else
        {
            float dropChance = enemy.baseDropRate + snackBonusFigureDropRate;
            if (enemy.dropFigureData != null && UnityEngine.Random.value <= dropChance)
            {
                if (InventoryManager.Instance.HasEmptyFigureSlot())
                {
                    InventoryManager.Instance.AddItem(enemy.dropFigureData);
                    clearMessage += $"\n<size=70%><color=#00FFFF>전리품: {enemy.dropFigureData.itemName} 획득!</color></size>";
                }
            }
        }

        ui?.ShowResult("#00FF00", clearMessage);
        Invoke(nameof(PromptShopChoice), 2.0f);
    }

    public void TryPeppermintCapture()
    {
        if (enemy == null || enemy.IsDead) return;
        float hpPercent = (float)enemy.CurrentHP / enemy.MaxHP;
        float t = Mathf.InverseLerp(1.0f, 0.05f, hpPercent);
        float successProbability = Mathf.Lerp(0.03f, 0.30f, t);

        if (UnityEngine.Random.value <= successProbability)
        {
            ui?.ShowResult("#00FF00", "페퍼민트 대성공!\n<size=70%>몬스터가 즉시 박제되었습니다!</size>");
            enemy.TakeDamage(enemy.CurrentHP, () => ProcessStageClear(true));
        }
        else
        {
            ui?.ShowResult("#FF5555", "박제 실패...\n<size=70%>몬스터가 저항했습니다.</size>");
            Invoke(nameof(HideResultAfterFailure), 1.5f);
        }
    }

    private void HideResultAfterFailure() { if (!ShopManager.IsShopOpen && !enemy.IsDead) ui?.HideResult(); }

    private IEnumerator ProcessTurnResult(string handName)
    {
        yield return new WaitForSeconds(0.4f);
        UpdateMainUI(handName);
        if (!enemy.IsDead)
        {
            if (currentPlayNum >= maxPlays)
            {
                ui?.ShowResult("#FF0000", "게임 오버");
                Invoke(nameof(RestartGame), 1.5f);
            }
            else
            {
                currentPlayNum++;
                Invoke(nameof(StartNewRound), 0.5f);
            }
        }
    }

    void InitializeSlots()
    {
        if (keepSlotParent != null) keepSlots = keepSlotParent.Cast<Transform>().ToArray();
        if (rollSlotParent != null) rollSlots = rollSlotParent.Cast<Transform>().ToArray();
    }

    private IEnumerator HandleDiceChangedDelayed()
    {
        yield return new WaitForSeconds(0.5f);
        HandleDiceChanged();
    }

    void HandleDiceChanged()
    {
        int keptCount = 0;
        bool hasDiceToRoll = false;
        foreach (var d in activeDiceList.Where(d => d != null))
        {
            if (d.isKept) { if (d.currentKeepIndex == -1) AssignToKeepSlot(d); keptCount++; }
            else { if (d.currentKeepIndex != -1) ReleaseFromKeepSlot(d); hasDiceToRoll = true; }
        }
        UpdateMainUI("없음");
        ui?.SetRollButtonInteractable((currentRerolls < maxRerolls + snackBonusRerolls) && hasDiceToRoll);
        ui?.SetFinishButtonInteractable(keptCount == keepSlots.Length);
    }

    void UpdateMainUI(string handName)
    {
        var targetDice = activeDiceList.Where(d => d != null).ToList();
        var allValues = targetDice.Select(d => d.currentValue).ToList();
        int baseSum = allValues.Count > 0 ? allValues.Sum() : 0;
        float baseMult = allValues.Count == 5 ? 0 : 1.0f;
        if (allValues.Count == 5) CalculateHandData(allValues, out baseMult, out handName);
        else if (allValues.Count > 0) handName = "계산 중...";

        float finalMult = baseMult + snackBonusMult;
        int darkDamageTotal = 0, iceBonusChips = 0;
        int currentSimulatedHP = (enemy != null) ? enemy.CurrentHP : 0;

        foreach (var d in targetDice)
        {
            if (d.myData.isCoated)
            {
                if (d.myData.type == DiceType.Prism) finalMult += (d.myData.multiplier - 1.0f);
                else if (d.myData.type == DiceType.Dark)
                {
                    int drop = Mathf.FloorToInt(currentSimulatedHP * 0.1f);
                    darkDamageTotal += drop; currentSimulatedHP -= drop;
                }
                else if (d.myData.type == DiceType.Ice) iceBonusChips += 10;
            }
        }

        int finalBaseSum = baseSum + iceBonusChips + snackBonusChips;
        int totalDamage = Mathf.FloorToInt(finalBaseSum * finalMult) + darkDamageTotal;

        string displayHand = $"<color=#FFD700>{handName}</color>";

        if (iceBonusChips > 0)
        {
            displayHand += $" <color=#00FFFF>+{iceBonusChips}</color>";
        }

        if (snackBonusChips > 0) displayHand += $" <color=#FFA500>+{snackBonusChips}(스낵)</color>";

        string formula = $"{finalBaseSum} x {finalMult:F1}배" + (darkDamageTotal > 0 ? $" + {darkDamageTotal}(다크)" : "");
        string combinedText = $"{displayHand}\n{formula}\n<color=#FF5555>= {totalDamage} 대미지 예정</color>";

        int remainingRerolls = (maxRerolls + snackBonusRerolls) - currentRerolls;
        ui?.UpdateGameUI(currentStage, enemy.CurrentHP, enemy.MaxHP, currentPlayNum, maxPlays, remainingRerolls, combinedText);

        // 박제 확률 실시간 UI 갱신 (적의 baseDropRate 참조)
        float currentEnemyDropRate = (enemy != null) ? enemy.baseDropRate : 0f;
        ui?.UpdateDropRateUI(currentEnemyDropRate, snackBonusFigureDropRate);
    }

    void AssignToKeepSlot(Dice d)
    {
        int index = Array.IndexOf(keepSlotOccupants, null);
        if (index != -1) { keepSlotOccupants[index] = d; d.currentKeepIndex = index; d.MoveToTarget(keepSlots[index].position); }
    }

    void ReleaseFromKeepSlot(Dice d)
    {
        if (d.currentKeepIndex != -1) { keepSlotOccupants[d.currentKeepIndex] = null; d.currentKeepIndex = -1; d.MoveToTarget(d.rollPos); }
    }

    void PromptShopChoice() { ui?.HideResult(); ui?.ShowShopChoice(); }
    public void GoToShop() { ui?.HideShopChoice(); shopManager?.OpenShop(); }
    public void SkipShopAndNextStage() { ui?.HideShopChoice(); NextStage(); }
    public void NextStage() { currentStage++; enemyMaxHP += 30; StartNewStage(); }
    void RestartGame() { currentStage = 1; enemyMaxHP = 40; StartNewStage(); }

    void CalculateHandData(List<int> values, out float multiplier, out string handName)
    {
        multiplier = 1.0f; handName = "탑 (High Card)";
        int[] counts = new int[7]; foreach (int v in values) counts[v]++;
        List<int> sortedValues = new List<int>(values); sortedValues.Sort();
        if (counts.Any(c => c == 5)) { multiplier = 2.5f; handName = "파이브 카드"; return; }
        bool isStraight = true;
        for (int i = 0; i < sortedValues.Count - 1; i++) if (sortedValues[i] + 1 != sortedValues[i + 1]) { isStraight = false; break; }
        if (isStraight) { multiplier = 2.0f; handName = "스트레이트"; return; }
        if (counts.Any(c => c == 4)) { multiplier = 1.8f; handName = "포카드"; return; }
        if (counts.Any(c => c == 3) && counts.Any(c => c == 2)) { multiplier = 1.7f; handName = "풀하우스"; return; }
        if (counts.Any(c => c == 3)) { multiplier = 1.5f; handName = "트리플"; return; }
        if (counts.Count(c => c == 2) == 2) { multiplier = 1.4f; handName = "투 페어"; return; }
        if (counts.Any(c => c == 2)) { multiplier = 1.2f; handName = "원 페어"; return; }
    }
}