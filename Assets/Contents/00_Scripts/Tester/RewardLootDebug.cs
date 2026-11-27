#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 전리품 v2 디버그 도구 (에디터·개발 빌드 전용).
/// - 1차 효과 17종을 하나씩 강제로 실행하고 전후 상태를 로그로 남긴다. 선택 창은 첫 카드를 자동으로 고른다.
///   에디터: 플레이 중 메뉴 Studio 10&6 / Reward Loot / Debug: Run 17 Effects
/// - 개발 빌드를 -rewardUICheck <폴더> [-rewardUICheckTag 1080] 로 실행하면 전리품 화면 캡처 + 효과 검증 후 종료한다.
/// </summary>
public class RewardLootDebug : MonoBehaviour
{
    // 효과 종류별 대표 보상
    public static readonly string[] EffectSamples =
    {
        "Rwd_Bandage_N",        // Heal_Flat
        "Rwd_Heal_R",           // Heal_Percent
        "Rwd_Heal_E",           // Heal_Full_MaxHP
        "Rwd_MaxHP_N",          // MaxHP_Add
        "Rwd_GoldPouch_R",      // Gold_Add
        "Rwd_Ticket_E",         // Ticket_Give ×2
        "Rwd_GoldenTicket_E",   // Ticket_Choice
        "Rwd_MasterTicket_L",   // Ticket_All
        "Rwd_PeppermintDuo_E",  // Snack_Give ×2
        "Rwd_HunterSet_R",      // Snack_Bundle
        "Rwd_SnackBag_R",       // Snack_Random ×2
        "Rwd_FridgeRaid_E",     // Snack_Fill
        "Rwd_Feast_L",          // Snack_Fill_Plus
        "Rwd_Stamp_E",          // Stamp_Random ×2
        "Rwd_DarkCoating_E",    // Coating_Apply ×2
        "Rwd_Jupiter_E",        // Satellite_Give ×2
        "Rwd_HighDice_E",       // Dice_Add
    };

    const string ArgOut = "-rewardUICheck";
    const string ArgTag = "-rewardUICheckTag";
    // 모니터보다 큰 해상도(예: 1920x1200)는 카메라를 이 크기의 렌더 텍스처로 돌려 찍는다.
    const string ArgRenderSize = "-rewardUICheckRT";
    RenderTexture renderTarget;

    string outDir;
    string tag = "";
    bool captureWindows;
    readonly HashSet<string> capturedWindows = new HashSet<string>();
    readonly StringBuilder log = new StringBuilder();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void BootFromCommandLine()
    {
        string dir = Arg(ArgOut);
        if (string.IsNullOrEmpty(dir)) return;
        var go = new GameObject("RewardLootDebug");
        DontDestroyOnLoad(go);
        var check = go.AddComponent<RewardLootDebug>();
        check.outDir = dir;
        check.tag = Arg(ArgTag) ?? "";
        check.captureWindows = true;
        check.StartCoroutine(check.CommandLineCheck());
    }

    public static void StartEffectRun()
    {
        var go = new GameObject("RewardLootDebug");
        var dbg = go.AddComponent<RewardLootDebug>();
        dbg.StartCoroutine(dbg.EffectRunThenDestroy());
    }

    IEnumerator EffectRunThenDestroy()
    {
        yield return RunAllEffects();
        Debug.Log(log.ToString());
        Destroy(gameObject);
    }

