using System;
using System.Collections.Generic;
using UnityEngine;

// 보드 위 주사위와 킵 슬롯, 풀 재사용을 담당합니다. 턴 규칙과 UI는 DiceManager가 결정합니다.
// MonoBehaviour가 아니므로 씬에 컴포넌트를 추가하지 않습니다.
public sealed class DiceBoardController
{
    public List<Dice> ActiveDice { get; }
    public bool HasRollingDice
    {
        get
        {
            foreach (var die in ActiveDice)
                if (die != null && die.IsRollAnimating) return true;
            return false;
        }
    }
    private readonly GameObject dicePrefab;
    private readonly DeckManager deckManager;
    private readonly Transform[] keepSlots;
    private readonly Transform[] rollSlots;
    private readonly Dice[] keepSlotOccupants;
    private readonly List<Dice> dicePool = new List<Dice>();

    public DiceBoardController(GameObject prefab, Transform keepParent, Transform rollParent, List<Dice> activeDice, DeckManager deck)
    {
        dicePrefab = prefab;
        deckManager = deck;
        ActiveDice = activeDice;
        keepSlots = ReadSlots(keepParent);
        rollSlots = ReadSlots(rollParent);
        keepSlotOccupants = new Dice[keepSlots.Length];
    }

    private static Transform[] ReadSlots(Transform parent)
    {
        if (parent == null) return Array.Empty<Transform>();
        var slots = new Transform[parent.childCount];
        for (int i = 0; i < slots.Length; i++) slots[i] = parent.GetChild(i);
        return slots;
    }

    // UI 버퍼와 분리된 정산 스냅샷. 슬롯 순서를 유지하고 전달받은 버퍼를 재사용합니다.
    public void CollectKept(List<Dice> dice, List<int> values)
    {
        dice.Clear();
        values.Clear();
        foreach (var d in keepSlotOccupants)
        {
            if (d == null) continue;
            dice.Add(d);
            values.Add(d.currentValue);
        }
    }

    public void SpawnDice(bool returnPreviousDiceToDiscard, int currentStage)
    {
        //기존 활성화된 주사위들을 파괴하지 않고 비활성화하여 풀(Pool)에 보관
        foreach (var d in ActiveDice)
        {
            if (d != null)
            {
                d.isKept = false;
                d.RefreshHoverJuice();
                d.currentKeepIndex = -1;
                d.gameObject.SetActive(false);
                dicePool.Add(d);
                if (returnPreviousDiceToDiscard && d.myData != null)
                {
                    deckManager.discardPile.Add(d.myData);
                }
            }

        }
        ActiveDice.Clear();
        Array.Clear(keepSlotOccupants, 0, keepSlotOccupants.Length);

        // 덱 리필 검사 로직 한 줄로 압축! (DeckManager에게 위임)
        deckManager.CheckAndRefillDrawPile(5);

        if (TutorialManager.Instance != null && TutorialManager.Instance.isTutorialActive)
        {
            // 2스테이지 첫 진입 시 하이롤러 주사위 확정 스폰!
            if (currentStage == 2 && deckManager.drawPile.Count >= 5)
            {
                string hrName = TutorialManager.Instance.tutorialHighRollerDice.itemName;
                int hrIdx = deckManager.drawPile.FindIndex(d => d.diceName == hrName);
                if (hrIdx != -1)
                {
                    var temp = deckManager.drawPile[0];
                    deckManager.drawPile[0] = deckManager.drawPile[hrIdx];
                    deckManager.drawPile[hrIdx] = temp;
                }
            }
            // 3스테이지 보스 반격 후 체력 회복 튜토리얼 (하트 주사위 확정)
            else if (currentStage == 3 && TutorialManager.Instance.currentStepIndex >= 28 && TutorialManager.Instance.currentStepIndex <= 30)
            {
                int heartIdx = deckManager.drawPile.FindIndex(d => d.specialEffect == SpecialDieEffect.Heart);
                if (heartIdx == -1)
                {
                    int discardIdx = deckManager.discardPile.FindIndex(d => d.specialEffect == SpecialDieEffect.Heart);
                    if (discardIdx != -1)
                    {
                        deckManager.drawPile.Insert(0, deckManager.discardPile[discardIdx]);
                        deckManager.discardPile.RemoveAt(discardIdx);
                    }
                }
                else
                {
                    var temp = deckManager.drawPile[0];
                    deckManager.drawPile[0] = deckManager.drawPile[heartIdx];
                    deckManager.drawPile[heartIdx] = temp;
                }
            }
            // 3스테이지 보스전 첫 번째 턴 (코팅 주사위 확정)
            else if (currentStage == 3 && TutorialManager.Instance.currentStepIndex >= 24 && TutorialManager.Instance.currentStepIndex <= 27)
            {
                int coatedIdx = deckManager.drawPile.FindIndex(d => d.isCoated);
                if (coatedIdx != -1)
                {
                    var temp = deckManager.drawPile[0];
                    deckManager.drawPile[0] = deckManager.drawPile[coatedIdx];
                    deckManager.drawPile[coatedIdx] = temp;
                }
            }
        }

        for (int i = 0; i < rollSlots.Length; i++)
        {
            // 복잡했던 덱 리필 및 드로우 로직이 단 한 줄로 끝납니다.
            DiceData1 drawnData = deckManager.DrawOneDice();
            if (drawnData == null) break;

            // Instantiate 대신 풀에서 대기 중인 주사위 꺼내 쓰기
            Dice d;
            if (dicePool.Count > 0)
            {
                d = dicePool[dicePool.Count - 1];
                dicePool.RemoveAt(dicePool.Count - 1);
                d.transform.position = rollSlots[i].position;
                d.gameObject.SetActive(true);
            }
            else
            {
                GameObject go = UnityEngine.Object.Instantiate(dicePrefab, rollSlots[i].position, Quaternion.identity);
                d = go.GetComponent<Dice>();
            }

            d.rollPos = rollSlots[i].position;
            int initialVal = drawnData.faceValues[UnityEngine.Random.Range(0, 6)];

            //튜토리얼 추가
            if (TutorialManager.Instance != null && TutorialManager.Instance.isTutorialActive)
            {
                int forcedVal = TutorialManager.Instance.GetForcedDiceValue(i);
                if (forcedVal != -1) initialVal = forcedVal;
            }

            d.SetData(drawnData, initialVal);
            ActiveDice.Add(d);
        }
    }

