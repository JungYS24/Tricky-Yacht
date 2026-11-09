using System.Collections.Generic;
using UnityEngine;

// 지도 진행: 노드 입장, 여정 주사위 굴림·적용, 차원문 몬스터, 바이옴 넘어가기, 지도·전투 화면 전환.
// DiceManager가 하나 만들어 들고 있습니다. 바깥 스크립트(MapPanel, RestPanel 등)는 예전처럼 DiceManager의 같은 이름 함수로 부릅니다.
public class MapFlowController
{
    readonly DiceManager dice;

    MapTableData cachedMapTable;
    bool pendingBiomeTransition;

    public MapFlowController(DiceManager dice)
    {
        this.dice = dice;
    }

    // 옮겨 온 코드가 그대로 읽히도록 자주 쓰는 DiceManager 값을 같은 이름으로 둡니다.
    StageManager stageProgression => dice.stageProgression;
    UIManager ui => dice.ui;
    ShopManager shopManager => dice.shopManager;
    BiomeDataSO currentBiome { get => dice.currentBiome; set => dice.currentBiome = value; }
    TravelHudLayout hud => dice.Hud;

    public MapTableData GetMapTable()
    {
        if (cachedMapTable == null && dice.mapTableAsset != null)
            cachedMapTable = MapTableData.Parse(dice.mapTableAsset.text);
        return cachedMapTable;
    }

    public string GetMapStageLabel(string biomeName)
    {
        if (stageProgression.currentMap == null) return null;
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
        if (stageProgression.pendingJourneyMove > 0) return false;
        if (stageProgression.currentNodeId >= 0 && !stageProgression.currentNodeCleared) return false;
        return true;
    }

    // 상인 고정층·보스에서 남은 이동을 멈추는지. 지도 표가 없으면 멈춘다.
    bool ShopRowForceStop()
    {
        MapTableData table = GetMapTable();
        return table == null || table.Config.ShopRowForceStop;
    }

    // 칸을 고를 수 있는 상태인지: 남은 이동이 있고, 현재 칸을 마쳤어야 한다.
    bool CanPickNode(out int move)
    {
        move = stageProgression.pendingJourneyMove;
        if (move <= 0) return false;
        return stageProgression.currentNodeId < 0 || stageProgression.currentNodeCleared;
    }

    public bool IsMapNodeSelectable(int nodeId)
    {
        MapNode node = stageProgression.FindNode(nodeId);
        if (node == null || !CanPickNode(out int move)) return false;
        return MapJourney.IsReachable(stageProgression.currentMap, stageProgression.currentNodeId, nodeId, move, ShopRowForceStop());
    }

    // 지금 고를 수 있는 칸을 한 번의 탐색으로 모두 모은다. 지도 화면이 칸마다 따로 탐색하지 않게 한다.
    // 칸마다 IsMapNodeSelectable을 부른 결과와 같다.
    internal void CollectSelectableNodes(HashSet<int> result)
    {
        result.Clear();
        if (!CanPickNode(out int move)) return;
        if (!MapJourney.TryCollect(stageProgression.currentMap, stageProgression.currentNodeId, move, ShopRowForceStop(),
                out Dictionary<int, int> distance, out _))
            return;
        foreach (KeyValuePair<int, int> pair in distance)
        {
            if (pair.Value >= 1 && pair.Value <= move && stageProgression.FindNode(pair.Key) != null)
                result.Add(pair.Key);
        }
    }

