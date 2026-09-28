using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.SceneManagement;
using DG.Tweening;

//생명주기 관리용 순수 데이터 클래스
public class StageContext
{
    public float stageBonusMult = 0f;
    public int stageBonusChips = 0;
    public float combatWinGoldMultiplier = 1.0f;
    public int extraAttackCount = 0;
    public bool isEnemySkillNullified = false;
    public bool isNextEnemyAttackFixedToOne = false;
    public int figureBonusFlameDamage = 0;
    public int accumulatedFlameDamage = 0;
    public bool isPeppermintActive = false;
    public float snackBonusFigureDropRate = 0f;
    public bool firstNormalAttackDone = false;
    // 현재 스테이지에서 사용한 1회 제한 노드
    public HashSet<string> usedFigureNodes = new HashSet<string>();

    public void ResetForNewStage()
    {
        stageBonusMult = 0f;
        stageBonusChips = 0;
        combatWinGoldMultiplier = 1.0f;
        extraAttackCount = 0;
        isEnemySkillNullified = false;
        isNextEnemyAttackFixedToOne = false;
        figureBonusFlameDamage = 0;
        accumulatedFlameDamage = 0;
        isPeppermintActive = false;
        snackBonusFigureDropRate = 0f;
        usedFigureNodes.Clear();
        firstNormalAttackDone = false;
    }
}

public class TurnContext
{
    public float snackBonusMult = 0f;
    public int snackBonusChips = 0;
    public int snackBonusRerolls = 0;
    public int figureBonusRerolls = 0;

    public void ResetForNewTurn()
    {
        snackBonusMult = 0f;
        snackBonusChips = 0;
        snackBonusRerolls = 0;
        // figureBonusRerolls는 결산 버튼 클릭 시 수동 리셋됨
    }
}

public class DiceManager : MonoBehaviour
{
    [Header("덱 시스템 ")]
    [Header("프리팹 및 슬롯 설정")]
    public GameObject dicePrefab;
    public Transform keepSlotParent;
    public Transform rollSlotParent;

    [Header("몬스터 소환 설정")]
    public Enemy enemyPrefab;        // 프로젝트 창에 있는 몬스터 프리팹
    public Transform enemySpawnPoint;// 몬스터가 소환될 위치 지정용 빈 오브젝트

    [Header("참조 설정")]
    public UIManager ui;
    public ShopManager shopManager;
    [HideInInspector] public Enemy enemy;
    public HandVFXManager handVFXManager;

    [Header("게임 데이터")]
    public int maxRerolls = 2;
    public int currentRerolls;
    [HideInInspector] public string currentHandName = ""; // 전투 결산 시 화면에 보이는 족보 이름
    [HideInInspector] public HandRank currentHandRank = HandRank.HighCard;
    [HideInInspector] public int flameDamageThisTurn = 0; // 이번 턴 화상 데미지

    [Header("페퍼민트 포획 연출")]
    public PeppermintCaptureEffect peppermintCaptureEffect;
    public Transform peppermintCaptureCenter;
    public GameObject peppermintVisualPrefab;

    // 전리품 선택 패널 연결
    [Header("전리품 시스템")]
    public LootSelectionPanel lootSelectionPanel;

    [Header("맵(생물군계) 설정")]
    public SpriteRenderer biomeBackgroundImage; // Canvas에 있는 Biome_Image 연결
    public List<BiomeDataSO> biomeList;                // 만들어둔 Biome 데이터들 (숲, 화산 등)

    [Header("엔딩 UI 설정")]
    public GameObject gameClearPanel;
    public Button mainFromClearButton;
    public TMPro.TextMeshProUGUI gameClearText;

    public BiomeSelectionPanel biomeSelectionPanel;
    private BiomeNavigator biomeNavigator = new BiomeNavigator();

    [Header("사운드 설정")]
    public AudioSource sfxSource;
    public AudioEvent playerHurtAudioEvent;

    [Header("게임 오버 UI 설정")]
    public GameOverPanelController gameOverPanel;

    [Header("보스전 가짜 주사위")]
    public Sprite fakeDiceShell; // 가짜 주사위 외곽선 이미지 (인스펙터에서 할당)
    public Sprite fakeDiceFace;  // 가짜 주사위 눈금 이미지 (X 표시 등)

    [Header("조우자 이벤트 시스템")]
    public EncounterEventPanel encounterEventPanel;

    [Header("조우자 특수 효과 상태 (임시 저장용)")]
    [HideInInspector] public bool isNextEnemyHPBoosted = false; // 눈먼 점술가 패널티
    [HideInInspector] public bool isNextCombatHPTiedToOne = false; // 숙원의 방랑자 패널티
    [HideInInspector] public int extraShopSlots = 0; //  슬롯 영구 확장
    [HideInInspector] public int perfumerWeaknessTurns = 0; // 조향사 시련 턴 수
    [HideInInspector] public bool isNextShopFree = false; //녹슨 닻의 선장 무료 상점

    [HideInInspector] public DiceData1 originalBossDice = null;
    [HideInInspector] public int fakeDiceIndex = -1;

    [HideInInspector] public bool isStageClearing = false; // 중복 클리어 방지용 스위치

    //컨텍스트 객체 생성 및 프로퍼티 위임
    public StageContext stageContext = new StageContext();
    public TurnContext turnContext = new TurnContext();

    //스테이지 및 바이옴 진행 전담 객체
    public StageManager stageProgression = new StageManager();
    // 플레이어 상태 전담 객체 
    public PlayerStatus playerStatus = new PlayerStatus();