    static string Arg(string flag)
    {
        string[] args = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(args, flag);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    void Log(string line)
    {
        log.AppendLine(line);
        Debug.Log("[RewardLootDebug] " + line);
    }

    // ---------- 명령줄 검사 ----------

    IEnumerator CommandLineCheck()
    {
        Directory.CreateDirectory(outDir);
        // 로비에서 시작하면 새 게임으로 지도 씬을 연다.
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "MainScene_Map")
        {
            yield return new WaitForSecondsRealtime(3f);
            PlayerPrefs.SetInt("LoadGame", 0);
            UnityEngine.SceneManagement.SceneManager.LoadScene("MainScene_Map");
            yield return null;
        }
        float timeout = Time.realtimeSinceStartup + 60f;
        while ((DiceManager.Instance == null || !DiceManager.Instance.CanSaveProgress) && Time.realtimeSinceStartup < timeout)
            yield return null;
        yield return new WaitForSecondsRealtime(3f);

        DiceManager dm = DiceManager.Instance;
        if (dm == null || dm.lootSelectionPanel == null)
        {
            Log("DiceManager 또는 전리품 패널이 없습니다.");
            Finish();
            yield break;
        }
        string rtArg = Arg(ArgRenderSize);
        if (!string.IsNullOrEmpty(rtArg))
        {
            string[] wh = rtArg.Split('x');
            renderTarget = new RenderTexture(int.Parse(wh[0]), int.Parse(wh[1]), 24);
            foreach (Camera cam in Camera.allCameras) cam.targetTexture = renderTarget;
            yield return null;
            yield return null;
        }
        Log($"해상도 {CaptureWidth}x{CaptureHeight}{(renderTarget != null ? " (렌더 텍스처)" : "")}, 바이옴 단계 {dm.CurrentBiomeTier}, 체력 {dm.currentPlayerHP}/{dm.playerMaxHP}, 골드 {dm.shopManager?.currentGold}");
        LootSelectionPanel panel = dm.lootSelectionPanel;

        // 1) 등급이 섞인 화면 2장 (노멀·레어·레전드 / 에픽·노멀·레전드)
        yield return CaptureRewards(panel, dm, "mixed_1", "Rwd_GoldPouch_N", "Rwd_FreshSet_R", "Rwd_Stamp_L");
        yield return CaptureRewards(panel, dm, "mixed_2", "Rwd_GoldenTicket_E", "Rwd_SnackBag_N", "Rwd_PrismCoating_L");
        yield return CaptureRewards(panel, dm, "mixed_3", "Rwd_Ticket_R", "Rwd_PeppermintDuo_E", "Rwd_HighDice_E");

        // 2) 스낵칸 가득: 체리·스테이크·가니쉬·팬케이크·라임 주스로 5칸을 채우면 포춘쿠키·냉장고 털기 카드가 비활성
        ClearSnacks();
        RewardAssetCatalog catalog = panel.rewardCatalog;
        foreach (SnackType t in new[] { SnackType.Cherry, SnackType.Steak, SnackType.Garnish, SnackType.Pancake, SnackType.LimeJuice })
            InventoryManager.Instance.AddItem(catalog.Snack(t));
        yield return CaptureRewards(panel, dm, "snack_full", "Rwd_MaxHP_R", "Rwd_FortuneCookie_R", "Rwd_CoinDice_N");
        yield return CaptureRewards(panel, dm, "snack_full_fill", "Rwd_Heal_N", "Rwd_FridgeRaid_E", "Rwd_Mercury_R");
        LootChoiceSlot fullSlot = panel.choiceSlots[1];
        Log($"스낵칸 가득: {fullSlot.CurrentReward?.Reward_ID} 선택 가능={fullSlot.Selectable}, 버튼 interactable={fullSlot.GetComponent<Button>()?.interactable}");
        ClosePanel(panel);
        ClearSnacks();

        // 3) 효과 17종
        yield return RunAllEffects();

        // 4) 실제 추첨 화면(시드 없음) 1장
        panel.OpenSelection(dm);
        yield return new WaitForSecondsRealtime(0.6f);
        yield return Capture("rolled");
        ClosePanel(panel);

        Finish();
    }

    void Finish()
    {
        if (!string.IsNullOrEmpty(outDir))
            File.WriteAllText(Path.Combine(outDir, $"effects{Suffix}.txt"), log.ToString(), new UTF8Encoding(false));
        if (captureWindows) Application.Quit();
    }

    string Suffix => string.IsNullOrEmpty(tag) ? "" : "_" + tag;

    IEnumerator CaptureRewards(LootSelectionPanel panel, DiceManager dm, string name, params string[] ids)
    {
        panel.ShowRewardsById(dm, ids);
        yield return new WaitForSecondsRealtime(0.6f);
        yield return Capture(name);
        var sb = new StringBuilder();
        foreach (LootChoiceSlot slot in panel.choiceSlots)
        {
            if (slot == null || !slot.gameObject.activeSelf) continue;
            Image icon = slot.lootIcon;
            sb.Append($"[{slot.CurrentReward?.Reward_ID} {slot.CurrentReward?.ItemGrade} 아이콘={(icon.sprite != null ? icon.sprite.name : "없음")} {icon.rectTransform.sizeDelta.x}x{icon.rectTransform.sizeDelta.y} 이름='{slot.lootNameText.text}' 설명='{slot.lootDescText.text.Replace("\n", " / ")}' 선택={slot.Selectable}] ");
        }
        Log($"{name}: {sb}");
        LogOverlap(panel, name);
    }

