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
        if (!dice.useMapFlow || stageProgression.currentMap == null) return null;
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
        if (!dice.UsesMapFlow) return false;
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
        if (!dice.UsesMapFlow) return;
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

    // 지도 굴림에 쓰는 덱의 여정 주사위. 못 찾으면 null이며, 굴림은 기본 면 구성으로 대신한다.
    public DiceData1 GetJourneyDiceData() => JourneyDice.Find(dice.deckManager.masterDeck);

    // 여정 주사위 면 스프라이트. 해골/하트/골드처럼 눈 값이 같아도 면 번호로 고른다.
    public Sprite GetJourneyFaceSprite(DiceData1 die, int faceIndex)
    {
        if (die != null && die.customFaceSprites != null && die.customFaceSprites.Length > 0)
        {
            int index = die.customFaceSprites.Length == 1 ? 0 : Mathf.Clamp(faceIndex, 0, die.customFaceSprites.Length - 1);
            if (die.customFaceSprites[index] != null) return die.customFaceSprites[index];
        }
        Dice prefabDice = dice.dicePrefab != null ? dice.dicePrefab.GetComponent<Dice>() : null;
        if (prefabDice != null && prefabDice.diceFaceSprites != null && prefabDice.diceFaceSprites.Length > 0)
        {
            int pip = die != null && die.faceValues != null && faceIndex >= 0 && faceIndex < die.faceValues.Length
                ? die.faceValues[faceIndex] : JourneyDice.MapPipsFromFaceIndex(faceIndex);
            return prefabDice.diceFaceSprites[Mathf.Clamp(pip - 1, 0, prefabDice.diceFaceSprites.Length - 1)];
        }
        return null;
    }

    // 굴림 연출 중 빠르게 바꿔 보여 줄 면 스프라이트 하나(무작위 면)
    public Sprite RandomJourneyFaceSprite()
    {
        DiceData1 die = GetJourneyDiceData() ?? JourneyDice.CreateDefault();
        return GetJourneyFaceSprite(die, JourneyDice.RollFaceIndex(die));
    }

    // 여정 주사위의 면 하나를 굴려 결과를 계산한다. 상태는 바꾸지 않는다.
    // 덱에서 여정 주사위를 못 찾아도 기본 면 구성으로 굴려 멈추지 않는다.
    public JourneyRollResult RollJourneyFace()
    {
        DiceData1 die = GetJourneyDiceData();
        if (die == null || die.faceValues == null || die.faceValues.Length == 0)
            die = JourneyDice.CreateDefault();

        int faceIndex = JourneyDice.RollFaceIndex(die);
        // 지도 효과량은 면 번호(1~6). 전투 눈금(faceValues)과 섞지 않는다.
        int mapPips = JourneyDice.MapPipsFromFaceIndex(faceIndex);
        SpecialDieEffect effect = die.GetFaceEffect(faceIndex);
        MapTableData table = GetMapTable();

        var result = new JourneyRollResult
        {
            faceIndex = faceIndex,
            pips = mapPips,
            effect = effect,
            faceSprite = GetJourneyFaceSprite(die, faceIndex)
        };

        // 지도에서는 면 효과만 쓴다. 코팅·위성 효과는 전투 결산에서만 적용한다.
        switch (effect)
        {
            case SpecialDieEffect.Thorn:
                result.outcome = JourneyOutcome.Damage;
                result.amount = Mathf.Max(0, mapPips);
                break;
            case SpecialDieEffect.Heart:
                result.outcome = JourneyOutcome.Heal;
                float healMult = FigureEffectManager.Instance != null ? FigureEffectManager.Instance.GetHealMultiplier() : 1f;
                result.amount = Mathf.Max(0, TurnCalculator.CalculateHeartHeal(mapPips, healMult));
                break;
            case SpecialDieEffect.Coin:
                result.outcome = JourneyOutcome.Gold;
                int goldPerPip = BalanceConfig.Current.journeyGoldPerPip;
                result.amount = Mathf.Max(0, mapPips * goldPerPip);
                break;
            default:
                result.outcome = JourneyOutcome.Move;
                result.amount = Mathf.Max(1, mapPips);
                break;
        }
        return result;
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
                    int hpBefore = dice.currentPlayerHP;
                    dice.currentPlayerHP = Mathf.Min(dice.currentPlayerHP, Mathf.Max(minHp, dice.currentPlayerHP - result.amount));
                    int hpLost = Mathf.Max(0, hpBefore - dice.currentPlayerHP);
                    dice.UpdateMainUI("");
                    // 몬스터에게 맞았을 때와 같은 화면 붉어짐·피격음(실제로 깎인 체력이 있을 때만)
                    if (hpLost > 0)
                    {
                        HurtVignetteController.Get(dice).PlayShieldOrHealthHit(ui, 0, hpLost, dice.playerStatus.currentShield);
                        dice.PlayPlayerHurtSound();
                    }
                    return hpLost;
                }
            case JourneyOutcome.Gold:
                // 전투와 같은 경로로 지급한다(피규어 골드 증가 효과 포함). 화면 숫자는 deferGoldUi면 코인 연출이 올린다.
                return shopManager != null ? shopManager.GrantGold(result.amount, deferGoldUi) : 0;
            case JourneyOutcome.Heal:
                {
                    int hpBefore = dice.currentPlayerHP;
                    dice.playerStatus.Heal(result.amount);
                    dice.UpdateMainUI("");
                    return Mathf.Max(0, dice.currentPlayerHP - hpBefore);
                }
        }
        return 0;
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
