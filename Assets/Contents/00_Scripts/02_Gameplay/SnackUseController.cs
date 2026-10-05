using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

[System.Serializable]
public sealed class SavedSnackSlot
{
    public int slotIndex;
    public string itemID;
    public string itemName;
    public bool pending;
    public bool committed; // 끝내기(라임은 즉시)에서 확정되기 전에는 취소 가능
    public bool preserved;
    public bool effectApplied;
    public bool visualFinished;
    public bool settled;
    public float chips;
    public float multiplier;
    public int order;
}

// 슬롯 위치를 예약한 사용 기록. 확정 시 보너스를 한 번 적용하고 정산에서는 기록된 수치만 표시합니다.
public sealed class SnackUseEntry
{
    public InventorySlot slot;
    public SnackItemSO item;
    public bool committed; // 끝내기(라임은 즉시)에서 확정되기 전에는 취소 가능
    public bool preserved;
    public bool effectApplied;
    public bool visualFinished;
    public bool settled;
    public float chips;
    public float multiplier;
    public int order;
    public Tween animation;
    public bool IsCurrent => slot != null && slot.PendingSnack == this && slot.currentItem == item;
    public bool IsScoreSnack => item.snackType == SnackType.Pancake || item.snackType == SnackType.Cherry;
    public bool IsImmediateSnack => item != null && (item.snackType == SnackType.LimeJuice || item.snackType == SnackType.FortuneCookie);
}

// InventoryManager가 소유하는 일반 C# 객체. 사용 확정/자리 예약/소모 방지/정산 시점을 관리합니다.
public sealed class SnackUseController
{
    private readonly InventoryManager inventory;
    private readonly List<SnackUseEntry> entries = new List<SnackUseEntry>(5);
    public IReadOnlyList<SnackUseEntry> Entries => entries;
    private int nextOrder;

    public SnackUseController(InventoryManager owner) => inventory = owner;

    public bool TryUse(InventorySlot slot, SnackItemSO snack)
    {
        DiceManager dm = inventory.diceManager;
        if (dm == null || dm.isCalculating || dm.currentPlayerHP <= 0 || dm.isStageClearing || ShopManager.IsShopOpen) return false;
        if (dm.enemy == null || dm.enemy.IsDead || slot == null || slot.isEmpty || slot.currentItem != snack) return false;
        if (slot.PendingSnack != null) return TryCancel(slot);
        if (snack.snackType == SnackType.Peppermint)
        {
            if (dm.isPeppermintActive) return false;
            foreach (var pending in entries)
                if (pending.IsCurrent && pending.item.snackType == SnackType.Peppermint) return false;
        }
        if (snack.snackType == SnackType.FortuneCookie)
        {
            if (dm.IsDiceInputLocked || dm.HasRollingDice || !dm.CanReplaceUnkeptDiceFromDeck) return false;
        }

        for (int i = entries.Count - 1; i >= 0; i--)
        {
            if (!entries[i].IsCurrent) entries.RemoveAt(i);
        }

        // 클릭은 사용 예약만 합니다. 취소 가능한 동안에는 효과와 확률 판정을 실행하지 않습니다.
        var entry = new SnackUseEntry
        {
            slot = slot,
            item = snack,
            order = nextOrder++
        };
        entries.Add(entry);
        slot.MarkSnackUsed(entry, inventory.snackUsedTint);

        // 튜토리얼의 사용 선택 안내는 기존 클릭 시점에 진행합니다.
        if (TutorialManager.Instance != null && TutorialManager.Instance.isTutorialActive)
            TutorialManager.Instance.OnItemUsed(snack.itemName);

        // 라임 주스와 포춘쿠키는 즉시 연출. 보존되면 아이콘만 숨기고 끝내기까지 칸과 입력을 잠급니다.
        if (entry.IsImmediateSnack)
        {
            Commit(entry);
            BeginDisappear(entry);
        }
        inventory.HideSellPopup();
        inventory.HideTooltip();
        return true;
    }

