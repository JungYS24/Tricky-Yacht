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
    public int debuffRerollPenalty;
    public float snackBonusMult = 0f;
    public int snackBonusChips = 0;
    public int snackBonusRerolls = 0;
    public int figureBonusRerolls = 0;

    public void ResetForNewTurn()
    {
        debuffRerollPenalty = 0;
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

    [Header("지도 시스템")]
    public bool useMapFlow;
    public TextAsset mapTableAsset;
    public MapPanel mapPanel;
    public RestPanel restPanel;
    public OpelPanel opelPanel;
    public MapIconSet mapIconSet;

    // 다음 전투에 강제할 몬스터. Enemy.Initialize가 쓰면 비웁니다.
    [System.NonSerialized] public MonsterDataSO pendingForcedMonster;
    // TODO: 엘리트 몬스터 풀
    [System.NonSerialized] public bool isEliteBattle;
    [System.NonSerialized] public bool mapBossBattle;

    MapTableData cachedMapTable;
    bool pendingBiomeTransition;

    public bool UsesMapFlow => useMapFlow && (TutorialManager.Instance == null || !TutorialManager.Instance.isTutorialActive);

    [Header("조우자 특수 효과 상태 (임시 저장용)")]
    [HideInInspector] public bool isNextEnemyHPBoosted = false; // 눈먼 점술가 패널티
    [HideInInspector] public bool isNextCombatHPTiedToOne = false; // 숙원의 방랑자 패널티
    [HideInInspector] public int extraShopSlots = 0; //  슬롯 영구 확장
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

        board = new DiceBoardController(dicePrefab, keepSlotParent, rollSlotParent, serializedActiveDice, deckManager, ApplyDrawDebuff);

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
            currentPlayerHP = playerMaxHP;
            deckManager.InitializeMasterDeck(JourneyDice.FindSource());

            // 첫 시작은 무조건 숲(Forest)으로 고정
            currentBiome = biomeList.Find(b => b.biomeType == BiomeType.Forest);
            saveStateReady = true;
            if (UsesMapFlow)
                BeginNewMapRun();
            else
                StartNewStage();
        }

        //튜토리얼 종료 후 메인 게임 진입 시, 현재 설정된 1스테이지(숲) 바이옴의 브금을 강제로 재생!
        if (BGMManager.Instance != null && currentBiome != null)
        {
            BGMManager.Instance.ChangeBGM(currentBiome.biomeBGM);
        }
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
            bool resumeCombat = UsesMapFlow && data.mapSaveVersion >= 1 && !string.IsNullOrEmpty(data.savedMonsterName);
            bool idleMap = UsesMapFlow && !resumeCombat;
            GameStateRestorer.Restore(this, data, defaultMaxRerolls, ref pendingPeppermintSuccess, idleMap);
            if (UsesMapFlow)
                RestoreMapProgress(data);

            if (idleMap)
            {
                isCalculating = false;
                ShowMapScreen();
                if (data.mapSaveVersion >= 1 && !stageProgression.currentNodeCleared && stageProgression.currentNodeId >= 0)
                    OpenCurrentNode(stageProgression.FindNode(stageProgression.currentNodeId));
                FigureEffectManager.Instance?.EvaluateLowHPTriggers(this, shopManager);
                saveStateReady = true;
                return;
            }

            if (UsesMapFlow)
                ShowCombatScreen();

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

    internal readonly List<DiceData1> currentCombatDebuffs = new List<DiceData1>();

    private void AddNodeDebuffs()
    {
        MapNode node = stageProgression.FindNode(stageProgression.currentNodeId);
        if (node == null) return;
        switch (node.type)
        {
            case MapNodeType.Elite:
                for (int i = 0; i < 3; i++)
                    AddCombatDebuff(UnityEngine.Random.Range(0, 2) == 0 ? DebuffDiceType.Thief : DebuffDiceType.Shackle);
                break;
            case MapNodeType.Portal:
                AddCombatDebuff(UnityEngine.Random.Range(0, 2) == 0 ? DebuffDiceType.Spine : DebuffDiceType.Curse);
                break;
            case MapNodeType.Boss:
                AddCombatDebuff(DebuffDiceType.Fake);
                break;
        }
    }

    private void AddCombatDebuff(DebuffDiceType type)
    {
        DiceItemSO source = FindDebuffSource(type);
        DiceData1 die = deckManager.AddDebuffDice(type, source);
        if (source == null) ApplyDebuffAppearance(die);
        currentCombatDebuffs.Add(die);
    }

    private DiceItemSO FindDebuffSource(DebuffDiceType type)
    {
        var database = GameSaveManager.Instance != null ? GameSaveManager.Instance.masterItemDatabase : null;
        return database != null ? database.Find(item => item is DiceItemSO dice && dice.debuffType == type) as DiceItemSO : null;
    }

    // 기존 아이템 DB의 눈별 에셋에서 외형을 모은다. 디버프 종류와 눈 값은 덱 생성 규칙을 유지한다.
    internal void ApplyDebuffAppearance(DiceData1 die)
    {
        DiceItemSO sourceAsset = FindDebuffSource(die.debuffType);
        if (sourceAsset != null)
        {
            die.customDiceShell = sourceAsset.customDiceShell;
            die.customFaceSprites = sourceAsset.customFaceSprites != null ? (Sprite[])sourceAsset.customFaceSprites.Clone() : null;
            return;
        }
        if (die.debuffType == DebuffDiceType.Fake)
        {
            die.customDiceShell = fakeDiceShell;
            die.customFaceSprites = new Sprite[] { fakeDiceFace, fakeDiceFace, fakeDiceFace, fakeDiceFace, fakeDiceFace, fakeDiceFace };
            return;
        }
        string assetName;
        switch (die.debuffType)
        {
            case DebuffDiceType.Thief: assetName = "Thief Dice"; break;
            case DebuffDiceType.Shackle: assetName = "Captive Dice"; break;
            case DebuffDiceType.Spine: assetName = "Spine_Dice_"; break;
            case DebuffDiceType.Curse: assetName = "Cursed Dice_"; break;
            default: return;
        }
        bool numbered = die.debuffType == DebuffDiceType.Spine || die.debuffType == DebuffDiceType.Curse;
        var sprites = new Sprite[6];
        for (int i = 0; i < 6; i++)
        {
            string sourceName = assetName + (numbered ? (i + 1).ToString() : "");
            DiceItemSO source = GameSaveManager.Instance != null
                ? GameSaveManager.Instance.masterItemDatabase.Find(item => item is DiceItemSO && item.name == sourceName) as DiceItemSO
                : null;
            if (source == null) continue;
            if (i == 0) die.customDiceShell = source.customDiceShell;
            sprites[i] = source.customFaceSprites != null && source.customFaceSprites.Length > 0
                ? source.customFaceSprites[0] : null;
        }
        die.customFaceSprites = sprites;
    }

    public void RemoveCombatDebuffs(bool temporaryOnly = false, bool currentEnemyOnly = false)
    {
        System.Predicate<DiceData1> match = die => die != null && die.IsDebuff
            && (!temporaryOnly || die.IsTemporaryDebuff)
            && (!currentEnemyOnly || currentCombatDebuffs.Contains(die));
        board?.RemoveDiceMatching(match);
        if (currentEnemyOnly)
        {
            deckManager.masterDeck.RemoveAll(match);
            deckManager.drawPile.RemoveAll(match);
            deckManager.discardPile.RemoveAll(match);
        }
        else if (temporaryOnly) deckManager.RemoveTemporaryDebuffDice();
        else deckManager.RemoveAllDebuffDice();
        currentCombatDebuffs.RemoveAll(match);
    }

    public void StartNewStage()
    {
        if (isStartingCombat) return;
        board.ClearBoard();
        DiscardDrawTriggers();
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
        currentCombatDebuffs.Clear();

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

        AddNodeDebuffs();

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
        CacheHud();
        hudObjects.TryGetValue("Deck_BTN", out GameObject deckButton);
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
        FlushDrawTriggers();
    }

    // 보스 등장 연출에서 가짜 주사위로 바뀌기 전에 보여 줄 일반 주사위 눈(6).
    Sprite NormalDiceFace()
    {
        Dice template = dicePrefab != null ? dicePrefab.GetComponent<Dice>() : null;
        if (template == null || template.diceFaceSprites == null || template.diceFaceSprites.Length == 0) return null;
        return template.diceFaceSprites[template.diceFaceSprites.Length - 1];
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

    // 뽑는 즉시 효과를 적용하고, 화면 연출은 주사위가 보드에 놓인 뒤 FlushDrawTriggers에서 보여 준다.
    private readonly List<DebuffFeedback.DrawTrigger> pendingDrawTriggers = new List<DebuffFeedback.DrawTrigger>();

    private void ApplyDrawDebuff(DiceData1 die)
    {
        if (die == null || !die.TriggersDebuffOnDraw) return;
        if (die.debuffType == DebuffDiceType.Thief && shopManager != null)
        {
            int stolen = Mathf.Min(100, Mathf.Max(0, shopManager.currentGold));
            shopManager.currentGold -= stolen;
            // 화면 골드 숫자는 코인이 빠져나가는 연출에서 내린다.
            pendingDrawTriggers.Add(new DebuffFeedback.DrawTrigger { data = die, stolenGold = stolen });
        }
        else if (die.debuffType == DebuffDiceType.Shackle)
        {
            turnContext.debuffRerollPenalty++;
            pendingDrawTriggers.Add(new DebuffFeedback.DrawTrigger { data = die });
        }
    }

    private void FlushDrawTriggers()
    {
        if (pendingDrawTriggers.Count == 0) return;
        var triggers = new List<DebuffFeedback.DrawTrigger>(pendingDrawTriggers);
        pendingDrawTriggers.Clear();
        DebuffFeedback.PlayDrawTriggers(triggers, activeDiceList, ui, shopManager);
    }

    private IEnumerator FlushDrawTriggersAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        FlushDrawTriggers();
    }

    // 연출을 보여 주지 못하고 넘어가는 경우(새 전투·재시작) 화면 골드만 실제 값에 맞춘다.
    private void DiscardDrawTriggers()
    {
        if (pendingDrawTriggers.Count == 0) return;
        pendingDrawTriggers.Clear();
        shopManager?.SyncGoldDisplay();
    }

    public void ForceUpdateUI() => HandleDiceChanged();

    public bool TryReplaceUnkeptDiceFromDeck()
    {
        if (board == null || IsDiceInputLocked) return false;
        if (ShopManager.IsShopOpen || FigureDetailPanel.IsPanelOpen || LootSelectionPanel.IsPanelOpen) return false;
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
        if (IsDiceInputLocked || RemainingRerolls <= 0 || ShopManager.IsShopOpen || FigureDetailPanel.IsPanelOpen || LootSelectionPanel.IsPanelOpen) return;

        GameSaveManager.Instance?.SaveGame(this, InventoryManager.Instance, shopManager); // 강제 종료 시에도 굴리기 전 정상 상태 유지
        isRolling = true; // 굴림 상태 켜기
        ui?.SetRollButtonInteractable(false);   //즉시 버튼 비활성화
        ui?.SetFinishButtonInteractable(false); //주사위가 굴러가는 동안 끝내기 버튼도 막기

        //리롤 버튼 클릭 및 주사위 굴러가는 소리

        CameraShake.Instance.Shake(0.1f, 0.1f);

        board.RollUnkept();

        // 클로버 스탬프가 찍힌 주사위가 있으면 리롤 횟수 유지
        if (board.ActiveDice.All(d => d.myData.stampType != StampType.Clover) ||
            UnityEngine.Random.Range(0, 10) >= 3)
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
        if (IsDiceInputLocked || ShopManager.IsShopOpen || FigureDetailPanel.IsPanelOpen || LootSelectionPanel.IsPanelOpen) return;

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
        try { yield return FinishTurnCore(); }
        finally { isResolvingTurn = false; }
    }

    private IEnumerator FinishTurnCore()
    {
        //데이터 독립 추출 (UI 버퍼 의존 X, 킵된 주사위만 스냅샷 수집)
        board.CollectKept(keptDice, keptValues);

        // 안전장치: 킵한 주사위가 정확히 5개가 아니면 결산 중단
        if (keptValues.Count != 5)
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

        yield return resolutionPresenter.PlayDiceValues(keptDice, resolutionTiming);

        // 가시 디버프는 눈이 뜬 직후 회복·점수 계산보다 먼저 찌른다. 체력이 0이 되면 남은 결산 없이 사망 처리로 끝낸다.
        var spineDice = new List<Dice>();
        foreach (var d in keptDice)
            if (d != null && d.myData != null && d.myData.debuffType == DebuffDiceType.Spine && d.currentValue > 0) spineDice.Add(d);
        bool downedByThorn = false;
        yield return CombatFlowController.ProcessDebuffThorn(this, spineDice, TurnCalculator.CalculateSpineDamage(keptDice), () => downedByThorn = true);
        if (downedByThorn)
        {
            if (snacks != null) yield return snacks.CompleteResolution();
            ui?.ClearMergedHandDamageText();
            yield break;
        }

        yield return resolutionPresenter.PlayHand(this, keptDice, handRank, handMult, resolutionTiming);

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

        // 면 효과가 Coin이라 골드를 준 주사위(코인 날아가기 연출 대상)
        var coinDice = new List<Dice>();
        foreach (var d in keptDice)
        {
            if (d == null || d.myData == null || d.currentValue <= 0) continue;
            if (d.myData.GetFaceEffect(d.ResolveFaceIndex()) == SpecialDieEffect.Coin) coinDice.Add(d);
        }

        // 완벽하게 쪼개진 수치들을 전투 지휘관(CombatFlowController)에게 전달
        yield return StartCoroutine(CombatFlowController.ProcessTurnResolution(this, total.damage, darkDamageTotal, finalHeal, expectedGold, totalFlameDamage, handName, calcResult.selfDamage, coinDice
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

        // 이번 전투 한정 디버프(도둑·족쇄)를 제거하고, 덱 버튼에서 연기처럼 사라지는 모습을 보여 준다.
        var vanishing = new List<Sprite>();
        foreach (var die in deckManager.masterDeck)
            if (die != null && die.IsTemporaryDebuff) vanishing.Add(DebuffFeedback.DieSprite(die));
        RemoveCombatDebuffs(temporaryOnly: true);
        currentCombatDebuffs.Clear();
        if (vanishing.Count > 0)
        {
            CacheHud();
            hudObjects.TryGetValue("Deck_BTN", out GameObject deckButton);
            DebuffFeedback.PlayTemporaryVanish(deckButton != null ? deckButton.transform : null, vanishing);
        }

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
            //스테이지 클리어 기본 골드 카운팅 연출 실행: GrantGold에서 지급과 UI 갱신을 함께 처리
            shopManager.GrantGold(baseClearReward);
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

        // 지도에서는 조우자가 노드로만 나옵니다.
        if (useMapFlow)
        {
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

    private readonly MainUIPresenter mainUIPresenter = new MainUIPresenter();

    public void UpdateMainUI(string handName)
    {
        // 기존 호출부 호환을 위해 매개변수를 유지합니다. 표시명은 현재 킵 상태에서 갱신합니다.
        flameDamageThisTurn = figureBonusFlameDamage; // 화상 데미지 상태 저장 연동
        mainUIPresenter.Refresh(this);
    }

    public void PromptShopChoice() { ui?.HideResult(); ui?.ShowShopChoice(); }
    public void GoToShop() { ui?.HideShopChoice(); shopManager?.OpenShop(); }
    public void SkipShopAndNextStage()
    {
        ui?.HideShopChoice();
        if (UsesMapFlow)
            OnShopClosedDuringMap();
        else
            NextStage();
    }

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
        if (UsesMapFlow)
        {
            MapTableData table = GetMapTable();
            if (selectedType == BiomeType.Void)
            {
                int voidStage = table != null ? table.Config.BiomeCountToVoid * 10 + 1 : 101;
                currentStage = voidStage;
                ShowCombatScreen();
                StartNewStage();
            }
            else
            {
                stageProgression.BeginBiome(table, selectedType);
                ShowMapScreen();
            }

            if (GameSaveManager.Instance != null)
                GameSaveManager.Instance.SaveGame(this, InventoryManager.Instance, shopManager);
            return;
        }

        StartNewStage();

        if (GameSaveManager.Instance != null)
        {
            GameSaveManager.Instance.SaveGame(this, InventoryManager.Instance, shopManager);
        }
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
        pendingDrawTriggers.Clear(); // 골드는 아래에서 초기값으로 다시 맞춘다

        // 낡은 변수 대신 우리가 만든 상태 초기화 함수를 씀
        playerStatus.ResetStatus(100);

        //덱 초기화 (상점에서 샀던 특수 주사위들을 모두 버리고 기본 11개 + 여정 주사위 1개로)
        deckManager.InitializeMasterDeck(JourneyDice.FindSource());

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
        if (UsesMapFlow)
            BeginNewMapRun();
        else
            StartNewStage();
    }

    public IEnumerator ShowGameOverPanelDelayed()
    {
        RemoveCombatDebuffs(temporaryOnly: true);
        currentCombatDebuffs.Clear();
        yield return new WaitForSeconds(1.2f); // "게임 오버"

        if (gameOverPanel != null)
        {
            gameOverPanel.SetupGameOver(currentStage);
        }
    }

    public MapTableData GetMapTable()
    {
        if (cachedMapTable == null && mapTableAsset != null)
            cachedMapTable = MapTableData.Parse(mapTableAsset.text);
        return cachedMapTable;
    }

    public string GetMapStageLabel(string biomeName)
    {
        if (!useMapFlow || stageProgression.currentMap == null) return null;
        MapTableData table = GetMapTable();
        int total = table != null ? table.Config.MaxPathLength : MapPathLength(stageProgression.currentMap);
        MapNode node = stageProgression.FindNode(stageProgression.currentNodeId);
        if (node == null) return biomeName;
        if (node.type == MapNodeType.Boss) return $"{biomeName} BOSS";
        return $"{biomeName} {node.row + 1}/{total}";
    }

    static int MapPathLength(MapData map)
    {
        int length = 1;
        if (map == null || map.nodes == null) return length;
        for (int i = 0; i < map.nodes.Count; i++)
        {
            if (map.nodes[i] != null && map.nodes[i].row > length)
                length = map.nodes[i].row;
        }
        return length;
    }

    public bool CanRollJourney()
    {
        if (!UsesMapFlow) return false;
        if (stageProgression.pendingJourneyMove > 0) return false;
        if (stageProgression.currentNodeId >= 0 && !stageProgression.currentNodeCleared) return false;
        return true;
    }

    public bool IsMapNodeSelectable(int nodeId)
    {
        MapNode node = stageProgression.FindNode(nodeId);
        if (node == null) return false;
        int move = stageProgression.pendingJourneyMove;
        if (move <= 0) return false;
        if (stageProgression.currentNodeId >= 0 && !stageProgression.currentNodeCleared) return false;
        MapTableData table = GetMapTable();
        bool forceStop = table == null || table.Config.ShopRowForceStop;
        return MapJourney.IsReachable(stageProgression.currentMap, stageProgression.currentNodeId, nodeId, move, forceStop);
    }

    public void EnterMapNode(int nodeId)
    {
        if (!UsesMapFlow) return;
        MapNode node = stageProgression.FindNode(nodeId);
        if (node == null || !IsMapNodeSelectable(nodeId)) return;

        MapTableData table = GetMapTable();
        bool forceStop = table == null || table.Config.ShopRowForceStop;
        int move = stageProgression.pendingJourneyMove;
        List<int> passed = MapJourney.PassedOnWay(stageProgression.currentMap, stageProgression.currentNodeId, nodeId, move, forceStop);
        if (stageProgression.passedNodeIds == null) stageProgression.passedNodeIds = new List<int>();
        for (int i = 0; i < passed.Count; i++)
        {
            if (!stageProgression.passedNodeIds.Contains(passed[i]))
                stageProgression.passedNodeIds.Add(passed[i]);
        }

        stageProgression.pendingJourneyMove = 0;
        stageProgression.currentNodeId = nodeId;
        stageProgression.currentNodeCleared = false;
        if (stageProgression.visitedNodeIds == null) stageProgression.visitedNodeIds = new List<int>();
        if (!stageProgression.visitedNodeIds.Contains(nodeId))
            stageProgression.visitedNodeIds.Add(nodeId);

        pendingBiomeTransition = false;
        if (table != null) stageProgression.ApplyNodeDifficulty(node, table.Config);
        SaveMapProgress();
        OpenCurrentNode(node);
    }

    // 지도 굴림에 쓰는 덱의 여정 주사위. 못 찾으면 null이며, 굴림은 기본 면 구성으로 대신한다.
    public DiceData1 GetJourneyDiceData() => JourneyDice.Find(deckManager.masterDeck);

    // 여정 주사위 면 스프라이트. 에셋 스프라이트가 없으면 기본 주사위 눈금 이미지로 대신한다.
    public Sprite GetJourneyFaceSprite(DiceData1 die, int pips)
    {
        if (die != null && die.customFaceSprites != null && die.customFaceSprites.Length > 0)
        {
            int index = die.customFaceSprites.Length == 1 ? 0 : Mathf.Clamp(pips - 1, 0, die.customFaceSprites.Length - 1);
            if (die.customFaceSprites[index] != null) return die.customFaceSprites[index];
        }
        Dice prefabDice = dicePrefab != null ? dicePrefab.GetComponent<Dice>() : null;
        if (prefabDice != null && prefabDice.diceFaceSprites != null && prefabDice.diceFaceSprites.Length > 0)
            return prefabDice.diceFaceSprites[Mathf.Clamp(pips - 1, 0, prefabDice.diceFaceSprites.Length - 1)];
        return null;
    }

    // 굴림 연출 중 빠르게 바꿔 보여 줄 면 스프라이트 하나(무작위 면)
    public Sprite RandomJourneyFaceSprite()
    {
        DiceData1 die = GetJourneyDiceData() ?? JourneyDice.CreateDefault();
        int pips = die.faceValues[JourneyDice.RollFaceIndex(die)];
        return GetJourneyFaceSprite(die, pips);
    }

    // 여정 주사위의 면 하나를 굴려 결과를 계산한다. 상태는 바꾸지 않는다.
    // 덱에서 여정 주사위를 못 찾아도 기본 면 구성({1~6}, {없음×3, 가시, 하트, 코인})으로 굴려 멈추지 않는다.
    public JourneyRollResult RollJourneyFace()
    {
        DiceData1 die = GetJourneyDiceData();
        if (die == null || die.faceValues == null || die.faceValues.Length == 0)
            die = JourneyDice.CreateDefault();

        int faceIndex = JourneyDice.RollFaceIndex(die);
        int pips = die.faceValues[faceIndex];
        SpecialDieEffect effect = die.GetFaceEffect(faceIndex);
        MapTableData table = GetMapTable();

        var result = new JourneyRollResult
        {
            faceIndex = faceIndex,
            pips = pips,
            effect = effect,
            faceSprite = GetJourneyFaceSprite(die, pips)
        };

        // 지도에서는 면 효과만 쓴다. 코팅·위성 효과는 전투 결산에서만 적용한다.
        switch (effect)
        {
            case SpecialDieEffect.Thorn:
                result.outcome = JourneyOutcome.Damage;
                result.amount = Mathf.Max(0, pips);
                break;
            case SpecialDieEffect.Heart:
                result.outcome = JourneyOutcome.Heal;
                float healMult = FigureEffectManager.Instance != null ? FigureEffectManager.Instance.GetHealMultiplier() : 1f;
                result.amount = Mathf.Max(0, TurnCalculator.CalculateHeartHeal(pips, healMult));
                break;
            case SpecialDieEffect.Coin:
                result.outcome = JourneyOutcome.Gold;
                int goldPerPip = table != null ? table.Config.JourneyMapGoldPerPip : 10;
                result.amount = Mathf.Max(0, pips * goldPerPip);
                break;
            default:
                result.outcome = JourneyOutcome.Move;
                result.amount = Mathf.Max(1, pips);
                break;
        }
        return result;
    }

    // 굴림 결과를 적용한다. 이동이면 true(이동 칸 수 대기), 그 외 효과는 바로 적용하고 false를 돌려준다.
    public bool ApplyJourneyResult(JourneyRollResult result)
    {
        return ApplyJourneyResult(result, out _, false);
    }

    // appliedAmount: 실제로 깎인 체력 / 회복된 체력 / 지급된 골드(이동이면 0)
    // deferGoldUi: 골드 숫자 갱신을 코인 날아가기 연출에 맡길 때 true(실제 골드는 바로 올라간다)
    public bool ApplyJourneyResult(JourneyRollResult result, out int appliedAmount, bool deferGoldUi)
    {
        appliedAmount = 0;
        if (!CanRollJourney()) return false;
        bool move = result.outcome == JourneyOutcome.Move;
        if (move)
            stageProgression.pendingJourneyMove = Mathf.Max(1, result.amount);
        else
        {
            stageProgression.pendingJourneyMove = 0;
            appliedAmount = ApplyJourneyEffect(result, deferGoldUi);
        }
        SaveMapProgress();
        return move;
    }

    int ApplyJourneyEffect(JourneyRollResult result, bool deferGoldUi)
    {
        MapTableData table = GetMapTable();
        switch (result.outcome)
        {
            case JourneyOutcome.Damage:
                {
                    // 지도 피해는 최소 체력 아래로 내려가지 않는다. 이미 그보다 낮으면 더 깎지 않는다.
                    int minHp = table != null ? table.Config.JourneyDamageMinHp : 1;
                    int hpBefore = currentPlayerHP;
                    currentPlayerHP = Mathf.Min(currentPlayerHP, Mathf.Max(minHp, currentPlayerHP - result.amount));
                    int hpLost = Mathf.Max(0, hpBefore - currentPlayerHP);
                    UpdateMainUI("");
                    // 몬스터에게 맞았을 때와 같은 화면 붉어짐·피격음(실제로 깎인 체력이 있을 때만)
                    if (hpLost > 0)
                    {
                        HurtVignetteController.Get(this).PlayShieldOrHealthHit(ui, 0, hpLost, playerStatus.currentShield);
                        PlayPlayerHurtSound();
                    }
                    return hpLost;
                }
            case JourneyOutcome.Gold:
                // 전투와 같은 경로로 지급한다(피규어 골드 증가 효과 포함). 화면 숫자는 deferGoldUi면 코인 연출이 올린다.
                return shopManager != null ? shopManager.GrantGold(result.amount, deferGoldUi) : 0;
            case JourneyOutcome.Heal:
                {
                    int hpBefore = currentPlayerHP;
                    playerStatus.Heal(result.amount);
                    UpdateMainUI("");
                    return Mathf.Max(0, currentPlayerHP - hpBefore);
                }
        }
        return 0;
    }

    public MonsterDataSO RollPortalMonster(MapNode node)
    {
        var pool = new List<MonsterDataSO>();
        if (biomeList == null || node == null) return null;
        for (int i = 0; i < biomeList.Count; i++)
        {
            BiomeDataSO biome = biomeList[i];
            if (biome == null || biome.biomeType == BiomeType.Void) continue;
            if (biome.biomeMonsters != null)
            {
                for (int m = 0; m < biome.biomeMonsters.Count; m++)
                {
                    if (biome.biomeMonsters[m] != null)
                        pool.Add(biome.biomeMonsters[m]);
                }
            }
            if (biome.bossMonster != null)
                pool.Add(biome.bossMonster);
        }
        if (pool.Count == 0) return null;

        int mapSeed = stageProgression.currentMap != null ? stageProgression.currentMap.seed : 0;
        var rng = new System.Random(unchecked(mapSeed + node.id));
        return pool[rng.Next(pool.Count)];
    }

    public void ReturnToMap()
    {
        stageProgression.currentNodeCleared = true;
        pendingBiomeTransition = false;
        isStageClearing = false;
        isCalculating = false;
        ShowMapScreen();
        SaveMapProgress();
    }

    public void OnMapLootFinished()
    {
        MapNode node = stageProgression.FindNode(stageProgression.currentNodeId);
        bool boss = node != null && node.type == MapNodeType.Boss;
        if (boss) pendingBiomeTransition = true;

        MapTableData table = GetMapTable();
        if (table != null && table.Config.PostBattleShop)
            PromptShopChoice();
        else if (boss)
            OpenBiomeTransition();
        else
            ReturnToMap();
    }

    public void OnShopClosedDuringMap()
    {
        if (pendingBiomeTransition)
            OpenBiomeTransition();
        else
            ReturnToMap();
    }

    void BeginNewMapRun()
    {
        MapTableData table = GetMapTable();
        if (table == null)
        {
            Debug.LogWarning("[Map] Map_Table이 없어 직선 진행으로 시작합니다.");
            StartNewStage();
            return;
        }

        stageProgression.BeginRun(UnityEngine.Random.Range(1, int.MaxValue));
        if (currentBiome == null && biomeList != null)
            currentBiome = biomeList.Find(b => b.biomeType == BiomeType.Forest);
        BiomeType biome = currentBiome != null ? currentBiome.biomeType : BiomeType.Forest;
        stageProgression.BeginBiome(table, biome);
        EnsureMapUi();
        ShowMapScreen();
        SaveMapProgress();
    }

    void RestoreMapProgress(SaveData data)
    {
        MapTableData table = GetMapTable();
        if (data.mapSaveVersion < 1)
        {
            stageProgression.BeginRun(UnityEngine.Random.Range(1, int.MaxValue));
            stageProgression.biomeIndex = Mathf.Max(0, (currentStage - 1) / 10);
        }
        else
        {
            stageProgression.runSeed = data.runSeed;
            stageProgression.biomeIndex = data.biomeIndex;
            stageProgression.currentNodeId = data.currentNodeId;
            stageProgression.currentNodeCleared = data.currentNodeCleared;
            stageProgression.visitedNodeIds = data.visitedNodeIds != null
                ? new List<int>(data.visitedNodeIds)
                : new List<int>();
            stageProgression.passedNodeIds = data.passedNodeIds != null
                ? new List<int>(data.passedNodeIds)
                : new List<int>();
            stageProgression.pendingJourneyMove = data.pendingJourneyMove;
        }

        BiomeType biome = currentBiome != null ? currentBiome.biomeType : BiomeType.Forest;
        stageProgression.currentMap = biome == BiomeType.Void || table == null
            ? null
            : MapGenerator.Generate(stageProgression.runSeed, stageProgression.biomeIndex, biome, table);
        if (!MapProgressMatches(stageProgression))
        {
            stageProgression.currentNodeId = -1;
            stageProgression.currentNodeCleared = false;
            stageProgression.ClearMapTrail();
        }
        EnsureMapUi();
    }

    void OpenBiomeTransition()
    {
        pendingBiomeTransition = false;
        stageProgression.currentNodeCleared = true;
        stageProgression.biomeIndex++;
        isStageClearing = false;
        isCalculating = false;
        HideMapOverlays();
        SetBoardVisible(false);

        MapTableData table = GetMapTable();
        int threshold = table != null ? table.Config.BiomeCountToVoid : 10;
        BiomeType current = currentBiome != null ? currentBiome.biomeType : BiomeType.Forest;
        List<BiomeType> options = biomeNavigator.GetNextBiomeOptions(current, stageProgression.biomeIndex, threshold);
        if (biomeSelectionPanel != null)
            biomeSelectionPanel.OpenPanel(this, options);
        SaveMapProgress();
    }

    void OpenCurrentNode(MapNode node)
    {
        if (node == null) return;
        switch (node.type)
        {
            case MapNodeType.Enemy:
                BeginMapCombat(false, false, null);
                break;
            case MapNodeType.Elite:
                // TODO: 엘리트 몬스터 풀
                BeginMapCombat(true, false, null);
                break;
            case MapNodeType.Boss:
                BeginMapCombat(false, true, null);
                break;
            case MapNodeType.Portal:
                BeginMapCombat(false, false, RollPortalMonster(node));
                break;
            case MapNodeType.Encounter:
                HideMapOverlays();
                SetBoardVisible(false);
                if (encounterEventPanel != null && currentBiome != null)
                    encounterEventPanel.StartEvent(currentBiome.biomeType);
                else
                    ReturnToMap();
                PlaceTravelChrome();
                SetHudActive("Book_Button", false);
                SetHudActive("Deck_BTN", false);
                break;
            case MapNodeType.ShopNyaon:
            case MapNodeType.ShopRonan:
            case MapNodeType.ShopOpel:
                HideMapOverlays();
                SetBoardVisible(false);
                if (shopManager != null)
                    shopManager.OpenMapShop(MerchantFor(node.type));
                else
                    ReturnToMap();
                PlaceTravelChrome();
                break;
            case MapNodeType.Rest:
                HideMapOverlays();
                SetBoardVisible(false);
                if (restPanel != null) restPanel.Open(this);
                else ReturnToMap();
                PlaceTravelChrome();
                break;
        }
    }

    void BeginMapCombat(bool elite, bool boss, MonsterDataSO forcedMonster)
    {
        isEliteBattle = elite;
        mapBossBattle = boss;
        pendingForcedMonster = forcedMonster;
        ShowCombatScreen();
        StartNewStage();
    }

    static ShopManager.MapMerchant MerchantFor(MapNodeType type)
    {
        switch (type)
        {
            case MapNodeType.ShopRonan: return ShopManager.MapMerchant.Ronan;
            case MapNodeType.ShopOpel: return ShopManager.MapMerchant.Opel;
            default: return ShopManager.MapMerchant.Nyaon;
        }
    }

    void ShowMapScreen()
    {
        EnsureMapUi();
        if (restPanel != null) restPanel.Hide();
        if (opelPanel != null) opelPanel.Hide();
        SetBoardVisible(false);
        if (mapPanel != null)
        {
            mapPanel.iconSet = mapIconSet;
            mapPanel.Show(this);
        }
        PlaceTravelChrome();
        UpdateMainUI("");
    }

    void ShowCombatScreen()
    {
        HideMapOverlays();
        SetBoardVisible(true);
    }

    void HideMapOverlays()
    {
        if (mapPanel != null) mapPanel.Hide();
        if (restPanel != null) restPanel.Hide();
        if (opelPanel != null) opelPanel.Hide();
    }

    static bool MapProgressMatches(StageManager stage)
    {
        if (stage == null || stage.currentMap == null) return true;
        if (stage.currentNodeId >= 0 && stage.FindNode(stage.currentNodeId) == null)
            return false;
        if (!NodeListMatches(stage, stage.visitedNodeIds)) return false;
        if (!NodeListMatches(stage, stage.passedNodeIds)) return false;
        return true;
    }

    static bool NodeListMatches(StageManager stage, List<int> ids)
    {
        if (ids == null) return true;
        for (int i = 0; i < ids.Count; i++)
        {
            if (stage.FindNode(ids[i]) == null)
                return false;
        }
        return true;
    }

    void SetBoardVisible(bool visible)
    {
        if (rollSlotParent != null) rollSlotParent.gameObject.SetActive(visible);
        if (keepSlotParent != null) keepSlotParent.gameObject.SetActive(visible);
        if (ui != null)
        {
            if (ui.rollButton != null) ui.rollButton.gameObject.SetActive(visible);
            if (ui.finishButton != null) ui.finishButton.gameObject.SetActive(visible);
            if (ui.flameStackRoot != null) ui.flameStackRoot.SetActive(visible);
            if (ui.resultPanel != null && !visible) ui.resultPanel.SetActive(false);
            if (!visible)
            {
                ui.HideResult();
                ui.HideShopChoice();
            }
        }
        SetCombatHud(visible);
        if (visible)
        {
            CacheHud();
            RestoreCombatChrome();
            if (hudObjects.TryGetValue("ScorePanel", out GameObject score) && score != null)
            {
                Image scoreImage = score.GetComponent<Image>();
                if (scoreImage != null) scoreImage.raycastTarget = true;
            }
        }
        if (!visible && enemy != null)
            enemy.gameObject.SetActive(false);
        if (!visible)
        {
            HideBoardDice();
            PlaceTravelChrome();
        }
    }

    void PlaceTravelChrome()
    {
        CacheHud();
        if (biomeBackgroundImage != null)
        {
            biomeBackgroundImage.gameObject.SetActive(true);
            biomeBackgroundImage.enabled = true;
            biomeBackgroundImage.transform.SetAsFirstSibling();
            if (stageProgression != null && stageProgression.currentBiome != null && stageProgression.currentBiome.backgroundImage != null)
                biomeBackgroundImage.sprite = stageProgression.currentBiome.backgroundImage;
        }

        if (mapPanel != null && mapPanel.gameObject.activeSelf)
            mapPanel.transform.SetSiblingIndex(1);

        RaiseHud("ScorePanel");
        if (hudObjects.TryGetValue("ScorePanel", out GameObject score) && score != null)
        {
            Image scoreImage = score.GetComponent<Image>();
            if (scoreImage != null) scoreImage.raycastTarget = false;
        }
        RaiseHud("FigureSlotArea");
        RaiseHud("Setting_BTN");
        EnableHudControl("Book_Button");
        EnableHudControl("Deck_BTN");
        EnableHudControl("Setting_BTN");
        EnableHudControl("CoinPanel");
        EnableHudControl("HeartPanel");
        EnableHudControl("FigureSlotArea");
        ApplyTravelChrome();
    }

    class HudRectSnapshot
    {
        public RectTransform rect;
        public Transform parent;
        public int siblingIndex;
        public Vector2 anchorMin;
        public Vector2 anchorMax;
        public Vector2 pivot;
        public Vector2 anchoredPosition;
        public Vector2 sizeDelta;
        public Vector3 localScale;
        public bool raycast;
        public bool hasImage;
    }

    readonly Dictionary<string, HudRectSnapshot> combatPlacements = new Dictionary<string, HudRectSnapshot>();
    bool combatPlacementsSaved;
    bool travelPlacementApplied;

    static readonly string[] TravelPlacementNames = { "Book_Button", "Deck_BTN", "CoinPanel", "HeartPanel" };

    void SaveCombatPlacements()
    {
        if (combatPlacementsSaved) return;
        CacheHud();
        combatPlacementsSaved = true;
        for (int i = 0; i < TravelPlacementNames.Length; i++)
        {
            string objectName = TravelPlacementNames[i];
            if (!hudObjects.TryGetValue(objectName, out GameObject target) || target == null) continue;
            RectTransform rect = target.GetComponent<RectTransform>();
            if (rect == null) continue;
            Image image = target.GetComponent<Image>();
            combatPlacements[objectName] = new HudRectSnapshot
            {
                rect = rect,
                parent = rect.parent,
                siblingIndex = rect.GetSiblingIndex(),
                anchorMin = rect.anchorMin,
                anchorMax = rect.anchorMax,
                pivot = rect.pivot,
                anchoredPosition = rect.anchoredPosition,
                sizeDelta = rect.sizeDelta,
                localScale = rect.localScale,
                raycast = image != null && image.raycastTarget,
                hasImage = image != null
            };
        }
    }

    void ApplyTravelChrome()
    {
        SaveCombatPlacements();
        if (!hudObjects.TryGetValue("ScorePanel", out GameObject score) || score == null) return;
        RectTransform canvas = score.transform.parent as RectTransform;
        if (canvas == null) return;

        RectTransform deck = PlacementRect("Deck_BTN");
        RectTransform book = PlacementRect("Book_Button");
        RectTransform coin = PlacementRect("CoinPanel");
        RectTransform heart = PlacementRect("HeartPanel");

        if (deck != null)
            PlaceBottomLeftCenter(deck, canvas, new Vector2(130f, 120f));
        if (book != null)
            PlaceBottomLeftCenter(book, canvas, new Vector2(130f, 300f));
        FitTravelStatusPanels(coin, heart);
        Vector2 coinPos = new Vector2(342f, -32f);
        Vector2 heartPos = new Vector2(329.6f, coinPos.y - 62f);
        if (coin != null)
        {
            PlaceAnchored(coin, canvas, new Vector2(0f, 1f), coinPos);
            SetPanelRaycast(coin, false);
        }
        if (heart != null)
        {
            PlaceAnchored(heart, canvas, new Vector2(0f, 1f), heartPos);
            SetPanelRaycast(heart, false);
        }
        travelPlacementApplied = true;
    }

    void RestoreCombatChrome()
    {
        SaveCombatPlacements();
        if (!travelPlacementApplied) return;
        foreach (KeyValuePair<string, HudRectSnapshot> pair in combatPlacements)
        {
            HudRectSnapshot snap = pair.Value;
            if (snap.rect == null) continue;
            snap.rect.SetParent(snap.parent, false);
            snap.rect.anchorMin = snap.anchorMin;
            snap.rect.anchorMax = snap.anchorMax;
            snap.rect.pivot = snap.pivot;
            snap.rect.sizeDelta = snap.sizeDelta;
            snap.rect.anchoredPosition = snap.anchoredPosition;
            snap.rect.localScale = snap.localScale;
            snap.rect.SetSiblingIndex(Mathf.Clamp(snap.siblingIndex, 0, snap.parent.childCount - 1));
            if (snap.hasImage)
            {
                Image image = snap.rect.GetComponent<Image>();
                if (image != null) image.raycastTarget = snap.raycast;
            }
        }
        travelPlacementApplied = false;
    }

    void FitTravelStatusPanels(RectTransform coin, RectTransform heart)
    {
        float width = 0f;
        if (coin != null && combatPlacements.TryGetValue("CoinPanel", out HudRectSnapshot coinSnap))
            width = Mathf.Max(width, coinSnap.sizeDelta.x);
        if (heart != null && combatPlacements.TryGetValue("HeartPanel", out HudRectSnapshot heartSnap))
            width = Mathf.Max(width, heartSnap.sizeDelta.x);
        ApplyTravelPanel(coin, "CoinPanel", width);
        ApplyTravelPanel(heart, "HeartPanel", width);
    }

    void ApplyTravelPanel(RectTransform rect, string objectName, float width)
    {
        if (rect == null || !combatPlacements.TryGetValue(objectName, out HudRectSnapshot snap)) return;
        Vector2 size = snap.sizeDelta;
        if (width > 0f) size.x = width;
        rect.sizeDelta = size;
        rect.localScale = snap.localScale * 0.7f;
    }

    static void PlaceBottomLeftCenter(RectTransform rect, RectTransform canvas, Vector2 center)
    {
        rect.SetParent(canvas, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.anchoredPosition = center;
        rect.SetAsLastSibling();
    }

    static void PlaceAnchored(RectTransform rect, RectTransform canvas, Vector2 anchor, Vector2 position)
    {
        rect.SetParent(canvas, false);
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.anchoredPosition = position;
        rect.SetAsLastSibling();
    }

    void HideBoardDice()
    {
        Dice[] dice = FindObjectsByType<Dice>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < dice.Length; i++)
        {
            if (dice[i] != null) dice[i].gameObject.SetActive(false);
        }
    }

    void SetHudActive(string objectName, bool active)
    {
        if (!hudObjects.TryGetValue(objectName, out GameObject target) || target == null) return;
        target.SetActive(active);
    }

    RectTransform PlacementRect(string objectName)
    {
        if (!hudObjects.TryGetValue(objectName, out GameObject target) || target == null) return null;
        return target.GetComponent<RectTransform>();
    }

    static void SetPanelRaycast(RectTransform rect, bool enabled)
    {
        Image image = rect.GetComponent<Image>();
        if (image != null) image.raycastTarget = enabled;
    }

    void RaiseHud(string objectName)
    {
        if (!hudObjects.TryGetValue(objectName, out GameObject target) || target == null) return;
        target.SetActive(true);
        target.transform.SetAsLastSibling();
    }

    void EnableHudControl(string objectName)
    {
        if (!hudObjects.TryGetValue(objectName, out GameObject target) || target == null) return;
        target.SetActive(true);
        Button button = target.GetComponent<Button>();
        if (button != null) button.interactable = true;
    }

    static readonly string[] CombatHudNames =
    {
        "ScorePanel_Outer", "Hand Description Text", "Active Figure Text", "Stage_Panel",
        "Drop_Rate_Panel", "slot_counter_Dice", "slot_counter_chip (1)", "plaque_chips_mult", "panel_score", "Snack"
    };

    static readonly string[] TravelHudNames =
    {
        "CoinPanel", "HeartPanel", "FigureSlotArea", "Setting_BTN", "Book_Button", "Deck_BTN"
    };

    readonly Dictionary<string, GameObject> hudObjects = new Dictionary<string, GameObject>();
    bool hudCached;

    void SetCombatHud(bool combat)
    {
        CacheHud();
        for (int i = 0; i < CombatHudNames.Length; i++)
        {
            if (hudObjects.TryGetValue(CombatHudNames[i], out GameObject target) && target != null)
                target.SetActive(combat);
        }
        if (hudObjects.TryGetValue("EnemyHPSlider", out GameObject enemyHp) && enemyHp != null)
            enemyHp.SetActive(combat);
        if (!combat)
        {
            for (int i = 0; i < TravelHudNames.Length; i++)
            {
                if (hudObjects.TryGetValue(TravelHudNames[i], out GameObject target) && target != null)
                    target.SetActive(true);
            }
        }
    }

    void CacheHud()
    {
        if (hudCached) return;
        hudCached = true;
        var transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var wanted = new HashSet<string>();
        for (int i = 0; i < CombatHudNames.Length; i++) wanted.Add(CombatHudNames[i]);
        for (int i = 0; i < TravelHudNames.Length; i++) wanted.Add(TravelHudNames[i]);
        wanted.Add("ScorePanel");
        wanted.Add("EnemyHPSlider");
        for (int i = 0; i < transforms.Length; i++)
        {
            if (transforms[i] == null || !wanted.Contains(transforms[i].name)) continue;
            if (!hudObjects.ContainsKey(transforms[i].name))
                hudObjects[transforms[i].name] = transforms[i].gameObject;
        }
    }

    void EnsureMapUi()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (mapPanel == null && canvas != null)
        {
            var go = new GameObject("MapRoot", typeof(RectTransform), typeof(MapPanel));
            go.transform.SetParent(canvas.transform, false);
            mapPanel = go.GetComponent<MapPanel>();
            go.SetActive(false);
        }
        if (restPanel == null)
            Debug.LogWarning("[Map] RestPanel이 씬에 없습니다. MainScene_Map의 배치를 확인하세요.");
        if (opelPanel == null && canvas != null)
        {
            var go = new GameObject("OpelPanel", typeof(RectTransform), typeof(OpelPanel));
            go.transform.SetParent(canvas.transform, false);
            opelPanel = go.GetComponent<OpelPanel>();
            go.SetActive(false);
        }
        if (mapIconSet == null)
        {
            mapIconSet = ScriptableObject.CreateInstance<MapIconSet>();
            mapIconSet.FillDefaultColors();
        }
        if (mapPanel != null) mapPanel.iconSet = mapIconSet;
    }

    void SaveMapProgress()
    {
        if (GameSaveManager.Instance != null)
            GameSaveManager.Instance.SaveGame(this, InventoryManager.Instance, shopManager);
    }
}
