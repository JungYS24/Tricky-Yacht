using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.SceneManagement;

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

    [Header("몬스터 소환 설정")]
    public Enemy enemyPrefab;        // 프로젝트 창에 있는 몬스터 프리팹
    public Transform enemySpawnPoint;// 몬스터가 소환될 위치 지정용 빈 오브젝트

    [Header("참조 설정")]
    public UIManager ui;
    public ShopManager shopManager;
    [HideInInspector] public Enemy enemy;
    public HandVFXManager handVFXManager;

    [Header("게임 데이터")]
    public int currentStage = 1;
    public int maxRerolls = 2;
    public int currentRerolls;
    public int playerMaxHP = 100;
    public int currentPlayerHP;

    [Header("페퍼민트 포획 연출")]
    public PeppermintCaptureEffect peppermintCaptureEffect;
    public Transform peppermintCaptureCenter;
    public GameObject peppermintVisualPrefab;

    [Header("광대 이벤트 시스템")]
    public ClownEventPanel clownEventPanel;

    // 전리품 선택 패널 연결
    [Header("전리품 시스템")]
    public LootSelectionPanel lootSelectionPanel;

    [Header("맵(생물군계) 설정")]
    public SpriteRenderer biomeBackgroundImage; // Canvas에 있는 Biome_Image 연결
    public List<BiomeDataSO> biomeList;                // 만들어둔 Biome 데이터들 (숲, 화산 등)
    public BiomeDataSO currentBiome;

    [Header("엔딩 UI 설정")]
    public GameObject gameClearPanel;
    public Button mainFromClearButton;
    public TMPro.TextMeshProUGUI gameClearText;

    public BiomeSelectionPanel biomeSelectionPanel;
    private BiomeNavigator biomeNavigator = new BiomeNavigator();

    // --- 스낵 시스템용 변수 ---
    private int defaultMaxRerolls;
    [HideInInspector] public float snackBonusMult = 0f;
    [HideInInspector] public int snackBonusChips = 0;
    [HideInInspector] public int snackBonusRerolls = 0;
    [HideInInspector] public float snackBonusFigureDropRate = 0f;

    //피규어로 얻은 1회성 리롤 추가 버프
    [HideInInspector] public int figureBonusRerolls = 0;

    //페퍼민트를 먹었는지 체크하는 상태 변수 (스테이지 동안 유지)
    [HideInInspector] public bool isPeppermintActive = false;


    public List<Dice> activeDiceList = new List<Dice>();
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

    // 전역 접근을 위한 싱글톤 인스턴스 선언 (클래스 상단 변수 선언부에 위치)
    public static DiceManager Instance { get; private set; }

    public static event System.Action OnDeckUpdateNeeded;//덱 주사위 실시간 변경 변수


    void Awake()
    {
        // --- [싱글톤 가드 및 인스턴스 할당] ---
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Debug.LogWarning($"[DiceManager] 중복된 매니저가 감지되어 파괴합니다. 오브젝트: {gameObject.name}");
            Destroy(gameObject);
            return;
        }
        if (ui == null) ui = FindFirstObjectByType<UIManager>();

        if (enemyPrefab != null && enemySpawnPoint != null)
        {
            enemy = Instantiate(enemyPrefab, enemySpawnPoint.position, Quaternion.identity);
        }

        InitializeSlots();
        keepSlotOccupants = new Dice[keepSlots.Length];

        // 주사위 상태 변경 이벤트 구독 연동
        Dice.OnDiceStateChanged += HandleDiceChanged;

        defaultMaxRerolls = maxRerolls;

        // Awake() 내부 기존 리스너 할당하는 부분 근처에 추가해 주세요.
        if (mainFromClearButton != null)
            mainFromClearButton.onClick.AddListener(OnGameClearMainButtonClick);

        // UI 버튼 리스너 동적 할당 세팅
        if (ui != null)
        {
            ui.goShopButton?.onClick.AddListener(GoToShop);
            ui.nextStageButton?.onClick.AddListener(SkipShopAndNextStage);
        }
    }
    void Start()
    {
        // 세이브 파일이 있고, 로비에서 이어하기(1)를 눌렀다면 세이브를 불러옴
        if (PlayerPrefs.GetInt("LoadGame", 0) == 1 && PlayerPrefs.HasKey("TrickYacht_Save") && GameSaveManager.Instance != null)
        {
            LoadSavedGame();
        }
        else
        {
            currentPlayerHP = playerMaxHP;
            InitializeMasterDeck();

            // 첫 시작은 무조건 숲(Forest)으로 고정
            currentBiome = biomeList.Find(b => b.biomeType == BiomeType.Forest);
            StartNewStage();
        }
    }

    void OnDestroy() => Dice.OnDiceStateChanged -= HandleDiceChanged;

    void LoadSavedGame()
    {
        SaveData data = GameSaveManager.Instance.LoadSaveData();
        if (data == null) return;

        currentStage = data.currentStage;
        currentPlayerHP = data.currentPlayerHP;

        //세이브에 값이 없으면 기본 100으로, 있으면 세이브된 값으로 덮어씌움
        playerMaxHP = data.playerMaxHP > 0 ? data.playerMaxHP : 100;

        if (shopManager != null)
        {
            shopManager.currentGold = data.currentGold;
            ui?.UpdateGoldUI(shopManager.currentGold);
            if (GoldCounter.Instance != null) GoldCounter.Instance.SetGold(shopManager.currentGold);
        }

        multHighCard = data.multHighCard; multOnePair = data.multOnePair;
        multTwoPair = data.multTwoPair; multTriple = data.multTriple;
        multFullHouse = data.multFullHouse; multFourOfAKind = data.multFourOfAKind;
        multStraight = data.multStraight; multYacht = data.multYacht;

        // 덱 복구 (코팅 정보 복원 포함)
        masterDeck.Clear();
        foreach (var dData in data.deckDiceList)
        {
            DiceData1 newDice = null;

            if (dData.diceName == "기본 주사위")
            {
                newDice = new DiceData1();
                masterDeck.Add(newDice);
            }
            else
            {
                DiceItemSO diceSO = GameSaveManager.Instance.FindItemByName(dData.diceName) as DiceItemSO;
                if (diceSO != null)
                {
                    diceSO.ApplyItemEffect(this);
                    newDice = masterDeck[masterDeck.Count - 1]; // 방금 추가된 주사위를 가져옴
                }
            }

            // 세이브 파일에 있던 코팅 상태를 덮어씌움
            if (newDice != null && dData.isCoated)
            {
                newDice.isCoated = dData.isCoated;
                newDice.type = (DiceType)dData.type;
                newDice.multiplier = dData.multiplier;
                newDice.diceColor = dData.diceColor;
            }
        }
        InventoryManager.Instance.ClearAllSlots();
        foreach (string fName in data.ownedFigureNames)
        {
            var item = GameSaveManager.Instance.FindItemByName(fName);
            if (item != null) InventoryManager.Instance.AddItem(item);
        }
        foreach (string sName in data.ownedSnackNames)
        {
            var item = GameSaveManager.Instance.FindItemByName(sName);
            if (item != null) InventoryManager.Instance.AddItem(item);
        }
        foreach (string tName in data.ownedTicketNames)
        {
            var item = GameSaveManager.Instance.FindItemByName(tName);
            if (item != null) InventoryManager.Instance.AddItem(item);
        }

        //환경(바이옴, BGM) 복구
        currentRerolls = 0;
        maxRerolls = defaultMaxRerolls;
        pendingPeppermintSuccess = false;
        //무조건 0으로 끄는 대신, 저장된 버프 수치를 그대로 가져옵니다!
        snackBonusMult = data.snackBonusMult;
        snackBonusChips = data.snackBonusChips;
        snackBonusRerolls = data.snackBonusRerolls;
        snackBonusFigureDropRate = data.snackBonusFigureDropRate;
        figureBonusRerolls = data.figureBonusRerolls;
        isPeppermintActive = data.isPeppermintActive;

        if (biomeList.Count > 0)
        {
            currentBiome = biomeList.Find(b => (int)b.biomeType == data.savedBiomeType);

            if (currentBiome == null) currentBiome = biomeList[0];

            if (biomeBackgroundImage != null && currentBiome.backgroundImage != null)
                biomeBackgroundImage.sprite = currentBiome.backgroundImage;
            if (BGMManager.Instance != null && currentBiome.biomeBGM != null)
                BGMManager.Instance.ChangeBGM(currentBiome.biomeBGM);
        }

        // 싸우던 몬스터 복구
        if (!string.IsNullOrEmpty(data.savedMonsterName))
        {
            MonsterDataSO savedMonster = GetMonsterDataByName(data.savedMonsterName);
            if (savedMonster != null)
            {
                enemy.RestoreMonster(savedMonster, data.savedMonsterHP, data.savedMonsterMaxHP, data.savedMonsterAttack, data.savedMonsterIndex);
            }
            else enemy.Initialize(currentStage, currentBiome); // 에러 방지용 안전장치
        }
        else
        {
            enemy.Initialize(currentStage, currentBiome);
        }

        // 덱 섞기 및 이번 턴 시작 (StartNewStage() 대신 호출)
        drawPile = new List<DiceData1>(masterDeck);
        discardPile.Clear();
        ShufflePile(drawPile);
        StartNewRound();
    }

    // 저장된 몬스터 이름으로 바이옴 리스트를 뒤져서 진짜 데이터를 찾아주는 탐지기 함수
    private MonsterDataSO GetMonsterDataByName(string mName)
    {
        foreach (var biome in biomeList)
        {
            if (biome.bossMonster != null && biome.bossMonster.monsterName == mName)
                return biome.bossMonster;
            foreach (var monster in biome.biomeMonsters)
            {
                if (monster != null && monster.monsterName == mName) return monster;
            }
        }
        return null;
    }

    void InitializeMasterDeck()
    {
        masterDeck.Clear();
        for (int i = 0; i < 12; i++) masterDeck.Add(new DiceData1()); // 주사위 12개로 수정
    }

    public List<DiceData1> GetRandomDiceForCoating(int count)
    {
        return masterDeck.OrderBy(x => UnityEngine.Random.value).Take(count).ToList();
    }


    void StartNewStage()
    {
        currentRerolls = 0;
        maxRerolls = defaultMaxRerolls;
        //페퍼민트 효과 초기화
        isPeppermintActive = false;
        pendingPeppermintSuccess = false;
        //가니쉬 효과 초기화
        snackBonusFigureDropRate = 0f;


       

        if (currentBiome != null)
        {
            if (biomeBackgroundImage != null && currentBiome.backgroundImage != null)
            {
                biomeBackgroundImage.sprite = currentBiome.backgroundImage;
            }
            if (BGMManager.Instance != null && currentBiome.biomeBGM != null)
            {
                BGMManager.Instance.ChangeBGM(currentBiome.biomeBGM);
            }

            enemy.Initialize(currentStage, currentBiome);
        }
        else
        {
            Debug.LogWarning("현재 설정된 바이옴이 없습니다!");
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

        SpawnDice();
        HandleDiceChanged();
    }

    public void ForceUpdateUI() => HandleDiceChanged();

    void SpawnDice()
    {
        foreach (var d in activeDiceList) if (d != null) Destroy(d.gameObject);
        activeDiceList.Clear();
        Array.Clear(keepSlotOccupants, 0, keepSlotOccupants.Length);

        // 덱에 주사위가 5개 미만으로 남았고, 버린 주사위가 있다면 다시 덱에 섞어 넣음
        if (drawPile.Count < 5 && discardPile.Count > 0)
        {
            drawPile.AddRange(discardPile);
            discardPile.Clear();
            ShufflePile(drawPile);
        }

        if (TutorialManager.Instance != null && TutorialManager.Instance.isTutorialActive)
        {
            // 2스테이지 첫 진입 시 하이롤러 주사위 확정 스폰!
            // 버튼 실행 순서에 상관없이 currentStage가 2가 되면 처음부터 무조건 발동!
            if (currentStage == 2 && drawPile.Count >= 5)
            {
                string hrName = TutorialManager.Instance.tutorialHighRollerDice.itemName;
                int hrIdx = drawPile.FindIndex(d => d.diceName == hrName);
                if (hrIdx != -1)
                {
                    var temp = drawPile[0];
                    drawPile[0] = drawPile[hrIdx];
                    drawPile[hrIdx] = temp;
                }
            }
            // 3스테이지 보스 반격 후 체력 회복 튜토리얼 (하트 주사위 확정)
            else if (currentStage == 3 && TutorialManager.Instance.currentStepIndex >= 28 && TutorialManager.Instance.currentStepIndex <= 30)
            {
                int heartIdx = drawPile.FindIndex(d => d.specialEffect == SpecialDieEffect.Heart);
                if (heartIdx == -1)
                {
                    int discardIdx = discardPile.FindIndex(d => d.specialEffect == SpecialDieEffect.Heart);
                    if (discardIdx != -1)
                    {
                        drawPile.Insert(0, discardPile[discardIdx]);
                        discardPile.RemoveAt(discardIdx);
                    }
                }
                else
                {
                    var temp = drawPile[0];
                    drawPile[0] = drawPile[heartIdx];
                    drawPile[heartIdx] = temp;
                }
            }
            // 3스테이지 보스전 첫 번째 턴 (코팅 주사위 확정)
            else if (currentStage == 3 && TutorialManager.Instance.currentStepIndex >= 24 && TutorialManager.Instance.currentStepIndex <= 27)
            {
                int coatedIdx = drawPile.FindIndex(d => d.isCoated);
                if (coatedIdx != -1)
                {
                    var temp = drawPile[0];
                    drawPile[0] = drawPile[coatedIdx];
                    drawPile[coatedIdx] = temp;
                }
            }
        }

        for (int i = 0; i < rollSlots.Length; i++)
        {
            if (drawPile.Count == 0)
            {
                if (discardPile.Count > 0)
                {
                    drawPile = new List<DiceData1>(discardPile);
                    discardPile.Clear();
                    ShufflePile(drawPile);
                }
                if (drawPile.Count == 0) break;
            }

            DiceData1 drawnData = drawPile[0];
            drawPile.RemoveAt(0);
            discardPile.Add(drawnData);

            GameObject go = Instantiate(dicePrefab, rollSlots[i].position, Quaternion.identity);
            Dice d = go.GetComponent<Dice>();
            d.rollPos = rollSlots[i].position;
            int initialVal = drawnData.faceValues[UnityEngine.Random.Range(0, 6)];

            //튜토리얼 추가
            if (TutorialManager.Instance != null && TutorialManager.Instance.isTutorialActive)
            {
                int forcedVal = TutorialManager.Instance.GetForcedDiceValue(i);
                if (forcedVal != -1) initialVal = forcedVal;
            }

            d.SetData(drawnData, initialVal);
            activeDiceList.Add(d);
        }
    }

    public void OnRollButtonClick()
    {
        if (isRolling || currentRerolls >= (maxRerolls + snackBonusRerolls + figureBonusRerolls) || ShopManager.IsShopOpen || FigureDetailPanel.IsPanelOpen || LootSelectionPanel.IsPanelOpen) return; 

        isRolling = true; // 굴림 상태 켜기
        ui?.SetRollButtonInteractable(false);   //즉시 버튼 비활성화
        ui?.SetFinishButtonInteractable(false); //주사위가 굴러가는 동안 끝내기 버튼도 막기

        //리롤 버튼 클릭 및 주사위 굴러가는 소리

        CameraShake.Instance.Shake(0.1f, 0.1f);

        foreach (var d in activeDiceList.Where(d => d != null && !d.isKept))
        {
            int finalResult = d.myData.faceValues[UnityEngine.Random.Range(0, 6)];
            //튜토리얼 추가
            if (TutorialManager.Instance != null && TutorialManager.Instance.isTutorialActive)
            {
                int diceIndex = activeDiceList.IndexOf(d);
                int forcedVal = TutorialManager.Instance.GetForcedDiceValue(diceIndex);
                if (forcedVal != -1) finalResult = forcedVal;
            }

            d.PlayRollEffect(finalResult);
        }

        currentRerolls++;

        StartCoroutine(HandleDiceChangedDelayed());
    }



    public void OnFinishButtonClick()
    {
        if (isRolling || isCalculating || ShopManager.IsShopOpen || FigureDetailPanel.IsPanelOpen || LootSelectionPanel.IsPanelOpen || enemy.IsDead) return;

        figureBonusRerolls = 0;

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

        List<int> finalDiceValues = keptDice.Select(d => d.currentValue).ToList();
        InventoryManager.Instance.EvaluateTurnEndTriggers(finalDiceValues, handName, this, shopManager);

        float finalMultiplier = comboMultiplier + snackBonusMult;
        int currentSimulatedHP = enemy.CurrentHP;
        int darkDamageTotal = 0, iceBonusChips = 0;

        foreach (var d in keptDice)
        {
            switch (d.myData.specialEffect)
            {
                case SpecialDieEffect.Coin:
                    if (shopManager != null)
                    {
                        shopManager.currentGold += d.currentValue;
                        ui?.UpdateGoldUI(shopManager.currentGold);
                        if (GoldCounter.Instance != null) GoldCounter.Instance.SetGold(shopManager.currentGold);
                    }
                    break;
                case SpecialDieEffect.Heart:
                    currentPlayerHP += d.currentValue;
                    if (currentPlayerHP > playerMaxHP) currentPlayerHP = playerMaxHP;
                    break;
            }

            if (d.myData.isCoated)
            {
                switch (d.myData.type)
                {
                    case DiceType.Prism: finalMultiplier += (d.myData.multiplier - 1.0f); break;
                    case DiceType.Gold:
                        if (shopManager != null)
                        {
                            shopManager.currentGold += d.currentValue;
                            // 골드 주사위 정산 즉시 카운팅 연출 실행
                            if (GoldCounter.Instance != null) GoldCounter.Instance.SetGold(shopManager.currentGold);
                        }
                        break;
                    case DiceType.Dark:
                        int drop = Mathf.FloorToInt(currentSimulatedHP * 0.1f);
                        darkDamageTotal += drop; currentSimulatedHP -= drop; break;
                    case DiceType.Ice: iceBonusChips += 10; break;
                }
            }
        }


        if (darkDamageTotal > 0) enemy.TakeDamage(darkDamageTotal, null);

        int damage = Mathf.FloorToInt((baseSum + iceBonusChips + snackBonusChips) * finalMultiplier);

        // 페퍼민트 성공 여부를 먼저 굴림
        pendingPeppermintSuccess = false;

        if (isPeppermintActive)
        {
            float dropChance = enemy.baseDropRate + snackBonusFigureDropRate;

            // 중복 획득 방지 조건 추가
            bool canCapture = enemy.dropFigureData != null && !InventoryManager.Instance.ownedFigures.Contains(enemy.dropFigureData);

            if (canCapture && UnityEngine.Random.value <= dropChance)
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
        // 공허 바이옴에서 보스를 잡았다면 최종 게임 클리어 처리
        if (currentBiome != null && currentBiome.biomeType == BiomeType.Void)
        {
            ShowGameClear();
            return;
        }

        int baseClearReward = 500;
        if (shopManager != null)
        {
            shopManager.currentGold += baseClearReward;
            ui?.UpdateGoldUI(shopManager.currentGold);
            //스테이지 클리어 기본 골드 카운팅 연출 실행

            if (GoldCounter.Instance != null) GoldCounter.Instance.SetGold(shopManager.currentGold);
        }

        if (pendingPeppermintSuccess)
        {
            if (enemy.dropFigureData != null)
            {
                InventoryManager.Instance.AddItem(enemy.dropFigureData);
            }
        }

        Invoke(nameof(ShowClownEvent), 1.0f);
    }

    public void ShowClownEvent()
    {

        // 튜토리얼 중에는 광대 이벤트를 아예 스킵하고 무조건 전리품으로 감.
        if (TutorialManager.Instance != null && TutorialManager.Instance.isTutorialActive)
        {
            if (currentStage == 3) return; // 3스테이지(보스전)는 아무것도 안 띄우고 종료
            ShowLootSelection();
            return;
        }

        // 10스테이지마다(보스 클리어 시) 광대 이벤트 등장!
        if (currentStage % 30 == 0 && clownEventPanel != null)
        {
            clownEventPanel.StartEvent();
        }
        else
        {
            // 그 외의 일반 스테이지(1~9 등)는 광대 없이 바로 전리품 선택으로 넘어갑니다.
            ShowLootSelection();
        }
    }

    private void ShowGameClear()
    {
        if (gameClearPanel != null)
        {
            gameClearPanel.SetActive(true);
        }

        if (gameClearText != null)
        {
            gameClearText.text = "<color=#00FF00>GAME CLEAR!</color>\n\n축하합니다!\n모든 시련을 이겨내고 공허를 정복했습니다!";
        }

        // 게임을 완전히 클리어했으므로 기존 세이브 데이터는 초기화(삭제)
        if (GameSaveManager.Instance != null)
        {
            GameSaveManager.Instance.DeleteSave();
        }
    }

    private void OnGameClearMainButtonClick()
    {
        Time.timeScale = 1f;
        UnityEngine.SceneManagement.SceneManager.LoadScene("Lobby");
    }

    public void ShowLootSelection()
    {
        if (lootSelectionPanel != null)
        {
            lootSelectionPanel.OpenSelection(this);
        }
        else
        {
            PromptShopChoice();
        }
    }

    private void HideResultAfterFailure() { if (!ShopManager.IsShopOpen && !enemy.IsDead) ui?.HideResult(); }

    private IEnumerator ProcessTurnResult(string handName)
    {
        yield return new WaitForSeconds(0.4f);
        UpdateMainUI(handName);

        if (!enemy.IsDead)
        {
            yield return new WaitForSeconds(0.6f);

            enemy.PlayAttackAnim();
            yield return new WaitForSeconds(0.2f);

            currentPlayerHP -= enemy.AttackPower;
            CameraShake.Instance.Shake(0.15f, 0.1f);
            UpdateMainUI("적 공격!");

            if (currentPlayerHP <= 0)
            {
                //게임 오버가 되면 기존 세이브 파일을 지워버림
                if (GameSaveManager.Instance != null) GameSaveManager.Instance.DeleteSave();

                ui?.ShowResult("#FF0000", "게임 오버");
                Invoke(nameof(RestartGame), 1.5f);
            }
            else
            {
                if (GameSaveManager.Instance != null)
                {
                    GameSaveManager.Instance.SaveGame(this, InventoryManager.Instance, shopManager);
                }

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
        ui?.SetRollButtonInteractable((currentRerolls < maxRerolls + snackBonusRerolls + figureBonusRerolls) && hasDiceToRoll);
        ui?.SetFinishButtonInteractable(keptCount == keepSlots.Length);

        OnDeckUpdateNeeded?.Invoke();
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
        int expectedGold = 0, expectedHeal = 0;
        int currentSimulatedHP = (enemy != null) ? enemy.CurrentHP : 0;

        foreach (var d in targetDice)
        {
            switch (d.myData.specialEffect)
            {
                case SpecialDieEffect.Coin:
                    expectedGold += d.currentValue;
                    break;
                case SpecialDieEffect.Heart:
                    expectedHeal += d.currentValue;
                    break;
            }

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
        //피규어 발동 실시간 시뮬레이션
        int figureBonusChips = 0;
        float figureBonusMult = 0f;
        List<string> activeFigureNames = new List<string>();

        if (allValues.Count == 5) // 5개가 모였을 때만 피규어 발동 검사
        {
            int[] diceCounts = new int[7];
            foreach (int v in allValues) diceCounts[v]++;

            foreach (var figure in InventoryManager.Instance.ownedFigures)
            {
                bool isTriggered = false;
                float tempChips = 0;
                float tempMult = 0;

                foreach (var node in figure.figureNodes)
                {
                    bool nodeTriggered = false;
                    switch (node.triggerType)
                    {
                        case FigureTriggerType.ThreeOf1: if (diceCounts[1] >= 3) nodeTriggered = true; break;
                        case FigureTriggerType.ThreeOf2: if (diceCounts[2] >= 3) nodeTriggered = true; break;
                        case FigureTriggerType.ThreeOf3: if (diceCounts[3] >= 3) nodeTriggered = true; break;
                        case FigureTriggerType.ThreeOf4: if (diceCounts[4] >= 3) nodeTriggered = true; break;
                        case FigureTriggerType.ThreeOf5: if (diceCounts[5] >= 3) nodeTriggered = true; break;
                        case FigureTriggerType.ThreeOf6: if (diceCounts[6] >= 3) nodeTriggered = true; break;
                        case FigureTriggerType.OnePair: if (handName == "원 페어") nodeTriggered = true; break;
                        case FigureTriggerType.TwoPair: if (handName == "투 페어") nodeTriggered = true; break;
                        case FigureTriggerType.Triple: if (handName == "트리플") nodeTriggered = true; break;
                        case FigureTriggerType.Straight: if (handName == "스트레이트") nodeTriggered = true; break;
                        case FigureTriggerType.FullHouse: if (handName == "풀하우스") nodeTriggered = true; break;
                        case FigureTriggerType.FourOfAKind: if (handName == "포카드") nodeTriggered = true; break;
                        case FigureTriggerType.Yacht: if (handName == "Yacht" || handName == "요트" || handName == "파이브 카드") nodeTriggered = true; break;
                        case FigureTriggerType.Passive: nodeTriggered = true; break;
                    }

                    if (nodeTriggered)
                    {
                        isTriggered = true;
                        foreach (var effect in node.effects)
                        {
                            if (effect.effectType == FigureEffectType.AddChips) tempChips += effect.effectValue;
                            if (effect.effectType == FigureEffectType.AddMultiplier) tempMult += effect.effectValue;
                        }
                    }
                }

                if (isTriggered)
                {
                    if (!activeFigureNames.Contains(figure.itemName))
                        activeFigureNames.Add(figure.itemName);
                    figureBonusChips += (int)tempChips;
                    figureBonusMult += tempMult;
                }
            }
        }




        // 텍스트 포매팅 (파란색 색상 적용)
        // ==========================================
        int finalBaseSum = baseSum + iceBonusChips + snackBonusChips + figureBonusChips;
        float totalFinalMult = finalMult + figureBonusMult;
        int totalDamage = Mathf.FloorToInt(finalBaseSum * totalFinalMult) + darkDamageTotal;

        string displayHand = $"<color=#FFD700>{handName}</color>";
        if (iceBonusChips > 0) displayHand += $" <color=#00FFFF>+{iceBonusChips}</color>";
        if (snackBonusChips > 0) displayHand += $" <color=#FFA500>+{snackBonusChips}(스낵)</color>";
        if (expectedGold > 0) displayHand += $" <color=#FFFF00>+{expectedGold}(코인)</color>";
        if (expectedHeal > 0) displayHand += $" <color=#FF5555>+{expectedHeal}(회복)</color>";

        // 피규어로 칩이나 배수가 올랐다면 파란색(#00BFFF)으로, 아니면 기본색 유지
        string chipsText = figureBonusChips > 0 ? $"<color=#00BFFF>{finalBaseSum}</color>" : finalBaseSum.ToString();
        string multText = figureBonusMult > 0f ? $"<color=#00BFFF>{totalFinalMult:F1}배</color>" : $"{totalFinalMult:F1}배";

        string formula = $"{chipsText} x {multText}" + (darkDamageTotal > 0 ? $" + {darkDamageTotal}(다크)" : "");
        string combinedText = $"{displayHand}\n{formula}\n<color=#FF5555>= {totalDamage} 대미지 예정</color>";

        // 피규어 발동 명단 텍스트 조합
        string activeFigStr = activeFigureNames.Count > 0 ? $"<color=#00BFFF> 발동 : {string.Join(", ", activeFigureNames)}</color>" : "";

        int remainingRerolls = (maxRerolls + snackBonusRerolls + figureBonusRerolls) - currentRerolls;
        string bName = (currentBiome != null) ? currentBiome.biomeName : "Stage";
        int localStage = ((currentStage - 1) % 3) + 1;
        string stageDisplayName = $"{bName} {localStage}";

        // UI 업데이트 호출 (새로 만든 피규어 텍스트 파라미터 전달)
        ui?.UpdateGameUI(stageDisplayName, enemy.CurrentHP, enemy.MaxHP, currentPlayerHP, playerMaxHP, remainingRerolls, combinedText, activeFigStr);

        float currentEnemyDropRate = isPeppermintActive ? enemy.baseDropRate : 0f;
        if (TutorialManager.Instance != null && TutorialManager.Instance.isTutorialActive)
        {
            if (currentStage == 3 && isPeppermintActive) currentEnemyDropRate = 0.3f;
        }

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

    public void PromptShopChoice() { ui?.HideResult(); ui?.ShowShopChoice(); }
    public void GoToShop() { ui?.HideShopChoice(); shopManager?.OpenShop(); }
    public void SkipShopAndNextStage() { ui?.HideShopChoice(); NextStage(); }
    public void NextStage()
    {
        currentStage++;

        // 방금 클리어한 곳이 5스테이지 단위(보스)였다면, 다음 스테이지 시작 전 바이옴 선택창 띄우기
        if ((currentStage - 1) %3 == 0 && currentStage <= 90)
        {
            ui?.HideShopChoice();

            List<BiomeType> nextOptions = biomeNavigator.GetNextBiomeOptions(currentBiome.biomeType, currentStage - 1);
            biomeSelectionPanel.OpenPanel(this, nextOptions);
        }

        else
        {
            // 일반 스테이지는 그대로 진행
            StartNewStage();
            if (GameSaveManager.Instance != null)
            {
                GameSaveManager.Instance.SaveGame(this, InventoryManager.Instance, shopManager);
            }
        }
    }

    // 바이옴 선택 버튼을 눌렀을 때 실행될 함수
    public void ApplySelectedBiome(BiomeType selectedType)
    {
        if (biomeSelectionPanel != null) biomeSelectionPanel.ClosePanel();

        currentBiome = biomeList.Find(b => b.biomeType == selectedType);
        StartNewStage();

        if (GameSaveManager.Instance != null)
        {
            GameSaveManager.Instance.SaveGame(this, InventoryManager.Instance, shopManager);
        }
    }


    public void GoToMainMenu()
    {
        if (GameSaveManager.Instance != null)
        {
            GameSaveManager.Instance.SaveGame(this, InventoryManager.Instance, shopManager);
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene("Lobby");
    }

    void RestartGame()
    {
        //기본 스테이지 데이터 초기화
        currentStage = 1;
        currentPlayerHP = playerMaxHP;

        //덱 초기화 (상점에서 샀던 특수 주사위들을 모두 버리고 기본 20개로)
        InitializeMasterDeck();

        //골드 초기화 (ShopManager 참조)
        if (shopManager != null)
        {
            shopManager.currentGold = 2000; // 초기 소지금 (기획에 맞게 수정하세요)
            ui?.UpdateGoldUI(shopManager.currentGold);
            // 재시작 및 메인 이동 시 초기 소지금 카운팅 연출 실행 (또는 초기화용)
            if (GoldCounter.Instance != null) GoldCounter.Instance.SetGold(shopManager.currentGold);
        }

        //인벤토리 초기화 (방금 만든 함수 호출)
        InventoryManager.Instance?.ClearAllSlots();

        //스낵 및 특수 상태 버프 초기화
        snackBonusMult = 0f;
        snackBonusChips = 0;
        snackBonusRerolls = 0;
        snackBonusFigureDropRate = 0f;
        isPeppermintActive = false;
        Debug.Log("게임이 완전히 초기화되었습니다. 다시 시작합니다.");
        //티켓으로 올렸던 배수를 다시 기본값으로 돌려줌
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

        // 바이옴을 첫 번째 맵(숲)으로 강제 초기화
        if (biomeList != null && biomeList.Count > 0)
        {
            currentBiome = biomeList.Find(b => b.biomeType == BiomeType.Forest);
        }

        // 새로운 스테이지 시작
        StartNewStage();
    }

    void CalculateHandData(List<int> values, out float multiplier, out string handName)
    {
        //숫자로 적혀있던 부분을 전부 mult변수로 교체합니다.
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