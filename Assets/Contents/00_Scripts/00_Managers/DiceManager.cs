using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class DiceManager : MonoBehaviour
{
    [Header("덱 시스템 ")]
    public List<DiceData1> masterDeck = new List<DiceData1>(); // 스테이지를 넘나드는 영구 20개 덱
    public List<DiceData1> drawPile = new List<DiceData1>();  // 이번 스테이지 뽑기 통
    private List<DiceData1> discardPile = new List<DiceData1>(); // 이번 스테이지 버린 통

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

    [Header("보상 설정")]
    public FigureItemSO dropMonsterFigureData; // 몬스터 처치 시 드랍할 몬스터 피규어

    [Header("게임 데이터")]
    public int enemyMaxHP = 40;
    public int currentStage = 1;
    public int maxPlays = 3;
    private int currentPlayNum;
    public int maxRerolls = 2;
    public int currentRerolls;

    // --- 스낵 시스템용 변수 추가 ---
    private int defaultMaxPlays;
    private int defaultMaxRerolls;
    [HideInInspector] public float snackBonusMult = 0f;    // 체리: 이번 라운드 배수 추가
    [HideInInspector] public int snackBonusChips = 0;      // 팬케이크: 이번 라운드 칩 추가
    [HideInInspector] public int snackBonusRerolls = 0;    // 라임 주스: 이번 라운드 리롤 추가
    [HideInInspector] public float snackBonusFigureDropRate = 0f; // 가니쉬: 몬스터 박제 확률 증가

    private List<Dice> activeDiceList = new List<Dice>();
    private Dice[] keepSlotOccupants;

    void Awake()
    {
        if (ui == null) ui = FindFirstObjectByType<UIManager>();
        InitializeSlots();
        keepSlotOccupants = new Dice[keepSlots.Length];
        Dice.OnDiceStateChanged += HandleDiceChanged;

        // 게임 시작 시 기본 플레이/리롤 횟수 기억
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
        for (int i = 0; i < 20; i++)
        {
            masterDeck.Add(new DiceData1());
        }
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

            Debug.Log($"마스터 덱 주사위 업그레이드! 유형: {coatingType}, 색상: {color}");
        }
    }

    void StartNewStage()
    {
        currentPlayNum = 1;
        currentRerolls = 0;

        // 스테이지가 넘어가면 최대 횟수 스낵 버프(스테이크) 초기화
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

        // 턴(라운드)이 넘어가면 일회성 스낵 버프 초기화
        snackBonusMult = 0f;
        snackBonusChips = 0;
        snackBonusRerolls = 0;
        snackBonusFigureDropRate = 0f; // 가니쉬 효과 초기화

        SpawnDice();
        HandleDiceChanged();
    }

    // 스낵을 클릭했을 때 UI를 즉각적으로 새로고침하는 함수
    public void ForceUpdateUI()
    {
        HandleDiceChanged();
    }

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
        // 남은 굴리기 계산에 라임 주스 버프(snackBonusRerolls) 적용
        if (currentRerolls >= (maxRerolls + snackBonusRerolls) || ShopManager.IsShopOpen) return;

        CameraShake.Instance.Shake(0.1f, 0.1f);

        foreach (var d in activeDiceList.Where(d => d != null && !d.isKept))
        {
            d.PlayRollEffect(UnityEngine.Random.Range(1, 7));
        }
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

        // 좋은 족보일 때 슬로모션
        if (comboMultiplier >= 2.0f)
        {
            SlowMotion.Instance.PlaySlowMotion(0.2f, 0.2f);
            CameraShake.Instance.Shake(0.2f, 0.15f);
        }

        // 체리 버프 적용!
        float finalMultiplier = comboMultiplier + snackBonusMult;

        int goldEarned = 0;
        int currentSimulatedHP = enemy.CurrentHP;
        int darkDamageTotal = 0;
        int iceBonusChips = 0;

        // --- 특수 코팅 효과 계산 ---
        foreach (var d in keptDice)
        {
            if (d.myData.isCoated)
            {
                switch (d.myData.type)
                {
                    case DiceType.Prism:
                        finalMultiplier += (d.myData.multiplier - 1.0f);
                        break;
                    case DiceType.Gold:
                        goldEarned += d.currentValue;
                        break;
                    case DiceType.Dark:
                        int drop = Mathf.FloorToInt(currentSimulatedHP * 0.1f);
                        darkDamageTotal += drop;
                        currentSimulatedHP -= drop;
                        break;
                    case DiceType.Ice:
                        iceBonusChips += 10;
                        break;
                }
            }
        }

        // --- 특수 효과 실제 적용 ---
        if (darkDamageTotal > 0)
        {
            enemy.TakeDamage(darkDamageTotal, null);
            Debug.Log($"[다크 효과] 공격 전 적 체력 {darkDamageTotal} 감소!");
        }

        if (goldEarned > 0 && shopManager != null)
        {
            shopManager.currentGold += goldEarned;
            ui?.UpdateGoldUI(shopManager.currentGold);
            Debug.Log($"[골드 효과] 눈금 합산하여 {goldEarned} 코인 획득!");
        }

        // 데미지 계산 시 팬케이크 버프 적용
        int damage = Mathf.FloorToInt((baseSum + iceBonusChips + snackBonusChips) * finalMultiplier);

        // 적 타격 및 공통 클리어 함수 호출
        enemy.TakeDamage(damage, () => {
            ProcessStageClear(false); // 일반 클리어
        });

        StartCoroutine(ProcessTurnResult(handName));
    }


    //공통 스테이지 클리어 로직
  
    private void ProcessStageClear(bool fromPeppermint)
    {
        int baseClearReward = 500;
        int figureBonusGold = InventoryManager.Instance.GetTotalFigureBonusGold();
        int finalReward = baseClearReward + figureBonusGold;

        // 골드 지급
        if (shopManager != null)
        {
            shopManager.currentGold += finalReward;
            ui?.UpdateGoldUI(shopManager.currentGold);
        }

        // 텍스트 생성
        string clearMessage = $"스테이지 클리어!\n<size=80%><color=#FFD700>+{baseClearReward} 코인 획득!</color></size>";
        if (figureBonusGold > 0)
            clearMessage += $"\n<size=60%><color=#FFA500>피규어 보너스 +{figureBonusGold}G</color></size>";

        // 피규어 드랍 체크 (페퍼민트면 100% 드랍, 아니면 50% + 가니쉬 확률)
        if (fromPeppermint)
        {
            clearMessage += $"\n<size=70%><color=#00FFFF>전리품: 몬스터 피규어 획득 (페퍼민트)</color></size>";
        }
        else
        {
            //박제 확률
            float dropChance = 0.5f + snackBonusFigureDropRate;
            if (dropMonsterFigureData != null && UnityEngine.Random.value <= dropChance)
            {
                if (InventoryManager.Instance.HasEmptyFigureSlot())
                {
                    InventoryManager.Instance.AddItem(dropMonsterFigureData);
                    clearMessage += $"\n<size=70%><color=#00FFFF>전리품: 몬스터 피규어 획득!</color></size>";
                }
            }
        }

        ui?.ShowResult("#00FF00", clearMessage);
        Invoke(nameof(PromptShopChoice), 2.0f);
    }

    public void TryPeppermintCapture()
    {
        if (enemy == null || enemy.IsDead) return;

        // 1. 현재 몬스터의 남은 체력 퍼센트 계산
        float hpPercent = (float)enemy.CurrentHP / enemy.MaxHP;

        // 2. 비례 확률 계산 (최대 30%)
        float t = Mathf.InverseLerp(1.0f, 0.05f, hpPercent);
        float successProbability = Mathf.Lerp(0.03f, 0.30f, t);

        // 3. 주사위 굴리기
        float roll = UnityEngine.Random.value;
        Debug.Log($"[페퍼민트] 적 체력 {hpPercent * 100:F1}% -> 성공 확률: {successProbability * 100:F1}%. 도박 결과: {roll}");

        if (roll <= successProbability)
        {
            // [성공] 페퍼민트 로또 성공!
            ui?.ShowResult("#00FF00", "페퍼민트 대성공!\n<size=70%>몬스터가 즉시 박제되었습니다!</size>");

            // 인벤토리에 피규어 추가
            if (dropMonsterFigureData != null && InventoryManager.Instance.HasEmptyFigureSlot())
            {
                InventoryManager.Instance.AddItem(dropMonsterFigureData);
            }

            // 적 즉사 처리 (잔여 체력만큼 데미지를 입히고 공통 스테이지 클리어 로직 실행)
            enemy.TakeDamage(enemy.CurrentHP, () => ProcessStageClear(true));
        }
        else
        {
            // [실패] 박제 실패
            ui?.ShowResult("#FF5555", "박제 실패...\n<size=70%>몬스터가 저항했습니다. 아무 일도 일어나지 않습니다.</size>");
            Invoke(nameof(HideResultAfterFailure), 1.5f);
        }
    }

    private void HideResultAfterFailure()
    {
        if (!ShopManager.IsShopOpen && !enemy.IsDead)
        {
            ui?.HideResult();
        }
    }

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
            if (d.isKept)
            {
                if (d.currentKeepIndex == -1) AssignToKeepSlot(d);
                keptCount++;
            }
            else
            {
                if (d.currentKeepIndex != -1) ReleaseFromKeepSlot(d);
                hasDiceToRoll = true;
            }
        }

        UpdateMainUI("없음");

        // 리롤 버튼 활성화 조건에 라임 주스 버프 적용
        ui?.SetRollButtonInteractable((currentRerolls < maxRerolls + snackBonusRerolls) && hasDiceToRoll);
        ui?.SetFinishButtonInteractable(keptCount == keepSlots.Length);
    }

    void UpdateMainUI(string handName)
    {
        var targetDice = activeDiceList.Where(d => d != null).ToList();
        var allValues = targetDice.Select(d => d.currentValue).ToList();

        int baseSum = 0;
        float baseMult = 1.0f;

        if (allValues.Count == 5)
        {
            baseSum = allValues.Sum();
            CalculateHandData(allValues, out baseMult, out handName);
        }
        else if (allValues.Count > 0)
        {
            baseSum = allValues.Sum();
            handName = "계산 중...";
        }

        // 체리 버프 적용!
        float finalMult = baseMult + snackBonusMult;
        int darkDamageTotal = 0;
        int currentSimulatedHP = (enemy != null) ? enemy.CurrentHP : 0;
        int iceBonusChips = 0;

        foreach (var d in targetDice)
        {
            if (d.myData.isCoated)
            {
                if (d.myData.type == DiceType.Prism)
                {
                    finalMult += (d.myData.multiplier - 1.0f);
                }
                else if (d.myData.type == DiceType.Dark)
                {
                    int drop = Mathf.FloorToInt(currentSimulatedHP * 0.1f);
                    darkDamageTotal += drop;
                    currentSimulatedHP -= drop;
                }
                else if (d.myData.type == DiceType.Ice)
                {
                    iceBonusChips += 10;
                }
            }
        }

        //기본 눈금 합 + 아이스 보너스 + 팬케이크 스낵 보너스
        int finalBaseSum = baseSum + iceBonusChips + snackBonusChips;

        int expectedDiceDamage = Mathf.FloorToInt(finalBaseSum * finalMult);
        int finalTotalDamage = expectedDiceDamage + darkDamageTotal;

        string displayHandName = $"<color=#FFD700>{handName}</color>";
        if (iceBonusChips > 0)
        {
            displayHandName += $" <color=#00FFFF>+{iceBonusChips}</color>";
        }
        // UI 표기: 팬케이크 칩 추가량을 주황색으로 표시
        if (snackBonusChips > 0)
        {
            displayHandName += $" <color=#FFA500>+{snackBonusChips}(스낵)</color>";
        }

        string formulaString = $"{finalBaseSum} x {finalMult:F1}배";
        if (darkDamageTotal > 0)
        {
            formulaString += $" + {darkDamageTotal}(다크)";
        }

        string finalCombinedText = $"{displayHandName}\n{formulaString}\n<color=#FF5555>= {finalTotalDamage} 대미지 예정</color>";

        int curHP = (enemy != null) ? enemy.CurrentHP : 0;
        int maxHP = (enemy != null) ? enemy.MaxHP : 100;

        //남은 굴리기 표시에 라임 주스 버프 적용
        int remainingRerolls = (maxRerolls + snackBonusRerolls) - currentRerolls;

        ui?.UpdateGameUI(currentStage, curHP, maxHP, currentPlayNum, maxPlays,
                         remainingRerolls, finalCombinedText);

        ui?.UpdateDropRateUI(0.5f, snackBonusFigureDropRate);
    }

    void AssignToKeepSlot(Dice d)
    {
        int index = Array.IndexOf(keepSlotOccupants, null);

        CameraShake.Instance.Shake(0.1f, 0.08f);

        if (index != -1)
        {
            keepSlotOccupants[index] = d;
            d.currentKeepIndex = index;
            d.MoveToTarget(keepSlots[index].position);
        }
    }

    void ReleaseFromKeepSlot(Dice d)
    {
        if (d.currentKeepIndex != -1)
        {
            keepSlotOccupants[d.currentKeepIndex] = null;
            d.currentKeepIndex = -1;
            d.MoveToTarget(d.rollPos);
        }
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
        for (int i = 0; i < sortedValues.Count - 1; i++)
            if (sortedValues[i] + 1 != sortedValues[i + 1]) { isStraight = false; break; }
        if (isStraight) { multiplier = 2.0f; handName = "스트레이트"; return; }
        if (counts.Any(c => c == 4)) { multiplier = 1.8f; handName = "포카드"; return; }
        if (counts.Any(c => c == 3) && counts.Any(c => c == 2)) { multiplier = 1.7f; handName = "풀하우스"; return; }
        if (counts.Any(c => c == 3)) { multiplier = 1.5f; handName = "트리플"; return; }
        if (counts.Count(c => c == 2) == 2) { multiplier = 1.4f; handName = "투 페어"; return; }
        if (counts.Any(c => c == 2)) { multiplier = 1.2f; handName = "원 페어"; return; }
    }
}