    public bool TryCancel(InventorySlot slot)
    {
        DiceManager dm = inventory.diceManager;
        var entry = slot != null ? slot.PendingSnack : null;
        if (dm == null || dm.isCalculating || dm.isStageClearing || dm.currentPlayerHP <= 0 || ShopManager.IsShopOpen) return false;
        if (dm.enemy == null || dm.enemy.IsDead || entry == null || !entry.IsCurrent || entry.committed || entry.IsImmediateSnack) return false;

        entries.Remove(entry);
        slot.ReleaseSnackReservation(); // 원래 색과 입력 상태 복구. 효과는 아직 적용하지 않았으므로 되돌릴 수치가 없습니다.
        inventory.HideTooltip();
        dm.ForceUpdateUI();
        return true;
    }

    private void Commit(SnackUseEntry entry)
    {
        if (!entry.IsCurrent || entry.committed) return;
        entry.committed = true;
        // 실제 사용이 확정된 스낵에 대해서만 소모 방지 여부를 한 번 판정합니다.
        entry.preserved = FigureEffectManager.Instance != null && FigureEffectManager.Instance.ShouldPreserveSnack();
        DiceManager dm = inventory.diceManager;
        int chipsBefore = dm.snackBonusChips;
        float multBefore = dm.snackBonusMult;
        entry.effectApplied = true;
        entry.item.ApplyItemEffect(dm);
        entry.chips = dm.snackBonusChips - chipsBefore;
        entry.multiplier = dm.snackBonusMult - multBefore;
        // 취소한 예약은 OnSnackUsed 피규어를 발동시키지 않습니다.
        FigureEffectManager.Instance?.EvaluateSnackUsedTriggers(dm, dm.shopManager);
    }

    public IEnumerator PlayFinishStart()
    {
        // 모든 회복을 먼저 적용한 뒤 연출을 기다립니다. 족보나 다른 스낵 연출 때문에 회복이 지연되지 않습니다.
        foreach (var entry in entries)
        {
            if (!entry.IsCurrent || entry.settled) continue;
            Commit(entry);
            // 구버전 저장에서 이미 사용 확정했지만 회복은 대기 중인 스테이크 호환
            if (entry.item.snackType == SnackType.Steak && !entry.effectApplied)
            {
                entry.effectApplied = true;
                entry.item.ApplyItemEffect(inventory.diceManager);
            }
            if (!entry.IsScoreSnack) BeginDisappear(entry);
        }
        foreach (var entry in entries)
        {
            if (!entry.IsScoreSnack) yield return CompleteVisual(entry);
        }
    }

    public void BeginDisappear(SnackUseEntry entry)
    {
        if (!entry.IsCurrent || !entry.committed || entry.settled || entry.visualFinished || entry.animation != null) return;
        entry.animation = SnackUseFeedback.Get(entry.slot).Disappear(inventory.snackDissolveDuration, () =>
        {
            entry.animation = null;
            if (!entry.IsCurrent) return;
            entry.visualFinished = true;
            if (!entry.preserved)
            {
                entry.settled = true;
                entry.slot.ClearSlot();
            }
        });
    }

    public IEnumerator CompleteVisual(SnackUseEntry entry)
    {
        if (!entry.IsCurrent || !entry.committed || entry.settled) yield break;
        BeginDisappear(entry);
        while (entry.IsCurrent && entry.animation != null && entry.animation.IsActive()) yield return null;
        if (!entry.IsCurrent || !entry.committed || entry.settled) yield break;

        // 보존된 스낵은 같은 칸으로 돌아오지만 이번 정산이 끝날 때까지 다시 사용할 수 없습니다.
        entry.settled = true;
        if (entry.preserved)
        {
            Tween reveal = SnackUseFeedback.Get(entry.slot).Restore(inventory.snackRestoreDuration);
            if (reveal != null && reveal.IsActive()) yield return reveal.WaitForCompletion();
        }
        else entry.slot.ClearSlot();
    }