    // 보드/더미는 전체 덱 인덱스를 저장하여 같은 종류의 주사위도 개별 인스턴스로 구분합니다.
    public void CaptureForSave(SaveData data)
    {
        var indices = new Dictionary<DiceData1, int>(deckManager.masterDeck.Count);
        for (int i = 0; i < deckManager.masterDeck.Count; i++)
        {
            var die = deckManager.masterDeck[i];
            if (die == null || indices.ContainsKey(die)) return;
            indices.Add(die, i);
        }
        for (int i = 0; i < ActiveDice.Count; i++)
        {
            var die = ActiveDice[i];
            if (die == null || !die.gameObject.activeInHierarchy || die.myData == null || !indices.TryGetValue(die.myData, out int deckIndex)) return;
            data.boardDice.Add(new SavedBoardDie { deckIndex = deckIndex, value = die.currentValue, rollSlotIndex = i, keepSlotIndex = die.isKept ? die.currentKeepIndex : -1 });
        }
        foreach (var die in deckManager.drawPile)
        {
            if (die == null || !indices.TryGetValue(die, out int index)) return;
            data.drawPileIndices.Add(index);
        }
        foreach (var die in deckManager.discardPile)
        {
            if (die == null || !indices.TryGetValue(die, out int index)) return;
            data.discardPileIndices.Add(index);
        }
        data.boardSaveVersion = 1;
        if (!CanRestoreState(data)) data.boardSaveVersion = 0;
    }

