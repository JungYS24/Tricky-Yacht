using UnityEngine;
using System.Collections.Generic;
using System.Linq;

// 세이브 파일(JSON)에 들어가는 데이터 형식. 저장은 GameSaveManager, 불러오기는 GameStateRestorer가 한다.
// 클래스·변수 이름이 곧 저장 키이므로 이름을 바꾸면 예전 세이브를 못 읽는다.
[System.Serializable]
public class SavedDiceData
{
    public string diceName;
    public int[] faceValues; // 강화/고정 눈금도 저장 당시 그대로 복원
    public int specialEffect;
    public int debuffType;
    public bool isCurrentCombatDebuff;
    public int[] faceEffects;       // 면별 효과(SpecialDieEffect의 int 값). 없으면 null
    public bool isJourneyDice;      // 덱에 항상 유지되는 여정 주사위 여부
    public bool isCoated;
    public int type;
    public float multiplier;
    public Color diceColor;
    public int stampType;           // StampType의 int 값. 예전 저장에는 없어서 0(None)

    public List<int> activeSatellites = new List<int>();
}

[System.Serializable]
public class SavedFigureKillCount
{
    public string figureName;
    public int count;
}

[System.Serializable]
public sealed class SavedBoardDie
{
    public int deckIndex;
    public int value;
    public int rollSlotIndex;
    public int keepSlotIndex = -1;
}

public class SaveData
{
    // 1부터 디버프를 덱 인스턴스로 저장한다. 0은 교체형 가짜 주사위 저장과의 호환용.
    public int debuffSaveVersion;
    public bool isEnemySkillNullified;

    public void MigrateLegacyDebuffs(bool resumeCombat)
    {
        if (debuffSaveVersion >= 1) return;
        // 3~4단계에서 작성된 버전 없는 새 형식 저장은 이미 디버프를 포함하고 있다.
        bool hasDebuffs = deckDiceList != null && deckDiceList.Exists(d => d != null && d.debuffType != 0);
        if (resumeCombat && !hasDebuffs && deckDiceList != null
            && savedFakeDiceIndex >= 0 && savedFakeDiceIndex < deckDiceList.Count)
        {
            SavedDiceData original = deckDiceList[savedFakeDiceIndex];
            if (original != null && !original.isJourneyDice)
            {
                int originalIndex = deckDiceList.Count;
                deckDiceList.Add(original);
                // 기존 보드/더미 인덱스는 가짜 주사위를 계속 가리키고 원본만 덱 끝에 복구한다.
                deckDiceList[savedFakeDiceIndex] = new SavedDiceData
                {
                    diceName = "가짜 주사위", faceValues = new int[6],
                    debuffType = (int)DebuffDiceType.Fake, isCurrentCombatDebuff = true,
                    multiplier = 1f, diceColor = Color.white
                };
                if (boardSaveVersion == 1 && drawPileIndices != null) drawPileIndices.Add(originalIndex);
            }
        }
        savedFakeDiceIndex = -1;
        debuffSaveVersion = 1;
    }

    // 0은 구버전 세이브. 1은 보드와 덱 순서를 그대로 복원합니다.
    public int boardSaveVersion;
    public List<SavedBoardDie> boardDice = new List<SavedBoardDie>();
    public List<int> drawPileIndices = new List<int>();
    public List<int> discardPileIndices = new List<int>();
    public int savedCurrentRerolls;
    public int savedMaxRerolls;
    public int debuffRerollPenalty;
    public int savedFakeDiceIndex = -1;

    public int currentStage;
    public int currentPlayerHP;

    // 지도 진행. mapSaveVersion이 0이면 지도 도입 이전 세이브입니다.
    public int mapSaveVersion;
    public int runSeed;
    public int biomeIndex;
    public int currentNodeId = -1;
    public bool currentNodeCleared;
    public List<int> visitedNodeIds = new List<int>();
    public List<int> passedNodeIds = new List<int>();
    public int pendingJourneyMove;
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

    // 조우자 효과. 예전 저장에는 없어서 0/false로 읽힌다.
    public bool isNextCombatHPTiedToOne; // 숙원의 방랑자: 다음 전투 체력 1
    public int wandererRestoreHP;        // 숙원의 방랑자: 그 전투에서 이기면 되돌릴 체력(0이면 없음)
    public int extraShopSlots;           // 밀렵꾼 상점 칸 봉인(음수)
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
    public bool firstNormalAttackDone;

    public List<SavedFigureKillCount> figureKillCounts =new List<SavedFigureKillCount>();

    public float permanentFigureMultiplier;

    // 스테이지당 1회 피규어 사용 기록
    public List<string> usedFigureNodes = new List<string>();

    // 아직 결산에서 누적 화염으로 옮기지 않은 피규어 화염량
    public int pendingFigureFlameDamage;

    public List<SavedDiceData> deckDiceList = new List<SavedDiceData>();

    public List<string> ownedFigureIDs = new List<string>(); //새로 추가된 ID 저장용 리스트

    public List<string> ownedSnackIDs = new List<string>();
    // 슬롯 위치와 사용 대기 상태를 함께 저장. 0은 이전 버전 저장 파일입니다.
    public int snackSlotSaveVersion;
    public List<SavedSnackSlot> snackSlots = new List<SavedSnackSlot>();
    public List<string> ownedTicketIDs = new List<string>();

    public float multHighCard, multOnePair, multTwoPair, multTriple, multFullHouse, multFourOfAKind, multStraight, multYacht;
}
