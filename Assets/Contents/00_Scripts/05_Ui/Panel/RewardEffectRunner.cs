using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 전리품 표(Reward_Loot_Table.json)의 Effect_Type을 실제 게임 효과로 바꾼다.
// 선택 창을 여러 번 여는 효과(티켓 N장, 스탬프 N개, 코팅 N개, 위성 N개)는 큐에 넣고 하나씩 연다.
public class RewardEffectRunner
{
    // 1차에서 처리하는 효과 17종
    public static readonly string[] Phase1Effects =
    {
        "Heal_Flat", "Heal_Percent", "Heal_Full_MaxHP", "MaxHP_Add", "Gold_Add",
        "Ticket_Give", "Ticket_Choice", "Ticket_All",
        "Snack_Give", "Snack_Bundle", "Snack_Random", "Snack_Fill", "Snack_Fill_Plus",
        "Stamp_Random", "Coating_Apply", "Satellite_Give", "Dice_Add"
    };

    static readonly StampType[] RandomStamps = { StampType.Spade, StampType.Heart, StampType.Clover, StampType.Diamond };

    readonly DiceManager dice;
    readonly RewardAssetCatalog catalog;
    readonly List<string> snackExclude;

    // 선택 창 열기 큐. 각 항목은 창을 열고, 실제로 열렸으면 true.
    readonly Queue<Func<bool>> choiceQueue = new Queue<Func<bool>>();

    public RewardEffectRunner(DiceManager dice, RewardAssetCatalog catalog, RewardLootTable table)
    {
        this.dice = dice;
        this.catalog = catalog;
        snackExclude = table != null ? table.SnackFillExclude : new List<string> { nameof(SnackType.Peppermint) };
    }

    ShopManager Shop => dice != null ? dice.shopManager : null;

    public static bool IsPhase1Effect(string effectType) => Array.IndexOf(Phase1Effects, effectType) >= 0;

    // ---------- 표시 ----------

    public int GoldAmount(RewardRow row)
    {
        int tier = dice != null ? dice.CurrentBiomeTier : 0;
        return Mathf.FloorToInt(row.Value_1 * BalanceConfig.Current.TierGold(tier));
    }

    // 카드 설명의 {0}에 넣을 값
    public object[] DescArgs(RewardRow row)
    {
        if (row == null) return Array.Empty<object>();
        switch (row.Effect_Type)
        {
            case "Gold_Add": return new object[] { GoldAmount(row) };
            default: return new object[] { FormatValue(row.Value_1) };
        }
    }

    static string FormatValue(float v) => Mathf.Approximately(v, Mathf.Round(v)) ? Mathf.RoundToInt(v).ToString() : v.ToString("0.##");

    // ---------- 스낵칸 ----------

    public static bool IsSnackEffect(RewardRow row)
    {
        if (row == null || string.IsNullOrEmpty(row.Effect_Type)) return false;
        return row.Effect_Type.StartsWith("Snack_", StringComparison.Ordinal) && IsPhase1Effect(row.Effect_Type);
    }

    // 지금 스낵칸 상태(빈 칸 수, 가진 스낵 종류)
    class SnackState
    {
        public int empty;
        public readonly HashSet<SnackType> owned = new HashSet<SnackType>();

        public bool CanPlace(SnackType type) => owned.Contains(type) || empty > 0;

        public void Place(SnackType type)
        {
            if (owned.Contains(type)) return;
            empty--;
            owned.Add(type);
        }
    }

    SnackState ReadSnackState()
    {
        var state = new SnackState();
        InventoryManager inv = InventoryManager.Instance;
        if (inv == null || inv.snackSlots == null) return state;
        int limit = Mathf.Min(inv.snackSlots.Length, inv.maxSnackSlots);
        for (int i = 0; i < limit; i++)
        {
            InventorySlot slot = inv.snackSlots[i];
            if (slot == null) continue;
            if (slot.isEmpty) state.empty++;
            else if (slot.currentItem is SnackItemSO owned) state.owned.Add(owned.snackType);
        }
        return state;
    }

