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
}
public class SaveData
{
    public int currentStage;
    public int currentPlayerHP;
    public int playerMaxHP;
    public int currentGold;


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

    public List<SavedDiceData> deckDiceList = new List<SavedDiceData>();

    public List<string> ownedFigureNames = new List<string>();
    public List<string> ownedSnackNames = new List<string>();
    public List<string> ownedTicketNames = new List<string>();

    public float multHighCard, multOnePair, multTwoPair, multTriple, multFullHouse, multFourOfAKind, multStraight, multYacht;
}

public class GameSaveManager : MonoBehaviour
{
    public static GameSaveManager Instance;

    [Header("게임 내 모든 아이템 총집합")]
    public List<BaseItemDataSO> masterItemDatabase = new List<BaseItemDataSO>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
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

        //몬스터가 살아있다면 현재 스탯 그대로 저장
        if (dice.enemy != null && !dice.enemy.IsDead)
        {
            data.savedMonsterName = dice.enemy.CurrentMonsterName;
            data.savedMonsterHP = dice.enemy.CurrentHP;
            data.savedMonsterMaxHP = dice.enemy.MaxHP;
            data.savedMonsterAttack = dice.enemy.AttackPower;
            data.savedMonsterIndex = dice.enemy.CurrentMonsterIndex;
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
            sdd.diceName = d.diceName;
            sdd.isCoated = d.isCoated;
            sdd.type = (int)d.type;
            sdd.multiplier = d.multiplier;
            sdd.diceColor = d.diceColor;
            data.deckDiceList.Add(sdd);
        }
        foreach (var f in inv.ownedFigures) data.ownedFigureNames.Add(f.itemName);
        foreach (var t in inv.ownedTickets) data.ownedTicketNames.Add(t.itemName);
        foreach (var s in inv.snackSlots)
        {
            if (!s.isEmpty && s.currentItem != null)
                data.ownedSnackNames.Add(s.currentItem.itemName);
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
}