using System;
using System.Collections.Generic;
using UnityEngine;

// 보드 위 주사위와 킵 슬롯, 풀 재사용을 담당합니다. 턴 규칙과 UI는 DiceManager가 결정합니다.
// MonoBehaviour가 아니므로 씬에 컴포넌트를 추가하지 않습니다.
public sealed class DiceBoardController
{
    public List<Dice> ActiveDice { get; }
    private readonly GameObject dicePrefab;
    private readonly DeckManager deckManager;
    private readonly Transform[] keepSlots;
    private readonly Transform[] rollSlots;
    private readonly Dice[] keepSlotOccupants;
    private readonly List<Dice> dicePool = new List<Dice>();
    private readonly List<Dice> remaining = new List<Dice>(5);

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
        if (returnPreviousDiceToDiscard && ActiveDice.Count > 0)
        {
            RecycleKeptDiceAndRefillFromDeck();
            return;
        }

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

    void RecycleKeptDiceAndRefillFromDeck()
    {
        remaining.Clear();
        foreach (var d in ActiveDice)
        {
            if (d == null) continue;

            if (d.isKept)
            {
                if (d.myData != null) deckManager.discardPile.Add(d.myData);
                d.isKept = false;
                d.RefreshHoverJuice();
                d.currentKeepIndex = -1;
                d.gameObject.SetActive(false);
                dicePool.Add(d);
            }
            else
            {
                d.currentKeepIndex = -1;
                remaining.Add(d);
            }
        }

        ActiveDice.Clear();
        Array.Clear(keepSlotOccupants, 0, keepSlotOccupants.Length);

        int slot = 0;
        foreach (var d in remaining)
        {
            if (slot >= rollSlots.Length) break;
            d.rollPos = rollSlots[slot].position;
            d.MoveToTarget(d.rollPos);
            ActiveDice.Add(d);
            slot++;
        }

        int need = rollSlots.Length - ActiveDice.Count;
        deckManager.CheckAndRefillDrawPile(Mathf.Max(need, 1));

        for (int i = slot; i < rollSlots.Length; i++)
        {
            DiceData1 drawnData = deckManager.DrawOneDice();
            if (drawnData == null) break;

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

            if (TutorialManager.Instance != null && TutorialManager.Instance.isTutorialActive)
            {
                int forcedVal = TutorialManager.Instance.GetForcedDiceValue(i);
                if (forcedVal != -1) initialVal = forcedVal;
            }

            d.SetData(drawnData, initialVal);
            ActiveDice.Add(d);
        }
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
