using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class LootSelectionPanel : MonoBehaviour
{
    //다른 스크립트에서 전리품 창이 열려있는지 확인할 수 있는 변수
    public static bool IsPanelOpen { get; private set; } = false;
    // 전리품을 실제로 받은 직후(튜토리얼 안내용). 인벤토리가 가득 차 못 받으면 부르지 않는다.
    public static event System.Action LootChosen;

    public GameObject panelRoot;
    public LootChoiceSlot[] choiceSlots;

    [Header("전리품 루트 테이블 (비어 있거나 못 읽으면 아래 기존 풀을 쓴다)")]
    public TextAsset rewardLootTable;
    public RewardAssetCatalog rewardCatalog;

    [Header("1번 칸 랜덤 전리품 풀 (체력, 골드, 티켓팩) — 튜토리얼·되돌림용")]
    public List<BaseItemDataSO> firstSlotPool;

    [Header("전리품 풀 세팅 — 튜토리얼·되돌림용")]
    public List<SnackItemSO> snackPool;
    [Header("3번 칸 랜덤 주사위 풀 — 튜토리얼·되돌림용")]
    public List<DiceItemSO> dicePool;

    private DiceManager diceManager;
    private RewardLootTable lootTable;
    private bool lootTableParsed;
    private RewardEffectRunner effectRunner;
    private bool rewardChosen;

    public RewardLootTable LootTable
    {
        get
        {
            if (!lootTableParsed)
            {
                lootTableParsed = true;
                lootTable = RewardLootTable.Parse(rewardLootTable != null ? rewardLootTable.text : null);
                if (!lootTable.Loaded)
                    Debug.LogWarning("[Reward Loot] Reward_Loot_Table.json을 읽지 못했습니다. 기존 전리품 풀을 씁니다.");
            }
            return lootTable;
        }
    }

    private void Awake()
    {
        IsPanelOpen = false; // 시작할 때 초기화
    }

    public void OpenSelection(DiceManager manager)
    {
        diceManager = manager;

        // 튜토리얼은 기존 풀 그대로. 표를 못 쓰면 경고를 남기고 기존 풀로 되돌아간다.
        if (!TutorialMode.Active && TryOpenFromTable())
            return;

        OpenLegacySelection();
    }

    void OpenLegacySelection()
    {
        if (snackPool == null) snackPool = new List<SnackItemSO>();
        SnackItemSO.RegisterResourceSnacks(snackPool);

        // 2번 칸은 누락된 에셋을 제외한 스낵(페퍼민트 포함), 3번 칸은 주사위를 랜덤으로 뽑는다.
        List<SnackItemSO> shuffledSnacks = snackPool.FindAll(snack => snack != null);
        List<DiceItemSO> shuffledDice = dicePool != null ? dicePool.FindAll(dice => dice != null) : new List<DiceItemSO>();

        // 각 랜덤 칸의 후보가 준비되었는지 확인
        if (shuffledSnacks.Count < 1 || shuffledDice.Count < 1 || firstSlotPool == null || firstSlotPool.Count < 1)
        {
            Debug.LogWarning("전리품 풀에 아이템이 부족하거나 1번 칸 아이템이 누락되었습니다!");
            ClosePanelAndProceed();
            return;
        }

        // 1번 칸 아이템 1개 랜덤 뽑기
        BaseItemDataSO selectedFirstSlotItem = firstSlotPool[Random.Range(0, firstSlotPool.Count)];

        ShuffleList(shuffledSnacks);
        ShuffleList(shuffledDice);

        // 슬롯 세팅 (1번: 성장 아이템, 2번: 랜덤 스낵, 3번: 랜덤 주사위)
        foreach (var slot in choiceSlots) if (slot != null) slot.gameObject.SetActive(true);
        choiceSlots[0].Setup(selectedFirstSlotItem, this);
        choiceSlots[1].Setup(shuffledSnacks[0], this);
        choiceSlots[2].Setup(shuffledDice[0], this);

        ShowPanel();
    }

    // 루트 테이블로 3칸을 추첨해 카드를 채운다. 표·목록이 없거나 3칸이 모두 비면 false.
    bool TryOpenFromTable()
    {
        if (rewardLootTable == null) return false;
        RewardLootTable table = LootTable;
        if (table == null || !table.Loaded) return false;
        if (rewardCatalog == null)
        {
            Debug.LogWarning("[Reward Loot] RewardAssetCatalog가 연결되지 않았습니다. 기존 전리품 풀을 씁니다.");
            return false;
        }

        MonsterGrade monster = diceManager != null && diceManager.enemy != null ? diceManager.enemy.Grade : MonsterGrade.Normal;
        int tier = diceManager != null ? diceManager.CurrentBiomeTier : 0;
        int pity = diceManager != null ? diceManager.rewardLegendaryPity : 0;
        var rng = new System.Random(Random.Range(int.MinValue, int.MaxValue));
        RewardRollResult result = RewardRoller.Roll(table, monster, tier, pity, rng);

        bool any = false;
        foreach (RewardRow row in result.Slots) any |= row != null;
        if (!any)
        {
            Debug.LogWarning("[Reward Loot] 추첨할 수 있는 보상이 없습니다. 기존 전리품 풀을 씁니다.");
            return false;
        }

        if (diceManager != null) diceManager.rewardLegendaryPity = result.PityCounterAfter;
        ShowRewards(result.Slots);
        return true;
    }

    // 정해진 보상으로 카드를 채운다(추첨 결과, 검증용 강제 표시 공용).
    public void ShowRewards(IList<RewardRow> rows)
    {
        effectRunner = new RewardEffectRunner(diceManager, rewardCatalog, LootTable);
        for (int i = 0; i < choiceSlots.Length; i++)
        {
            LootChoiceSlot slot = choiceSlots[i];
            if (slot == null) continue;
            RewardRow row = rows != null && i < rows.Count ? rows[i] : null;
            if (row == null)
            {
                slot.gameObject.SetActive(false);
                continue;
            }

            slot.gameObject.SetActive(true);
            effectRunner.CountSnacks(row, out int total, out int placeable);
            bool selectable = !RewardEffectRunner.IsSnackEffect(row) || placeable > 0;

            string name = RewardText(row.Name_Key, row.Name_KR, row.Name_EN);
            string desc = SafeFormat(RewardText(row.Desc_Key, row.Desc_KR, row.Desc_EN), effectRunner.DescArgs(row));
            if (!selectable)
                desc += "\n<color=#FF8F7A>" + LocalizationManager.GetSys("SYS_LOOT_SNACK_FULL", "스낵칸이 가득 찼습니다.") + "</color>";

            slot.Setup(row, ResolveIcon(row), name, desc, selectable, this);
        }
        ShowPanel();
    }

    // 검증용: Reward_ID로 카드를 채운다.
    public void ShowRewardsById(DiceManager manager, params string[] rewardIds)
    {
        diceManager = manager;
        var rows = new List<RewardRow>();
        foreach (string id in rewardIds) rows.Add(LootTable.Find(id));
        ShowRewards(rows);
    }

    public RewardEffectRunner CreateEffectRunner(DiceManager manager)
    {
        diceManager = manager;
        return new RewardEffectRunner(manager, rewardCatalog, LootTable);
    }

    void ShowPanel()
    {
        rewardChosen = false;
        panelRoot.SetActive(true);
        panelRoot.transform.SetAsLastSibling();
        IsPanelOpen = true;
    }

    // 표의 Name_Key/Desc_Key 번역. 키가 없으면 한국어면 KR, 그 밖의 언어면 EN 문구.
    static string RewardText(string key, string kr, string en)
    {
        if (LocalizationManager.TryGetLocalized(LocalizationManager.ItemTable, key, out string value)) return value;
        string lang = LocalizationManager.Instance != null ? LocalizationManager.Instance.CurrentLanguageCode : LocalizationManager.DefaultLanguageCode;
        if (lang == "ko" || string.IsNullOrEmpty(en)) return kr ?? string.Empty;
        return en;
    }

    static string SafeFormat(string template, object[] args)
    {
        if (string.IsNullOrEmpty(template) || args == null || args.Length == 0 || !template.Contains("{")) return template;
        try { return string.Format(template, args); }
        catch (System.FormatException) { return template; }
    }

    // 아이콘 고르는 순서: 보상 전용 아이콘 → 대상 에셋 아이콘 → 효과 종류 기본 아이콘
    public Sprite ResolveIcon(RewardRow row) => ResolveIcon(rewardCatalog, row);

    public static Sprite ResolveIcon(RewardAssetCatalog rewardCatalog, RewardRow row)
    {
        if (row == null || rewardCatalog == null) return null;
        Sprite own = rewardCatalog.RewardIcon(row.Reward_ID);
        if (own != null) return own;

        Sprite target = TargetIcon(rewardCatalog, row);
        if (target != null) return target;

        switch (row.Effect_Type)
        {
            case "Heal_Flat":
            case "Heal_Percent":
            case "Heal_Full_MaxHP":
            case "MaxHP_Add":
                return rewardCatalog.maxHpItem != null ? rewardCatalog.maxHpItem.icon : null;
            case "Gold_Add":
                return rewardCatalog.coinItem != null ? rewardCatalog.coinItem.icon : null;
            case "Ticket_Give":
            case "Ticket_Choice":
            case "Ticket_All":
                return rewardCatalog.ticketPack != null ? rewardCatalog.ticketPack.icon : null;
            case "Stamp_Random":
                foreach (var s in rewardCatalog.stamps) if (s != null && s.icon != null) return s.icon;
                return null;
            default:
                foreach (var s in rewardCatalog.snacks) if (s != null && s.icon != null && row.SlotType == RewardSlotType.Snack) return s.icon;
                return null;
        }
    }

    static Sprite TargetIcon(RewardAssetCatalog rewardCatalog, RewardRow row)
    {
        switch (row.Effect_Type)
        {
            case "Snack_Give":
            case "Snack_Fill_Plus":
                return IconOf(rewardCatalog.Snack(row.Param_Str));
            case "Snack_Bundle":
                {
                    string first = string.IsNullOrEmpty(row.Param_Str) ? null : row.Param_Str.Split('|')[0];
                    return IconOf(rewardCatalog.Snack(first));
                }
            case "Dice_Add": return IconOf(rewardCatalog.Dice(row.Param_Str));
            case "Coating_Apply": return IconOf(rewardCatalog.Coating(row.Param_Str));
            case "Satellite_Give": return IconOf(rewardCatalog.Satellite(row.Param_Str));
            default: return null;
        }
    }

    static Sprite IconOf(BaseItemDataSO item) => item != null ? item.icon : null;

    // 루트 테이블 보상 카드를 골랐을 때
    public void OnRewardSelected(LootChoiceSlot slot)
    {
        if (rewardChosen || slot == null || slot.CurrentReward == null || !slot.Selectable) return;
        RewardRow row = slot.CurrentReward;

        // 카드를 그린 뒤 스낵칸이 바뀌었을 수도 있으니 한 번 더 확인한다.
        if (effectRunner == null) effectRunner = new RewardEffectRunner(diceManager, rewardCatalog, LootTable);
        effectRunner.CountSnacks(row, out int total, out int placeable);
        if (RewardEffectRunner.IsSnackEffect(row) && placeable == 0)
        {
            if (ToastPopupController.Instance != null)
                ToastPopupController.Instance.ShowToast(LocalizationManager.GetSys("SYS_LOOT_SNACK_FULL", "스낵칸이 가득 찼습니다."));
            return;
        }

        rewardChosen = true;
        Sfx.Play(SfxId.Shop_Purchase);
        IsPanelOpen = false;
        panelRoot.SetActive(false);
        diceManager.StartCoroutine(RewardRoutine(effectRunner, row));
    }

    IEnumerator RewardRoutine(RewardEffectRunner runner, RewardRow row)
    {
        // 즉시 효과를 주고, 선택 창(티켓·스탬프·코팅·위성)은 하나씩 열어 모두 닫힐 때까지 기다린다.
        yield return runner.Run(row);
        LootChosen?.Invoke();
        yield return WaitAndOpenShopRoutine();
    }

    public void OnLootSelected(BaseItemDataSO selectedLoot)
    {
        if (selectedLoot is SnackItemSO snack)
        {
            bool added = InventoryManager.Instance.AddItem(snack);
            if (!added)
            {
                Debug.Log("스낵 인벤토리가 꽉 차서 받을 수 없습니다!");
                if (ToastPopupController.Instance != null)
                    ToastPopupController.Instance.ShowToast(LocalizationManager.GetSys("SYS_INVENTORY_FULL", "인벤토리가 가득 찼습니다."));
                return; // 꽉 차서 안 들어가면 리턴하여 단계가 넘어가지 않도록 방지
            }
        }
        else
        {
            // DiceItemSO 또는 MaxHPItemSO일 경우 인벤토리에 들어가지 않고 즉시 효과 발동
            selectedLoot.ApplyItemEffect(diceManager);
        }
        Sfx.Play(SfxId.Shop_Purchase);
        LootChosen?.Invoke();
        ClosePanelAndProceed();
    }

    private void ClosePanelAndProceed()
    {
        // 패널 닫기
        IsPanelOpen = false;
        panelRoot.SetActive(false);

        // DiceManager를 통해 잠깐 기다린 뒤 다음 단계로 넘어간다(선택 창이 열려 있으면 닫힐 때까지 기다림).
        diceManager.StartCoroutine(WaitAndOpenShopRoutine());
    }

    private void ShuffleList<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int rnd = Random.Range(i, list.Count);
            T temp = list[i];
            list[i] = list[rnd];
            list[rnd] = temp;
        }
    }
    // 0.2초 뒤 지도 흐름으로 넘기는 코루틴(지도 표의 전투 후 상점 설정이 켜져 있으면 "상점 갈래?" 선택이 뜬다)
    private IEnumerator WaitAndOpenShopRoutine()
    {
        yield return new WaitForSeconds(0.2f);

        // 티켓·코팅·스탬프·위성 선택 창이 열려 있다면, 유저가 고를 때까지 대기
        if (diceManager.shopManager != null)
        {
            while (diceManager.shopManager.IsAnyChoicePanelOpen)
            {
                yield return null;
            }
        }

        diceManager.OnMapLootFinished();
    }

}