    // 무작위 스낵 후보(페퍼민트 등 제외 목록을 뺀 등록 스낵)
    List<SnackItemSO> RandomSnackCandidates()
    {
        var list = new List<SnackItemSO>();
        if (catalog == null) return list;
        foreach (SnackItemSO s in catalog.snacks)
        {
            if (s == null || snackExclude.Contains(s.snackType.ToString())) continue;
            if (list.Exists(x => x.snackType == s.snackType)) continue;
            list.Add(s);
        }
        return list;
    }

    // 이 보상으로 줄 스낵 수(total)와 지금 넣을 수 있는 수(placeable). 스낵 보상이 아니면 total = 0.
    public void CountSnacks(RewardRow row, out int total, out int placeable)
    {
        total = 0;
        placeable = 0;
        if (!IsSnackEffect(row)) return;

        SnackState state = ReadSnackState();
        int count = Mathf.Max(1, Mathf.RoundToInt(row.Value_1));
        switch (row.Effect_Type)
        {
            case "Snack_Give":
                if (RewardAssetCatalog.TryParseSnack(row.Param_Str, out SnackType give))
                {
                    total = count;
                    placeable = state.CanPlace(give) ? count : 0;
                }
                break;

            case "Snack_Bundle":
                foreach (string key in SplitParams(row.Param_Str))
                {
                    if (!RewardAssetCatalog.TryParseSnack(key, out SnackType t)) continue;
                    total += count;
                    if (state.CanPlace(t)) { state.Place(t); placeable += count; }
                }
                break;

            case "Snack_Random":
                total = count;
                placeable = RandomSnackCandidates().Exists(s => state.CanPlace(s.snackType)) ? count : 0;
                break;

            case "Snack_Fill":
                total = placeable = Mathf.Max(0, state.empty);
                break;

            case "Snack_Fill_Plus":
                if (RewardAssetCatalog.TryParseSnack(row.Param_Str, out SnackType first))
                {
                    total += count;
                    if (state.CanPlace(first)) { state.Place(first); placeable += count; }
                }
                total += Mathf.Max(0, state.empty);
                placeable += Mathf.Max(0, state.empty);
                break;
        }
    }

    static IEnumerable<string> SplitParams(string param)
    {
        if (string.IsNullOrEmpty(param)) yield break;
        foreach (string part in param.Split('|'))
        {
            string p = part.Trim();
            if (p.Length > 0) yield return p;
        }
    }

    bool GiveSnack(SnackItemSO snack, ref int given)
    {
        if (snack == null || InventoryManager.Instance == null) return false;
        if (!InventoryManager.Instance.AddItem(snack)) return false;
        given++;
        return true;
    }

    void GiveSnackType(SnackType type, int count, ref int given)
    {
        SnackItemSO snack = catalog != null ? catalog.Snack(type) : null;
        if (snack == null)
        {
            Debug.LogWarning($"[Reward Loot] {type} 스낵 에셋이 RewardAssetCatalog에 없습니다.");
            return;
        }
        for (int i = 0; i < count; i++)
            if (!GiveSnack(snack, ref given)) break;
    }

    void GiveRandomSnacks(int count, ref int given)
    {
        List<SnackItemSO> candidates = RandomSnackCandidates();
        for (int i = 0; i < count; i++)
        {
            SnackState state = ReadSnackState();
            List<SnackItemSO> placeable = candidates.FindAll(s => state.CanPlace(s.snackType));
            if (placeable.Count == 0) break;
            GiveSnack(placeable[UnityEngine.Random.Range(0, placeable.Count)], ref given);
        }
    }

