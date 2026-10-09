using UnityEngine;
using System.Collections.Generic;

// 저장 데이터의 상태 복원만 담당합니다. 씬 컴포넌트나 별도 상태/복사본을 만들지 않습니다.
// 새 라운드 시작과 피규어 저체력 검사 호출은 DiceManager가 이어서 진행합니다.
public static class GameStateRestorer
{
    // 비공개 포획 대기 플래그는 참조로 받아 기존 복원 순서에서 초기화합니다.
    public static void Restore(DiceManager dm, SaveData data, int defaultMaxRerolls, ref bool pendingPeppermintSuccess, bool skipCombatBootstrap = false)
    {
        dm.stageContext.firstNormalAttackDone = data.firstNormalAttackDone;

        dm.stageContext.usedFigureNodes.Clear();

        if (data.usedFigureNodes != null)
        {
            foreach (string nodeKey in data.usedFigureNodes)
            {
                dm.stageContext.usedFigureNodes.Add(nodeKey);
            }
        }

        dm.figureBonusFlameDamage = data.pendingFigureFlameDamage;

        dm.currentStage = data.currentStage;
        dm.currentPlayerHP = data.currentPlayerHP;

        //누적 보너스 로드
        dm.stageBonusMult = data.stageBonusMult;
        dm.stageBonusChips = data.stageBonusChips;

        //세이브에 값이 없으면 기본 100으로, 있으면 세이브된 값으로 덮어씌움
        dm.playerMaxHP = data.playerMaxHP > 0 ? data.playerMaxHP : 100;

        //세이브 파일에서 보호막 불러오기 및 UI 갱신
        dm.currentShield = data.currentShield;
        dm.ui?.UpdateShieldUI(dm.currentShield);

        if (dm.shopManager != null)
        {
            dm.shopManager.currentGold = data.currentGold;
            dm.ui?.UpdateGoldUI(dm.shopManager.currentGold);
            if (GoldCounter.Instance != null) GoldCounter.Instance.SetGold(dm.shopManager.currentGold);
        }

        dm.multHighCard = data.multHighCard; dm.multOnePair = data.multOnePair;
        dm.multTwoPair = data.multTwoPair; dm.multTriple = data.multTriple;
        dm.multFullHouse = data.multFullHouse; dm.multFourOfAKind = data.multFourOfAKind;
        dm.multStraight = data.multStraight; dm.multYacht = data.multYacht;

        // 덱 복구 (코팅 정보 복원 포함)
        dm.masterDeck.Clear();
        foreach (var dData in data.deckDiceList)
        {
            DiceData1 newDice = null;

            if (dData.diceName == "기본 주사위")
            {
                newDice = new DiceData1();
                dm.masterDeck.Add(newDice);
            }
            else
            {
                DiceItemSO diceSO = GameSaveManager.Instance.FindItemByName(dData.diceName) as DiceItemSO;
                if (diceSO != null)
                {
                    diceSO.ApplyItemEffect(dm);
                    newDice = dm.masterDeck[dm.masterDeck.Count - 1]; // 방금 추가된 주사위를 가져옴
                }
            }

            // 새 저장은 실제 눈금/능력을 기록하므로 데이터베이스 항목이 없어도 인덱스를 유지합니다.
            if (newDice == null && dData.faceValues != null && dData.faceValues.Length == 6)
            {
                newDice = new DiceData1(dData.diceName, dData.faceValues);
                dm.masterDeck.Add(newDice);
                Debug.LogWarning($"[주사위 복원] '{dData.diceName}' 외형 데이터를 찾지 못해 저장된 눈금으로 복원합니다.");
            }
            if (newDice != null && dData.faceValues != null && dData.faceValues.Length == 6)
            {
                newDice.faceValues = dData.faceValues;
                newDice.specialEffect = (SpecialDieEffect)dData.specialEffect;
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
            InventoryManager.Instance.SnackUses.RestoreSaved(data.snackSlots, GameSaveManager.Instance, data.snackSlotSaveVersion);
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
        dm.currentRerolls = 0;
        dm.maxRerolls = defaultMaxRerolls;
        pendingPeppermintSuccess = false;
        //무조건 0으로 끄는 대신, 저장된 버프 수치를 그대로 가져옵니다!
        dm.snackBonusMult = data.snackBonusMult;
        dm.snackBonusChips = data.snackBonusChips;
        dm.snackBonusRerolls = data.snackBonusRerolls;
        dm.snackBonusFigureDropRate = data.snackBonusFigureDropRate;
        dm.figureBonusRerolls = data.figureBonusRerolls;
        dm.isPeppermintActive = data.isPeppermintActive;

        if (dm.biomeList.Count > 0)
        {
            dm.currentBiome = dm.biomeList.Find(b => (int)b.biomeType == data.savedBiomeType);

            if (dm.currentBiome == null) dm.currentBiome = dm.biomeList[0];

            if (dm.biomeBackgroundImage != null && dm.currentBiome.backgroundImage != null)
                dm.biomeBackgroundImage.sprite = dm.currentBiome.backgroundImage;
            if (BGMManager.Instance != null && dm.currentBiome.biomeBGM != null)
                BGMManager.Instance.ChangeBGM(dm.currentBiome.biomeBGM);
        }

        // 싸우던 몬스터 복구. 지도에서 맵으로 돌아오는 세이브는 전투를 다시 열지 않습니다.
        if (!skipCombatBootstrap && !string.IsNullOrEmpty(data.savedMonsterName))
        {
            MonsterDataSO savedMonster = GetMonsterDataByName(dm, data.savedMonsterName);
            if (savedMonster != null)
            {
                dm.enemy.RestoreMonster(savedMonster, data.savedMonsterHP, data.savedMonsterMaxHP, data.savedMonsterAttack, data.savedMonsterIndex, data.savedMonsterCurrentTurn, data.savedMonsterMaxTurn);
            }
            else dm.enemy.Initialize(dm.currentStage, dm.currentBiome); // 에러 방지용 안전장치

            dm.accumulatedFlameDamage = data.savedFlameDamage;
            dm.ui?.UpdateFlameStackUI(dm.accumulatedFlameDamage); //세이브 로드 시 스택 UI 갱신
        }
        else if (!skipCombatBootstrap)
        {
            dm.enemy.Initialize(dm.currentStage, dm.currentBiome);
            dm.accumulatedFlameDamage = 0; // 새로 시작할 땐 확실하게 0으로 초기화
            dm.ui?.UpdateFlameStackUI(0);
        }
        else if (dm.enemy != null)
        {
            dm.enemy.gameObject.SetActive(false);
            dm.accumulatedFlameDamage = 0;
            dm.ui?.UpdateFlameStackUI(0);
        }

        bool restoreBoard = data.boardSaveVersion == 1 && data.deckDiceList.Count == dm.masterDeck.Count && dm.CanRestoreBoard(data);
        if (data.boardSaveVersion != 0 && !restoreBoard)
            Debug.LogWarning("[이어하기] 보드 기록이 유효하지 않아 기존 라운드 복원 방식으로 진행합니다.");
        if (!restoreBoard) data.boardSaveVersion = 0;

        //세이브 로드 시에도 적 능력을 체크. 새 저장은 가짜 주사위 위치를 다시 추첨하지 않습니다.
        if (dm.enemy.CurrentBossAbility == BossAbilityType.FakeDice)
        {
            if (!restoreBoard || data.savedFakeDiceIndex >= 0)
                dm.deckManager.ApplyFakeDice(dm.fakeDiceShell, dm.fakeDiceFace, ref dm.originalBossDice, ref dm.fakeDiceIndex, restoreBoard ? data.savedFakeDiceIndex : -1);
        }

        if (restoreBoard)
        {
            dm.drawPile.Clear();
            dm.discardPile.Clear();
            foreach (int index in data.drawPileIndices) dm.drawPile.Add(dm.masterDeck[index]);
            foreach (int index in data.discardPileIndices) dm.discardPile.Add(dm.masterDeck[index]);
        }
        else
        {
            // 덱 섞기 및 이번 턴 시작 (StartNewStage() 대신 호출): 구버전 저장 호환
            dm.drawPile = new List<DiceData1>(dm.masterDeck);
            dm.discardPile.Clear();
            dm.deckManager.ShufflePile(dm.drawPile);
        }
        dm.permanentFigureMultiplier =
    data.permanentFigureMultiplier;

        dm.figureKillCounts.Clear();

        if (data.figureKillCounts != null)
        {
            foreach (var saved in data.figureKillCounts)
            {
                if (saved == null ||
                    string.IsNullOrEmpty(saved.figureName))
                {
                    continue;
                }

                dm.figureKillCounts[saved.figureName] =
                    Mathf.Max(0, saved.count);
            }
        }
    }

    // 저장된 몬스터 이름으로 바이옴 리스트를 뒤져서 진짜 데이터를 찾아주는 탐지기 함수
    private static MonsterDataSO GetMonsterDataByName(DiceManager dm, string mName)
    {
        foreach (var biome in dm.biomeList)
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


}
