using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class GameSaveManager : MonoBehaviour
{
    public static GameSaveManager Instance;

    [Header("게임 내 모든 아이템 총집합")]
    public List<BaseItemDataSO> masterItemDatabase = new List<BaseItemDataSO>();

    // 피규어도 아래의 공용 ID 딕셔너리를 사용하여 중복 캐시와 초기화 누락을 방지합니다.

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            InitializeDictionary(); // 시작할 때 딕셔너리 셋업
        }
        else Destroy(gameObject);
    }

    // 피규어뿐만 아니라 모든 아이템을 담는 만능 딕셔너리
    private Dictionary<string, BaseItemDataSO> itemDictionary = new Dictionary<string, BaseItemDataSO>();
    // List에 있는 피규어들을 초고속 탐색용 딕셔너리로 압축하는 함수
    private void InitializeDictionary()
    {
        SnackItemSO.RegisterResourceSnacks(masterItemDatabase);
        itemDictionary.Clear();
        foreach (var item in masterItemDatabase)
        {
            if (item == null) continue;
            // 모든 아이템(BaseItemDataSO)은 Item_ID를 가지므로 그대로 등록
            if (!string.IsNullOrEmpty(item.Item_ID))
            {
                if (!itemDictionary.ContainsKey(item.Item_ID))
                    itemDictionary.Add(item.Item_ID, item);
            }
        }
    }

    // 만능 탐지기 함수
    public BaseItemDataSO FindItemByID(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (itemDictionary.TryGetValue(id, out BaseItemDataSO item)) return item;
        return null;
    }

    //모바일 백그라운드로 가거나 창을 닫을 때 자동으로 실행됨
    private void OnApplicationQuit() { AutoSave(); }
    private void OnApplicationPause(bool pauseStatus) { if (pauseStatus) AutoSave(); }

    private void AutoSave()
    {
        // 씬에 매니저들이 다 정상적으로 켜져 있을 때만(전투/상점 중일 때만) 저장
        if (DiceManager.Instance != null && InventoryManager.Instance != null)
        {
            SaveGame(DiceManager.Instance, InventoryManager.Instance, DiceManager.Instance.shopManager);
        }
    }

    private bool savePending;

    private void LateUpdate()
    {
        var dice = DiceManager.Instance;
        if (savePending && dice != null && dice.IsSaveStateStable && InventoryManager.Instance != null)
            SaveGame(dice, InventoryManager.Instance, dice.shopManager);
    }

    public void SaveGame(DiceManager dice, InventoryManager inv, ShopManager shop)
    {
        // 튜토리얼 중에는 기존 이어하기 세이브를 덮어쓰지 않는다.
        if (TutorialMode.Active) return;
        if (dice == null || inv == null || dice.currentPlayerHP <= 0) return;
        // 초기화/복원이 끝나기 전이나 복원이 실패한 상태로 정상 세이브를 덮어쓰지 않는다.
        if (!dice.CanSaveProgress) return;
        // 굴리기/정산 중에는 직전 정상 세이브를 보존하고 안정된 시점에 다시 저장합니다.
        if (!dice.IsSaveStateStable) { savePending = true; return; }
        savePending = false;
        SaveData data = new SaveData();
        // 항목별로 나눠 담는다(담는 순서는 예전과 같다).
        WritePlayerAndBuffs(data, dice);
        WriteMapProgress(data, dice);
        WritePlayerStatus(data, dice, shop);
        WriteMonster(data, dice);
        WriteDeck(data, dice);
        WriteInventory(data, inv);
        WriteHandMultipliers(data, dice);

        dice.CaptureBoardForSave(data);
        string json = JsonUtility.ToJson(data);
        PlayerPrefs.SetString("TrickYacht_Save", json);
        PlayerPrefs.Save();
    }

    // 세이브 버전, 피규어 배수·처치 기록, 이번 스테이지 기록, 스낵·조우자 버프, 지금 바이옴
    static void WritePlayerAndBuffs(SaveData data, DiceManager dice)
    {
        data.debuffSaveVersion = 1;
        data.isEnemySkillNullified = dice.isEnemySkillNullified;

        data.permanentFigureMultiplier =
    dice.permanentFigureMultiplier;

        foreach (var entry in dice.figureKillCounts)
        {
            data.figureKillCounts.Add(new SavedFigureKillCount
            {
                figureName = entry.Key,
                count = entry.Value
            });
        }
        data.firstNormalAttackDone =dice.stageContext.firstNormalAttackDone;
        data.usedFigureNodes = new List<string>(dice.stageContext.usedFigureNodes);

        data.pendingFigureFlameDamage = dice.figureBonusFlameDamage;


        //일회성 버프들도 잊지 말고 세이브 파일에 도장 찍기
        data.playerMaxHP = dice.playerMaxHP;
        data.snackBonusMult = dice.snackBonusMult;
        data.snackBonusChips = dice.snackBonusChips;
        data.snackBonusRerolls = dice.snackBonusRerolls;
        data.snackBonusFigureDropRate = dice.snackBonusFigureDropRate;
        data.figureBonusRerolls = dice.figureBonusRerolls;
        data.debuffRerollPenalty = dice.turnContext.debuffRerollPenalty;
        data.isPeppermintActive = dice.isPeppermintActive;
        data.isNextCombatHPTiedToOne = dice.isNextCombatHPTiedToOne;
        data.wandererRestoreHP = dice.wandererRestoreHP;
        data.extraShopSlots = dice.extraShopSlots;
        if (dice.currentBiome != null)
        {
            data.savedBiomeType = (int)dice.currentBiome.biomeType;
        }
    }

    // 스테이지 번호와 지도 진행(시드, 바이옴 번호, 현재·방문·지나친 칸, 남은 이동)
    static void WriteMapProgress(SaveData data, DiceManager dice)
    {
        data.currentStage = dice.currentStage;
        data.mapSaveVersion = 1; // 지도 진행 세이브. 0은 지도 도입 이전 세이브(불러올 때 새 지도로 이어 준다)
        data.runSeed = dice.stageProgression.runSeed;
        data.biomeIndex = dice.stageProgression.biomeIndex;
        data.currentNodeId = dice.stageProgression.currentNodeId;
        data.currentNodeCleared = dice.stageProgression.currentNodeCleared;
        data.visitedNodeIds = dice.stageProgression.visitedNodeIds != null
            ? new List<int>(dice.stageProgression.visitedNodeIds)
            : new List<int>();
        data.passedNodeIds = dice.stageProgression.passedNodeIds != null
            ? new List<int>(dice.stageProgression.passedNodeIds)
            : new List<int>();
        data.pendingJourneyMove = dice.stageProgression.pendingJourneyMove;
    }

    // 체력, 골드, 보호막, 누적 보너스
    static void WritePlayerStatus(SaveData data, DiceManager dice, ShopManager shop)
    {
        data.currentPlayerHP = dice.currentPlayerHP;
        data.currentGold = shop != null ? shop.currentGold : 0;

        //보호막 저장
        data.currentShield = dice.currentShield;

        //누적 보너스 저장
        data.stageBonusMult = dice.stageBonusMult;
        data.stageBonusChips = dice.stageBonusChips;
    }

    // 싸우던 몬스터가 살아 있으면 그 상태
    static void WriteMonster(SaveData data, DiceManager dice)
    {
        //몬스터가 살아있다면 현재 스탯 그대로 저장
        if (dice.enemy != null && !dice.enemy.IsDead)
        {
            data.savedMonsterName = dice.enemy.CurrentMonsterName;
            data.savedMonsterHP = dice.enemy.CurrentHP;
            data.savedMonsterMaxHP = dice.enemy.MaxHP;
            data.savedMonsterAttack = dice.enemy.AttackPower;
            data.savedMonsterIndex = dice.enemy.CurrentMonsterIndex;
            //싸우던 몬스터가 살아있다면 화염 스택도 같이 저장
            data.savedFlameDamage = dice.accumulatedFlameDamage;

            // 현재 남은 턴 수와 최대 턴 수 저장
            data.savedMonsterCurrentTurn = dice.enemy.CurrentAttackTurn;
            data.savedMonsterMaxTurn = dice.enemy.MaxAttackTurn;
        }
    }

    // 덱의 주사위 전부(코팅·위성·스탬프·디버프·여정 주사위 포함)
    static void WriteDeck(SaveData data, DiceManager dice)
    {
        // 주사위 코팅 정보까지 전부 추출해서 저장
        foreach (var d in dice.masterDeck)
        {
            DiceData1 targetToSave = d;

            SavedDiceData sdd = new SavedDiceData();
            sdd.debuffType = (int)targetToSave.debuffType;
            sdd.isCurrentCombatDebuff = dice.currentCombatDebuffs.Contains(d);
            sdd.diceName = targetToSave.diceName;
            sdd.faceValues = targetToSave.faceValues != null ? (int[])targetToSave.faceValues.Clone() : null;
            sdd.specialEffect = (int)targetToSave.specialEffect;
            sdd.isJourneyDice = targetToSave.isJourneyDice;
            if (targetToSave.faceEffects != null && targetToSave.faceEffects.Length > 0)
            {
                sdd.faceEffects = new int[targetToSave.faceEffects.Length];
                for (int i = 0; i < sdd.faceEffects.Length; i++)
                    sdd.faceEffects[i] = (int)targetToSave.faceEffects[i];
            }
            sdd.isCoated = targetToSave.isCoated;
            sdd.type = (int)targetToSave.type;
            sdd.multiplier = targetToSave.multiplier;
            sdd.diceColor = targetToSave.diceColor;
            sdd.stampType = (int)targetToSave.stampType;

            sdd.activeSatellites = new List<int>();

            if (targetToSave.activeSatellites != null)
            {
                foreach (var sat in targetToSave.activeSatellites)
                {
                    sdd.activeSatellites.Add((int)sat);
                }
            }

            data.deckDiceList.Add(sdd);
        }
    }

    // 피규어, 티켓, 스낵
    static void WriteInventory(SaveData data, InventoryManager inv)
    {
        foreach (var f in inv.ownedFigures)
        {
            data.ownedFigureIDs.Add(f.Item_ID);    //고유 아이디를 세이브 파일에 기록
        }

        inv.CollectTicketNamesForSave(data.ownedTicketIDs);
        data.snackSlotSaveVersion = 2;
        inv.SnackUses.CollectForSave(data.snackSlots);
        foreach (var s in inv.snackSlots)
        {
            if (s.CountsAsOwnedSnack && s.currentItem != null)
                data.ownedSnackIDs.Add(s.currentItem.itemName);
        }
    }

    // 족보 배수(티켓으로 올린 값)
    static void WriteHandMultipliers(SaveData data, DiceManager dice)
    {
        data.multHighCard = dice.multHighCard; data.multOnePair = dice.multOnePair;
        data.multTwoPair = dice.multTwoPair; data.multTriple = dice.multTriple;
        data.multFullHouse = dice.multFullHouse; data.multFourOfAKind = dice.multFourOfAKind;
        data.multStraight = dice.multStraight; data.multYacht = dice.multYacht;
    }

    public SaveData LoadSaveData()
    {
        if (PlayerPrefs.HasKey("TrickYacht_Save"))
        {
            string json = PlayerPrefs.GetString("TrickYacht_Save");
            return JsonUtility.FromJson<SaveData>(json);
        }
        return null;
    }

    public BaseItemDataSO FindItemByName(string name)
    {
        if (string.IsNullOrEmpty(name) || masterItemDatabase == null) return null;
        return masterItemDatabase.FirstOrDefault(x => x != null && x.itemName == name);
    }

    public void DeleteSave()
    {
        savePending = false;
        // 튜토리얼 중 패배해도 기존 이어하기 세이브는 지우지 않는다.
        if (TutorialMode.Active) return;
        PlayerPrefs.DeleteKey("TrickYacht_Save");
    }

    // 초기화된 공용 딕셔너리를 이용해 ID로 피규어를 찾습니다.
    public FigureItemSO FindFigureByID(string id)
    {
        return FindItemByID(id) as FigureItemSO;
    }
}