    public void EnterMapNode(int nodeId)
    {
        MapNode node = stageProgression.FindNode(nodeId);
        if (node == null || !CanPickNode(out int move)) return;
        // 갈 수 있는지와 지나친 칸을 한 번의 탐색으로 함께 구한다(IsMapNodeSelectable과 같은 판정).
        if (!MapJourney.TryCollect(stageProgression.currentMap, stageProgression.currentNodeId, move, ShopRowForceStop(),
                out Dictionary<int, int> distance, out Dictionary<int, int> cameFrom))
            return;
        if (!distance.TryGetValue(nodeId, out int steps) || steps < 1 || steps > move) return;

        Sfx.Play(SfxId.Journey_Step);

        MapTableData table = GetMapTable();
        List<int> passed = MapJourney.PassedOnWay(cameFrom, stageProgression.currentNodeId, nodeId);
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

    // 굴림 결과를 적용한다. 이동이면 true(이동 칸 수 대기), 그 외 효과는 바로 적용하고 false를 돌려준다.
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
            appliedAmount = JourneyDice.ApplyEffect(dice, result, deferGoldUi);
        }
        SaveMapProgress();
        return move;
    }

    public MonsterDataSO RollPortalMonster(MapNode node)
    {
        var pool = new List<MonsterDataSO>();
        if (dice.biomeList == null || node == null) return null;
        for (int i = 0; i < dice.biomeList.Count; i++)
        {
            BiomeDataSO biome = dice.biomeList[i];
            if (biome == null || biome.biomeType == BiomeType.Void) continue;
            if (biome.biomeMonsters != null)
            {
                for (int m = 0; m < biome.biomeMonsters.Count; m++)
                {
                    if (biome.biomeMonsters[m] != null)
                        pool.Add(biome.biomeMonsters[m]);
                }
            }
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
        dice.isStageClearing = false;
        dice.isCalculating = false;
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
            dice.PromptShopChoice();
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

    internal void BeginNewMapRun()
    {
        MapTableData table = GetMapTable();
        if (table == null)
        {
            Debug.LogWarning("[Map] Map_Table이 없어 직선 진행으로 시작합니다.");
            dice.StartNewStage();
            return;
        }

        stageProgression.BeginRun(UnityEngine.Random.Range(1, int.MaxValue));
        if (currentBiome == null && dice.biomeList != null)
            currentBiome = dice.biomeList.Find(b => b.biomeType == BiomeType.Forest);
        BiomeType biome = currentBiome != null ? currentBiome.biomeType : BiomeType.Forest;
        stageProgression.BeginBiome(table, biome);
        EnsureMapUi();
        ShowMapScreen();
        SaveMapProgress();
    }

    internal void RestoreMapProgress(SaveData data)
    {
        MapTableData table = GetMapTable();
        if (data.mapSaveVersion < 1)
        {
            stageProgression.BeginRun(UnityEngine.Random.Range(1, int.MaxValue));
            stageProgression.biomeIndex = Mathf.Max(0, (dice.currentStage - 1) / 10);
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
        stageProgression.currentMap = table == null
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
        dice.isStageClearing = false;
        dice.isCalculating = false;
        HideMapOverlays();
        SetBoardVisible(false);

        // 데모는 정해진 수의 바이옴 보스를 잡으면 다음 바이옴 선택 대신 클리어 화면으로 끝낸다.
        int demoLimit = BalanceConfig.Current.demoBiomeLimit;
        if (demoLimit > 0 && !TutorialMode.Active && stageProgression.biomeIndex >= demoLimit)
        {
            dice.ShowGameClear(demo: true);
            return;
        }

        MapTableData table = GetMapTable();
        int threshold = table != null ? table.Config.BiomeCountToVoid : 10;
        BiomeType current = currentBiome != null ? currentBiome.biomeType : BiomeType.Forest;
        List<BiomeType> options = dice.biomeNavigator.GetNextBiomeOptions(current, stageProgression.biomeIndex, threshold);
        if (dice.biomeSelectionPanel != null)
            dice.biomeSelectionPanel.OpenPanel(dice, options);
        SaveMapProgress();
    }

    internal void OpenCurrentNode(MapNode node)
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
                // 비차원 보스(악마주사위)는 조우자 대화창으로 등장 대사를 먼저 보여 주고 전투를 연다.
                if (currentBiome != null && currentBiome.biomeType == BiomeType.Void && dice.encounterEventPanel != null && !TutorialMode.Active)
                {
                    HideMapOverlays();
                    SetBoardVisible(false);
                    dice.encounterEventPanel.StartBossIntro(currentBiome.bossMonster, DevilDiceBoss.IntroKeyPrefix, DevilDiceBoss.IntroFallback,
                        () => BeginMapCombat(false, true, null));
                    PlaceTravelChrome();
                }
                else
                    BeginMapCombat(false, true, null);
                break;
            case MapNodeType.Portal:
                BeginMapCombat(false, false, RollPortalMonster(node));
                break;
            case MapNodeType.Encounter:
                HideMapOverlays();
                SetBoardVisible(false);
                if (dice.encounterEventPanel != null && currentBiome != null)
                    dice.encounterEventPanel.StartEvent(currentBiome.biomeType);
                else
                    ReturnToMap();
                PlaceTravelChrome();
                hud.SetHudActive("Book_Button", false);
                hud.SetHudActive("Deck_BTN", false);
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
                if (dice.restPanel != null) dice.restPanel.Open(dice);
                else ReturnToMap();
                PlaceTravelChrome();
                break;
        }
    }

    void BeginMapCombat(bool elite, bool boss, MonsterDataSO forcedMonster)
    {
        dice.isEliteBattle = elite;
        dice.mapBossBattle = boss;
        dice.pendingForcedMonster = forcedMonster;
        ShowCombatScreen();
        dice.StartNewStage();
        dice.RaiseMapCombatStarted();
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

    internal void ShowMapScreen()
    {
        EnsureMapUi();
        if (dice.restPanel != null) dice.restPanel.Hide();
        SetBoardVisible(false);
        if (dice.mapPanel != null)
        {
            dice.mapPanel.iconSet = dice.mapIconSet;
            dice.mapPanel.Show(dice);
        }
        PlaceTravelChrome();
        dice.UpdateMainUI("");
    }

    internal void ShowCombatScreen()
    {
        HideMapOverlays();
        SetBoardVisible(true);
    }

    void HideMapOverlays()
    {
        if (dice.mapPanel != null) dice.mapPanel.Hide();
        if (dice.restPanel != null) dice.restPanel.Hide();
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
        if (dice.rollSlotParent != null) dice.rollSlotParent.gameObject.SetActive(visible);
        if (dice.keepSlotParent != null) dice.keepSlotParent.gameObject.SetActive(visible);
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
        hud.SetCombatHud(visible);
        if (visible)
            hud.ShowCombat();
        if (!visible && dice.enemy != null)
            dice.enemy.gameObject.SetActive(false);
        if (!visible)
        {
            HideBoardDice();
            PlaceTravelChrome();
        }
        // 디버프 표시는 전투 화면에서는 나무 패널 옆, 지도 화면에서는 체력 칸 아래로 옮깁니다.
        travelScreen = !visible;
        if (DebuffTrackerHud.Instance != null) DebuffTrackerHud.Instance.Place(travelScreen);
    }

    bool travelScreen;
    internal bool TravelScreen => travelScreen; // 지금 지도 화면인지(디버프 표시 위치용)

    void PlaceTravelChrome()
    {
        if (dice.biomeBackgroundImage != null)
        {
            dice.biomeBackgroundImage.gameObject.SetActive(true);
            dice.biomeBackgroundImage.enabled = true;
            dice.biomeBackgroundImage.transform.SetAsFirstSibling();
            if (stageProgression != null && stageProgression.currentBiome != null && stageProgression.currentBiome.backgroundImage != null)
                dice.biomeBackgroundImage.sprite = stageProgression.currentBiome.backgroundImage;
        }

        if (dice.mapPanel != null && dice.mapPanel.gameObject.activeSelf)
            dice.mapPanel.transform.SetSiblingIndex(1);

        hud.ShowTravel();
    }

    void HideBoardDice()
    {
        Dice[] boardDice = Object.FindObjectsByType<Dice>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < boardDice.Length; i++)
        {
            if (boardDice[i] != null) boardDice[i].gameObject.SetActive(false);
        }
    }

    void EnsureMapUi()
    {
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (dice.mapPanel == null && canvas != null)
        {
            var go = new GameObject("MapRoot", typeof(RectTransform), typeof(MapPanel));
            go.transform.SetParent(canvas.transform, false);
            dice.mapPanel = go.GetComponent<MapPanel>();
            go.SetActive(false);
        }
        if (dice.restPanel == null)
            Debug.LogWarning("[Map] RestPanel이 씬에 없습니다. MainScene_Map의 배치를 확인하세요.");
        if (dice.mapIconSet == null)
        {
            dice.mapIconSet = ScriptableObject.CreateInstance<MapIconSet>();
            dice.mapIconSet.FillDefaultColors();
        }
        if (dice.mapPanel != null) dice.mapPanel.iconSet = dice.mapIconSet;
    }

    void SaveMapProgress()
    {
        if (GameSaveManager.Instance != null)
            GameSaveManager.Instance.SaveGame(dice, InventoryManager.Instance, shopManager);
    }
}
