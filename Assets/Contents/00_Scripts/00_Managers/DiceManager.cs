using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.SceneManagement;
using DG.Tweening;

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
    internal readonly BiomeNavigator biomeNavigator = new BiomeNavigator();

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

    [Header("지도 시스템")]
    public TextAsset mapTableAsset;
    public MapPanel mapPanel;
    public RestPanel restPanel;
    public MapIconSet mapIconSet;

    // 다음 전투에 강제할 몬스터. Enemy.Initialize가 쓰면 비웁니다.
    [System.NonSerialized] public MonsterDataSO pendingForcedMonster;
    // TODO: 엘리트 몬스터 풀
    [System.NonSerialized] public bool isEliteBattle;
    [System.NonSerialized] public bool mapBossBattle;

    // 지도 진행과 지도 화면 HUD 배치는 따로 맡깁니다(아래 "지도" 연결 참고). 처음 쓸 때 만듭니다.
    MapFlowController mapFlow;
    TravelHudLayout hud;
    internal MapFlowController MapFlow => mapFlow ??= new MapFlowController(this);
    internal TravelHudLayout Hud => hud ??= new TravelHudLayout();

    [Header("조우자 특수 효과 상태 (임시 저장용)")]
    [HideInInspector] public bool isNextEnemyHPBoosted = false; // 눈먼 점술가 패널티
    [HideInInspector] public bool isNextCombatHPTiedToOne = false; // 숙원의 방랑자 패널티
    [HideInInspector] public int wandererRestoreHP = 0; // 숙원의 방랑자: 체력 1로 싸우는 전투에서 이기면 되돌릴 체력. 0이면 없음
    [HideInInspector] public int extraShopSlots = 0; // 상점 칸 증감. 밀렵꾼 봉인이면 음수
    [HideInInspector] public int perfumerWeaknessTurns = 0; // 조향사 시련 턴 수
    [HideInInspector] public bool isNextShopFree = false; //녹슨 닻의 선장 무료 상점


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

    private bool pendingPeppermintSuccess = false;
    private bool enemyDeathHandled = false;
    private bool isRolling = false; // 주사위 굴러가는중 
    private bool isStartingCombat;
    public bool IsDiceInputLocked => isStartingCombat || isRolling || isCalculating || currentPlayerHP <= 0 || enemy == null || enemy.IsDead || isStageClearing;
    public bool HasRollingDice => board != null && board.HasRollingDice;
    public bool CanReplaceUnkeptDiceFromDeck => board != null && board.CanReplaceUnkeptFromDeck;
    private bool isRestoringSave;
    private bool isResolvingTurn;
    public bool IsSaveStateStable => !isStartingCombat && !isRolling && !isRestoringSave && !isResolvingTurn && (!isCalculating || isStageClearing) && (board == null || !board.HasRollingDice);
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
    private float[] defaultHandMultipliers; // Awake에서 위 값을 기억해 두고 ResetHandMultipliers에서 되돌림
    static readonly HandType[] AllHandTypes = (HandType[])Enum.GetValues(typeof(HandType));
    float unknownHandMult; // 없는 족보 값이 들어왔을 때 쓰는 임시 칸(항상 1배, 실제 배수는 안 바뀜)


    private readonly List<Dice> keptDice = new List<Dice>(5);
    private readonly List<int> keptValues = new List<int>(5);
    private readonly List<DiceScoreBonus> diceScoreBonuses = new List<DiceScoreBonus>(20);


    // 전역 접근을 위한 싱글톤 인스턴스 선언 (클래스 상단 변수 선언부에 위치)
    public static DiceManager Instance { get; private set; }

    public static event System.Action OnDeckUpdateNeeded;//덱 주사위 실시간 변경 변수
    public static event System.Action MapCombatStarted; // 지도 노드에서 전투가 시작된 직후(튜토리얼 안내용)


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

        board = new DiceBoardController(dicePrefab, keepSlotParent, rollSlotParent, serializedActiveDice, deckManager, Debuffs.ApplyDrawDebuff);
        DebuffTrackerHud.Create(this); // 덱의 디버프 주사위를 왼쪽 위에 종류별로 보여 주는 표시

        // 주사위 상태 변경 이벤트 구독 연동
        Dice.OnDiceStateChanged += HandleDiceChanged;

        maxRerolls = BalanceConfig.Current.maxRerolls;
        defaultMaxRerolls = maxRerolls;
        defaultHandMultipliers = new float[AllHandTypes.Length];
        for (int i = 0; i < AllHandTypes.Length; i++) defaultHandMultipliers[i] = HandMult(AllHandTypes[i]);

        if (mainFromClearButton != null)
            mainFromClearButton.onClick.AddListener(OnGameClearMainButtonClick);

        // UI 버튼 리스너 동적 할당 세팅
        if (ui != null)
        {
            ui.goShopButton?.onClick.AddListener(GoToShop);
            ui.nextStageButton?.onClick.AddListener(SkipShopAndNextStage);
        }
    }

    private bool saveStateReady;
    public bool CanSaveProgress => saveStateReady && !isRestoringSave;

    void Start()
    {
        // 세이브 파일이 있고, 로비에서 이어하기(1)를 눌렀다면 세이브를 불러옴
        if (PlayerPrefs.GetInt("LoadGame", 0) == 1 && PlayerPrefs.HasKey("TrickYacht_Save") && GameSaveManager.Instance != null)
        {
            LoadSavedGame();
        }
        else
        {
            playerStatus.ResetStatus(StartingMaxHP);
            if (shopManager != null) shopManager.currentGold = BalanceConfig.Current.startGold;
            ui?.UpdateGoldUI(shopManager != null ? shopManager.currentGold : 0);
            if (GoldCounter.Instance != null && shopManager != null) GoldCounter.Instance.SetInstant(shopManager.currentGold);
            deckManager.InitializeMasterDeck(JourneyDice.FindSource());

            // 첫 시작은 무조건 숲(Forest)으로 고정
            currentBiome = biomeList.Find(b => b.biomeType == BiomeType.Forest);
            saveStateReady = true;
            MapFlow.BeginNewMapRun();
        }

        // 디버프 표시가 화면에 자리 잡도록 한 번 배치합니다.
        if (DebuffTrackerHud.Instance != null) DebuffTrackerHud.Instance.Place(MapFlow.TravelScreen);

        //튜토리얼 종료 후 메인 게임 진입 시, 현재 설정된 1스테이지(숲) 바이옴의 브금을 강제로 재생!
        if (BGMManager.Instance != null && currentBiome != null)
        {
            BGMManager.Instance.ChangeBGM(currentBiome.biomeBGM);
        }

        if (TutorialMode.ShowsOverlay) TutorialOverlay.Spawn(this);
    }

    void OnDestroy()
    {
        Dice.OnDiceStateChanged -= HandleDiceChanged;
        DebuffFeedback.EndIntro();
    }

    void LoadSavedGame()
    {
        saveStateReady = false;
        SaveData data = GameSaveManager.Instance.LoadSaveData();
        if (data == null) return;
        bool hasSavedTurnState = data.debuffSaveVersion >= 1 || data.boardSaveVersion == 1;

        isRestoringSave = true;
        try
        {
            bool resumeCombat = data.mapSaveVersion >= 1 && !string.IsNullOrEmpty(data.savedMonsterName);
            bool idleMap = !resumeCombat;
            GameStateRestorer.Restore(this, data, defaultMaxRerolls, ref pendingPeppermintSuccess, idleMap);
            MapFlow.RestoreMapProgress(data);

            if (idleMap)
            {
                isCalculating = false;
                MapFlow.ShowMapScreen();
                if (data.mapSaveVersion >= 1 && !stageProgression.currentNodeCleared && stageProgression.currentNodeId >= 0)
                    MapFlow.OpenCurrentNode(stageProgression.FindNode(stageProgression.currentNodeId));
                FigureEffectManager.Instance?.EvaluateLowHPTriggers(this, shopManager);
                saveStateReady = true;
                return;
            }

            MapFlow.ShowCombatScreen();

            if (board.TryRestoreState(data))
            {
                // 새 라운드를 시작하지 않으므로 셔플/리롤/라운드 시작 효과를 재실행하지 않습니다.
                isCalculating = false;
                currentRerolls = data.savedCurrentRerolls;
                maxRerolls = data.savedMaxRerolls;
                ui?.HideResult();
                HandleDiceChanged();
            }
            else
            {
                // 보드 기록이 없는 구버전 저장은 기존 방식으로 호환합니다.
                StartNewRound(isFromLoad: true, returnPreviousDiceToDiscard: false);
                if (hasSavedTurnState)
                {
                    currentRerolls = Mathf.Max(0, data.savedCurrentRerolls);
                    maxRerolls = Mathf.Max(0, data.savedMaxRerolls);
                }
                HandleDiceChanged();
            }
            // 사용 기록은 유지한 채, 아직 미사용인 낮은 체력 효과만 검사
            FigureEffectManager.Instance?.EvaluateLowHPTriggers(this, shopManager);
            saveStateReady = true;
        }
        finally { isRestoringSave = false; }
    }

    internal bool CanRestoreBoard(SaveData data) => board.CanRestoreState(data);

    internal void CaptureBoardForSave(SaveData data)
    {
        if (enemy == null || enemy.IsDead || isStageClearing) return;
        data.savedCurrentRerolls = currentRerolls;
        data.savedMaxRerolls = maxRerolls;
        board.CaptureForSave(data);
    }

    // ===== 전투 디버프 주사위 =====
    // 넣기·빼기·외형·뽑을 때 발동은 CombatDebuffController가 맡습니다. 처음 쓸 때 만듭니다.
    // 아래는 바깥 스크립트(세이브, 복원, 피규어, 디버프 표시)가 예전 이름 그대로 쓰는 연결입니다.
    CombatDebuffController debuffs;
    internal CombatDebuffController Debuffs => debuffs ??= new CombatDebuffController(this);
    internal DiceBoardController Board => board;
    internal List<DiceData1> currentCombatDebuffs => Debuffs.currentCombatDebuffs;
    internal DiceItemSO FindDebuffSource(DebuffDiceType type) => Debuffs.FindDebuffSource(type);
    internal void ApplyDebuffAppearance(DiceData1 die) => Debuffs.ApplyDebuffAppearance(die);
    public void RemoveCombatDebuffs(bool temporaryOnly = false, bool currentEnemyOnly = false) => Debuffs.RemoveCombatDebuffs(temporaryOnly, currentEnemyOnly);

    public void StartNewStage()
    {
        if (isStartingCombat) return;
        board.ClearBoard();
        Debuffs.DiscardDrawTriggers();
        InventoryManager.Instance?.SnackUses.ResetForStage();
        isStageClearing = false;
        enemyDeathHandled = false;
        //스테이지 생명주기 데이터 일괄 안전 초기화
        stageContext.ResetForNewStage();
        turnContext.figureBonusRerolls = 0;

        playerStatus.currentShield = 0; // 매 스테이지 시작 시 보호막만 초기화!

        ui?.UpdateShieldUI(playerStatus.currentShield);
        ui?.UpdateFlameStackUI(stageContext.accumulatedFlameDamage);

        RemoveCombatDebuffs(temporaryOnly: true);
        Debuffs.RemoveFightOnlyDebuffs();
        currentCombatDebuffs.Clear();
        DevilDiceBoss.ClearCurses(deckManager.masterDeck);

        //(숙원의 방랑자 패널티 적용)
        if (isNextCombatHPTiedToOne)
        {
            wandererRestoreHP = currentPlayerHP; // 이 전투에서 이기면 되돌릴 체력
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

        Debuffs.AddNodeDebuffs();

        // 몬스터 생성 완료 직후 로직
        if (FigureEffectManager.Instance != null)
        {
            FigureEffectManager.Instance.EvaluateCombatStartTriggers(this, shopManager);
        }

        // 소지 중인 영구 디버프가 아니라, 이번 전투에서 실제로 추가하는 주사위만 연출한다.
        // 전투 시작 피규어가 무효화/제거한 주사위는 이 목록에서 이미 빠져 있다.
        if (currentCombatDebuffs.Count > 0 && currentPlayerHP > 0 && !enemy.IsDead)
        {
            var incoming = new List<DiceData1>(currentCombatDebuffs);
            foreach (var die in incoming) deckManager.masterDeck.Remove(die);
            isStartingCombat = true;
            HandleDiceChanged();
            StartCoroutine(PlayCombatIntro(incoming));
        }
        else
        {
            deckManager.PrepareDeckForNewStage();
            StartNewRound(returnPreviousDiceToDiscard: false);
            FigureEffectManager.Instance?.EvaluateLowHPTriggers(this, shopManager);
        }
    }

    private IEnumerator PlayCombatIntro(List<DiceData1> incoming)
    {
        GameObject deckButton = Hud.Find("Deck_BTN");
        Transform target = deckButton != null ? deckButton.transform : null;
        bool committed = false;
        try
        {
            yield return DebuffFeedback.PlayArrival(enemy, incoming, target, rollSlotParent, NormalDiceFace());
            deckManager.masterDeck.AddRange(incoming);
            committed = true;
            deckManager.PrepareDeckForNewStage();
            StartNewRound(returnPreviousDiceToDiscard: false);
            yield return DebuffFeedback.PlayFirstDraw(activeDiceList, target);
            FigureEffectManager.Instance?.EvaluateLowHPTriggers(this, shopManager);
        }
        finally
        {
            // 연출 도중 중단돼도 지급 예정 주사위를 잃지 않는다.
            if (!committed) deckManager.masterDeck.AddRange(incoming);
            DebuffFeedback.EndIntro();
            isStartingCombat = false;
            HandleDiceChanged();
        }
        // 첫 손패가 제자리에 놓인 뒤 뽑을 때 발동한 디버프를 보여 준다.
        Debuffs.FlushDrawTriggers();
    }

    // 보스 등장 연출에서 가짜 주사위로 바뀌기 전에 보여 줄 일반 주사위 눈(6).
    Sprite NormalDiceFace()
    {
        Dice template = dicePrefab != null ? dicePrefab.GetComponent<Dice>() : null;
        if (template == null || template.diceFaceSprites == null || template.diceFaceSprites.Length == 0) return null;
        return template.diceFaceSprites[template.diceFaceSprites.Length - 1];
    }

    // 판 밖 연출(조우 제물 등)에서 덱 주사위를 판 위 모습 그대로 그릴 그림과 색. 디버프 주사위는 전용 그림을 쓴다.
    public Sprite DeckDiePreview(DiceData1 die, out Color tint)
    {
        tint = Color.white;
        if (die == null) return null;
        if (die.IsDebuff) return DebuffFeedback.DieSprite(die);
        Dice template = dicePrefab != null ? dicePrefab.GetComponent<Dice>() : null;
        return template != null ? template.PreviewFace(die, out tint) : null;
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

        board.SpawnDice(returnPreviousDiceToDiscard, currentStage, applyDrawEffects: !isFromLoad);
        // 악마주사위 1페이즈부터: 매 턴 보드의 주사위 1개에 저주를 건다. 지난 턴의 저주는 풀린다.
        if (!isFromLoad && DevilDiceBoss.IsActive(enemy))
        {
            DevilDiceBoss.ClearCurses(deckManager.masterDeck);
            DevilDiceBoss.CurseOneDie(board.ActiveDice);
        }
        HandleDiceChanged();
        // 전투 시작 연출 중이면 첫 드로우 연출이 끝난 뒤 PlayCombatIntro가 보여 준다.
        if (!isStartingCombat) StartCoroutine(FlushDrawTriggersAfter(0.15f));

        //주사위 세팅이 끝나고 새로운 라운드(턴)가 본격적으로 시작되는 시점
        // 이어하기는 새 라운드 발동 효과를 다시 지급하지 않음
        if (!isFromLoad && FigureEffectManager.Instance != null)
        {
            FigureEffectManager.Instance.EvaluateRoundStartTriggers(this, shopManager);
        }
    }

    public int RemainingRerolls => Mathf.Max(0, maxRerolls + snackBonusRerolls + figureBonusRerolls - turnContext.debuffRerollPenalty - currentRerolls);

    private IEnumerator FlushDrawTriggersAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        Debuffs.FlushDrawTriggers();
    }

    public void ForceUpdateUI() => HandleDiceChanged();

    public bool TryReplaceUnkeptDiceFromDeck()
    {
        if (board == null || IsDiceInputLocked) return false;
        if (ShopManager.IsShopOpen || FigureDetailPanel.IsPanelOpen || DebuffDetailPanel.IsPanelOpen || LootSelectionPanel.IsPanelOpen) return false;
        if (!board.ReplaceUnkeptDiceFromDeck()) return false;

        isRolling = true;
        ui?.SetRollButtonInteractable(false);
        ui?.SetFinishButtonInteractable(false);
        if (CameraShake.Instance != null) CameraShake.Instance.Shake(0.1f, 0.1f);
        StartCoroutine(HandleDiceChangedDelayed());
        StartCoroutine(FlushDrawTriggersAfter(0.5f)); // 새로 뽑힌 주사위가 다 굴러간 뒤
        return true;
    }

    public void OnRollButtonClick()
    {
        // 결산 중(isCalculating)일 때 리롤 진입 완벽 차단 방어막 추가
        if (IsDiceInputLocked || RemainingRerolls <= 0 || ShopManager.IsShopOpen || FigureDetailPanel.IsPanelOpen || DebuffDetailPanel.IsPanelOpen || LootSelectionPanel.IsPanelOpen) return;

        GameSaveManager.Instance?.SaveGame(this, InventoryManager.Instance, shopManager); // 강제 종료 시에도 굴리기 전 정상 상태 유지
        isRolling = true; // 굴림 상태 켜기
        ui?.SetRollButtonInteractable(false);   //즉시 버튼 비활성화
        ui?.SetFinishButtonInteractable(false); //주사위가 굴러가는 동안 끝내기 버튼도 막기

        //리롤 버튼 클릭 및 주사위 굴러가는 소리

        CameraShake.Instance.Shake(0.1f, 0.1f);

        board.RollUnkept();

        // 클로버 스탬프: 이번에 굴러간(킵 안 된) 클로버 주사위마다 30%씩 따로 굴려, 하나라도 맞으면 리롤 횟수 유지(최대 1회)
        bool keepRerollCharge = false;
        foreach (var d in board.ActiveDice)
        {
            if (d == null || d.isKept || d.myData == null || d.myData.stampType != StampType.Clover) continue;
            if (UnityEngine.Random.Range(0, 100) < TurnCalculator.CloverKeepChancePercent) keepRerollCharge = true;
        }
        if (!keepRerollCharge)
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
        if (IsDiceInputLocked || ShopManager.IsShopOpen || FigureDetailPanel.IsPanelOpen || DebuffDetailPanel.IsPanelOpen || LootSelectionPanel.IsPanelOpen) return;

        // 버튼 상태와 별개로 직접 호출도 5개 킵 조건을 검증합니다.
        board.SyncKeepSlots(out int keptCount, out _);
        if (keptCount != 5) return;

        GameSaveManager.Instance?.SaveGame(this, InventoryManager.Instance, shopManager); // 정산 도중 종료하면 직전 정상 상태로 복원

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
        isResolvingTurn = true;
        try { yield return CombatFlowController.ResolveTurn(this); }
        finally { isResolvingTurn = false; }
    }

    // 턴 결산(CombatFlowController.ResolveTurn)이 쓰는 값. 목록은 매 턴 새로 만들지 않고 다시 쓴다.
    internal List<Dice> KeptDice => keptDice;
    internal List<int> KeptValues => keptValues;
    internal List<DiceScoreBonus> DiceScoreBonuses => diceScoreBonuses;
    internal TurnResolutionPresenter ResolutionPresenter => resolutionPresenter;


    //티켓 아이템 먹었을 때 호출할 함수
    public void UpgradeHand(HandType handType, float amount)
    {
        HandMult(handType) *= amount;
    }

    // 티켓으로 올린 족보 배수를 인스펙터 기본값으로 되돌린다 (재시작, 심연의 딜러)
    public void ResetHandMultipliers()
    {
        if (defaultHandMultipliers == null) return;
        for (int i = 0; i < AllHandTypes.Length; i++)
            HandMult(AllHandTypes[i]) = defaultHandMultipliers[i];
    }

    // 티켓 도감에 보여줄 현재 배수
    public float GetHandMultiplier(HandType handType) => HandMult(handType);

    // 족보 종류별 배수 칸. 티켓 강화·도감·되돌리기가 모두 이 한 곳을 거친다.
    // 없는 족보 값이면 임시 칸(1배)을 돌려줘서 실제 배수는 바뀌지 않는다.
    ref float HandMult(HandType handType)
    {
        switch (handType)
        {
            case HandType.HighCard: return ref multHighCard;
            case HandType.OnePair: return ref multOnePair;
            case HandType.TwoPair: return ref multTwoPair;
            case HandType.Triple: return ref multTriple;
            case HandType.FullHouse: return ref multFullHouse;
            case HandType.FourOfAKind: return ref multFourOfAKind;
            case HandType.Straight: return ref multStraight;
            case HandType.Yacht: return ref multYacht;
            default:
                unknownHandMult = 1.0f;
                return ref unknownHandMult;
        }
    }

    public void OnEnemyKilled()
    {
        if (enemy == null || !enemy.IsDead || isStageClearing || enemyDeathHandled)
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

        bool showCapture = pendingPeppermintSuccess &&
            peppermintCaptureEffect != null &&
            peppermintCaptureCenter != null &&
            peppermintVisualPrefab != null;
        StartCoroutine(PlayEnemyDeathThenClear(enemy, showCapture));
    }

    private IEnumerator PlayEnemyDeathThenClear(Enemy defeatedEnemy, bool showCapture)
    {
        // 사망 판정·포획 확률은 OnEnemyKilled에서 한 번만 처리합니다.
        // 연출 시작 전에 피격/색상/Animator를 정리하여 흡수 도중 사라지는 충돌을 막습니다.
        if (defeatedEnemy == null) yield break;
        // 사망은 이미 확정했지만 마지막 타격의 섬광/화염/다크가 끝날 때까지 유지합니다.
        yield return defeatedEnemy.WaitForHitPresentation();
        if (defeatedEnemy == null || enemy != defeatedEnemy || !defeatedEnemy.BeginDeathPresentation()) yield break;
        if (showCapture)
        {
            yield return peppermintCaptureEffect.PlayCapture(
                defeatedEnemy.transform,
                peppermintCaptureCenter.position,
                peppermintVisualPrefab);
            if (defeatedEnemy != null) defeatedEnemy.gameObject.SetActive(false);
        }
        else
        {
            // 포획 연출 참조가 누락되어도 일반 사망 후 정상적으로 보상을 지급합니다.
            yield return defeatedEnemy.PlayNormalDeathPresentation();
        }

        ProcessStageClear(false);
    }

    // --- 스테이지 클리어 공통 시스템 ---

    public void ProcessStageClear(bool fromPeppermint)
    {
        if (isStageClearing) return; //이미 클리어 처리 중이면 중복 실행 방지
        isStageClearing = true;
        StartCoroutine(PlayStageClearSfxDelayed());

        // 이번 전투 한정 디버프(도둑·족쇄)를 제거하고, 왼쪽 위 디버프 표시에서 연기처럼 사라지는 모습을 보여 준다.
        var vanishing = new List<DiceData1>();
        foreach (var die in deckManager.masterDeck)
            if (die != null && (die.IsTemporaryDebuff || Debuffs.IsFightOnly(die))) vanishing.Add(die);
        RemoveCombatDebuffs(temporaryOnly: true);
        Debuffs.RemoveFightOnlyDebuffs();
        currentCombatDebuffs.Clear();
        DevilDiceBoss.ClearCurses(deckManager.masterDeck);
        if (vanishing.Count > 0)
        {
            GameObject deckButton = Hud.Find("Deck_BTN");
            DebuffFeedback.PlayTemporaryVanish(deckButton != null ? deckButton.transform : null, vanishing);
        }
        DebuffTrackerHud.Instance?.Refresh();

        // 숙원의 방랑자: 체력 1로 버틴 전투에서 이겼으므로 전투 전 체력으로 되돌린다.
        if (wandererRestoreHP > 0)
        {
            currentPlayerHP = Mathf.Min(wandererRestoreHP, playerMaxHP);
            wandererRestoreHP = 0;
            UpdateMainUI("");
        }

        // 공허 바이옴에서 보스를 잡았다면 최종 게임 클리어 처리
        if (currentBiome != null && currentBiome.biomeType == BiomeType.Void)
        {
            ShowGameClear();
            return;
        }

        // 처치 골드, 보스 처치 회복, 클리어 피규어 효과, 피규어 획득은 StageClearRewards가 이 순서대로 준다.
        StageClearRewards.Give(this, pendingPeppermintSuccess);

        Invoke(nameof(ShowClownEvent), 1.0f);
    }

    // 몇 번째 바이옴인지(0부터).
    public int CurrentBiomeTier => stageProgression.biomeIndex;

    // 이 바이옴에서 이미 치른 전투 수(지금 전투 제외). 지나친 노드는 싸우지 않았으므로 방문 기록만 센다.
    public int CombatsClearedInBiome
    {
        get
        {
            int count = 0;
            List<int> visited = stageProgression.visitedNodeIds;
            if (visited == null) return 0;
            for (int i = 0; i < visited.Count; i++)
            {
                int id = visited[i];
                if (id == stageProgression.currentNodeId) continue;
                MapNode node = stageProgression.FindNode(id);
                // 전투 칸(일반·엘리트·차원문)에 보스까지 센다. MapTableData.IsCombatNode는 보스를 빼므로 따로 더한다.
                if (node != null && (MapTableData.IsCombatNode(node.type) || node.type == MapNodeType.Boss)) count++;
            }
            return count;
        }
    }

    // 캐릭터 선택에서 고른 시작 체력. 고르지 않고 들어왔으면 기본 최대 체력.
    public static int StartingMaxHP => CharacterStatus.Selected != null && CharacterStatus.Selected.HP > 0
        ? CharacterStatus.Selected.HP
        : BalanceConfig.Current.playerMaxHP;

    // 몬스터 처치 1초 뒤 ProcessStageClear가 Invoke로 부른다(이름으로 불리므로 이름을 바꾸지 않는다).
    // 조우자는 지도의 조우 칸에서만 나오므로 여기서는 전리품 선택만 띄운다.
    public void ShowClownEvent()
    {
        ShowLootSelection();
    }

    internal void ShowGameClear(bool demo = false)
    {
        if (gameClearPanel != null)
        {
            gameClearPanel.SetActive(true);
        }

        if (gameClearText != null)
        {
            gameClearText.text = demo
                ? LocalizationManager.GetUi(
                    "UI_DEMO_CLEAR",
                    "<color=#00FF00>DEMO CLEAR!</color>\n\n축하합니다!\n체험판의 세 바이옴을 모두 정복했습니다!\n이어지는 모험은 정식 출시에서 만나요!")
                : LocalizationManager.GetUi(
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
        bool canAct = !IsDiceInputLocked;

        ui?.SetRollButtonInteractable(canAct && RemainingRerolls > 0 && hasDiceToRoll);

        ui?.SetFinishButtonInteractable(canAct && keptCount == 5);

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
        if (playerHurtAudioEvent != null)
        {
            if (AudioControl.Instance != null)
            {
                AudioControl.Instance.PlaySFX(playerHurtAudioEvent);
                return;
            }

            if (sfxSource != null)
                playerHurtAudioEvent.PlayOneShot(sfxSource);
            return;
        }

        Sfx.Play(SfxId.Player_Hurt);
    }

    IEnumerator PlayStageClearSfxDelayed()
    {
        yield return new WaitForSeconds(0.4f);
        Sfx.Play(SfxId.Stage_Clear);
    }

    private readonly MainUIPresenter mainUIPresenter = new MainUIPresenter();

    public void UpdateMainUI(string handName)
    {
        // 기존 호출부 호환을 위해 매개변수를 유지합니다. 표시명은 현재 킵 상태에서 갱신합니다.
        flameDamageThisTurn = figureBonusFlameDamage; // 화상 데미지 상태 저장 연동
        mainUIPresenter.Refresh(this);
    }

    public void PromptShopChoice() { ui?.HideResult(); ui?.ShowShopChoice(); }
    public void GoToShop() { ui?.HideShopChoice(); shopManager?.OpenShop(); }
    // 전투 후 "상점 갈래?" 선택에서 다음으로 넘어가기 버튼(Awake에서 연결). 지도로 돌아가거나 보스 처치 후면 바이옴 선택으로 간다.
    public void SkipShopAndNextStage()
    {
        ui?.HideShopChoice();
        OnShopClosedDuringMap();
    }

    // 바이옴 선택 버튼을 눌렀을 때 실행될 함수
    public void ApplySelectedBiome(BiomeType selectedType)
    {
        if (biomeSelectionPanel != null) biomeSelectionPanel.ClosePanel();

        stageProgression.SetNewBiome(biomeList, selectedType); // 이관된 로직
        // 비차원(Void)도 지도로 진행한다. 적·엘리트 노드는 MapGenerator 가 차원문으로 바꾼다.
        MapTableData table = GetMapTable();
        stageProgression.BeginBiome(table, selectedType);
        MapFlow.ShowMapScreen();

        if (GameSaveManager.Instance != null)
            GameSaveManager.Instance.SaveGame(this, InventoryManager.Instance, shopManager);
    }


    public void GoToMainMenu()
    {
        if (isStartingCombat) return;
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
        if (isStartingCombat) return;
        permanentFigureMultiplier = 0f;
        //재시작시 가짜 주사위 참조 안전하게 비우기
        currentCombatDebuffs.Clear();
        Debuffs.DropDrawTriggers(); // 골드는 아래에서 초기값으로 다시 맞춘다

        // 낡은 변수 대신 우리가 만든 상태 초기화 함수를 씀
        playerStatus.ResetStatus(StartingMaxHP);

        //덱 초기화 (상점에서 샀던 특수 주사위들을 모두 버리고 기본 11개 + 여정 주사위 1개로)
        deckManager.InitializeMasterDeck(JourneyDice.FindSource());

        //골드 초기화 (ShopManager 참조)
        if (shopManager != null)
        {
            shopManager.currentGold = BalanceConfig.Current.startGold; // 초기 소지금
            ui?.UpdateGoldUI(shopManager.currentGold);
            // 재시작 및 메인 이동 시 초기 소지금 카운팅 연출 실행 (또는 초기화용)
            if (GoldCounter.Instance != null) GoldCounter.Instance.SetInstant(shopManager.currentGold);
        }

        //인벤토리 초기화 (방금 만든 함수 호출)
        InventoryManager.Instance?.ClearAllSlots();

        //스낵 및 특수 상태 버프 일괄 초기화
        turnContext.ResetForNewTurn();
        stageContext.ResetForNewStage();
        turnContext.figureBonusRerolls = 0;
        Debug.Log("게임이 완전히 초기화되었습니다. 다시 시작합니다.");
        //티켓으로 올렸던 배수를 다시 기본값으로 돌려줌
        ResetHandMultipliers();

        //몬스터 초기화
        enemy.ResetMonsterIndex();
        stageProgression.InitFirstBiome(biomeList);
        // 새 지도로 다시 시작
        MapFlow.BeginNewMapRun();
    }

    public IEnumerator ShowGameOverPanelDelayed()
    {
        RemoveCombatDebuffs(temporaryOnly: true);
        currentCombatDebuffs.Clear();
        yield return new WaitForSeconds(1.2f); // "게임 오버"
        Sfx.Play(SfxId.Game_Over);

        if (gameOverPanel != null)
        {
            gameOverPanel.SetupGameOver(currentStage);
        }
    }

    // ===== 지도 =====
    // 지도 진행은 MapFlowController, 지도 화면의 HUD 배치는 TravelHudLayout이 맡습니다.
    // 아래는 바깥 스크립트(MapPanel, RestPanel, LootSelectionPanel 등)가 예전 이름 그대로 부를 수 있게 남긴 연결입니다.
    public MapTableData GetMapTable() => MapFlow.GetMapTable();
    public string GetMapStageLabel(string biomeName) => MapFlow.GetMapStageLabel(biomeName);
    public bool CanRollJourney() => MapFlow.CanRollJourney();
    public bool IsMapNodeSelectable(int nodeId) => MapFlow.IsMapNodeSelectable(nodeId);
    public void EnterMapNode(int nodeId) => MapFlow.EnterMapNode(nodeId);
    // 여정 주사위(찾기·면 그림·굴리기)는 JourneyDice가 맡는다.
    public DiceData1 GetJourneyDiceData() => JourneyDice.FindInDeck(this);
    public Sprite GetJourneyFaceSprite(DiceData1 die, int faceIndex) => JourneyDice.FaceSprite(this, die, faceIndex);
    public Sprite RandomJourneyFaceSprite() => JourneyDice.RandomFaceSprite(this);
    public JourneyRollResult RollJourneyFace() => JourneyDice.Roll(this);
    public bool ApplyJourneyResult(JourneyRollResult result, out int appliedAmount, bool deferGoldUi) => MapFlow.ApplyJourneyResult(result, out appliedAmount, deferGoldUi);
    public void ReturnToMap() => MapFlow.ReturnToMap();
    public void OnMapLootFinished() => MapFlow.OnMapLootFinished();
    public void OnShopClosedDuringMap() => MapFlow.OnShopClosedDuringMap();

    // 디버프 표시처럼 실행 중에 붙는 HUD가 기존 HUD 위치를 기준으로 자리를 잡을 때 씁니다.
    internal RectTransform HudRect(string objectName) => Hud.RectOf(objectName);

    // 이벤트는 선언한 클래스 안에서만 부를 수 있어서 MapFlowController가 이 함수로 알립니다.
    internal void RaiseMapCombatStarted() => MapCombatStarted?.Invoke();

    // 주사위 프리팹의 Dice 컴포넌트. 여정 주사위 굴림 연출이 면을 여러 번 바꿀 때 매번 꺼내지 않도록 한 번만 꺼내 둔다.
    // 이 씬의 DiceManager가 들고 있어서 씬이 바뀌면 같이 정리된다.
    Dice dicePrefabDice;
    internal Dice DicePrefabDice => dicePrefabDice != null ? dicePrefabDice : (dicePrefabDice = dicePrefab != null ? dicePrefab.GetComponent<Dice>() : null);
}