    public bool IsScoreEntry(SnackUseEntry entry, bool isChips)
    {
        return entry.IsCurrent && entry.committed && !entry.settled && entry.item.snackType == (isChips ? SnackType.Pancake : SnackType.Cherry);
    }

    public IEnumerator CompleteResolution()
    {
        // 점수판 참조가 없는 경우에도 사용 확정된 스낵과 예약 칸은 정리합니다.
        foreach (var entry in entries) yield return CompleteVisual(entry);
        foreach (var entry in entries)
        {
            if (entry.IsCurrent) entry.slot.ReleaseSnackReservation();
        }
        entries.Clear();
    }

    public void ResetForStage()
    {
        // 미확정 예약은 취소하고, 이미 사용한 라임/구버전 스낵은 기존 소모 방지 결과를 따릅니다.
        foreach (var entry in entries)
        {
            if (!entry.IsCurrent) continue;
            if (!entry.committed || entry.preserved) entry.slot.ReleaseSnackReservation();
            else entry.slot.ClearSlot();
        }
        entries.Clear();
    }

    public void Clear() { entries.Clear(); nextOrder = 0; }

    public void CollectForSave(List<SavedSnackSlot> destination)
    {
        destination.Clear();
        for (int i = 0; i < inventory.snackSlots.Length; i++)
        {
            var slot = inventory.snackSlots[i];
            if (slot == null || slot.isEmpty || !(slot.currentItem is SnackItemSO snack)) continue;
            var pending = slot.PendingSnack;
            destination.Add(new SavedSnackSlot
            {
                slotIndex = i, itemID = snack.Item_ID, itemName = snack.itemName,
                committed = pending != null && pending.committed,
                pending = pending != null, preserved = pending != null && pending.preserved,
                effectApplied = pending != null && pending.effectApplied,
                visualFinished = pending != null && pending.visualFinished,
                settled = pending != null && pending.settled,
                chips = pending != null ? pending.chips : 0f,
                multiplier = pending != null ? pending.multiplier : 0f,
                order = pending != null ? pending.order : 0
            });
        }
    }

    public void RestoreSaved(List<SavedSnackSlot> savedSlots, GameSaveManager saves, int saveVersion = 2)
    {
        if (savedSlots == null) return;
        foreach (var saved in savedSlots)
        {
            if (saved == null || saved.slotIndex < 0 || saved.slotIndex >= inventory.snackSlots.Length) continue;
            var snack = (!string.IsNullOrEmpty(saved.itemID) ? saves.FindItemByID(saved.itemID) : null) as SnackItemSO;
            if (snack == null) snack = saves.FindItemByName(saved.itemName) as SnackItemSO;
            var slot = inventory.snackSlots[saved.slotIndex];
            if (snack == null || slot == null || !slot.isEmpty) continue;
            slot.SetItem(snack);
            if (!saved.pending) continue;

            var entry = new SnackUseEntry
            {
                slot = slot, item = snack, committed = saveVersion < 2 || saved.committed, preserved = saved.preserved, effectApplied = saved.effectApplied,
                visualFinished = saved.visualFinished, settled = saved.settled,
                chips = saved.chips, multiplier = saved.multiplier, order = saved.order
            };
            entries.Add(entry);
            nextOrder = Mathf.Max(nextOrder, entry.order + 1);
            slot.MarkSnackUsed(entry, inventory.snackUsedTint);
            if (entry.visualFinished && !entry.settled) SnackUseFeedback.Get(slot).ShowUsed(inventory.snackUsedTint, true);
            if (entry.settled) SnackUseFeedback.Get(slot).ShowUsed(Color.white);
            // 즉시 연출 중 저장한 라임/포춘쿠키는 효과를 재지급하지 않고 사라지는 연출만 이어갑니다.
            if (entry.IsImmediateSnack && !entry.visualFinished && !entry.settled) BeginDisappear(entry);
        }
        entries.Sort((a, b) => a.order.CompareTo(b.order));
    }
}
