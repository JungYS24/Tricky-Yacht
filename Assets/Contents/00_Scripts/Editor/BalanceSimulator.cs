#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// 데모 밸런스 봇 시뮬레이터 (Docs/Balancing/DemoBalance_v1.md 4장).
// 실제 맵 생성기·몬스터 SO·상점 재고 표·BalanceConfig 를 읽고, 전투·이동은 게임 규칙을 단순화해 재현한다.
// 배치모드: Unity -batchmode -quit -projectPath <path> -executeMethod BalanceSimulator.RunBatch [-simRuns 1000] [-simOut Docs/Balancing/SimResult_v1.md] [-simSeed 1]
//   -simSet "필드=값,..."  : BalanceConfig 값을 이번 실행에서만 바꾼다(에셋은 그대로). 배열은 ; 로 구분. 예) tierHp=1;1.4
//   -simScale "등급.항목=배율,..." : 몬스터 기본 수치를 등급별로 곱한다(JSON 은 그대로). 예) Boss.hp=0.8,Elite.atk=0.9
//   -simBiomes N : 기본(BalanceConfig.demoBiomeLimit, 2) 대신 바이옴 N개까지 진행한다. 3 이상이면 바이옴 단계별 생존표를 붙인다.
public static class BalanceSimulator
{
    const string BiomeFolder = "Assets/Contents/10_Resources/Data/DiceData/Map";
    const string MapTablePath = "Assets/Contents/10_Resources/Data/Map_Table.json";
    const string StockTablePath = "Assets/Contents/10_Resources/Data/Shop_Stock_Table.json";
    const string DefaultOut = "Docs/Balancing/SimResult_v1.md";

    // 피규어 효과는 종류가 많아 재현하지 않고, 1개당 족보 배수 +0.1 로 근사한다.
    const float FigureMultBonus = 0.1f;
    // 프리즘 코팅 배수 (Prism.asset scoreMultiplier 1.2 → 족보 배수 +0.2)
    const float PrismMultBonus = 0.2f;
    const int MaxTurnsPerCombat = 60;
    const int DemoBiomeCount = 2;

    // 씬 DiceManager 기본 족보 배수 (하이카드 ~ 야추)
    static readonly float[] HandMult = { 1.0f, 1.2f, 1.4f, 1.5f, 1.7f, 1.8f, 2.0f, 2.5f };
    static readonly int[] BasicFaces = { 1, 2, 3, 4, 5, 6 };
    static readonly int[] JourneyFaces = { 1, 2, 3, 1, 1, 1 };

    [MenuItem("Tools/Balance/Simulate Demo (1000)")]
    public static void RunFromMenu()
    {
        string path = Run(1000, 1, DefaultOut);
        EditorUtility.RevealInFinder(path);
    }

    public static void RunBatch()
    {
        string[] args = Environment.GetCommandLineArgs();
        int runs = ArgInt(args, "-simRuns", 1000);
        int seed = ArgInt(args, "-simSeed", 1);
        string output = ArgString(args, "-simOut", DefaultOut);
        string set = ArgString(args, "-simSet", null);
        string scale = ArgString(args, "-simScale", null);
        int demoLimit = BalanceConfig.Current.demoBiomeLimit;
        int fullRun = MapTableData.Parse(AssetDatabase.LoadAssetAtPath<TextAsset>(MapTablePath).text).Config.BiomeCountToVoid + 1;
        int biomes = ArgInt(args, "-simBiomes", demoLimit > 0 ? demoLimit : fullRun);
        try
        {
            if (!string.IsNullOrEmpty(set)) BalanceConfig.OverrideForTesting(ApplyOverrides(BalanceConfig.Current, set));
            Run(runs, seed, output, set, scale, biomes);
        }
        finally { BalanceConfig.OverrideForTesting(null); }
    }

    // BalanceConfig 를 복제해 필드 값을 바꾼다. 원본 에셋은 건드리지 않는다.
    static BalanceConfig ApplyOverrides(BalanceConfig source, string set)
    {
        BalanceConfig copy = UnityEngine.Object.Instantiate(source);
        foreach (string pair in set.Split(','))
        {
            string[] kv = pair.Split('=');
            if (kv.Length != 2) continue;
            var field = typeof(BalanceConfig).GetField(kv[0].Trim());
            if (field == null) { Debug.LogWarning($"[Balance Sim] 없는 필드: {kv[0]}"); continue; }
            string raw = kv[1].Trim();
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            if (field.FieldType == typeof(int)) field.SetValue(copy, int.Parse(raw, inv));
            else if (field.FieldType == typeof(float)) field.SetValue(copy, float.Parse(raw, inv));
            else if (field.FieldType == typeof(bool)) field.SetValue(copy, bool.Parse(raw));
            else if (field.FieldType == typeof(float[])) field.SetValue(copy, raw.Split(';').Select(x => float.Parse(x, inv)).ToArray());
            else if (field.FieldType == typeof(int[])) field.SetValue(copy, raw.Split(';').Select(x => int.Parse(x, inv)).ToArray());
        }
        return copy;
    }

    // "Boss.hp=0.8,Elite.atk=0.9" → 등급별 [hp, atk, gold] 배율
    static float[,] ParseScale(string scale)
    {
        var result = new float[3, 3] { { 1, 1, 1 }, { 1, 1, 1 }, { 1, 1, 1 } };
        if (string.IsNullOrEmpty(scale)) return result;
        foreach (string pair in scale.Split(','))
        {
            string[] kv = pair.Split('=');
            string[] key = kv[0].Trim().Split('.');
            if (kv.Length != 2 || key.Length != 2 || !Enum.TryParse(key[0], out MonsterGrade grade)) continue; // 몬스터 ID 키는 ParseMonsterScale
            int stat = key[1] == "hp" ? 0 : key[1] == "atk" ? 1 : key[1] == "gold" ? 2 : -1;
            if (stat < 0) continue;
            result[(int)grade, stat] = float.Parse(kv[1], System.Globalization.CultureInfo.InvariantCulture);
        }
        return result;
    }

