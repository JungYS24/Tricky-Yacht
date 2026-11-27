#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 전리품 루트 테이블 v2 검증.
// - 바이옴 단계 0~2 × 일반·엘리트·보스, 각 10,000 화면(시드 고정) 등급 비율 vs 표(±1%p)
// - 2차 항목(Enabled=false) 미등장, 같은 Group_ID 중복 0
// - 레전드 천장 15 화면
// - 아이콘·에셋 참조(빈 아이콘 0, Missing 0)
// 메뉴: Studio 10&6 / Reward Loot / Verify Loot Table
// 배치: -executeMethod RewardLootVerifier.RunBatch -rewardVerifyOut <md 경로>
public static class RewardLootVerifier
{
    const string TablePath = "Assets/Contents/10_Resources/Data/Reward_Loot_Table.json";
    const string CatalogPath = "Assets/Contents/10_Resources/Data/RewardAssetCatalog.asset";
    const string ScenePath = "Assets/Contents/01_Scenes/MainScene_Map.unity";
    const int Screens = 10000;
    const float Tolerance = 0.01f;
    static readonly ItemGrade[] Grades = { ItemGrade.Normal, ItemGrade.Rare, ItemGrade.Epic, ItemGrade.Legendary };

    [MenuItem("Studio 10&6/Reward Loot/Verify Loot Table")]
    public static void RunMenu()
    {
        string md = Verify(out bool ok, checkScene: false);
        Debug.Log(md);
        EditorUtility.DisplayDialog("Reward Loot", ok ? "모든 검증 통과" : "실패 항목이 있습니다. 콘솔을 보세요.", "확인");
    }

    // 플레이 중에 1차 효과 17종을 하나씩 강제로 실행한다(선택 창은 첫 카드를 자동 선택). 결과는 콘솔에 남는다.
    [MenuItem("Studio 10&6/Reward Loot/Debug: Run 17 Effects (Play Mode)")]
    public static void RunEffectsInPlayMode()
    {
        if (!EditorApplication.isPlaying || DiceManager.Instance == null)
        {
            EditorUtility.DisplayDialog("Reward Loot", "MainScene_Map 을 플레이 중일 때만 쓸 수 있습니다.", "확인");
            return;
        }
        RewardLootDebug.StartEffectRun();
    }

    [MenuItem("Studio 10&6/Reward Loot/Debug: Run 17 Effects (Play Mode)", true)]
    static bool RunEffectsInPlayModeValidate() => EditorApplication.isPlaying;

    public static void RunBatch()
    {
        string outPath = ArgAfter("-rewardVerifyOut");
        string md = Verify(out bool ok, checkScene: true);
        if (!string.IsNullOrEmpty(outPath)) File.WriteAllText(outPath, md, new UTF8Encoding(false));
        Debug.Log(md);
        Debug.Log(ok ? "[RewardLootVerifier] PASS" : "[RewardLootVerifier] FAIL");
        EditorApplication.Exit(ok ? 0 : 1);
    }

