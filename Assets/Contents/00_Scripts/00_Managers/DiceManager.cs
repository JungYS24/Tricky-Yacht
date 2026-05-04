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

    [Header("페퍼민트 포획 연출")]
    public PeppermintCaptureEffect peppermintCaptureEffect;
    public Transform peppermintCaptureCenter;
    public GameObject peppermintVisualPrefab;

    [Header("맵(생물군계) 설정")]
    public SpriteRenderer biomeBackgroundImage; // Canvas에 있는 Biome_Image 연결
    public List<BiomeDataSO> biomeList;               // 만들어둔 Biome 데이터들 (숲, 화산 등)
    private BiomeDataSO currentBiome;

    // --- 스낵 시스템용 변수 ---
    private int defaultMaxPlays;
    private int defaultMaxRerolls;
    [HideInInspector] public float snackBonusMult = 0f;
    [HideInInspector] public int snackBonusChips = 0;
    [HideInInspector] public int snackBonusRerolls = 0;
    [HideInInspector] public float snackBonusFigureDropRate = 0f;

    //페퍼민트를 먹었는지 체크하는 상태 변수 (스테이지 동안 유지)
    [HideInInspector] public bool isPeppermintActive = false;


    //달마 체리 체크
    [HideInInspector] public int consumedCherryCount = 0;
    //클락판다 스테이지 버프 활성화 여부
    [HideInInspector] public int pandaBonusRerolls = 0;

    private List<Dice> activeDiceList = new List<Dice>();
    private Dice[] keepSlotOccupants;
    private bool pendingPeppermintSuccess = false;
    private bool isRolling = false; // 주사위 굴러가는중 
    private bool isCalculating = false; // 끝내기 버튼

    //족보별 배수
    [Header("족보 배수 설정")]
    public float multHighCard = 1.0f;
    public float multOnePair = 1.2f;
    public float multTwoPair = 1.4f;
    public float multTriple = 1.5f;
    public float multFullHouse = 1.7f;
    public float multFourOfAKind = 1.8f;
    public float multStraight = 2.0f;
    public float multYacht = 2.5f;


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


    public List<DiceData1> GetRandomDiceForCoating(int count)
    {
        return masterDeck.OrderBy(x => UnityEngine.Random.value).Take(count).ToList();
    }


    void StartNewStage()
    {
        currentPlayNum = 1;
        currentRerolls = 0;

        maxPlays = defaultMaxPlays;
        maxRerolls = defaultMaxRerolls;

        pandaBonusRerolls = 0;
        //페퍼민트 효과 초기화
        isPeppermintActive = false;
        pendingPeppermintSuccess = false;
        //가니쉬 효과 초기화
        snackBonusFigureDropRate = 0f;
        //2스테이지마다 바이옴(맵) 변경 로직
        if (biomeList.Count > 0)
        { 
           
            int biomeIndex = ((currentStage - 1) / /*여기 바꾸면 맵 바뀜*/2) % biomeList.Count;
            currentBiome = biomeList[biomeIndex];

            // UI 배경 이미지 교체
            if (biomeBackgroundImage != null && currentBiome.backgroundImage != null)
            {
                biomeBackgroundImage.sprite = currentBiome.backgroundImage;
            }

            //Enemy를 초기화할 때, 현재 맵에 맞는 몬스터 리스트를 같이 넘겨줌!
            enemy.Initialize(currentStage, currentBiome);
        }
        else
        {
            // 혹시 맵 데이터를 안 넣었을 경우를 대비한 안전 장치
            Debug.LogWarning("DiceManager에 Biome List가 비어있습니다!");
            enemy.Initialize(currentStage, null);
        }



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
        isCalculating = false;

        ui?.HideResult();
        currentRerolls = 0;
        snackBonusMult = 0f;
        snackBonusChips = 0;
        snackBonusRerolls = 0;
        //snackBonusFigureDropRate = 0f;

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
            int initialVal = drawnData.faceValues[UnityEngine.Random.Range(0, 6)];
            d.SetData(drawnData, initialVal);
            activeDiceList.Add(d);
        }
    }
    
    public void OnRollButtonClick()
    {
        if (isRolling || currentRerolls >= (maxRerolls + snackBonusRerolls + pandaBonusRerolls) || ShopManager.IsShopOpen) return;

        isRolling = true; // 굴림 상태 켜기
        ui?.SetRollButtonInteractable(false);   //즉시 버튼 비활성화
        ui?.SetFinishButtonInteractable(false); //주사위가 굴러가는 동안 끝내기 버튼도 막기

        //리롤 버튼 클릭 및 주사위 굴러가는 소리

        CameraShake.Instance.Shake(0.1f, 0.1f);

        foreach (var d in activeDiceList.Where(d => d != null && !d.isKept))
        {
            int finalResult = d.myData.faceValues[UnityEngine.Random.Range(0, 6)];
            d.PlayRollEffect(finalResult);
        }

        currentRerolls++;
        StartCoroutine(HandleDiceChangedDelayed());
    }

    public void OnFinishButtonClick()
    {
        if (isRolling || isCalculating || ShopManager.IsShopOpen || enemy.IsDead) return;

        isCalculating = true; //결산 연출 시작
        ui?.SetRollButtonInteractable(false);   //즉시 버튼 비활성화
        ui?.SetFinishButtonInteractable(false); //즉시 버튼 비활성화

        // 사운드 끝내기 버튼 클릭 소리 재생


        CameraShake.Instance.Shake(0.2f, 0.15f);

        var keptDice = activeDiceList.Where(d => d != null && d.isKept).ToList();
        int baseSum = keptDice.Sum(d => d.currentValue);
        CalculateHandData(keptDice.Select(d => d.currentValue).ToList(), out float comboMultiplier, out string handName);

        // 사운드 족보 달성 및 데미지 가하는 소리 재생

        handVFXManager?.PlayHandVFX(handName);

        if (comboMultiplier >= 2.0f)
        {
            SlowMotion.Instance?.PlaySlowMotion(0.2f, 0.2f);
        }

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

        //유니콘: 프리즘 개수 * 0.2배
        int prismCount = keptDice.Count(d => d.myData.isCoated && d.myData.type == DiceType.Prism);
        if (InventoryManager.Instance.HasActiveFigureAbility(FigureAbility.PrismDamageBonus))
        {
            finalMultiplier += (prismCount * 0.2f);
        }

        //달마: 먹은 체리 개수 * 10칩
        if (InventoryManager.Instance.HasActiveFigureAbility(FigureAbility.CherryChipBonus))
        {
            iceBonusChips += (consumedCherryCount * 10);
        }

        // 복고양이: 파이브 카드(요트) 달성 시 10골드 획득
        if (handName == "Yacht" && InventoryManager.Instance.HasActiveFigureAbility(FigureAbility.YachtGoldBonus))
        {
            if (shopManager != null)
            {
                shopManager.currentGold += 10;
                ui?.UpdateGoldUI(shopManager.currentGold);
                Debug.Log("복고양이 발동: 요트 완성! +10 G");
            }
        }

        // 클락판다: 3눈금이 3개 이상일 때 리롤 1회 반환
        int threeFaceCount = keptDice.Count(d => d.currentValue == 3);
        if (threeFaceCount >= 3 && InventoryManager.Instance.HasActiveFigureAbility(FigureAbility.ThreeDiceRerollBonus))
        {
            if (pandaBonusRerolls == 0) // 아직 버프가 활성화되지 않았을 때만 실행
            {
                pandaBonusRerolls = 1;
                Debug.Log("클락판다 효과 발동: 이번 스테이지 동안 리롤 기회가 1회 늘어납니다!");
            }
        }

        if (darkDamageTotal > 0) enemy.TakeDamage(darkDamageTotal, null);

        int damage = Mathf.FloorToInt((baseSum + iceBonusChips + snackBonusChips) * finalMultiplier);

        // 페퍼민트 성공 여부를 먼저 굴림
        pendingPeppermintSuccess = false;

        if (isPeppermintActive)
        {
            float dropChance = enemy.baseDropRate + snackBonusFigureDropRate;

            if (enemy.dropFigureData != null &&
                InventoryManager.Instance.HasEmptyFigureSlot() &&
                UnityEngine.Random.value <= dropChance)
            {
                pendingPeppermintSuccess = true;
            }
        }

        // 성공할 때만 외부 포획 연출 사용
        enemy.useExternalDeathSequence = pendingPeppermintSuccess;
        enemy.TakeDamage(damage, OnEnemyKilled);

        StartCoroutine(ProcessTurnResult(handName));
    }
    //티켓 아이템 먹었을 때 호출할 함수
    public void UpgradeHand(HandType handType, float amount)
    {
        switch (handType)
        {
            case HandType.HighCard: multHighCard *= amount; break;
            case HandType.OnePair: multOnePair *= amount; break;
            case HandType.TwoPair: multTwoPair *= amount; break;
            case HandType.Triple: multTriple *= amount; break;
            case HandType.FullHouse: multFullHouse *= amount; break;
            case HandType.FourOfAKind: multFourOfAKind *= amount; break;
            case HandType.Straight: multStraight *= amount; break;
            case HandType.Yacht: multYacht *= amount; break;
        }
    }

    private void OnEnemyKilled()
    {
        if (pendingPeppermintSuccess &&
            peppermintCaptureEffect != null &&
            peppermintCaptureCenter != null &&
            peppermintVisualPrefab != null &&
            enemy != null)
        {
            StartCoroutine(PlayPeppermintCaptureThenClear());
        }
        else
        {
            ProcessStageClear(false);
        }
    }

    private IEnumerator PlayPeppermintCaptureThenClear()
    {
        yield return StartCoroutine(
            peppermintCaptureEffect.PlayCapture(
                enemy.transform,
                peppermintCaptureCenter.position,
                peppermintVisualPrefab
            )
        );

        ProcessStageClear(false);
    }

    // --- 스테이지 클리어 공통 시스템 ---

    
    private void ProcessStageClear(bool fromPeppermint)
    {

        // 사운드 스테이지 클리어 축하 소리 재생
        int baseClearReward = 500;
        int figureBonusGold = InventoryManager.Instance.ApplyAllFigurePassives(this, shopManager);

        if (shopManager != null)
        {
            shopManager.currentGold += baseClearReward;
            ui?.UpdateGoldUI(shopManager.currentGold);
        }

        string clearMessage = $"스테이지 클리어!\n<size=80%><color=#FFD700>+{baseClearReward} 코인 획득!</color></size>";
        if (figureBonusGold > 0) clearMessage += $"\n<size=60%><color=#FFA500>피규어 보너스 +{figureBonusGold}G</color></size>";
        if (pendingPeppermintSuccess)
        {
            if (enemy.dropFigureData != null)
            {
                InventoryManager.Instance.AddItem(enemy.dropFigureData);
                clearMessage += $"\n<size=70%><color=#00FFFF>전리품: {enemy.dropFigureData.itemName} 박제 성공! (페퍼민트 효과)</color></size>";
            }
        }

        ui?.ShowResult("#00FF00", clearMessage);
        Invoke(nameof(PromptShopChoice), 2.0f);
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
        isRolling = false;

        int keptCount = 0;
        bool hasDiceToRoll = false;
        foreach (var d in activeDiceList.Where(d => d != null))
        {
            if (d.isKept) { if (d.currentKeepIndex == -1) AssignToKeepSlot(d); keptCount++; }
            else { if (d.currentKeepIndex != -1) ReleaseFromKeepSlot(d); hasDiceToRoll = true; }
        }
        UpdateMainUI("없음");
        ui?.SetRollButtonInteractable((currentRerolls < maxRerolls + snackBonusRerolls + pandaBonusRerolls) && hasDiceToRoll);
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

        int prismCount = targetDice.Count(d => d.myData.isCoated && d.myData.type == DiceType.Prism);
        if (InventoryManager.Instance.HasActiveFigureAbility(FigureAbility.PrismDamageBonus))
        {
            finalMult += (prismCount * 0.2f);
        }

        int figureBonusChips = 0;
        if (InventoryManager.Instance.HasActiveFigureAbility(FigureAbility.CherryChipBonus))
        {
            figureBonusChips = consumedCherryCount * 10;
        }

        int finalBaseSum = baseSum + iceBonusChips + snackBonusChips + figureBonusChips;
        int totalDamage = Mathf.FloorToInt(finalBaseSum * finalMult) + darkDamageTotal;

        string displayHand = $"<color=#FFD700>{handName}</color>";

        if (iceBonusChips > 0)
        {
            displayHand += $" <color=#00FFFF>+{iceBonusChips}</color>";
        }

        if (snackBonusChips > 0) displayHand += $" <color=#FFA500>+{snackBonusChips}(스낵)</color>";
        if (figureBonusChips > 0) displayHand += $" <color=#FF69B4>+{figureBonusChips}(달마)</color>";

        string formula = $"{finalBaseSum} x {finalMult:F1}배" + (darkDamageTotal > 0 ? $" + {darkDamageTotal}(다크)" : "");
        string combinedText = $"{displayHand}\n{formula}\n<color=#FF5555>= {totalDamage} 대미지 예정</color>";

        int remainingRerolls = (maxRerolls + snackBonusRerolls + pandaBonusRerolls) - currentRerolls;
        ui?.UpdateGameUI(currentStage, enemy.CurrentHP, enemy.MaxHP, currentPlayNum, maxPlays, remainingRerolls, combinedText);

        float currentEnemyDropRate = isPeppermintActive ? enemy.baseDropRate : 0f;
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
    void RestartGame()
    {
        // 1. 기본 스테이지 데이터 초기화
        currentStage = 1;
        enemyMaxHP = 40;

        // 2. 덱 초기화 (상점에서 샀던 특수 주사위들을 모두 버리고 기본 20개로)
        InitializeMasterDeck();

        // 3. 골드 초기화 (ShopManager 참조)
        if (shopManager != null)
        {
            shopManager.currentGold = 3000; // 초기 소지금 (기획에 맞게 수정하세요)
            ui?.UpdateGoldUI(shopManager.currentGold);
        }

        // 4. 인벤토리 초기화 (방금 만든 함수 호출)
        InventoryManager.Instance?.ClearAllSlots();

        // 5. 스낵 및 특수 상태 버프 초기화
        snackBonusMult = 0f;
        snackBonusChips = 0;
        snackBonusRerolls = 0;
        snackBonusFigureDropRate = 0f;
        isPeppermintActive = false;
        consumedCherryCount = 0;
        Debug.Log("게임이 완전히 초기화되었습니다. 다시 시작합니다.");
        //6. 티켓으로 올렸던 배수를 다시 기본값으로 돌려줌
        multHighCard = 1.0f;
        multOnePair = 1.2f;
        multTwoPair = 1.4f;
        multTriple = 1.5f;
        multFullHouse = 1.7f;
        multFourOfAKind = 1.8f;
        multStraight = 2.0f;
        multYacht = 2.5f;

        //몬스터 초기화
        enemy.ResetMonsterIndex();
        // 6. 새로운 스테이지 시작
        StartNewStage();
    }

    void CalculateHandData(List<int> values, out float multiplier, out string handName)
    {
        // ⬇️ 숫자로 적혀있던 부분을 전부 mult변수로 교체합니다.
        multiplier = multHighCard; handName = "탑 (High Card)";
        int[] counts = new int[7]; foreach (int v in values) counts[v]++;
        List<int> sortedValues = new List<int>(values); sortedValues.Sort();

        if (counts.Any(c => c == 5)) { multiplier = multYacht; handName = "Yacht"; return; }

        bool isStraight = true;
        for (int i = 0; i < sortedValues.Count - 1; i++) if (sortedValues[i] + 1 != sortedValues[i + 1]) { isStraight = false; break; }
        if (isStraight) { multiplier = multStraight; handName = "스트레이트"; return; }

        if (counts.Any(c => c == 4)) { multiplier = multFourOfAKind; handName = "포카드"; return; }
        if (counts.Any(c => c == 3) && counts.Any(c => c == 2)) { multiplier = multFullHouse; handName = "풀하우스"; return; }
        if (counts.Any(c => c == 3)) { multiplier = multTriple; handName = "트리플"; return; }
        if (counts.Count(c => c == 2) == 2) { multiplier = multTwoPair; handName = "투 페어"; return; }
        if (counts.Any(c => c == 2)) { multiplier = multOnePair; handName = "원 페어"; return; }
    }
}