    // "DevilDice.hp=0.5" 처럼 몬스터 ID 로 준 배율. 등급 배율 위에 한 번 더 곱한다.
    static Dictionary<string, float[]> ParseMonsterScale(string scale)
    {
        var result = new Dictionary<string, float[]>();
        if (string.IsNullOrEmpty(scale)) return result;
        foreach (string pair in scale.Split(','))
        {
            string[] kv = pair.Split('=');
            int dot = kv[0].LastIndexOf('.');
            if (kv.Length != 2 || dot <= 0) continue;
            string id = kv[0].Substring(0, dot).Trim();
            if (Enum.TryParse(id, out MonsterGrade _)) continue;
            string statName = kv[0].Substring(dot + 1).Trim();
            int stat = statName == "hp" ? 0 : statName == "atk" ? 1 : statName == "gold" ? 2 : -1;
            if (stat < 0) continue;
            if (!result.TryGetValue(id, out float[] values)) result[id] = values = new float[] { 1, 1, 1 };
            values[stat] = float.Parse(kv[1], System.Globalization.CultureInfo.InvariantCulture);
        }
        return result;
    }

    static int ArgInt(string[] args, string key, int fallback)
    {
        string raw = ArgString(args, key, null);
        return raw != null && int.TryParse(raw, out int v) ? v : fallback;
    }

    static string ArgString(string[] args, string key, string fallback)
    {
        int i = Array.IndexOf(args, key);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
    }

    // ---------------- 데이터 ----------------

    class ShopOffer { public string name; public string cls; public int price; public bool nyaon, ronan, opel; }

    class World
    {
        public BalanceConfig cfg;
        public MapTableData table;
        public Dictionary<BiomeType, BiomeDataSO> biomes = new Dictionary<BiomeType, BiomeDataSO>();
        public List<MonsterDataSO> portalPool = new List<MonsterDataSO>();
        public List<ShopOffer> stock = new List<ShopOffer>();
        public int slotCount = 6;
        public float[,] scale = ParseScale(null);
        public int biomeCount = DemoBiomeCount;
        public Dictionary<string, float[]> monsterScale = new Dictionary<string, float[]>();
        float Extra(MonsterDataSO m, int stat) => monsterScale.TryGetValue(m.monsterID ?? m.name, out float[] v) || monsterScale.TryGetValue(m.name, out v) ? v[stat] : 1f;
        public int BaseHp(MonsterDataSO m) => Mathf.RoundToInt(m.maxHp * scale[(int)m.monsterGrade, 0] * Extra(m, 0));
        public int BaseAtk(MonsterDataSO m) => Mathf.RoundToInt(m.baseAtk * scale[(int)m.monsterGrade, 1] * Extra(m, 1));
        public int BaseGold(MonsterDataSO m) => Mathf.RoundToInt(m.dropGold * scale[(int)m.monsterGrade, 2] * Extra(m, 2));
    }

    [Serializable] class StockRow { public string SO_Name; public string SO_Class; public int Price; public bool Nyaon, Ronan, Opel, Implemented; }
    [Serializable] class StockFile { public int slotCount; public List<StockRow> stock; }

    static World LoadWorld()
    {
        var w = new World { cfg = BalanceConfig.Current };
        w.table = MapTableData.Parse(AssetDatabase.LoadAssetAtPath<TextAsset>(MapTablePath).text);
        foreach (string guid in AssetDatabase.FindAssets("t:BiomeDataSO", new[] { BiomeFolder }))
        {
            var b = AssetDatabase.LoadAssetAtPath<BiomeDataSO>(AssetDatabase.GUIDToAssetPath(guid));
            if (b == null) continue;
            w.biomes[b.biomeType] = b;
            if (b.biomeType == BiomeType.Void || b.biomeMonsters == null) continue;
            foreach (var m in b.biomeMonsters) if (m != null) w.portalPool.Add(m);
        }
        var file = JsonUtility.FromJson<StockFile>(File.ReadAllText(StockTablePath));
        if (file.slotCount > 0) w.slotCount = file.slotCount;
        foreach (var r in file.stock)
        {
            if (!r.Implemented) continue;
            w.stock.Add(new ShopOffer { name = r.SO_Name, cls = r.SO_Class, price = r.Price, nyaon = r.Nyaon, ronan = r.Ronan, opel = r.Opel });
        }
        return w;
    }

    // ---------------- 한 판의 상태 ----------------

    enum DieKind { Basic, Journey, Fake, Spine, Curse, Thief, Shackle }

    class Die
    {
        public DieKind kind;
        public int[] faces;
        public bool temporary;
        public string coating; // Prism / Ice / Gold / Dark
        public string stamp;   // Spade / Heart / Diamond / Clover
        public bool devilCursed; // 악마주사위 1페이즈 저주(이번 전투만)
        public Die(DieKind k, int[] f, bool temp = false) { kind = k; faces = f; temporary = temp; }
    }

    class RunState
    {
        public System.Random rng;
        public int hp, maxHp, gold;
        public List<Die> deck = new List<Die>();
        public HashSet<string> figures = new HashSet<string>();
        public int FigureCount => figures.Count;
    }

    class CombatResult { public bool won; public int turns; public int hits; }

    // ---------------- 지표 ----------------

    class Stats
    {
        public int runs;
        public int[] tierReached, tierCleared;
        public List<List<float>> tierArrivalHp = new List<List<float>>(), tierBossArrivalHp = new List<List<float>>();
        public int forestDeaths, forestBossReached, forestBossKilled;
        public int secondBossReached, secondBossKilled, secondDeaths;
        public List<int> forestNormalTurns = new List<int>(), forestEliteTurns = new List<int>(), forestBossTurns = new List<int>();
        public List<int> forestNormalHits = new List<int>(), forestEliteHits = new List<int>(), forestBossHits = new List<int>();
        public List<int> secondNormalTurns = new List<int>(), secondEliteTurns = new List<int>(), secondBossTurns = new List<int>();
        public List<float> forestBossArrivalHpPct = new List<float>();
        public List<int> firstShopGold = new List<int>();
        public List<int> forestCombats = new List<int>();
        public Dictionary<string, int> deathCause = new Dictionary<string, int>();
        public Dictionary<BiomeType, int[]> secondBiome = new Dictionary<BiomeType, int[]>(); // [도착, 보스처치]
        public List<int> figuresAtForestBoss = new List<int>();
    }

