using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[System.Serializable]
public class SavedDiceData
{
    public string diceName;
    public bool isCoated;
    public int type;
    public float multiplier;
    public Color diceColor;

    public List<int> activeSatellites = new List<int>();
}

public class SaveData
{
    public int currentStage;
    public int currentPlayerHP;
    public int playerMaxHP;
    public int currentGold;

    //전투 중 나갔을 때를 대비한 보호막 저장
    public int currentShield;

    //전투(스테이지) 전용 누적 보너스 저장
    public float stageBonusMult;
    public int stageBonusChips;


    //일회성 버프 상태 저장
    public float snackBonusMult;
    public int snackBonusChips;
    public int snackBonusRerolls;
    public float snackBonusFigureDropRate;
    public int figureBonusRerolls;
    public bool isPeppermintActive;

    public int savedBiomeType; // 바이옴 저장

    //싸우던 몬스터 상태 저장
    public string savedMonsterName;
    public int savedMonsterHP;
    public int savedMonsterMaxHP;
    public int savedMonsterAttack;
    public int savedMonsterIndex;

    public int savedFlameDamage;

    public int savedMonsterCurrentTurn;//몬스터 턴개념 추가
    public int savedMonsterMaxTurn;

    // 스테이지당 1회 피규어 사용 기록
    public List<string> usedFigureNodes = new List<string>();

    // 아직 결산에서 누적 화염으로 옮기지 않은 피규어 화염량
    public int pendingFigureFlameDamage;

    public List<SavedDiceData> deckDiceList = new List<SavedDiceData>();

    public List<string> ownedFigureIDs = new List<string>(); //새로 추가된 ID 저장용 리스트

    public List<string> ownedSnackIDs = new List<string>();
    public List<string> ownedTicketIDs = new List<string>();

    public float multHighCard, multOnePair, multTwoPair, multTriple, multFullHouse, multFourOfAKind, multStraight, multYacht;
}

public class GameSaveManager : MonoBehaviour
{
    public static GameSaveManager Instance;

    [Header("게임 내 모든 아이템 총집합")]
    public List<BaseItemDataSO> masterItemDatabase = new List<BaseItemDataSO>();

    //탐색 속도 최적화를 위한 피규어 전용 딕셔너리
    private Dictionary<string, FigureItemSO> figureDictionary = new Dictionary<string, FigureItemSO>();

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
        itemDictionary.Clear();
        foreach (var item in masterItemDatabase)
        {
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

    public void SaveGame(DiceManager dice, InventoryManager inv, ShopManager shop)
    {
        SaveData data = new SaveData();
        data.usedFigureNodes = new List<string>(dice.stageContext.usedFigureNodes);

        data.pendingFigureFlameDamage = dice.figureBonusFlameDamage;


        //일회성 버프들도 잊지 말고 세이브 파일에 도장 찍기
        data.playerMaxHP = dice.playerMaxHP;
        data.snackBonusMult = dice.snackBonusMult;
        data.snackBonusChips = dice.snackBonusChips;
        data.snackBonusRerolls = dice.snackBonusRerolls;
        data.snackBonusFigureDropRate = dice.snackBonusFigureDropRate;
        data.figureBonusRerolls = dice.figureBonusRerolls;
        data.isPeppermintActive = dice.isPeppermintActive;

        if (dice.currentBiome != null)
        {
            data.savedBiomeType = (int)dice.currentBiome.biomeType;
        }


        data.currentStage = dice.currentStage;
        data.currentPlayerHP = dice.currentPlayerHP;
        data.currentGold = shop != null ? shop.currentGold : 0;

        //보호막 저장
        data.currentShield = dice.currentShield;

        //누적 보너스 저장
        data.stageBonusMult = dice.stageBonusMult;
        data.stageBonusChips = dice.stageBonusChips;

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

        // 주사위 코팅 정보까지 전부 추출해서 저장
        foreach (var d in dice.masterDeck)
        {
            DiceData1 targetToSave = d;

            //가짜 주사위라면 원본 주사위를 대신 저장시킴!
            if (d.diceName == "가짜 주사위" && dice.originalBossDice != null)
            {
                targetToSave = dice.originalBossDice;
            }


            SavedDiceData sdd = new SavedDiceData();
            sdd.diceName = targetToSave.diceName;
            sdd.isCoated = targetToSave.isCoated;
            sdd.type = (int)targetToSave.type;
            sdd.multiplier = targetToSave.multiplier;
            sdd.diceColor = targetToSave.diceColor;

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

        foreach (var f in inv.ownedFigures)
        {
            data.ownedFigureIDs.Add(f.Item_ID);    //고유 아이디를 세이브 파일에 기록
        }

        inv.CollectTicketNamesForSave(data.ownedTicketIDs);
        foreach (var s in inv.snackSlots)
        {
            if (!s.isEmpty && s.currentItem != null)
                data.ownedSnackIDs.Add(s.currentItem.itemName);
        }

        data.multHighCard = dice.multHighCard; data.multOnePair = dice.multOnePair;
        data.multTwoPair = dice.multTwoPair; data.multTriple = dice.multTriple;
        data.multFullHouse = dice.multFullHouse; data.multFourOfAKind = dice.multFourOfAKind;
        data.multStraight = dice.multStraight; data.multYacht = dice.multYacht;

        string json = JsonUtility.ToJson(data);
        PlayerPrefs.SetString("TrickYacht_Save", json);
        PlayerPrefs.Save();
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
        return masterItemDatabase.FirstOrDefault(x => x.itemName == name);
    }

    public void DeleteSave() { PlayerPrefs.DeleteKey("TrickYacht_Save"); }

    // 딕셔너리를 이용해 ID로 피규어를 0.001초 만에 찾아내는 함수
    public FigureItemSO FindFigureByID(string id)
    {
        if (figureDictionary.TryGetValue(id, out FigureItemSO figure))
        {
            return figure;
        }
        return null;
    }
}