    // 덱 전담 객체
    public DeckManager deckManager = new DeckManager();

    //stage 전담 객체
    [HideInInspector] public int currentStage { get => stageProgression.currentStage; set => stageProgression.currentStage = value; }
    [HideInInspector] public BiomeDataSO currentBiome { get => stageProgression.currentBiome; set => stageProgression.currentBiome = value; }
    // 튜토리얼 매니저와 세이브 데이터의 에러를 막기 위한 위임 프로퍼티!
    public List<DiceData1> masterDeck { get => deckManager.masterDeck; set => deckManager.masterDeck = value; }
    public List<DiceData1> drawPile { get => deckManager.drawPile; set => deckManager.drawPile = value; }
    public List<DiceData1> discardPile { get => deckManager.discardPile; set => deckManager.discardPile = value; }

    // 외부 스크립트 연결 유지를 위한 프로퍼티 위임 (에러 완벽 방어)
    [HideInInspector] public int playerMaxHP { get => playerStatus.maxHP; set => playerStatus.maxHP = value; }
    [HideInInspector] public int currentPlayerHP { get => playerStatus.currentHP; set => playerStatus.currentHP = value; }
    [HideInInspector] public int currentShield { get => playerStatus.currentShield; set => playerStatus.currentShield = value; }
    [HideInInspector] public float stageBonusMult { get => stageContext.stageBonusMult; set => stageContext.stageBonusMult = value; }
    [HideInInspector] public int stageBonusChips { get => stageContext.stageBonusChips; set => stageContext.stageBonusChips = value; }
    [HideInInspector] public float combatWinGoldMultiplier { get => stageContext.combatWinGoldMultiplier; set => stageContext.combatWinGoldMultiplier = value; }
    [HideInInspector] public int extraAttackCount { get => stageContext.extraAttackCount; set => stageContext.extraAttackCount = value; }
    [HideInInspector] public bool isEnemySkillNullified { get => stageContext.isEnemySkillNullified; set => stageContext.isEnemySkillNullified = value; }
    [HideInInspector] public bool isNextEnemyAttackFixedToOne { get => stageContext.isNextEnemyAttackFixedToOne; set => stageContext.isNextEnemyAttackFixedToOne = value; }
    [HideInInspector] public int figureBonusFlameDamage { get => stageContext.figureBonusFlameDamage; set => stageContext.figureBonusFlameDamage = value; }
    [HideInInspector] public int accumulatedFlameDamage { get => stageContext.accumulatedFlameDamage; set => stageContext.accumulatedFlameDamage = value; }
    [HideInInspector] public bool isPeppermintActive { get => stageContext.isPeppermintActive; set => stageContext.isPeppermintActive = value; }
    [HideInInspector] public float snackBonusFigureDropRate { get => stageContext.snackBonusFigureDropRate; set => stageContext.snackBonusFigureDropRate = value; }

    [HideInInspector] public float snackBonusMult { get => turnContext.snackBonusMult; set => turnContext.snackBonusMult = value; }
    [HideInInspector] public int snackBonusChips { get => turnContext.snackBonusChips; set => turnContext.snackBonusChips = value; }
    [HideInInspector] public int snackBonusRerolls { get => turnContext.snackBonusRerolls; set => turnContext.snackBonusRerolls = value; }
    [HideInInspector] public int figureBonusRerolls { get => turnContext.figureBonusRerolls; set => turnContext.figureBonusRerolls = value; }

    // --- 스낵 시스템용 변수 ---
    private int defaultMaxRerolls;

    // 기존 씬의 activeDiceList 데이터를 보존하며 같은 목록을 보드 담당에 전달합니다.
    [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("activeDiceList")]
    private List<Dice> serializedActiveDice = new List<Dice>();
    public List<Dice> activeDiceList => board != null ? board.ActiveDice : serializedActiveDice;
    private DiceBoardController board;
    private readonly TurnResolutionPresenter resolutionPresenter = new TurnResolutionPresenter();

    [Header("정산 연출 시간")]
    public TurnResolutionTiming resolutionTiming = new TurnResolutionTiming();

    //오브젝트 풀링 및 UI 갱신용 재사용 버퍼
    private List<Dice> uiDiceBuffer = new List<Dice>(5);
    private List<int> uiValuesBuffer = new List<int>(5);

    private bool pendingPeppermintSuccess = false;
    private bool enemyDeathHandled = false;
    private bool isRolling = false; // 주사위 굴러가는중 
    public bool IsDiceInputLocked => isRolling || isCalculating || currentPlayerHP <= 0 || enemy == null || enemy.IsDead || isStageClearing;
    public bool isCalculating = false;  //끝내기 버튼                                
    public Dictionary<string, int> figureKillCounts = new Dictionary<string, int>();// 피규어 획득 후 처치 기록. 스테이지가 바뀌어도 유지
    public float permanentFigureMultiplier = 0f;

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


    private readonly List<Dice> keptDice = new List<Dice>(5);
    private readonly List<int> keptValues = new List<int>(5);
    private readonly List<DiceScoreBonus> diceScoreBonuses = new List<DiceScoreBonus>(20);


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

        board = new DiceBoardController(dicePrefab, keepSlotParent, rollSlotParent, serializedActiveDice, deckManager);

        // 주사위 상태 변경 이벤트 구독 연동
        Dice.OnDiceStateChanged += HandleDiceChanged;

        defaultMaxRerolls = maxRerolls;

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
            deckManager.InitializeMasterDeck();