    // 빈 칸마다 새 종류 스낵을 하나씩 넣는다(같은 종류가 겹쳐 쌓여 빈 칸이 남지 않게). 새 종류가 모자라면 무작위로 채운다.
    void FillEmptySlots(ref int given)
    {
        List<SnackItemSO> candidates = RandomSnackCandidates();
        int guard = 16;
        while (guard-- > 0)
        {
            SnackState state = ReadSnackState();
            if (state.empty <= 0) break;
            List<SnackItemSO> fresh = candidates.FindAll(s => !state.owned.Contains(s.snackType));
            List<SnackItemSO> pool = fresh.Count > 0 ? fresh : candidates;
            if (pool.Count == 0) break;
            if (!GiveSnack(pool[UnityEngine.Random.Range(0, pool.Count)], ref given)) break;
            if (fresh.Count == 0) break; // 남은 빈 칸은 같은 종류가 겹쳐 쌓이므로 더 채울 수 없다
        }
    }

    // ---------- 실행 ----------

    // 즉시 효과를 주고, 선택 창 효과는 모두 닫힐 때까지 하나씩 연다.
    public IEnumerator Run(RewardRow row)
    {
        if (row == null) yield break;
        choiceQueue.Clear();
        Apply(row);

        ShopManager shop = Shop;
        while (choiceQueue.Count > 0)
        {
            // 앞 창이 완전히 닫힐 때까지(선택 연출 포함) 기다린다.
            while (shop != null && shop.IsAnyChoicePanelOpen) yield return null;
            yield return null;

            Func<bool> open = choiceQueue.Dequeue();
            if (shop == null)
            {
                Debug.LogWarning("[Reward Loot] ShopManager가 없어 선택 창을 열 수 없습니다.");
                continue;
            }
            bool opened = open();
            // 열 수 있는 주사위가 없어 창이 바로 닫힌 회차는 소모된 것으로 본다(창이 토스트로 알린다).
            if (!opened) continue;
            yield return null;
        }
        while (shop != null && shop.IsAnyChoicePanelOpen) yield return null;
    }

    void Apply(RewardRow row)
    {
        int count = Mathf.Max(1, Mathf.RoundToInt(row.Value_1));
        ShopManager shop = Shop;
        switch (row.Effect_Type)
        {
            case "Heal_Flat":
                Heal(Mathf.FloorToInt(row.Value_1));
                break;

            case "Heal_Percent":
                Heal(Mathf.FloorToInt(dice.playerMaxHP * row.Value_1));
                break;

            case "Heal_Full_MaxHP":
                dice.IncreaseMaxHP(Mathf.RoundToInt(row.Value_1));
                Heal(dice.playerMaxHP);
                break;

            case "MaxHP_Add":
                dice.IncreaseMaxHP(Mathf.RoundToInt(row.Value_1));
                RefreshUi();
                break;

            case "Gold_Add":
                if (shop != null) shop.GrantGold(GoldAmount(row));
                break;

            case "Ticket_Give":
                for (int i = 0; i < count; i++)
                    choiceQueue.Enqueue(() => { shop.ShowTicketSelection(); return shop.ticketSelectionPanel != null && shop.ticketSelectionPanel.activeSelf; });
                break;

            case "Ticket_Choice":
                choiceQueue.Enqueue(() =>
                {
                    shop.ShowTicketSelection(AllHandTickets(shop), row.Value_1);
                    return shop.ticketSelectionPanel != null && shop.ticketSelectionPanel.activeSelf;
                });
                break;

            case "Ticket_All":
                foreach (HandType hand in (HandType[])Enum.GetValues(typeof(HandType)))
                    dice.UpgradeHand(hand, row.Value_1);
                RefreshUi();
                break;

            case "Snack_Give":
            case "Snack_Bundle":
            case "Snack_Random":
            case "Snack_Fill":
            case "Snack_Fill_Plus":
                ApplySnacks(row, count);
                break;

            case "Stamp_Random":
                for (int i = 0; i < count; i++)
                    choiceQueue.Enqueue(() =>
                    {
                        StampType type = RandomStamps[UnityEngine.Random.Range(0, RandomStamps.Length)];
                        StampItemSO stamp = catalog != null ? catalog.Stamp(type) : null;
                        shop.ShowStampSelection(type, stamp != null ? stamp.icon : null);
                        return StampSelectionPanel.IsPanelOpen;
                    });
                break;

            case "Coating_Apply":
                {
                    CoatingItemSO coating = catalog != null ? catalog.Coating(row.Param_Str) : null;
                    if (coating == null) { Debug.LogWarning($"[Reward Loot] {row.Reward_ID}: Coating_ID {row.Param_Str} 에셋이 없습니다."); break; }
                    for (int i = 0; i < count; i++)
                        choiceQueue.Enqueue(() =>
                        {
                            shop.ShowCoatingSelection(coating.coatingType, coating.scoreMultiplier, coating.coatingColor);
                            return CoatingSelectionPanel.IsPanelOpen;
                        });
                    break;
                }

            case "Satellite_Give":
                if (!Enum.TryParse(row.Param_Str, true, out SatelliteType satellite))
                {
                    Debug.LogWarning($"[Reward Loot] {row.Reward_ID}: 위성 {row.Param_Str}을(를) 알 수 없습니다.");
                    break;
                }
                for (int i = 0; i < count; i++)
                    choiceQueue.Enqueue(() =>
                    {
                        shop.ShowSatelliteSelection(satellite);
                        return SatelliteSelectionPanel.IsPanelOpen;
                    });
                break;

            case "Dice_Add":
                {
                    DiceItemSO die = catalog != null ? catalog.Dice(row.Param_Str) : null;
                    if (die == null) { Debug.LogWarning($"[Reward Loot] {row.Reward_ID}: 주사위 {row.Param_Str} 에셋이 없습니다."); break; }
                    for (int i = 0; i < count; i++) die.ApplyItemEffect(dice);
                    RefreshUi();
                    break;
                }

            default:
                Debug.LogWarning($"[Reward Loot] {row.Reward_ID}: 1차에서 처리하지 않는 효과 {row.Effect_Type}");
                break;
        }
    }