    static string ArgAfter(string flag)
    {
        string[] args = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(args, flag);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    public static string Verify(out bool ok, bool checkScene)
    {
        ok = true;
        var sb = new StringBuilder();
        var tableAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(TablePath);
        RewardLootTable table = RewardLootTable.Parse(tableAsset != null ? tableAsset.text : null);
        if (!table.Loaded)
        {
            ok = false;
            return "표를 읽지 못했습니다: " + TablePath;
        }

        int enabled = table.Rows.Count(RewardLootTable.IsRollable);
        int disabled = table.Rows.Count(r => !r.Enabled);
        sb.AppendLine("## 6. 추첨 검증 결과 (Reward_UI 1차)");
        sb.AppendLine();
        sb.AppendLine($"> 표 {table.Version}, 보상 {table.Rows.Count}개(1차 {enabled}개, 2차 {disabled}개). `RewardLootVerifier`(Studio 10&6 / Reward Loot / Verify Loot Table)로 생성.");
        sb.AppendLine($"> 조건마다 {Screens:N0} 화면 × 3칸 = {Screens * 3:N0}장, 시드 고정. 천장 영향을 빼려고 등급 비율 측정은 매 화면 천장 카운터 0으로 굴림. 허용 오차 ±{Tolerance * 100:0}%p.");
        sb.AppendLine();
        sb.AppendLine("### 6-1. 등급 비율 (표 → 실측, 단위 %)");
        sb.AppendLine();
        sb.AppendLine("| 단계 | 표 | 노멀 | 레어 | 에픽 | 레전드 | 2차 등장 | Group 중복 | 결과 |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|---|");

        MonsterGrade[] monsters = { MonsterGrade.Normal, MonsterGrade.Elite, MonsterGrade.Boss };
        string[] monsterNames = { "일반", "엘리트", "보스" };
        for (int tier = 0; tier <= 2; tier++)
        {
            for (int m = 0; m < monsters.Length; m++)
            {
                var rng = new System.Random(20261128 + tier * 100 + m);
                var counts = new int[4];
                int phase2 = 0, dupGroups = 0, cards = 0;
                for (int s = 0; s < Screens; s++)
                {
                    RewardRollResult r = RewardRoller.Roll(table, monsters[m], tier, 0, rng);
                    var groups = new HashSet<string>();
                    foreach (RewardRow row in r.Slots)
                    {
                        if (row == null) continue;
                        cards++;
                        counts[Array.IndexOf(Grades, row.ItemGrade)]++;
                        if (!row.Enabled) phase2++;
                        if (!string.IsNullOrEmpty(row.Group_ID) && !groups.Add(row.Group_ID)) dupGroups++;
                    }
                }

                RewardGradeWeightRow w = table.WeightRowFor(monsters[m], tier);
                bool pass = phase2 == 0 && dupGroups == 0;
                var cells = new List<string>();
                for (int g = 0; g < 4; g++)
                {
                    float expected = w.WeightOf(Grades[g]);
                    float actual = cards > 0 ? counts[g] / (float)cards : 0f;
                    bool within = Mathf.Abs(actual - expected) <= Tolerance;
                    pass &= within;
                    cells.Add($"{expected * 100:0} → {actual * 100:0.00}{(within ? "" : " ❌")}");
                }
                ok &= pass;
                sb.AppendLine($"| {tier} | {monsterNames[m]} | {string.Join(" | ", cells)} | {phase2} | {dupGroups} | {(pass ? "통과" : "실패")} |");
            }
        }

        sb.AppendLine();
        sb.AppendLine("### 6-2. 레전드 천장");
        sb.AppendLine();
        sb.AppendLine("| 시험 | 기대 | 실측 | 결과 |");
        sb.AppendLine("|---|---|---|---|");
        ok &= PityTests(table, sb);

        sb.AppendLine();
        sb.AppendLine("### 6-3. 데이터·참조");
        sb.AppendLine();
        sb.AppendLine("| 항목 | 결과 |");
        sb.AppendLine("|---|---|");
        ok &= ReferenceTests(table, sb, checkScene);
        return sb.ToString();
    }

    // 레전드 가중치를 0으로 만든 표로, 천장만으로 레전드가 나오는 화면을 센다.
    static bool PityTests(RewardLootTable table, StringBuilder sb)
    {
        bool ok = true;
        int pity = table.LegendaryPityScreens;
        RewardLootTable noLegend = RewardLootTable.Parse(AssetDatabase.LoadAssetAtPath<TextAsset>(TablePath).text);
        foreach (var list in new[] { noLegend.Weights.standard, noLegend.Weights.elite, noLegend.Weights.boss })
            foreach (var w in list) w.Legendary = 0f;

        // 1) 일반 전투만 연속 50 화면: 16, 32, 48번째 화면에서만 레전드가 확정되어야 한다.
        var rng = new System.Random(7);
        int counter = 0;
        var legendScreens = new List<int>();
        bool slotOk = true;
        for (int s = 1; s <= 50; s++)
        {
            RewardRollResult r = RewardRoller.Roll(noLegend, MonsterGrade.Normal, 0, counter, rng);
            if (r.HasLegendary) legendScreens.Add(s);
            if (r.PityTriggered && (r.Slots[r.PitySlot] == null || r.Slots[r.PitySlot].ItemGrade != ItemGrade.Legendary)) slotOk = false;
            counter = r.PityCounterAfter;
        }
        string expected1 = $"{pity + 1}, {2 * (pity + 1)}, {3 * (pity + 1)}";
        string actual1 = string.Join(", ", legendScreens);
        bool pass1 = actual1 == expected1 && slotOk;
        ok &= pass1;
        sb.AppendLine($"| 일반 전투 50화면 연속(레전드 확률 0) — 레전드가 나온 화면 | {expected1} | {actual1} | {(pass1 ? "통과" : "실패")} |");

        // 2) 보스 화면은 세지 않는다: 일반 10 → 보스 5 → 일반 5 → 다음(21번째) 일반 화면에서 확정
        rng = new System.Random(11);
        counter = 0;
        var seq = new List<MonsterGrade>();
        for (int i = 0; i < 10; i++) seq.Add(MonsterGrade.Normal);
        for (int i = 0; i < 5; i++) seq.Add(MonsterGrade.Boss);
        for (int i = 0; i < 6; i++) seq.Add(MonsterGrade.Normal);
        int firstForced = -1;
        for (int s = 0; s < seq.Count; s++)
        {
            RewardRollResult r = RewardRoller.Roll(noLegend, seq[s], 0, counter, rng);
            if (r.PityTriggered && firstForced < 0) firstForced = s + 1;
            counter = r.PityCounterAfter;
        }
        bool pass2 = firstForced == 21;
        ok &= pass2;
        sb.AppendLine($"| 일반10·보스5·일반6 — 천장이 터진 화면 | 21 | {firstForced} | {(pass2 ? "통과" : "실패")} |");

        // 3) 카운터 14에서는 확정되지 않는다
        rng = new System.Random(3);
        bool early = false;
        for (int i = 0; i < 1000; i++)
            early |= RewardRoller.Roll(noLegend, MonsterGrade.Normal, 0, pity - 1, rng).HasLegendary;
        ok &= !early;
        sb.AppendLine($"| 카운터 {pity - 1}에서 1,000회 — 레전드 등장 | 0회 | {(early ? "있음" : "0회")} | {(!early ? "통과" : "실패")} |");

        // 4) 레전드가 나오면 0으로
        RewardRollResult forced = RewardRoller.Roll(noLegend, MonsterGrade.Normal, 0, pity, new System.Random(5));
        bool pass4 = forced.HasLegendary && forced.PityCounterAfter == 0;
        ok &= pass4;
        sb.AppendLine($"| 확정 화면 뒤 카운터 | 0 | {forced.PityCounterAfter} | {(pass4 ? "통과" : "실패")} |");
        return ok;
    }

    static bool ReferenceTests(RewardLootTable table, StringBuilder sb, bool checkScene)
    {
        bool ok = true;
        var catalog = AssetDatabase.LoadAssetAtPath<RewardAssetCatalog>(CatalogPath);
        if (catalog == null)
        {
            sb.AppendLine("| RewardAssetCatalog | 없음 ❌ |");
            return false;
        }

        // 빈 아이콘
        var missingIcon = new List<string>();
        int ownIcons = 0;
        foreach (RewardRow row in table.Rows.Where(RewardLootTable.IsRollable))
        {
            if (catalog.RewardIcon(row.Reward_ID) != null) ownIcons++;
            if (LootSelectionPanel.ResolveIcon(catalog, row) == null) missingIcon.Add(row.Reward_ID);
        }
        bool iconOk = missingIcon.Count == 0;
        ok &= iconOk;
        sb.AppendLine($"| 1차 보상 아이콘 (전용 {ownIcons}개 + 대상 에셋 아이콘) — 빈 아이콘 | {(iconOk ? "0개" : string.Join(", ", missingIcon) + " ❌")} |");

        // 전용 아이콘 26장 = 카탈로그 26개, 이름이 모두 Reward_ID
        var unknownIds = catalog.rewardIcons.Where(e => table.Find(e.rewardId) == null).Select(e => e.rewardId).ToList();
        bool idOk = catalog.rewardIcons.Count == 26 && unknownIds.Count == 0 && catalog.rewardIcons.All(e => e.icon != null);
        ok &= idOk;
        sb.AppendLine($"| 보상 전용 아이콘 등록 | {catalog.rewardIcons.Count}개{(unknownIds.Count > 0 ? ", 표에 없는 ID: " + string.Join(", ", unknownIds) : "")} {(idOk ? "" : "❌")} |");

        // 효과·파라미터
        var badEffect = new List<string>();
        foreach (RewardRow row in table.Rows.Where(RewardLootTable.IsRollable))
        {
            if (!RewardEffectRunner.IsPhase1Effect(row.Effect_Type)) { badEffect.Add(row.Reward_ID + "(효과)"); continue; }
            switch (row.Effect_Type)
            {
                case "Snack_Give":
                case "Snack_Fill_Plus":
                    if (catalog.Snack(row.Param_Str) == null) badEffect.Add(row.Reward_ID);
                    break;
                case "Snack_Bundle":
                    if (row.Param_Str.Split('|').Any(k => catalog.Snack(k) == null)) badEffect.Add(row.Reward_ID);
                    break;
                case "Dice_Add":
                    if (catalog.Dice(row.Param_Str) == null) badEffect.Add(row.Reward_ID);
                    break;
                case "Coating_Apply":
                    if (catalog.Coating(row.Param_Str) == null) badEffect.Add(row.Reward_ID);
                    break;
                case "Satellite_Give":
                    if (catalog.Satellite(row.Param_Str) == null) badEffect.Add(row.Reward_ID);
                    break;
            }
        }
        bool effectOk = badEffect.Count == 0;
        ok &= effectOk;
        var usedEffects = table.Rows.Where(RewardLootTable.IsRollable).Select(r => r.Effect_Type).Distinct().Count();
        sb.AppendLine($"| 1차 효과 {usedEffects}종·대상 에셋 연결 | {(effectOk ? "모두 연결" : string.Join(", ", badEffect) + " ❌")} |");

        // 카탈로그 Missing
        int nulls = catalog.snacks.Count(s => s == null) + catalog.dice.Count(d => d.dice == null) + catalog.coatings.Count(c => c.coating == null)
            + catalog.satellites.Count(s => s == null) + catalog.stamps.Count(s => s == null) + catalog.rewardIcons.Count(i => i.icon == null)
            + (catalog.maxHpItem == null ? 1 : 0) + (catalog.coinItem == null ? 1 : 0) + (catalog.ticketPack == null ? 1 : 0);
        ok &= nulls == 0;
        sb.AppendLine($"| RewardAssetCatalog Missing 참조 | {nulls}개{(nulls == 0 ? "" : " ❌")} |");

        if (checkScene)
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var panels = UnityEngine.Object.FindObjectsByType<LootSelectionPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int wired = panels.Count(p => p.rewardLootTable != null && p.rewardCatalog != null);
            int missingScripts = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                    missingScripts += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
            int legacyNull = panels.Sum(p => (p.firstSlotPool?.Count(x => x == null) ?? 0) + (p.snackPool?.Count(x => x == null) ?? 0) + (p.dicePool?.Count(x => x == null) ?? 0));
            bool sceneOk = panels.Length > 0 && wired == panels.Length && legacyNull == 0;
            ok &= sceneOk;
            sb.AppendLine($"| MainScene_Map 전리품 패널 연결 (표·카탈로그) | {wired}/{panels.Length} {(sceneOk ? "" : "❌")} |");
            sb.AppendLine($"| MainScene_Map 기존 풀(튜토리얼용) Missing | {legacyNull}개 |");
            sb.AppendLine($"| MainScene_Map Missing 스크립트 | {missingScripts}개 |");
        }
        return ok;
    }
}
#endif