    // ---------------- 실행 ----------------

    public static string Run(int runs, int seed, string outputPath, string set = null, string scale = null, int biomeCount = DemoBiomeCount)
    {
        World world = LoadWorld();
        world.scale = ParseScale(scale);
        world.monsterScale = ParseMonsterScale(scale);
        world.biomeCount = Mathf.Max(1, biomeCount);
        var stats = new Stats { runs = runs, tierReached = new int[world.biomeCount], tierCleared = new int[world.biomeCount] };
        for (int i = 0; i < world.biomeCount; i++) { stats.tierArrivalHp.Add(new List<float>()); stats.tierBossArrivalHp.Add(new List<float>()); }
        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < runs; i++)
            SimulateRun(world, stats, new System.Random(seed * 100003 + i), seed * 7919 + i);
        sw.Stop();

        string report = BuildReport(world, stats, runs, seed, sw.Elapsed.TotalSeconds, set, scale);
        string full = Path.IsPathRooted(outputPath) ? outputPath : Path.Combine(Directory.GetParent(Application.dataPath).FullName, outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(full));
        File.WriteAllText(full, report, new UTF8Encoding(false));
        Debug.Log($"[Balance Sim] {runs}판 완료 ({sw.Elapsed.TotalSeconds:F1}s) → {full}\n{report}");
        return full;
    }

    static void SimulateRun(World w, Stats s, System.Random rng, int runSeed)
    {
        var run = new RunState { rng = rng, maxHp = w.cfg.playerMaxHP, gold = w.cfg.startGold };
        run.hp = run.maxHp;
        run.deck.Add(new Die(DieKind.Journey, JourneyFaces));
        for (int i = 0; i < 11; i++) run.deck.Add(new Die(DieKind.Basic, BasicFaces));

        BiomeType biome = BiomeType.Forest;
        var navigator = new BiomeNavigator();
        for (int tier = 0; tier < w.biomeCount; tier++)
        {
            if (tier >= 1 && tier >= w.table.Config.BiomeCountToVoid)
            {
                biome = BiomeType.Void;
            }
            else if (tier >= 1)
            {
                // 몬스터·보스 데이터가 있는 바이옴만 고른다(하늘섬처럼 비어 있는 곳 제외).
                var options = navigator.BiomeRoutes[biome].Where(b => w.biomes.TryGetValue(b, out var d)
                    && d.bossMonster != null && d.biomeMonsters != null && d.biomeMonsters.Count > 0).ToList();
                if (options.Count == 0) return;
                biome = options[rng.Next(options.Count)];
                if (tier == 1)
                {
                    if (!s.secondBiome.ContainsKey(biome)) s.secondBiome[biome] = new int[2];
                    s.secondBiome[biome][0]++;
                }
            }
            s.tierReached[tier]++;
            s.tierArrivalHp[tier].Add(100f * run.hp / run.maxHp);
            bool alive = SimulateBiome(w, s, run, biome, tier, runSeed);
            if (!alive) return;
            s.tierCleared[tier]++;
        }
    }

    // 바이옴 하나를 진행한다. 보스까지 잡으면 true.
    static bool SimulateBiome(World w, Stats s, RunState run, BiomeType biomeType, int tier, int runSeed)
    {
        BiomeDataSO biome = w.biomes[biomeType];
        MapData map = MapGenerator.Generate(runSeed, tier, biomeType, w.table);
        bool forest = tier == 0;
        int current = -1;
        int combatsInBiome = 0;
        bool shopGoldRecorded = false;
        int guard = 0;

        while (guard++ < 500)
        {
            // 여정 주사위: 면 0~2 이동 1~3, 3 해골(피해 4), 4 하트(회복 5), 5 코인(눈 6 x 골드)
            int face = run.rng.Next(6);
            if (face == 3) { run.hp = Math.Min(run.hp, Math.Max(w.table.Config.JourneyDamageMinHp, run.hp - 4)); continue; }
            if (face == 4) { run.hp = Math.Min(run.maxHp, run.hp + 5); continue; }
            if (face == 5) { run.gold += 6 * w.cfg.journeyGoldPerPip; continue; }

            int target = ChooseTarget(map, current, face + 1, w.table.Config.ShopRowForceStop, run.rng);
            if (target < 0) return true;
            current = target;
            MapNode node = map.nodes[current];

            if (forest && node.isShopRow && !shopGoldRecorded)
            {
                s.firstShopGold.Add(run.gold);
                shopGoldRecorded = true;
            }

            switch (node.type)
            {
                case MapNodeType.Enemy:
                case MapNodeType.Elite:
                case MapNodeType.Portal:
                case MapNodeType.Boss:
                {
                    MonsterGrade grade;
                    MonsterDataSO monster = PickMonster(w, biome, node.type, run.rng, out grade);
                    if (node.type == MapNodeType.Boss)
                    {
                        if (forest)
                        {
                            s.forestBossReached++;
                            s.forestBossArrivalHpPct.Add(100f * run.hp / run.maxHp);
                            s.figuresAtForestBoss.Add(run.FigureCount);
                            s.forestCombats.Add(combatsInBiome);
                        }
                        else if (tier == 1) s.secondBossReached++;
                        s.tierBossArrivalHp[tier].Add(100f * run.hp / run.maxHp);
                    }
                    AddNodeDebuffs(w, run, node.type, tier);
                    CombatResult r = Fight(w, run, monster, grade, tier, combatsInBiome);
                    run.deck.RemoveAll(d => d.temporary);
                    combatsInBiome++;
                    if (tier <= 1) RecordCombat(s, forest, grade, node.type, r);
                    if (!r.won)
                    {
                        string key = (forest ? "숲 " : $"{tier + 1}번째 바이옴 ") + GradeName(grade);
                        s.deathCause[key] = s.deathCause.TryGetValue(key, out int c) ? c + 1 : 1;
                        if (forest) s.forestDeaths++; else if (tier == 1) s.secondDeaths++;
                        return false;
                    }
                    GrantRewards(w, run, monster, grade, tier);
                    if (node.type == MapNodeType.Boss)
                    {
                        if (forest) s.forestBossKilled++;
                        else if (tier == 1)
                        {
                            s.secondBossKilled++;
                            s.secondBiome[biomeType][1]++;
                        }
                        return true;
                    }
                    break;
                }
                case MapNodeType.ShopNyaon:
                case MapNodeType.ShopRonan:
                case MapNodeType.ShopOpel:
                    VisitShop(w, run, node.type, tier);
                    break;
                case MapNodeType.Rest:
                    // 봇 휴식: 영구 디버프가 2개 이상이고 체력이 절반 이상이면 디버프 1개를 파괴, 아니면 회복
                    var debuffs = run.deck.Where(d => d.kind == DieKind.Spine || d.kind == DieKind.Curse || d.kind == DieKind.Fake).ToList();
                    if (debuffs.Count >= 2 && run.hp * 2 >= run.maxHp)
                        run.deck.Remove(debuffs[run.rng.Next(debuffs.Count)]);
                    else
                        run.hp = Math.Min(run.maxHp, run.hp + Mathf.FloorToInt(run.maxHp * (w.cfg.restHealPercent / 100f)));
                    break;
            }
        }
        return true;
    }

    // 봇 이동: 이번 눈으로 갈 수 있는 가장 먼 칸들 중 하나를 고른다.
    static int ChooseTarget(MapData map, int current, int move, bool forceStop, System.Random rng)
    {
        if (!MapJourney.TryCollect(map, current, move, forceStop, out Dictionary<int, int> distance, out _)) return -1;
        int best = 0;
        foreach (var kv in distance) if (kv.Key != current && kv.Value > best) best = kv.Value;
        if (best == 0) return -1;
        var candidates = distance.Where(kv => kv.Key != current && kv.Value == best).Select(kv => kv.Key).ToList();
        return candidates[rng.Next(candidates.Count)];
    }

    static MonsterDataSO PickMonster(World w, BiomeDataSO biome, MapNodeType type, System.Random rng, out MonsterGrade grade)
    {
        MonsterDataSO m;
        switch (type)
        {
            case MapNodeType.Boss:
                grade = MonsterGrade.Boss;
                return biome.bossMonster;
            case MapNodeType.Portal:
                m = w.portalPool[rng.Next(w.portalPool.Count)];
                grade = m.monsterGrade;
                return m;
            case MapNodeType.Elite:
                grade = MonsterGrade.Elite;
                return PickGrade(biome.biomeMonsters, MonsterGrade.Elite, rng, true);
            default:
                grade = MonsterGrade.Normal;
                return PickGrade(biome.biomeMonsters, MonsterGrade.Normal, rng, false);
        }
    }

    // Enemy.PickBiomeMonster 와 같은 규칙
    static MonsterDataSO PickGrade(List<MonsterDataSO> list, MonsterGrade wanted, System.Random rng, bool elite)
    {
        var pool = list.Where(x => x != null && x.monsterGrade == wanted).ToList();
        if (pool.Count > 0) return pool[rng.Next(pool.Count)];
        if (elite) return list.Where(x => x != null).OrderByDescending(x => x.maxHp).First();
        var all = list.Where(x => x != null).ToList();
        return all[rng.Next(all.Count)];
    }

    // DiceManager.AddNodeDebuffs 와 같은 규칙. 엘리트·차원문은 바이옴 단계별 개수만큼 넣는다.
    static void AddNodeDebuffs(World w, RunState run, MapNodeType type, int tier)
    {
        int[] ones = { 1, 1, 1, 1, 1, 1 };
        switch (type)
        {
            case MapNodeType.Elite:
                for (int i = DebuffCount(w, run, tier); i > 0; i--)
                    run.deck.Add(new Die(run.rng.Next(2) == 0 ? DieKind.Thief : DieKind.Shackle, ones, true));
                break;
            case MapNodeType.Portal:
                for (int i = DebuffCount(w, run, tier); i > 0; i--)
                    run.deck.Add(new Die(run.rng.Next(2) == 0 ? DieKind.Spine : DieKind.Curse, BasicFaces, !w.cfg.portalDebuffPermanent));
                break;
            case MapNodeType.Boss:
                run.deck.Add(new Die(DieKind.Fake, new[] { 0, 0, 0, 0, 0, 0 }));
                break;
        }
    }

    static int DebuffCount(World w, RunState run, int tier)
    {
        int min = w.cfg.tierDebuffMin[Mathf.Clamp(tier, 0, w.cfg.tierDebuffMin.Length - 1)];
        int max = Math.Max(min, w.cfg.tierDebuffMax[Mathf.Clamp(tier, 0, w.cfg.tierDebuffMax.Length - 1)]);
        return run.rng.Next(min, max + 1);
    }

    // DiceManager.GetClearRewardGold 와 같은 계산
    static void GrantRewards(World w, RunState run, MonsterDataSO m, MonsterGrade grade, int tier)
    {
        int gold = Mathf.FloorToInt(w.BaseGold(m) * w.cfg.monsterGoldMultiplier);
        if (grade == MonsterGrade.Boss)
        {
            gold += w.cfg.biomeClearBonusGold;
            run.hp = Math.Min(run.maxHp, run.hp + Mathf.FloorToInt(run.maxHp * (w.cfg.biomeClearHealPercent / 100f)));
        }
        run.gold += Mathf.FloorToInt(gold * w.cfg.GoldMultiplierFor(grade) * w.cfg.TierGold(tier));
        if (m.dropFigureData != null && !run.figures.Contains(m.dropFigureData.name) && run.rng.NextDouble() < w.cfg.DropRateFor(grade))
            run.figures.Add(m.dropFigureData.name);
    }

    // 봇 상점: 진열된 칸 중 하나는 세일한다. 피규어를 살 수 있는 가장 비싼 것부터 사고,
    // 남은 골드로 코팅·스탬프를 비싼 것부터 사서 아직 강화 안 된 기본 주사위에 붙인다(리롤 안 함).
    static void VisitShop(World w, RunState run, MapNodeType type, int tier)
    {
        Func<ShopOffer, bool> sells = type == MapNodeType.ShopRonan ? (Func<ShopOffer, bool>)(o => o.ronan)
            : type == MapNodeType.ShopOpel ? (o => o.opel) : (o => o.nyaon);
        var shown = w.stock.Where(sells).OrderBy(_ => run.rng.Next()).Take(w.slotCount).ToList();
        if (shown.Count == 0) return;
        ShopOffer sale = shown[run.rng.Next(shown.Count)];
        float tierPrice = w.cfg.TierShopPrice(tier);
        Func<ShopOffer, int> price = o =>
        {
            int listed = Mathf.RoundToInt(o.price * tierPrice);
            return o == sale ? Mathf.FloorToInt(listed * w.cfg.shopSaleMultiplier) : listed;
        };

        foreach (var offer in shown.Where(o => o.cls == "FigureItemSO" && !run.figures.Contains(o.name)).OrderByDescending(price))
        {
            if (price(offer) > run.gold) continue;
            run.gold -= price(offer);
            run.figures.Add(offer.name);
        }
        foreach (var offer in shown.Where(o => o.cls == "CoatingItemSO" || o.cls == "StampItemSO").OrderByDescending(price))
        {
            if (price(offer) > run.gold) continue;
            bool coating = offer.cls == "CoatingItemSO";
            var target = run.deck.Where(d => d.kind == DieKind.Basic && (coating ? d.coating == null : d.stamp == null)).ToList();
            if (target.Count == 0) continue;
            run.gold -= price(offer);
            Die die = target[run.rng.Next(target.Count)];
            if (coating) die.coating = offer.name; else die.stamp = offer.name;
        }
    }

    // ---------------- 전투 ----------------

    static CombatResult Fight(World w, RunState run, MonsterDataSO m, MonsterGrade grade, int tier, int nodeInBiome)
    {
        BalanceConfig cfg = w.cfg;
        int maxHp = cfg.ScaleHp(w.BaseHp(m), tier, nodeInBiome, 1);
        int atk = cfg.ScaleAtk(w.BaseAtk(m), tier, nodeInBiome, 1);
        int enemyHp = maxHp;
        int maxTurn = cfg.AttackTurnFor(grade);
        int turnLeft = maxTurn;
        bool phase2 = false;
        bool devil = m.bossAbility == BossAbilityType.DevilDice;
        int devilPhase = 1;
        int baseAtk = atk;

        var draw = new List<Die>(run.deck);
        Shuffle(draw, run.rng);
        var discard = new List<Die>();
        var result = new CombatResult();
        int shield = 0; // 하트 스탬프 보호막. 전투마다 초기화된다.

        while (result.turns < MaxTurnsPerCombat)
        {
            result.turns++;
            if (draw.Count < 5) { draw.AddRange(discard); discard.Clear(); Shuffle(draw, run.rng); }
            var hand = draw.Take(5).ToList();
            draw.RemoveRange(0, hand.Count);

            int rerolls = cfg.maxRerolls;
            foreach (var d in hand)
            {
                if (d.kind == DieKind.Thief) run.gold -= Math.Min(100, Math.Max(0, run.gold));
                else if (d.kind == DieKind.Shackle) rerolls--;
            }
            rerolls = Math.Max(0, rerolls);

            // 악마주사위: 매 턴 손의 주사위 1개에 저주, 2페이즈부터 원페어·투페어 피해 무효
            if (devil)
            {
                foreach (Die d in run.deck) d.devilCursed = false;
                var targets = hand.Where(d => d.kind == DieKind.Basic).ToList();
                if (targets.Count > 0) targets[run.rng.Next(targets.Count)].devilCursed = true;
            }
            BlockPairs = devil && devilPhase >= 2;

            int[] values = hand.Select(d => d.faces[run.rng.Next(6)]).ToArray();
            for (int r = 0; r < rerolls; r++)
            {
                int mask = BestRerollMask(hand, values, run.FigureCount, enemyHp, run.rng);
                if (mask == 0) break;
                for (int i = 0; i < hand.Count; i++)
                    if ((mask & (1 << i)) != 0) values[i] = hand[i].faces[run.rng.Next(6)];
            }

            // 결산: 가시 피해 → 피해량 → 전투 코인(여정 주사위 코인 면은 눈 x10)
            int spine = 0;
            for (int i = 0; i < hand.Count; i++) if (hand[i].kind == DieKind.Spine) spine += values[i] * 2;
            if (spine > 0) { run.hp -= spine; if (run.hp <= 0) { result.won = false; return result; } }

            int dmg = Damage(hand, values, run.FigureCount, enemyHp);
            enemyHp -= dmg;
            for (int i = 0; i < hand.Count; i++)
            {
                if (hand[i].coating == "Gold") run.gold += values[i] * TurnCalculator.GoldPerPip;
                if (hand[i].stamp == "Diamond") run.gold += values[i] * TurnCalculator.DiamondGoldPerPip;
                if (hand[i].stamp == "Heart") shield += TurnCalculator.HeartShield;
            }
            discard.AddRange(hand);
            if (enemyHp <= 0) { EndCombat(run); result.won = true; return result; }

            if (devil)
            {
                float ratio = (float)enemyHp / maxHp;
                int phase = ratio <= cfg.devilPhase3HpRatio ? 3 : ratio <= cfg.devilPhase2HpRatio ? 2 : 1;
                if (phase == 3 && devilPhase < 3) RollDevilPattern(run, baseAtk, ref atk, ref maxTurn, ref turnLeft);
                devilPhase = Math.Max(devilPhase, phase);
            }

            if (!phase2 && grade == MonsterGrade.Boss && enemyHp <= maxHp * cfg.bossPhase2HpRatio)
            {
                phase2 = true;
                maxTurn = Math.Max(1, Math.Min(maxTurn, cfg.bossPhase2AttackTurn));
                turnLeft = Math.Min(turnLeft, maxTurn);
            }

            turnLeft--;
            if (turnLeft <= 0)
            {
                int blocked = Math.Min(shield, atk);
                shield -= blocked;
                run.hp -= atk - blocked;
                result.hits++;
                turnLeft = maxTurn;
                if (devil && devilPhase >= 3) RollDevilPattern(run, baseAtk, ref atk, ref maxTurn, ref turnLeft);
                if (run.hp <= 0) { EndCombat(run); result.won = false; return result; }
            }
        }
        EndCombat(run);
        result.won = false;
        return result;
    }

    // DevilDiceBoss.RollPattern 과 같은 표: 눈 1~2 1턴 x0.6, 3~4 2턴 x1, 5~6 3턴 x1.8
    static void RollDevilPattern(RunState run, int baseAtk, ref int atk, ref int maxTurn, ref int turnLeft)
    {
        int face = run.rng.Next(6);
        int[] turns = { 1, 1, 2, 2, 3, 3 };
        float[] mult = { 0.6f, 0.6f, 1f, 1f, 1.8f, 1.8f };
        maxTurn = turns[face];
        turnLeft = maxTurn;
        atk = Math.Max(1, Mathf.RoundToInt(baseAtk * mult[face]));
    }

    static void EndCombat(RunState run)
    {
        BlockPairs = false;
        foreach (Die d in run.deck) d.devilCursed = false;
    }

    // 악마주사위 2페이즈: 원페어·투페어는 피해 0 (봇의 기대 피해 계산에도 반영)
    static bool BlockPairs;

    // TurnCalculator 와 같은 순서: (눈 합 + 얼음·스페이드 칩) x (족보 + 피규어 + 프리즘 배수) x 저주, 이어서 스페이드 미사용 눈·어둠 피해를 더한다.
    static int Damage(List<Die> hand, int[] values, int figures, int enemyHp)
    {
        int chips = 0, flat = 0, darkCount = 0;
        float curse = 1f, prism = 0f;
        float handMult = HandMultiplier(values);
        bool straight = handMult == HandMult[6];
        for (int i = 0; i < values.Length; i++)
        {
            chips += values[i];
            if (hand[i].kind == DieKind.Curse || hand[i].devilCursed) curse *= Mathf.Clamp01(1f - values[i] * 0.05f);
            switch (hand[i].coating)
            {
                case "Prism": prism += PrismMultBonus; break;
                case "Ice": chips += 10; break;
                case "Dark": darkCount++; break;
            }
            if (hand[i].stamp == "Spade")
            {
                bool used = straight || CountOf(values, values[i]) >= 2;
                if (used) chips += TurnCalculator.SpadeChips; else flat += Mathf.Max(0, values[i]);
            }
        }
        if (BlockPairs && (handMult == HandMult[1] || handMult == HandMult[2])) return 0;
        float mult = (handMult + figures * FigureMultBonus + prism) * curse;
        int damage = Mathf.FloorToInt(chips * mult) + flat;
        int hp = enemyHp;
        for (int i = 0; i < darkCount; i++) { int drop = Mathf.FloorToInt(hp * 0.1f); damage += drop; hp -= drop; }
        return damage;
    }

    static int CountOf(int[] values, int v)
    {
        int c = 0;
        foreach (int x in values) if (x == v) c++;
        return c;
    }

    // TurnCalculator.CalculateHand 와 같은 판정. 눈은 0~6 이라 배열로 센다.
    static readonly int[] countBuffer = new int[7];
    static float HandMultiplier(int[] v)
    {
        Array.Clear(countBuffer, 0, countBuffer.Length);
        int min = int.MaxValue, max = int.MinValue;
        foreach (int x in v)
        {
            countBuffer[Mathf.Clamp(x, 0, 6)]++;
            if (x < min) min = x;
            if (x > max) max = x;
        }
        int pairs = 0, unique = 0;
        bool triple = false, four = false, yacht = false;
        foreach (int c in countBuffer)
        {
            if (c > 0) unique++;
            if (c == 2) pairs++;
            else if (c == 3) triple = true;
            else if (c == 4) four = true;
            else if (c == 5) yacht = true;
        }
        if (yacht) return HandMult[7];
        if (v.Length == 5 && unique == 5 && countBuffer[0] == 0 && max - min == 4) return HandMult[6];
        if (four) return HandMult[5];
        if (triple && pairs > 0) return HandMult[4];
        if (triple) return HandMult[3];
        if (pairs == 2) return HandMult[2];
        if (pairs > 0) return HandMult[1];
        return HandMult[0];
    }

    // 봇: 남길 주사위 조합마다 다시 굴린 결과의 기대 피해를 몬테카를로로 재고, 기대 피해가 가장 큰 조합을 고른다.
    const int Samples = 24;
    static int BestRerollMask(List<Die> hand, int[] values, int figures, int enemyHp, System.Random rng)
    {
        int n = hand.Count;
        int bestMask = 0;
        float bestValue = Damage(hand, values, figures, enemyHp);
        int[] trial = new int[n];
        for (int mask = 1; mask < (1 << n); mask++)
        {
            float total = 0f;
            for (int s = 0; s < Samples; s++)
            {
                for (int i = 0; i < n; i++)
                    trial[i] = (mask & (1 << i)) != 0 ? hand[i].faces[rng.Next(6)] : values[i];
                total += Damage(hand, trial, figures, enemyHp);
            }
            float avg = total / Samples;
            if (avg > bestValue + 0.01f) { bestValue = avg; bestMask = mask; }
        }
        return bestMask;
    }

    static void Shuffle<T>(List<T> list, System.Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    static void RecordCombat(Stats s, bool forest, MonsterGrade grade, MapNodeType type, CombatResult r)
    {
        if (!r.won) return;
        if (type == MapNodeType.Portal) return; // 차원문은 다른 바이옴 몬스터라 지표에서 뺀다
        if (forest)
        {
            if (grade == MonsterGrade.Normal) { s.forestNormalTurns.Add(r.turns); s.forestNormalHits.Add(r.hits); }
            else if (grade == MonsterGrade.Elite) { s.forestEliteTurns.Add(r.turns); s.forestEliteHits.Add(r.hits); }
            else { s.forestBossTurns.Add(r.turns); s.forestBossHits.Add(r.hits); }
        }
        else
        {
            if (grade == MonsterGrade.Normal) s.secondNormalTurns.Add(r.turns);
            else if (grade == MonsterGrade.Elite) s.secondEliteTurns.Add(r.turns);
            else s.secondBossTurns.Add(r.turns);
        }
    }

    static string GradeName(MonsterGrade g) => g == MonsterGrade.Boss ? "보스" : g == MonsterGrade.Elite ? "엘리트" : "일반";

    // ---------------- 보고서 ----------------

    static float Avg(List<int> l) => l.Count > 0 ? (float)l.Average() : float.NaN;
    static float Avg(List<float> l) => l.Count > 0 ? l.Average() : float.NaN;

    static string Check(float v, float lo, float hi) => float.IsNaN(v) ? "-" : v < lo ? "낮음" : v > hi ? "높음" : "OK";

    static string BuildReport(World w, Stats s, int runs, int seed, double seconds, string set, string scale)
    {
        BalanceConfig c = w.cfg;
        float forestDeath = 100f * s.forestDeaths / runs;
        float secondKill = 100f * s.secondBossKilled / runs;
        var sb = new StringBuilder();
        sb.AppendLine("# 데모 밸런스 시뮬레이션 결과");
        sb.AppendLine();
        sb.AppendLine($"- 봇 {runs}판, 시드 {seed}, 소요 {seconds:F1}초. `BalanceSimulator.RunBatch` 로 생성.");
        sb.AppendLine("- 범위: 숲 → 숲에서 갈 수 있는 바이옴(초원·정글·동굴) 중 무작위 1개 → 두 번째 보스까지.");
        if (!string.IsNullOrEmpty(set)) sb.AppendLine($"- 설정 덮어쓰기(에셋에는 미반영): `{set}`");
        if (!string.IsNullOrEmpty(scale)) sb.AppendLine($"- 몬스터 기본 수치 배율(JSON 에는 미반영): `{scale}`");
        sb.AppendLine();
        sb.AppendLine("## 목표 대비");
        sb.AppendLine();
        sb.AppendLine("| 지표 | 목표 | 결과 | 판정 |");
        sb.AppendLine("|---|---|---|---|");
        float fn = Avg(s.forestNormalTurns), fe = Avg(s.forestEliteTurns), arrival = Avg(s.forestBossArrivalHpPct), shop = Avg(s.firstShopGold);
        sb.AppendLine($"| 숲 일반 전투 평균 턴 | 2.3~3.0 | {fn:F2} (n={s.forestNormalTurns.Count}) | {Check(fn, 2.3f, 3.0f)} |");
        sb.AppendLine($"| 숲 엘리트 평균 턴 | 4~5 | {fe:F2} (n={s.forestEliteTurns.Count}) | {Check(fe, 4f, 5f)} |");
        sb.AppendLine($"| 숲 보스 도착 시 남은 체력 | 40~55% | {arrival:F1}% (n={s.forestBossArrivalHpPct.Count}) | {Check(arrival, 40f, 55f)} |");
        sb.AppendLine($"| 숲 탈락률 | 35~45% | {forestDeath:F1}% | {Check(forestDeath, 35f, 45f)} |");
        if (w.biomeCount > 2)
        {
            // 데모가 3바이옴 이상이면 마지막 보스를 데모 클리어 목표로 보고, 두 번째 보스는 중간 목표로 둔다.
            float demoClear = 100f * s.tierCleared[w.biomeCount - 1] / runs;
            sb.AppendLine($"| 두 번째 바이옴 보스 처치율 | 35~45% | {secondKill:F1}% | {Check(secondKill, 35f, 45f)} |");
            if (w.biomeCount > 3)
                sb.AppendLine($"| 최종 클리어({w.biomeCount}번째 비차원 보스) 처치율 | 10~20% | {demoClear:F1}% | {Check(demoClear, 10f, 20f)} |");
            else
                sb.AppendLine($"| 데모 클리어({w.biomeCount}번째 보스) 처치율 | 20~30% | {demoClear:F1}% | {Check(demoClear, 20f, 30f)} |");
        }
        else
            sb.AppendLine($"| 두 번째 바이옴 보스 처치율 | 20~30% | {secondKill:F1}% | {Check(secondKill, 20f, 30f)} |");
        sb.AppendLine($"| 첫 상점 도착 골드 | 350~450 | {shop:F0} (n={s.firstShopGold.Count}) | {Check(shop, 350f, 450f)} |");
        sb.AppendLine();
        sb.AppendLine("## 참고 지표");
        sb.AppendLine();
        sb.AppendLine("| 지표 | 값 |");
        sb.AppendLine("|---|---|");
        sb.AppendLine($"| 숲 일반 / 엘리트 / 보스 평균 피격 횟수 | {Avg(s.forestNormalHits):F2} / {Avg(s.forestEliteHits):F2} / {Avg(s.forestBossHits):F2} |");
        sb.AppendLine($"| 숲 보스 평균 턴 | {Avg(s.forestBossTurns):F2} (n={s.forestBossTurns.Count}) |");
        sb.AppendLine($"| 숲 보스 도달 / 처치 | {s.forestBossReached} / {s.forestBossKilled} |");
        sb.AppendLine($"| 숲 보스 전까지 전투 수 평균 | {Avg(s.forestCombats):F1} |");
        sb.AppendLine($"| 숲 보스 도착 시 피규어 수 평균 | {Avg(s.figuresAtForestBoss):F2} |");
        sb.AppendLine($"| 두 번째 바이옴 일반 / 엘리트 / 보스 평균 턴 | {Avg(s.secondNormalTurns):F2} / {Avg(s.secondEliteTurns):F2} / {Avg(s.secondBossTurns):F2} |");
        sb.AppendLine($"| 두 번째 보스 도달 / 처치 | {s.secondBossReached} / {s.secondBossKilled} |");
        sb.AppendLine($"| 첫 상점 골드 최소 / 중앙 / 최대 | {Pct(s.firstShopGold, 0f)} / {Pct(s.firstShopGold, 0.5f)} / {Pct(s.firstShopGold, 1f)} |");
        sb.AppendLine();
        sb.AppendLine("| 두 번째 바이옴 | 도착 | 보스 처치 | 처치율 |");
        sb.AppendLine("|---|---|---|---|");
        foreach (var kv in s.secondBiome.OrderBy(k => k.Key.ToString()))
            sb.AppendLine($"| {kv.Key} | {kv.Value[0]} | {kv.Value[1]} | {(kv.Value[0] > 0 ? 100f * kv.Value[1] / kv.Value[0] : 0f):F1}% |");
        sb.AppendLine();
        sb.AppendLine("| 탈락 위치 | 판 수 |");
        sb.AppendLine("|---|---|");
        foreach (var kv in s.deathCause.OrderByDescending(k => k.Value))
            sb.AppendLine($"| {kv.Key} | {kv.Value} |");
        sb.AppendLine();
        if (w.biomeCount > 2)
        {
            sb.AppendLine($"## 바이옴 단계별 생존 ({w.biomeCount}개까지)");
            sb.AppendLine();
            sb.AppendLine("| 단계 | 도착 | 보스 처치 | 처치율(전체 대비) | 도착 시 체력 | 보스 도착 시 체력 |");
            sb.AppendLine("|---|---|---|---|---|---|");
            for (int i = 0; i < w.biomeCount; i++)
                sb.AppendLine($"| {i + 1} | {s.tierReached[i]} | {s.tierCleared[i]} | {100f * s.tierCleared[i] / runs:F1}% | {Avg(s.tierArrivalHp[i]):F0}% | {Avg(s.tierBossArrivalHp[i]):F0}% |");
            sb.AppendLine();
        }
        sb.AppendLine("## 사용한 설정");
        sb.AppendLine();
        sb.AppendLine($"- 플레이어 체력 {c.playerMaxHP}, 시작 골드 {c.startGold}, 굴리기 {c.maxRerolls}, 휴식 {c.restHealPercent}%, 바이옴 클리어 회복 {c.biomeClearHealPercent}%");
        sb.AppendLine($"- 공격 턴 일반 {c.normalAttackTurn} / 엘리트 {c.eliteAttackTurn} / 보스 {c.bossAttackTurn} (체력 {c.bossPhase2HpRatio:P0} 이하 {c.bossPhase2AttackTurn})");
        sb.AppendLine($"- tierHp [{string.Join(", ", c.tierHp)}], tierAtk [{string.Join(", ", c.tierAtk)}], 전투당 체력 +{c.hpPerNodeInBiome:P0}, 공격력 +1 / {c.nodesPerAtkStep}전투");
        sb.AppendLine($"- 몬스터 골드 x{c.monsterGoldMultiplier}, 바이옴 클리어 {c.biomeClearBonusGold}, 여정 골드 눈당 {c.journeyGoldPerPip}");
        sb.AppendLine($"- 드롭률 {c.normalDropRate} / {c.eliteDropRate} / {c.bossDropRate}");
        sb.AppendLine();
        sb.AppendLine("## 봇 규칙과 단순화");
        sb.AppendLine();
        sb.AppendLine("- 주사위: 매 턴 덱(기본 11 + 여정 1 + 디버프)에서 5개를 뽑고, 굴리기마다 남길 조합 32가지의 기대 피해를 24회 몬테카를로로 재서 가장 큰 쪽을 고른다.");
        sb.AppendLine($"- 상점: 진열 칸 하나는 x{w.cfg.shopSaleMultiplier} 세일. 피규어를 살 수 있는 가장 비싼 것부터 사고, 남은 골드로 코팅·스탬프를 비싼 것부터 사서 강화 안 된 기본 주사위에 붙인다. 주사위·스낵·위성은 사지 않고 리롤도 안 한다.");
        sb.AppendLine($"- 피규어 효과는 1개당 족보 배수 +{FigureMultBonus} 로 근사한다. 코팅(프리즘 +{PrismMultBonus} 배수, 얼음 +10 칩, 황금 골드, 어둠 10% 피해)과 스탬프(스페이드 칩/피해, 하트 보호막, 다이아 골드)는 실제 수치로 넣었고, 클로버·시너지·스낵·위성·조우 이벤트 효과는 없다.");
        sb.AppendLine("- 지도: 실제 MapGenerator 로 만든 지도에서 여정 주사위를 굴려 갈 수 있는 가장 먼 칸 중 하나로 이동한다. 휴식 칸에서는 항상 회복한다.");
        sb.AppendLine("- 디버프: 엘리트 도둑·족쇄 3개(임시), 차원문 가시·저주 1개, 보스 가짜 1개(영구)를 실제 규칙대로 덱에 넣는다.");
        sb.AppendLine("- 차원문 전투는 다른 바이옴 몬스터라 턴·피격 지표에서 뺐다(탈락에는 포함).");
        return sb.ToString();
    }

    static string Pct(List<int> l, float p)
    {
        if (l.Count == 0) return "-";
        var sorted = l.OrderBy(x => x).ToList();
        return sorted[Mathf.Clamp(Mathf.RoundToInt(p * (sorted.Count - 1)), 0, sorted.Count - 1)].ToString();
    }
}
#endif