    void ApplySnacks(RewardRow row, int count)
    {
        CountSnacks(row, out int total, out int placeable);
        int given = 0;
        switch (row.Effect_Type)
        {
            case "Snack_Give":
                if (RewardAssetCatalog.TryParseSnack(row.Param_Str, out SnackType give)) GiveSnackType(give, count, ref given);
                break;
            case "Snack_Bundle":
                foreach (string key in SplitParams(row.Param_Str))
                    if (RewardAssetCatalog.TryParseSnack(key, out SnackType t)) GiveSnackType(t, count, ref given);
                break;
            case "Snack_Random":
                GiveRandomSnacks(count, ref given);
                break;
            case "Snack_Fill":
                FillEmptySlots(ref given);
                break;
            case "Snack_Fill_Plus":
                if (RewardAssetCatalog.TryParseSnack(row.Param_Str, out SnackType first)) GiveSnackType(first, count, ref given);
                FillEmptySlots(ref given);
                break;
        }

        // 일부만 들어갔으면 토스트로 알린다.
        if (given < total && ToastPopupController.Instance != null)
            ToastPopupController.Instance.ShowToast(LocalizationManager.GetSys("SYS_LOOT_SNACK_PARTIAL",
                "스낵칸이 부족해 {0}개 중 {1}개만 받았습니다.", total, given));
        if (given > 0) RefreshUi();
    }

    void Heal(int amount)
    {
        if (amount > 0) dice.playerStatus.Heal(amount);
        RefreshUi();
    }

    void RefreshUi()
    {
        // 휴식·여정 회복과 같은 경로로 체력·배수 표시를 갱신한다.
        if (dice != null) dice.UpdateMainUI("");
    }

    static List<TicketItemSO> AllHandTickets(ShopManager shop)
    {
        var list = new List<TicketItemSO>();
        if (shop == null || shop.allTicketsPool == null) return list;
        foreach (HandType hand in (HandType[])Enum.GetValues(typeof(HandType)))
        {
            TicketItemSO ticket = shop.allTicketsPool.Find(t => t != null && t.targetHand == hand);
            if (ticket != null) list.Add(ticket);
        }
        return list;
    }
}