            // 첫 시작은 무조건 숲(Forest)으로 고정
            currentBiome = biomeList.Find(b => b.biomeType == BiomeType.Forest);
            StartNewStage();
        }

        //튜토리얼 종료 후 메인 게임 진입 시, 현재 설정된 1스테이지(숲) 바이옴의 브금을 강제로 재생!
        if (BGMManager.Instance != null && currentBiome != null)
        {
            BGMManager.Instance.ChangeBGM(currentBiome.biomeBGM);
        }
    }

    void OnDestroy() => Dice.OnDiceStateChanged -= HandleDiceChanged;

    void LoadSavedGame()
    {
        SaveData data = GameSaveManager.Instance.LoadSaveData();
        if (data == null) return;
        stageContext.firstNormalAttackDone = data.firstNormalAttackDone;

        stageContext.usedFigureNodes.Clear();

        if (data.usedFigureNodes != null)
        {
            foreach (string nodeKey in data.usedFigureNodes)
            {
                stageContext.usedFigureNodes.Add(nodeKey);
            }
        }

        figureBonusFlameDamage = data.pendingFigureFlameDamage;

        currentStage = data.currentStage;
        currentPlayerHP = data.currentPlayerHP;

        //누적 보너스 로드
        stageBonusMult = data.stageBonusMult;
        stageBonusChips = data.stageBonusChips;

        //세이브에 값이 없으면 기본 100으로, 있으면 세이브된 값으로 덮어씌움
        playerMaxHP = data.playerMaxHP > 0 ? data.playerMaxHP : 100;

        //세이브 파일에서 보호막 불러오기 및 UI 갱신
        currentShield = data.currentShield;
        ui?.UpdateShieldUI(currentShield);

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

            //세이브 파일에 있던 위성 상태를 덮어씌움
            if (newDice != null && dData.activeSatellites != null)
            {
                newDice.activeSatellites.Clear();
                foreach (int satInt in dData.activeSatellites)
                {
                    newDice.activeSatellites.Add((SatelliteType)satInt);
                }
            }
        }
        InventoryManager.Instance.ClearAllSlots();

        //최적화된 ID 기반으로 피규어 복원
        if (data.ownedFigureIDs != null && data.ownedFigureIDs.Count > 0)
        {
            foreach (string fID in data.ownedFigureIDs)
            {
                var item = GameSaveManager.Instance.FindFigureByID(fID);
                if (item != null) InventoryManager.Instance.RestoreItem(item);
                else Debug.LogError($"[피규어 복원 실패] 저장된 아이디: '{fID}'를 찾을 수 없습니다.");
            }
        }

        if (data.snackSlotSaveVersion >= 1)
        {
            // 사용 예약과 소모 방지 결과를 복구합니다. 효과/확률 판정은 다시 실행하지 않습니다.
            InventoryManager.Instance.SnackUses.RestoreSaved(data.snackSlots, GameSaveManager.Instance);
        }
        else
        {
            // 이전 버전 저장 파일은 기존 스낵 목록으로 복구
            foreach (string sName in data.ownedSnackIDs)
            {
                var item = GameSaveManager.Instance.FindItemByName(sName);
                if (item != null) InventoryManager.Instance.AddItem(item);
            }
        }
        foreach (string tName in data.ownedTicketIDs)
        {
            var item = GameSaveManager.Instance.FindItemByName(tName);
            if (item != null)
                InventoryManager.Instance.AddItem(item);
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
                enemy.RestoreMonster(savedMonster, data.savedMonsterHP, data.savedMonsterMaxHP, data.savedMonsterAttack, data.savedMonsterIndex, data.savedMonsterCurrentTurn, data.savedMonsterMaxTurn);
            }
            else enemy.Initialize(currentStage, currentBiome); // 에러 방지용 안전장치

            accumulatedFlameDamage = data.savedFlameDamage;
            ui?.UpdateFlameStackUI(accumulatedFlameDamage); //세이브 로드 시 스택 UI 갱신
        }
        else
        {
            enemy.Initialize(currentStage, currentBiome);
            accumulatedFlameDamage = 0; // 새로 시작할 땐 확실하게 0으로 초기화
            ui?.UpdateFlameStackUI(0);
        }

        //세이브 로드 시에도 적 능력을 체크해서 다시 발동
        if (enemy.CurrentBossAbility == BossAbilityType.FakeDice)
        {
            deckManager.ApplyFakeDice(fakeDiceShell, fakeDiceFace, ref originalBossDice, ref fakeDiceIndex);
        }

        // 덱 섞기 및 이번 턴 시작 (StartNewStage() 대신 호출)
        drawPile = new List<DiceData1>(masterDeck);
        discardPile.Clear();
        deckManager.ShufflePile(drawPile);
        permanentFigureMultiplier =
    data.permanentFigureMultiplier;

        figureKillCounts.Clear();

        if (data.figureKillCounts != null)
        {
            foreach (var saved in data.figureKillCounts)
            {
                if (saved == null ||
                    string.IsNullOrEmpty(saved.figureName))
                {
                    continue;
                }

                figureKillCounts[saved.figureName] =
                    Mathf.Max(0, saved.count);
            }
        }
        StartNewRound(isFromLoad: true, returnPreviousDiceToDiscard: false);
        // 사용 기록은 유지한 채, 아직 미사용인 낮은 체력 효과만 검사
        FigureEffectManager.Instance?.EvaluateLowHPTriggers(this, shopManager);
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


    void StartNewStage()
    {
        InventoryManager.Instance?.SnackUses.ResetForStage();
        isStageClearing = false;
        enemyDeathHandled = false;
        //스테이지 생명주기 데이터 일괄 안전 초기화
        stageContext.ResetForNewStage();
        turnContext.figureBonusRerolls = 0;

        playerStatus.currentShield = 0; // 매 스테이지 시작 시 보호막만 초기화!

        ui?.UpdateShieldUI(playerStatus.currentShield);
        ui?.UpdateFlameStackUI(stageContext.accumulatedFlameDamage);

        // 기존에 가짜 주사위 기믹이 남아있다면 원상복구
        deckManager.RestoreFakeDice(ref originalBossDice, ref fakeDiceIndex);

        //(숙원의 방랑자 패널티 적용)
        if (isNextCombatHPTiedToOne)
        {
            currentPlayerHP = 1; // 플레이어 체력을 1로 고정
            isNextCombatHPTiedToOne = false; // 적용했으니 스위치를 다시 끔
        }

        currentRerolls = 0;
        maxRerolls = defaultMaxRerolls;
        isPeppermintActive = false;
        pendingPeppermintSuccess = false;
        snackBonusFigureDropRate = 0f;

        //화염 데미지 스택 초기화
        accumulatedFlameDamage = 0;
        ui?.UpdateFlameStackUI(0); //새 스테이지 돌입 시 스택 UI 숨김

        // StageManager가 배경과 브금을 알아서 세팅함
        stageProgression.ApplyBiomeEnvironment(biomeBackgroundImage);

        // 몬스터 소환 시 현재 스테이지와 바이옴 정보를 넘겨줌
        if (currentBiome != null) enemy.Initialize(currentStage, currentBiome);
        else enemy.Initialize(currentStage, null);

        if (enemy.CurrentBossAbility == BossAbilityType.FakeDice)
        {
            deckManager.ApplyFakeDice(fakeDiceShell, fakeDiceFace, ref originalBossDice, ref fakeDiceIndex);
        }

        // 몬스터 생성 완료 직후 로직
        if (FigureEffectManager.Instance != null)
        {
            FigureEffectManager.Instance.EvaluateCombatStartTriggers(this, shopManager);
        }

        // 드로우 리스트 초기화 및 셔플
        deckManager.PrepareDeckForNewStage();
        StartNewRound(returnPreviousDiceToDiscard: false); FigureEffectManager.Instance?.EvaluateLowHPTriggers(this, shopManager);
    }

    void StartNewRound(bool isFromLoad = false, bool returnPreviousDiceToDiscard = true)
    {
        isCalculating = false;

        ui?.HideResult();
        currentRerolls = 0;

        if (!isFromLoad)
        {
            turnContext.ResetForNewTurn(); // 로드해서 들어올 때는 증발하지 않도록 스킵
        }

        board.SpawnDice(returnPreviousDiceToDiscard, currentStage);
        HandleDiceChanged();

        //주사위 세팅이 끝나고 새로운 라운드(턴)가 본격적으로 시작되는 시점
        // 이어하기는 새 라운드 발동 효과를 다시 지급하지 않음
        if (!isFromLoad && FigureEffectManager.Instance != null)
        {
            FigureEffectManager.Instance.EvaluateRoundStartTriggers(this, shopManager);
        }
    }

    public void ForceUpdateUI() => HandleDiceChanged();

    public void OnRollButtonClick()
    {
        // 결산 중(isCalculating)일 때 리롤 진입 완벽 차단 방어막 추가
        if (isRolling || isCalculating || currentRerolls >= (maxRerolls + snackBonusRerolls + figureBonusRerolls) || ShopManager.IsShopOpen || FigureDetailPanel.IsPanelOpen || LootSelectionPanel.IsPanelOpen) return;

        isRolling = true; // 굴림 상태 켜기
        ui?.SetRollButtonInteractable(false);   //즉시 버튼 비활성화
        ui?.SetFinishButtonInteractable(false); //주사위가 굴러가는 동안 끝내기 버튼도 막기

        //리롤 버튼 클릭 및 주사위 굴러가는 소리

        CameraShake.Instance.Shake(0.1f, 0.1f);

        board.RollUnkept();

        currentRerolls++;
        //리롤 시 발동(OnDiceReroll)하는 6번 카테고리 피규어 발동
        if (FigureEffectManager.Instance != null)
        {
            FigureEffectManager.Instance.EvaluateRerollTriggers(this, shopManager);
        }

        StartCoroutine(HandleDiceChangedDelayed());
    }

    public void OnFinishButtonClick()
    {
        if (isRolling || isCalculating || ShopManager.IsShopOpen || FigureDetailPanel.IsPanelOpen || LootSelectionPanel.IsPanelOpen || enemy.IsDead) return;

        figureBonusRerolls = 0;

        isCalculating = true; //결산 연출 시작
        ui?.SetRollButtonInteractable(false);   //즉시 버튼 비활성화
        ui?.SetFinishButtonInteractable(false); //즉시 버튼 비활성화

        //끝내기 버튼을 누르는 순간 화면 전체를 묵직하게 흔듭니다.
        CameraShake.Instance.Shake(0.3f, 0.2f);

        // 끝내기 버튼 자체도 크게 튕기게 만들어 누르는 손맛을 줍니다.
        if (ui != null && ui.finishButton != null)
        {
            ui.finishButton.transform.DOKill(true);
            ui.finishButton.transform.DOPunchScale(new Vector3(0.2f, 0.2f, 0f), 0.3f, 5, 0.5f);
        }

        // 실제 연산 로직은 코루틴으로 넘겨서 실행합니다.
        StartCoroutine(FinishTurnRoutine());
    }


    IEnumerator FinishTurnRoutine()
    {
        //데이터 독립 추출 (UI 버퍼 의존 X, 킵된 주사위만 스냅샷 수집)
        board.CollectKept(keptDice, keptValues);

        // 안전장치: 킵한 주사위가 없으면 결산 중단
        if (keptValues.Count == 0)
        {
            isCalculating = false;
            HandleDiceChanged();
            yield break;
        }

        // 끝내기 직후: 스테이크 회복, 가니쉬/페퍼민트 소멸, 보존된 라임 복귀를 먼저 처리
        SnackUseController snacks = InventoryManager.Instance != null ? InventoryManager.Instance.SnackUses : null;
        if (snacks != null) yield return snacks.PlayFinishStart();

        // 족보 판정 (TurnCalculator가 주는 정확한 배수와 이름을 그대로 사용)
        float handMult = 1.0f;
        HandRank handRank = TurnCalculator.CalculateHand(keptValues, this, out handMult);
        currentHandRank = handRank;
        string handName = LocalizationManager.GetHandDisplayName(handRank);

        yield return resolutionPresenter.PlayOpening(this, keptDice, handRank, handMult, resolutionTiming);

        // 피규어 계산에 필요한 기본 눈금 합만 먼저 구함
        int baseSumBeforeFigures = 0;
        foreach (int value in keptValues)
        {
            baseSumBeforeFigures += value;
        }

        int chipsBeforeFigures = snackBonusChips;
        float multBeforeFigures = snackBonusMult;

        // 피규어 발동 및 선택창 처리가 끝날 때까지 대기
        if (FigureEffectManager.Instance != null)
        {
            yield return StartCoroutine(FigureEffectManager.Instance.EvaluateTurnEndTriggersCoroutine(keptValues, handRank, baseSumBeforeFigures, this, shopManager));
        }

        // 피규어 적용 이후의 회복 배수와 적 체력을 사용
        float healMultUI = FigureEffectManager.Instance != null ? FigureEffectManager.Instance.GetHealMultiplier() : 1.0f;

        int simEnemyHP = enemy != null ? enemy.CurrentHP : 0;

        // 변경된 코팅·위성을 반영하여 주사위 효과 계산
        TurnCalcResult calcResult = TurnCalculator.CalculateDiceEffects(keptDice, simEnemyHP, healMultUI, diceScoreBonuses);
        int extraIceChipsForDisplay = FigureEffectManager.Instance != null ? FigureEffectManager.Instance.GetIceChipsBonus() : 0;

        // 주사위별 기본 눈금 표시. 코팅·위성 칩은 아래 순차 결산에서 표시합니다.
        foreach (var d in keptDice)
        {
            if (d == null || d.myData == null) continue;

            // 기존 피규어 발동 알림은 유지
            if (d.myData.isCoated && d.myData.type == DiceType.Ice)
            {
                if (extraIceChipsForDisplay > 0) FigureEffectManager.Instance?.NotifyPassiveApplied(FigureEffectType.AddIceChips);
                if (calcResult.iceBonusMult > 0f) FigureEffectManager.Instance?.NotifyPassiveApplied(FigureEffectType.AddIceMultiplier);
            }
        }

        var bonuses = new TurnScoreBonuses
        {
            stageChips = stageBonusChips,
            snackChips = snackBonusChips,
            stageMult = stageBonusMult,
            snackMult = snackBonusMult,
            permanentMult = permanentFigureMultiplier
        };
        TurnScoreResult total = TurnCalculator.CalculateScore(calcResult, handMult, bonuses);
        yield return resolutionPresenter.PlayScore(ui, keptDice, handRank, handName, handMult, calcResult, diceScoreBonuses, bonuses, chipsBeforeFigures, multBeforeFigures, total, resolutionTiming, snacks);
        if (snacks != null) yield return snacks.CompleteResolution();

        // 다크 데미지 별도 계산 (최신 HP 기준)
        int darkDamageTotal = TurnCalculator.CalculateDarkDamage(keptDice, simEnemyHP);

        yield return resolutionPresenter.PlayFinalDamage(ui, total.damage + darkDamageTotal, resolutionTiming);

        // 화염 및 기타 수치 종합
        int totalFlameDamage = calcResult.flameDamageThisTurn + figureBonusFlameDamage;
        figureBonusFlameDamage = 0;
        int finalHeal = calcResult.expectedHeal;
        int expectedGold = calcResult.expectedGold; // 코인 주사위 등에서 얻은 골드

        // 완벽하게 쪼개진 수치들을 전투 지휘관(CombatFlowController)에게 전달
        yield return StartCoroutine(CombatFlowController.ProcessTurnResolution(this, total.damage, darkDamageTotal, finalHeal, expectedGold, totalFlameDamage, handName
        ));
        ui?.ClearMergedHandDamageText();
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

    public void OnEnemyKilled()
    {
        if (enemy == null || isStageClearing || enemyDeathHandled)
            return;
        enemyDeathHandled = true;

        // 포획 보상으로 새 피규어를 얻기 전에 기존 보유 피규어의 처치 수 기록
        FigureEffectManager.Instance?.RecordEnemyKill(this);

        // 포획 성공 여부를 실제로 계산해서 저장
        pendingPeppermintSuccess = CaptureResolver.CheckCaptureSuccess(
            enemy,
            snackBonusFigureDropRate,
            isPeppermintActive
        );

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

    public void ProcessStageClear(bool fromPeppermint)
    {
        if (isStageClearing) return; //이미 클리어 처리 중이면 중복 실행 방지
        isStageClearing = true;

        // 보스를 잡자마자 가장 먼저 가짜 주사위 원상 복구 (상점/이벤트 가기 전 덱 정상화)
        deckManager.RestoreFakeDice(ref originalBossDice, ref fakeDiceIndex);

        // 공허 바이옴에서 보스를 잡았다면 최종 게임 클리어 처리
        if (currentBiome != null && currentBiome.biomeType == BiomeType.Void)
        {
            ShowGameClear();
            return;
        }

        int baseClearReward = 500;
        baseClearReward = Mathf.FloorToInt(baseClearReward * combatWinGoldMultiplier); // 투탕카멘 2배 적용
        combatWinGoldMultiplier = 1.0f; // 초기화

        if (shopManager != null)
        {
            shopManager.GrantGold(baseClearReward);
        }
        if (shopManager != null)
        {
            shopManager.currentGold += baseClearReward;
            ui?.UpdateGoldUI(shopManager.currentGold);
            //스테이지 클리어 기본 골드 카운팅 연출 실행

            if (GoldCounter.Instance != null) GoldCounter.Instance.SetGold(shopManager.currentGold);
        }

        //스테이지 클리어 시 패시브(Passive) 피규어 효과 일괄 발동
        if (FigureEffectManager.Instance != null)
        {
            FigureEffectManager.Instance.EvaluateStageClearTriggers(this, shopManager);
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
        // 튜토리얼 중에는 조우자 이벤트를 아예 스킵하고 무조건 전리품으로 감.
        if (TutorialManager.Instance != null && TutorialManager.Instance.isTutorialActive)
        {
            if (currentStage == 3) return; // 3스테이지(보스전)는 아무것도 안 띄우고 종료
            ShowLootSelection();
            return;
        }

        //7, 17, 27, 37... 등 10라운드 주기로 끝자리가 7인 스테이지 클리어 시 조우자 등장
        if (currentStage % 10 == 1 && encounterEventPanel != null)
        {
            // 조우자 이벤트를 시작할 때 현재 바이옴 타입을 넘겨주어 등장 가능한 조우자만 필터링
            encounterEventPanel.StartEvent(currentBiome.biomeType);
        }
        else
        {
            // 그 외의 일반 스테이지는 조우자 없이 바로 전리품 선택으로 넘어감
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
            gameClearText.text = LocalizationManager.GetUi(
                "UI_GAME_CLEAR",
                "<color=#00FF00>GAME CLEAR!</color>\n\n축하합니다!\n모든 시련을 이겨내고 공허를 정복했습니다!");
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


    private IEnumerator HandleDiceChangedDelayed()
    {
        yield return new WaitForSeconds(0.5f);
        isRolling = false;
        HandleDiceChanged();
    }

    void HandleDiceChanged()
    {

        board.SyncKeepSlots(out int keptCount, out bool hasDiceToRoll);
        UpdateMainUI("");
        bool canAct = !isRolling && !isCalculating && currentPlayerHP > 0 && enemy != null && !enemy.IsDead && !isStageClearing;

        ui?.SetRollButtonInteractable(canAct && currentRerolls < maxRerolls + snackBonusRerolls + figureBonusRerolls && hasDiceToRoll);

        ui?.SetFinishButtonInteractable(canAct && keptCount >= 1);

        OnDeckUpdateNeeded?.Invoke();
    }

    //CombatFlowController 전용 도우미 함수들
    public void InvokeRestartGame(float time) { Invoke(nameof(RestartGame), time); }

    //매개변수가 있는 함수를 안전하게 지연 실행하기 위해 코루틴으로 교체
    public void InvokeStartNewRound(float floatTime) { StartCoroutine(StartNewRoundCoroutine(floatTime)); }
    private IEnumerator StartNewRoundCoroutine(float time)
    {
        yield return new WaitForSeconds(time);
        StartNewRound();
    }
    public void PlayPlayerHurtSound()
    {
        if (playerHurtAudioEvent == null)
            return;

        if (AudioControl.Instance != null)
        {
            AudioControl.Instance.PlaySFX(playerHurtAudioEvent);
            return;
        }

        if (sfxSource != null)
            playerHurtAudioEvent.PlayOneShot(sfxSource);
    }

    // 현재 족보 이름에 맞는 진짜 배수를 찾아오는 함수
    public float GetHandMultiplier(HandRank rank)
    {
        switch (rank)
        {
            case HandRank.HighCard: return multHighCard;
            case HandRank.OnePair: return multOnePair;
            case HandRank.TwoPair: return multTwoPair;
            case HandRank.Triple: return multTriple;
            case HandRank.Straight: return multStraight;
            case HandRank.FullHouse: return multFullHouse;
            case HandRank.FourOfAKind: return multFourOfAKind;
            case HandRank.Yacht: return multYacht;
            default: return 1.0f;
        }
    }

    private readonly HashSet<FigureItemSO> previewFigures = new HashSet<FigureItemSO>();
    private readonly List<string> previewFigureNames = new List<string>();
    private readonly List<Sprite> previewFigureSprites = new List<Sprite>();
    private readonly int[] previewDiceCounts = new int[7];

    public void UpdateMainUI(string handName)
    {
        //매 틱마다 List를 새로 만들지 않고, 고정된 버퍼를 비우고 다시 채워 메모리 낭비 차단
        uiDiceBuffer.Clear();
        uiValuesBuffer.Clear();
        foreach (var d in activeDiceList)
        {
            if (d != null && d.gameObject.activeInHierarchy && d.isKept)
            {
                uiDiceBuffer.Add(d);
                uiValuesBuffer.Add(d.currentValue);
            }
        }

        float baseMult = 1.0f;

        HandRank rank = currentHandRank;
        if (!isCalculating)
        {
            if (uiValuesBuffer.Count > 0)
            {
                rank = TurnCalculator.CalculateHand(uiValuesBuffer, this, out baseMult);
                currentHandRank = rank;
                handName = LocalizationManager.GetHandDisplayName(rank);
            }
            else
            {
                rank = HandRank.HighCard;
                currentHandRank = rank;
                handName = "";
            }

            currentHandName = handName;
        }
        else
        {
            if (uiValuesBuffer.Count == 5)
                TurnCalculator.CalculateHand(uiValuesBuffer, this, out baseMult);
        }

        // 순수 연산기를 통한 통합 연산 호출
        float healMultUI = FigureEffectManager.Instance != null ? FigureEffectManager.Instance.GetHealMultiplier() : 1.0f;
        int simEnemyHP = (enemy != null) ? enemy.CurrentHP : 0;
        TurnCalcResult calcResult = TurnCalculator.CalculateDiceEffects(uiDiceBuffer, simEnemyHP, healMultUI);

        int baseSum = calcResult.baseSum;
        float finalMult = baseMult + snackBonusMult + calcResult.prismMultTotal + calcResult.satelliteBonusMult + calcResult.iceBonusMult;
        int iceBonusChips = calcResult.iceBonusChips;
        int satelliteBonusChips = calcResult.satelliteBonusChips;

        // 다크 데미지는 피규어 이후 계산을 시뮬레이션하기 위해 따로 빼서 수동 계산
        int darkDamageTotal = TurnCalculator.CalculateDarkDamage(uiDiceBuffer, simEnemyHP);

        flameDamageThisTurn = figureBonusFlameDamage; // 화상 데미지 상태 저장 연동

        // 피규어 발동 실시간 시뮬레이션
        int figureBonusChips = 0;
        float figureBonusMult = 0f;
        previewFigures.Clear();
        previewFigureNames.Clear();
        previewFigureSprites.Clear();
        List<string> activeFigureNames = previewFigureNames;
        List<Sprite> activeFigureSprites = previewFigureSprites; //피규어 아이콘 담을 리스트

        if (uiValuesBuffer.Count == 5 && InventoryManager.Instance != null) // 5개가 모였을 때만 피규어 발동 검사
        {
            System.Array.Clear(previewDiceCounts, 0, previewDiceCounts.Length);
            int[] diceCounts = previewDiceCounts;
            foreach (int v in uiValuesBuffer)
            {
                if (v >= 0 && v <= 6)
                {
                    diceCounts[v]++;
                }
            }

            foreach (var figure in InventoryManager.Instance.ownedFigures)
            {
                bool isTriggered = false;
                float tempChips = 0;
                float tempMult = 0;

                foreach (var node in figure.figureNodes)
                {
                    if (node == null || node.effects == null || node.effects.Count == 0) continue;
                    if (node.requiredKills > 0 && (!figureKillCounts.TryGetValue(figure.itemName, out int kills) || kills < node.requiredKills)) continue;
                    if (node.oncePerStage && stageContext.usedFigureNodes.Contains($"{figure.itemName}:{figure.figureNodes.IndexOf(node)}")) continue;
                    bool nodeTriggered = false;
                    switch (node.triggerType)
                    {
                        case FigureTriggerType.ThreeOf1: if (diceCounts[1] >= 3) nodeTriggered = true; break;
                        case FigureTriggerType.ThreeOf2: if (diceCounts[2] >= 3) nodeTriggered = true; break;
                        case FigureTriggerType.ThreeOf3: if (diceCounts[3] >= 3) nodeTriggered = true; break;
                        case FigureTriggerType.ThreeOf4: if (diceCounts[4] >= 3) nodeTriggered = true; break;
                        case FigureTriggerType.ThreeOf5: if (diceCounts[5] >= 3) nodeTriggered = true; break;
                        case FigureTriggerType.ThreeOf6: if (diceCounts[6] >= 3) nodeTriggered = true; break;
                        case FigureTriggerType.OnePair:
                        case FigureTriggerType.TwoPair:
                        case FigureTriggerType.Triple:
                        case FigureTriggerType.Straight:
                        case FigureTriggerType.FullHouse:
                        case FigureTriggerType.FourOfAKind:
                        case FigureTriggerType.Yacht:
                            if (HandRankUtil.MatchesTrigger(rank, node.triggerType)) nodeTriggered = true;
                            break;
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
                    if (previewFigures.Add(figure))
                    {
                        activeFigureNames.Add(figure.itemName);
                        activeFigureSprites.Add(figure.icon); //발동된 피규어의 아이콘 저장
                    }
                    figureBonusChips += (int)tempChips;
                    figureBonusMult += tempMult;
                }
            }
        }

        FigureFeedback.ApplyPreview(previewFigures);

        // 끝내기 버튼을 누르기 전과 후의 UI 렌더링을 분리형 텍스트에 맞게 수정
        string bName = (currentBiome != null)
            ? LocalizationManager.GetBiomeDisplayName(currentBiome.biomeType)
            : "Stage";
        string stageDisplayName = $"{bName} {currentStage}";
        int remainingRerolls = (maxRerolls + snackBonusRerolls + figureBonusRerolls) - currentRerolls;
        int remainingFinishes = isCalculating ? 0 : 1;

        if (!isCalculating)
        {
            int displayBaseSum = baseSum + iceBonusChips + satelliteBonusChips + stageBonusChips;
            float displayMult = 1.0f + stageBonusMult + permanentFigureMultiplier;

            int displayDamage = Mathf.FloorToInt(displayBaseSum * displayMult) + darkDamageTotal;

            string displayHand = string.IsNullOrEmpty(handName) ? "" : $"<color=#FFD700>{handName}</color>";
            if (iceBonusChips > 0) displayHand += $" <color=#00FFFF>+{iceBonusChips}</color>";
            if (darkDamageTotal > 0) displayHand += $" <color=#A9A9A9>+{darkDamageTotal}</color>";
            if (satelliteBonusChips > 0) displayHand += $" <color=#B19CD9>+{satelliteBonusChips}{LocalizationManager.GetUi("UI_SATELLITE_TAG", "(위성)")}</color>";

            // 분리된 텍스트에 각각 할당
            if (ui != null)
            {
                ui.SetHandNameText(displayHand);
                UiCountUpText.On(ui.chipsSumText, UiCountUpText.FormatKind.Chips)?.Play(displayBaseSum);
                UiCountUpText.On(ui.multSumText, UiCountUpText.FormatKind.Mult)?.Play(displayMult);

                if (ui.chipsLogText != null) ui.chipsLogText.text = "";
                if (ui.multLogText != null) ui.multLogText.text = "";
                if (ui.finalDamageText != null)
                    UiCountUpText.On(ui.finalDamageText, UiCountUpText.FormatKind.Integer)?.Clear();
            }

            // UpdateGameUI의 combinedText 매개변수는 빈 문자열로 보냄
            ui?.UpdateGameUI(stageDisplayName, enemy.CurrentHP, enemy.MaxHP, currentPlayerHP, playerMaxHP, remainingRerolls, "", "", activeFigureSprites, remainingFinishes);
        }
        else
        {
            // 끝내기를 누른 후(결산 중): 시퀀스 코루틴이 각 텍스트를 개별 제어하므로 건드리지 않음
            ui?.UpdateGameUI(stageDisplayName, enemy.CurrentHP, enemy.MaxHP, currentPlayerHP, playerMaxHP, remainingRerolls, "", "", activeFigureSprites, remainingFinishes);
        }

        float currentEnemyDropRate = isPeppermintActive ? enemy.baseDropRate : 0f;
    }


    public void PromptShopChoice() { ui?.HideResult(); ui?.ShowShopChoice(); }
    public void GoToShop() { ui?.HideShopChoice(); shopManager?.OpenShop(); }
    public void SkipShopAndNextStage() { ui?.HideShopChoice(); NextStage(); }

    //발표용 떄문에 튜토리얼 수정
    public void NextStage()
    {
        bool isTutorial = TutorialManager.Instance != null && TutorialManager.Instance.isTutorialActive;

        // StageManager에게 라운드 증가를 지시하고, 지금이 바이옴을 넘길 타이밍인지 판정받음
        bool shouldChangeBiome = stageProgression.AdvanceToNextStage(isTutorial);

        if (shouldChangeBiome)
        {
            ui?.HideShopChoice();
            List<BiomeType> nextOptions = biomeNavigator.GetNextBiomeOptions(currentBiome.biomeType, currentStage - 1);
            biomeSelectionPanel.OpenPanel(this, nextOptions);
        }
        else
        {
            // 튜토리얼 중이거나, 바이옴 넘어갈 타이밍이 아닐 때는 그대로 게임 진행
            ui?.HideShopChoice();
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

        stageProgression.SetNewBiome(biomeList, selectedType); // 이관된 로직
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

    //재시작
    public void RestartGame()
    {
        permanentFigureMultiplier = 0f;
        //재시작시 가짜 주사위 참조 안전하게 비우기
        originalBossDice = null;
        fakeDiceIndex = -1;

        // 낡은 변수 대신 우리가 만든 상태 초기화 함수를 씀
        playerStatus.ResetStatus(100);

        //덱 초기화 (상점에서 샀던 특수 주사위들을 모두 버리고 기본 20개로)
        deckManager.InitializeMasterDeck();

        //골드 초기화 (ShopManager 참조)
        if (shopManager != null)
        {
            shopManager.currentGold = 50000; // 초기 소지금
            ui?.UpdateGoldUI(shopManager.currentGold);
            // 재시작 및 메인 이동 시 초기 소지금 카운팅 연출 실행 (또는 초기화용)
            if (GoldCounter.Instance != null) GoldCounter.Instance.SetGold(shopManager.currentGold);
        }

        //인벤토리 초기화 (방금 만든 함수 호출)
        InventoryManager.Instance?.ClearAllSlots();

        //스낵 및 특수 상태 버프 일괄 초기화
        turnContext.ResetForNewTurn();
        stageContext.ResetForNewStage();
        turnContext.figureBonusRerolls = 0;
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
        stageProgression.InitFirstBiome(biomeList);
        // 새로운 스테이지 시작
        StartNewStage();
    }

    public IEnumerator ShowGameOverPanelDelayed()
    {
        yield return new WaitForSeconds(1.2f); // "게임 오버"

        if (gameOverPanel != null)
        {
            gameOverPanel.SetupGameOver(currentStage);
        }
    }
}
