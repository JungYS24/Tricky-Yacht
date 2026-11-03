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
public static class BalanceSimulator
{
    const string BiomeFolder = "Assets/Contents/10_Resources/Data/DiceData/Map";
    const string MapTablePath = "Assets/Contents/10_Resources/Data/Map_Table.json";
    const string StockTablePath = "Assets/Contents/10_Resources/Data/Shop_Stock_Table.json";
    const string DefaultOut = "Docs/Balancing/SimResult_v1.md";

    // 피규어 효과는 종류가 많아 재현하지 않고, 1개당 족보 배수 +0.1 로 근사한다.
    const float FigureMultBonus = 0.1f;
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
        try
        {
            if (!string.IsNullOrEmpty(set)) BalanceConfig.OverrideForTesting(ApplyOverrides(BalanceConfig.Current, set));
            Run(runs, seed, output, set, scale);
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
            if (kv.Length != 2 || key.Length != 2 || !Enum.TryParse(key[0], out MonsterGrade grade)) continue;
            int stat = key[1] == "hp" ? 0 : key[1] == "atk" ? 1 : key[1] == "gold" ? 2 : -1;
            if (stat < 0) continue;
            result[(int)grade, stat] = float.Parse(kv[1], System.Globalization.CultureInfo.InvariantCulture);
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
        public int BaseHp(MonsterDataSO m) => Mathf.RoundToInt(m.maxHp * scale[(int)m.monsterGrade, 0]);
        public int BaseAtk(MonsterDataSO m) => Mathf.RoundToInt(m.baseAtk * scale[(int)m.monsterGrade, 1]);
        public int BaseGold(MonsterDataSO m) => Mathf.RoundToInt(m.dropGold * scale[(int)m.monsterGrade, 2]);
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

    public static string Run(int runs, int seed, string outputPath, string set = null, string scale = null)
    {
        World world = LoadWorld();
        world.scale = ParseScale(scale);
        var stats = new Stats { runs = runs };
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
        for (int tier = 0; tier < DemoBiomeCount; tier++)
        {
            if (tier == 1)
            {
                var options = new BiomeNavigator().BiomeRoutes[BiomeType.Forest];
                biome = options[rng.Next(options.Count)];
                if (!s.secondBiome.ContainsKey(biome)) s.secondBiome[biome] = new int[2];
                s.secondBiome[biome][0]++;
            }
            bool alive = SimulateBiome(w, s, run, biome, tier, runSeed);
            if (!alive) return;
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
                        else s.secondBossReached++;
                    }
                    AddNodeDebuffs(run, node.type);
                    CombatResult r = Fight(w, run, monster, grade, tier, combatsInBiome);
                    run.deck.RemoveAll(d => d.temporary);
                    combatsInBiome++;
                    RecordCombat(s, forest, grade, node.type, r);
                    if (!r.won)
                    {
                        string key = (forest ? "숲 " : "2번째 바이옴 ") + GradeName(grade);
                        s.deathCause[key] = s.deathCause.TryGetValue(key, out int c) ? c + 1 : 1;
                        if (forest) s.forestDeaths++; else s.secondDeaths++;
                        return false;
                    }
                    GrantRewards(w, run, monster, grade);
                    if (node.type == MapNodeType.Boss)
                    {
                        if (forest) s.forestBossKilled++;
                        else
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
                    VisitShop(w, run, node.type);
                    break;
                case MapNodeType.Rest:
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

    // DiceManager.AddNodeDebuffs 와 같은 규칙
    static void AddNodeDebuffs(RunState run, MapNodeType type)
    {
        int[] ones = { 1, 1, 1, 1, 1, 1 };
        switch (type)
        {
            case MapNodeType.Elite:
                for (int i = 0; i < 3; i++)
                    run.deck.Add(new Die(run.rng.Next(2) == 0 ? DieKind.Thief : DieKind.Shackle, ones, true));
                break;
            case MapNodeType.Portal:
                run.deck.Add(new Die(run.rng.Next(2) == 0 ? DieKind.Spine : DieKind.Curse, BasicFaces));
                break;
            case MapNodeType.Boss:
                run.deck.Add(new Die(DieKind.Fake, new[] { 0, 0, 0, 0, 0, 0 }));
                break;
        }
    }

    static void GrantRewards(World w, RunState run, MonsterDataSO m, MonsterGrade grade)
    {
        run.gold += Mathf.FloorToInt(w.BaseGold(m) * w.cfg.monsterGoldMultiplier);
        if (grade == MonsterGrade.Boss) run.gold += w.cfg.biomeClearBonusGold;
        if (m.dropFigureData != null && !run.figures.Contains(m.dropFigureData.name) && run.rng.NextDouble() < w.cfg.DropRateFor(grade))
            run.figures.Add(m.dropFigureData.name);
    }

    // 봇 상점: 진열된 피규어 중 살 수 있는 가장 비싼 것부터 산다(리롤 안 함).
    static void VisitShop(World w, RunState run, MapNodeType type)
    {
        Func<ShopOffer, bool> sells = type == MapNodeType.ShopRonan ? (Func<ShopOffer, bool>)(o => o.ronan)
            : type == MapNodeType.ShopOpel ? (o => o.opel) : (o => o.nyaon);
        var shown = w.stock.Where(sells).OrderBy(_ => run.rng.Next()).Take(w.slotCount).ToList();
        foreach (var offer in shown.Where(o => o.cls == "FigureItemSO" && !run.figures.Contains(o.name)).OrderByDescending(o => o.price))
        {
            if (offer.price > run.gold) continue;
            run.gold -= offer.price;
            run.figures.Add(offer.name);
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

        var draw = new List<Die>(run.deck);
        Shuffle(draw, run.rng);
        var discard = new List<Die>();
        var result = new CombatResult();

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

            int[] values = hand.Select(d => d.faces[run.rng.Next(6)]).ToArray();
            for (int r = 0; r < rerolls; r++)
            {
                int mask = BestRerollMask(hand, values, run.FigureCount, run.rng);
                if (mask == 0) break;
                for (int i = 0; i < hand.Count; i++)
                    if ((mask & (1 << i)) != 0) values[i] = hand[i].faces[run.rng.Next(6)];
            }

            // 결산: 가시 피해 → 피해량 → 전투 코인(여정 주사위 코인 면은 눈 x10)
            int spine = 0;
            for (int i = 0; i < hand.Count; i++) if (hand[i].kind == DieKind.Spine) spine += values[i] * 2;
            if (spine > 0) { run.hp -= spine; if (run.hp <= 0) { result.won = false; return result; } }

            int dmg = Damage(hand, values, run.FigureCount);
            enemyHp -= dmg;
            discard.AddRange(hand);
            if (enemyHp <= 0) { result.won = true; return result; }

            if (!phase2 && grade == MonsterGrade.Boss && enemyHp <= maxHp * cfg.bossPhase2HpRatio)
            {
                phase2 = true;
                maxTurn = Math.Max(1, Math.Min(maxTurn, cfg.bossPhase2AttackTurn));
                turnLeft = Math.Min(turnLeft, maxTurn);
            }

            turnLeft--;
            if (turnLeft <= 0)
            {
                run.hp -= atk;
                result.hits++;
                turnLeft = maxTurn;
                if (run.hp <= 0) { result.won = false; return result; }
            }
        }
        result.won = false;
        return result;
    }

    static int Damage(List<Die> hand, int[] values, int figures)
    {
        int sum = 0;
        float curse = 1f;
        for (int i = 0; i < values.Length; i++)
        {
            sum += values[i];
            if (hand[i].kind == DieKind.Curse) curse *= Mathf.Clamp01(1f - values[i] * 0.05f);
        }
        float mult = (HandMultiplier(values) + figures * FigureMultBonus) * curse;
        return Mathf.FloorToInt(sum * mult);
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
    static int BestRerollMask(List<Die> hand, int[] values, int figures, System.Random rng)
    {
        int n = hand.Count;
        int bestMask = 0;
        float bestValue = Damage(hand, values, figures);
        int[] trial = new int[n];
        for (int mask = 1; mask < (1 << n); mask++)
        {
            float total = 0f;
            for (int s = 0; s < Samples; s++)
            {
                for (int i = 0; i < n; i++)
                    trial[i] = (mask & (1 << i)) != 0 ? hand[i].faces[rng.Next(6)] : values[i];
                total += Damage(hand, trial, figures);
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
        sb.AppendLine("## 사용한 설정");
        sb.AppendLine();
        sb.AppendLine($"- 플레이어 체력 {c.playerMaxHP}, 시작 골드 {c.startGold}, 굴리기 {c.maxRerolls}, 휴식 {c.restHealPercent}%");
        sb.AppendLine($"- 공격 턴 일반 {c.normalAttackTurn} / 엘리트 {c.eliteAttackTurn} / 보스 {c.bossAttackTurn} (체력 {c.bossPhase2HpRatio:P0} 이하 {c.bossPhase2AttackTurn})");
        sb.AppendLine($"- tierHp [{string.Join(", ", c.tierHp)}], tierAtk [{string.Join(", ", c.tierAtk)}], 전투당 체력 +{c.hpPerNodeInBiome:P0}, 공격력 +1 / {c.nodesPerAtkStep}전투");
        sb.AppendLine($"- 몬스터 골드 x{c.monsterGoldMultiplier}, 바이옴 클리어 {c.biomeClearBonusGold}, 여정 골드 눈당 {c.journeyGoldPerPip}");
        sb.AppendLine($"- 드롭률 {c.normalDropRate} / {c.eliteDropRate} / {c.bossDropRate}");
        sb.AppendLine();
        sb.AppendLine("## 봇 규칙과 단순화");
        sb.AppendLine();
        sb.AppendLine("- 주사위: 매 턴 덱(기본 11 + 여정 1 + 디버프)에서 5개를 뽑고, 굴리기마다 남길 조합 32가지의 기대 피해를 24회 몬테카를로로 재서 가장 큰 쪽을 고른다.");
        sb.AppendLine("- 상점: 진열된 피규어 중 살 수 있는 가장 비싼 것부터 산다. 주사위·스낵·코팅 등은 사지 않고, 리롤도 하지 않는다.");
        sb.AppendLine($"- 피규어 효과는 1개당 족보 배수 +{FigureMultBonus} 로 근사한다. 스낵·코팅·스탬프·위성·조우 이벤트 효과는 없다.");
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