    void LogOverlap(LootSelectionPanel panel, string name)
    {
        var rects = new List<Rect>();
        foreach (LootChoiceSlot slot in panel.choiceSlots)
        {
            if (slot == null || !slot.gameObject.activeSelf) continue;
            RectTransform rt = slot.cardBody != null ? slot.cardBody : (RectTransform)slot.transform;
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            Canvas canvas = rt.GetComponentInParent<Canvas>();
            Camera cam = canvas != null && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.rootCanvas.worldCamera : null;
            Vector2 a = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
            Vector2 b = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);
            rects.Add(Rect.MinMaxRect(a.x, a.y, b.x, b.y));
        }
        bool overlap = false;
        for (int i = 0; i < rects.Count; i++)
            for (int j = i + 1; j < rects.Count; j++)
                overlap |= rects[i].Overlaps(rects[j]);
        bool inside = rects.All(r => r.xMin >= 0 && r.yMin >= 0 && r.xMax <= CaptureWidth && r.yMax <= CaptureHeight);
        Log($"{name}: 카드 {rects.Count}장 겹침={(overlap ? "있음" : "없음")} 화면 안={inside} ({string.Join(", ", rects.Select(r => $"x{r.xMin:0}~{r.xMax:0} y{r.yMin:0}~{r.yMax:0}"))})");
    }

    int CaptureWidth => renderTarget != null ? renderTarget.width : Screen.width;
    int CaptureHeight => renderTarget != null ? renderTarget.height : Screen.height;

    // 화면을 그 프레임 끝에서 바로 읽어 PNG로 쓴다(CaptureScreenshot은 저장이 늦어 다음 화면이 찍힐 수 있다).
    IEnumerator Capture(string name)
    {
        if (!captureWindows || string.IsNullOrEmpty(outDir)) yield break;
        yield return new WaitForEndOfFrame();
        Texture2D tex;
        if (renderTarget != null)
        {
            foreach (Camera cam in Camera.allCameras) if (cam.targetTexture != renderTarget) cam.targetTexture = renderTarget;
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = renderTarget;
            tex = new Texture2D(renderTarget.width, renderTarget.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, renderTarget.width, renderTarget.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
        }
        else tex = ScreenCapture.CaptureScreenshotAsTexture();
        string path = Path.Combine(outDir, $"{name}{Suffix}.png");
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Destroy(tex);
        Log($"캡처 {path} ({tex.width}x{tex.height})");
    }

    static void ClosePanel(LootSelectionPanel panel)
    {
        if (panel.panelRoot != null) panel.panelRoot.SetActive(false);
    }

    static void ClearSnacks()
    {
        InventoryManager inv = InventoryManager.Instance;
        if (inv == null) return;
        foreach (InventorySlot slot in inv.snackSlots)
            if (slot != null && !slot.isEmpty) slot.ClearSlot();
    }

    // ---------- 효과 17종 ----------

    public IEnumerator RunAllEffects()
    {
        DiceManager dm = DiceManager.Instance;
        LootSelectionPanel panel = dm != null ? dm.lootSelectionPanel : null;
        if (panel == null || panel.LootTable == null || !panel.LootTable.Loaded)
        {
            Log("효과 검증: 전리품 패널 또는 표가 없습니다.");
            yield break;
        }
        RewardEffectRunner runner = panel.CreateEffectRunner(dm);
        var covered = new HashSet<string>();
        int pass = 0;

        foreach (string id in EffectSamples)
        {
            RewardRow row = panel.LootTable.Find(id);
            if (row == null) { Log($"{id}: 표에 없음"); continue; }

            // 회복 확인을 위해 체력을 낮추고, 스낵칸 검사는 빈 칸 상태에서 시작한다.
            if (row.Effect_Type.StartsWith("Heal")) dm.currentPlayerHP = Mathf.Max(1, dm.playerMaxHP / 3);
            if (row.Effect_Type.StartsWith("Snack")) ClearSnacks();
            if (row.Effect_Type == "Snack_Fill" || row.Effect_Type == "Snack_Fill_Plus")
                InventoryManager.Instance.AddItem(panel.rewardCatalog.Snack(SnackType.Cherry)); // 1칸 채운 상태에서 나머지 채우기

            State before = State.Read(dm);
            int windows = 0;
            bool done = false;
            StartCoroutine(WrapInner(runner.Run(row), () => done = true));
            yield return null;
            while (!done)
            {
                string open = OpenWindow(dm.shopManager);
                if (open != null)
                {
                    windows++;
                    yield return new WaitForSecondsRealtime(1.0f);
                    if (captureWindows && capturedWindows.Add(row.Effect_Type)) yield return Capture("window_" + row.Effect_Type);
                    PickFirst(dm.shopManager, open);
                    float t = Time.realtimeSinceStartup + 5f;
                    while (OpenWindow(dm.shopManager) == open && Time.realtimeSinceStartup < t) yield return null;
                }
                yield return null;
            }
            State after = State.Read(dm);
            bool ok = Check(row, before, after, windows, runner, out string detail);
            if (ok) { pass++; covered.Add(row.Effect_Type); }
            Log($"{(ok ? "통과" : "실패")} {row.Effect_Type,-16} {row.Reward_ID,-20} 창 {windows}회 | {detail}");
        }
        Log($"효과 검증: {pass}/{EffectSamples.Length} 통과, 효과 종류 {covered.Count}/17");
    }

    IEnumerator WrapInner(IEnumerator inner, Action onDone)
    {
        yield return inner;
        onDone();
    }

    static string OpenWindow(ShopManager shop)
    {
        if (shop == null) return null;
        if (shop.ticketSelectionPanel != null && shop.ticketSelectionPanel.activeSelf) return "ticket";
        if (CoatingSelectionPanel.IsPanelOpen) return "coating";
        if (StampSelectionPanel.IsPanelOpen) return "stamp";
        if (SatelliteSelectionPanel.IsPanelOpen) return "satellite";
        return null;
    }

    static void PickFirst(ShopManager shop, string window)
    {
        if (window == "ticket")
        {
            TicketChoiceSlot slot = shop.ticketSelectionPanel.GetComponentsInChildren<TicketChoiceSlot>(false).FirstOrDefault();
            if (slot != null) slot.selectButton.onClick.Invoke();
            return;
        }
        GameObject root = window == "coating" ? shop.coatingSelectionPanel.panelRoot
            : window == "stamp" ? shop.stampSelectionPanel.panelRoot
            : shop.satelliteSelectionPanel.panelRoot;
        DiceChoiceWindow choice = root.GetComponentInChildren<DiceChoiceWindow>(false);
        GameObject card = choice != null ? choice.Cards.FirstOrDefault() : null;
        Button button = card != null ? card.GetComponentInChildren<Button>() : null;
        if (button != null) button.onClick.Invoke();
    }

    struct State
    {
        public int hp, maxHp, gold, deck, snackTotal, snackSlotsUsed, coated, stamped, satellites;
        public float handSum, handHigh;
        public Dictionary<SnackType, int> snacks;

        public static State Read(DiceManager dm)
        {
            var s = new State
            {
                hp = dm.currentPlayerHP,
                maxHp = dm.playerMaxHP,
                gold = dm.shopManager != null ? dm.shopManager.currentGold : 0,
                deck = dm.masterDeck.Count,
                coated = dm.masterDeck.Count(d => d.isCoated),
                stamped = dm.masterDeck.Count(d => d.stampType != StampType.None),
                satellites = dm.masterDeck.Sum(d => d.activeSatellites != null ? d.activeSatellites.Count : 0),
                snacks = new Dictionary<SnackType, int>(),
            };
            foreach (HandType h in (HandType[])Enum.GetValues(typeof(HandType)))
                s.handSum += dm.GetHandMultiplier(h);
            s.handHigh = dm.GetHandMultiplier(HandType.HighCard);
            foreach (InventorySlot slot in InventoryManager.Instance.snackSlots)
            {
                if (slot == null || slot.isEmpty || !(slot.currentItem is SnackItemSO snack)) continue;
                s.snackSlotsUsed++;
                s.snackTotal += slot.Count;
                s.snacks.TryGetValue(snack.snackType, out int c);
                s.snacks[snack.snackType] = c + slot.Count;
            }
            return s;
        }

        public string SnackText => string.Join(",", snacks.Select(kv => $"{kv.Key}×{kv.Value}"));
    }

    static bool Check(RewardRow row, State b, State a, int windows, RewardEffectRunner runner, out string detail)
    {
        int v = Mathf.RoundToInt(row.Value_1);
        switch (row.Effect_Type)
        {
            case "Heal_Flat":
                detail = $"체력 {b.hp}→{a.hp} (기대 +{Mathf.Min(v, b.maxHp - b.hp)})";
                return a.hp - b.hp == Mathf.Min(v, b.maxHp - b.hp);
            case "Heal_Percent":
                {
                    int heal = Mathf.Min(Mathf.FloorToInt(b.maxHp * row.Value_1), b.maxHp - b.hp);
                    detail = $"체력 {b.hp}→{a.hp} (기대 +{heal})";
                    return a.hp - b.hp == heal;
                }
            case "Heal_Full_MaxHP":
                detail = $"최대 {b.maxHp}→{a.maxHp}, 체력 {b.hp}→{a.hp}";
                return a.maxHp == b.maxHp + v && a.hp == a.maxHp;
            case "MaxHP_Add":
                detail = $"최대 {b.maxHp}→{a.maxHp}, 체력 {b.hp}→{a.hp}";
                return a.maxHp == b.maxHp + v && a.hp == b.hp + v;
            case "Gold_Add":
                {
                    int expect = runner.GoldAmount(row);
                    detail = $"골드 {b.gold}→{a.gold} (표시값 {expect}, 피규어 보너스 포함 시 이상)";
                    return a.gold - b.gold >= expect;
                }
            case "Ticket_Give":
                detail = $"족보 배수 합 {b.handSum:0.###}→{a.handSum:0.###}";
                return windows == v && a.handSum > b.handSum;
            case "Ticket_Choice":
                detail = $"족보 배수 합 {b.handSum:0.###}→{a.handSum:0.###}, 하이카드 {b.handHigh:0.###}→{a.handHigh:0.###} (8종 중 첫 칸=하이카드 ×{row.Value_1})";
                return windows == 1 && Mathf.Abs(a.handHigh - b.handHigh * row.Value_1) < 0.0001f;
            case "Ticket_All":
                detail = $"족보 배수 합 {b.handSum:0.###}→{a.handSum:0.###} (기대 ×{row.Value_1})";
                return Mathf.Abs(a.handSum - b.handSum * row.Value_1) < 0.001f;
            case "Snack_Give":
            case "Snack_Bundle":
            case "Snack_Random":
            case "Snack_Fill":
            case "Snack_Fill_Plus":
                {
                    detail = $"스낵 {b.SnackText}→{a.SnackText} (칸 {b.snackSlotsUsed}→{a.snackSlotsUsed}, 개수 {b.snackTotal}→{a.snackTotal})";
                    bool noPepper = row.Effect_Type == "Snack_Random" || row.Effect_Type == "Snack_Fill"
                        ? !a.snacks.ContainsKey(SnackType.Peppermint) : true;
                    if (row.Effect_Type == "Snack_Fill") return a.snackSlotsUsed == 5 && noPepper;
                    if (row.Effect_Type == "Snack_Fill_Plus")
                    {
                        a.snacks.TryGetValue(SnackType.Peppermint, out int pep);
                        b.snacks.TryGetValue(SnackType.Peppermint, out int pepB);
                        int otherPepper = pep - pepB - v; // 채우기에서 페퍼민트가 더 나오면 안 된다
                        return a.snackSlotsUsed == 5 && otherPepper == 0;
                    }
                    int expectCount = row.Effect_Type == "Snack_Bundle" ? v * row.Param_Str.Split('|').Length : v;
                    return a.snackTotal - b.snackTotal == expectCount && noPepper;
                }
            case "Stamp_Random":
                detail = $"스탬프 주사위 {b.stamped}→{a.stamped}";
                return windows == v && a.stamped >= b.stamped + 1;
            case "Coating_Apply":
                detail = $"코팅 주사위 {b.coated}→{a.coated}";
                return windows == v && a.coated >= b.coated + 1;
            case "Satellite_Give":
                detail = $"위성 {b.satellites}→{a.satellites}";
                return windows == v && a.satellites == b.satellites + v;
            case "Dice_Add":
                detail = $"덱 {b.deck}→{a.deck}";
                return a.deck == b.deck + v;
            default:
                detail = "알 수 없는 효과";
                return false;
        }
    }
}
#endif