    public bool CanRestoreState(SaveData data)
    {
        if (data.boardSaveVersion != 1 || data.boardDice == null || data.boardDice.Count == 0 || data.boardDice.Count > rollSlots.Length || data.drawPileIndices == null || data.discardPileIndices == null) return false;
        int count = deckManager.masterDeck.Count;
        if (data.savedFakeDiceIndex < -1 || data.savedFakeDiceIndex >= count || data.savedCurrentRerolls < 0 || data.savedMaxRerolls < 0) return false;
        var usedDice = new bool[count];
        var usedRollSlots = new bool[rollSlots.Length];
        var usedKeepSlots = new bool[keepSlots.Length];
        for (int i = 0; i < data.boardDice.Count; i++)
        {
            var saved = data.boardDice[i];
            if (saved == null || saved.deckIndex < 0 || saved.deckIndex >= count || usedDice[saved.deckIndex] || saved.rollSlotIndex < 0 || saved.rollSlotIndex >= rollSlots.Length || usedRollSlots[saved.rollSlotIndex] || saved.keepSlotIndex < -1 || saved.keepSlotIndex >= keepSlots.Length) return false;
            // ActiveDice 순서와 원래 굴림 위치가 일치해야 다음 저장/리롤에서도 위치가 유지됩니다.
            if (saved.rollSlotIndex != i) return false;
            usedDice[saved.deckIndex] = true;
            usedRollSlots[saved.rollSlotIndex] = true;
            if (saved.keepSlotIndex >= 0)
            {
                if (usedKeepSlots[saved.keepSlotIndex]) return false;
                usedKeepSlots[saved.keepSlotIndex] = true;
            }
        }
        foreach (int index in data.drawPileIndices)
        {
            if (index < 0 || index >= count || usedDice[index]) return false;
            usedDice[index] = true;
        }
        foreach (int index in data.discardPileIndices)
        {
            if (index < 0 || index >= count || usedDice[index]) return false;
            usedDice[index] = true;
        }
        return true;
    }

    public bool TryRestoreState(SaveData data)
    {
        if (!CanRestoreState(data)) return false;
        // 기존 오브젝트를 풀에 돌려놓되, 저장에서 복원한 버린 덱은 변경하지 않습니다.
        foreach (var die in ActiveDice)
        {
            if (die == null) continue;
            die.isKept = false;
            die.currentKeepIndex = -1;
            die.gameObject.SetActive(false);
            dicePool.Add(die);
        }
        ActiveDice.Clear();
        Array.Clear(keepSlotOccupants, 0, keepSlotOccupants.Length);
        foreach (var saved in data.boardDice)
        {
            Vector3 rollPosition = rollSlots[saved.rollSlotIndex].position;
            Dice die;
            if (dicePool.Count > 0)
            {
                int last = dicePool.Count - 1;
                die = dicePool[last];
                dicePool.RemoveAt(last);
                die.gameObject.SetActive(true);
            }
            else die = UnityEngine.Object.Instantiate(dicePrefab, rollPosition, Quaternion.identity).GetComponent<Dice>();
            die.isKept = false;
            die.SetData(deckManager.masterDeck[saved.deckIndex], saved.value);
            die.rollPos = rollPosition;
            die.currentKeepIndex = saved.keepSlotIndex;
            die.isKept = saved.keepSlotIndex >= 0;
            die.transform.position = die.isKept ? keepSlots[saved.keepSlotIndex].position : rollPosition;
            die.RefreshRestoredState();
            if (die.isKept) keepSlotOccupants[saved.keepSlotIndex] = die;
            ActiveDice.Add(die);
        }
        return true;
    }

    void AssignToKeepSlot(Dice d)
    {
        int index = Array.IndexOf(keepSlotOccupants, null);
        if (index != -1) { keepSlotOccupants[index] = d; d.currentKeepIndex = index; d.MoveToTarget(keepSlots[index].position); }
    }

    void ReleaseFromKeepSlot(Dice d)
    {
        if (d.currentKeepIndex != -1) { keepSlotOccupants[d.currentKeepIndex] = null; d.currentKeepIndex = -1; d.MoveToTarget(d.rollPos); }
    }

    public void RollUnkept()
    {
        foreach (var d in ActiveDice)
        {
            if (d == null || d.isKept) continue;
            int finalResult = d.myData.faceValues[UnityEngine.Random.Range(0, 6)];
            //튜토리얼 추가
            if (TutorialManager.Instance != null && TutorialManager.Instance.isTutorialActive)
            {
                int diceIndex = ActiveDice.IndexOf(d);
                int forcedVal = TutorialManager.Instance.GetForcedDiceValue(diceIndex);
                if (forcedVal != -1) finalResult = forcedVal;
            }

            d.PlayRollEffect(finalResult);
        }

    }

    public void SyncKeepSlots(out int keptCount, out bool hasDiceToRoll)
    {
        keptCount = 0;
        hasDiceToRoll = false;
        foreach (var d in ActiveDice)
        {
            if (d == null) continue;
            if (d.isKept) { if (d.currentKeepIndex == -1) AssignToKeepSlot(d); keptCount++; }
            else { if (d.currentKeepIndex != -1) ReleaseFromKeepSlot(d); hasDiceToRoll = true; }
        }
    }
}
