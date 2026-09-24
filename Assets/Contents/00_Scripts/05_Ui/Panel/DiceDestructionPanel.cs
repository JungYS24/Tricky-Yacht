using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class DiceDestructionPanel : MonoBehaviour
{
    public GameObject panelRoot;
    public Transform slotParent;
    public GameObject diceSlotPrefab;

    private List<GameObject> activeSlots = new List<GameObject>();
    private DiceManager diceManager;

    public void OpenSelection(DiceManager dm)
    {
        diceManager = dm;

        // 덱에서 주사위를 랜덤으로 최대 5개 추출 (GetRandomDiceForCoating 재사용)
        List<DiceData1> options = dm.deckManager.GetRandomDiceForCoating(5);

        if (options.Count == 0)
        {
            Debug.Log("파괴할 수 있는 주사위가 덱에 없습니다!");
            if (ToastPopupController.Instance != null)
                ToastPopupController.Instance.ShowToast(LocalizationManager.GetSys("SYS_NO_EQUIPPABLE_DICE", "장착 가능한 주사위가 없습니다."));
            return;
        }

        ClearSlots();
        panelRoot.SetActive(true);

        foreach (var dice in options)
        {
            GameObject slotGo = Instantiate(diceSlotPrefab, slotParent);
            activeSlots.Add(slotGo);

            Button btn = slotGo.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.AddListener(() => OnDiceSelected(dice));
            }

            DeckSlot deckSlot = slotGo.GetComponent<DeckSlot>();
            if (deckSlot != null)
            {
                deckSlot.SetDice(dice, false);
            }
        }
    }

    private void OnDiceSelected(DiceData1 selectedDice)
    {
        if (diceManager == null || selectedDice == null) return;

        // 선택한 주사위를 덱(masterDeck)에서 영구 삭제
        if (diceManager.masterDeck.Remove(selectedDice))
        {
            // 뽑기 더미와 버린 더미에 남아 있는 같은 주사위도 제거
            diceManager.deckManager.drawPile.RemoveAll(
                dice => ReferenceEquals(dice, selectedDice));

            diceManager.deckManager.discardPile.RemoveAll(
                dice => ReferenceEquals(dice, selectedDice));

            Debug.Log($"{selectedDice.diceName} 주사위가 덱에서 영구히 파괴되었습니다! 남은 주사위: {diceManager.masterDeck.Count}");

            // 파괴 선택창을 닫은 뒤 주사위 파괴 피규어 효과 발동
            ClosePanel();

            FigureEffectManager.Instance?.EvaluateDiceDestroyedTriggers(
                diceManager,
                diceManager.shopManager);

            diceManager.ForceUpdateUI();
            return;
        }

        ClosePanel();
    }

    public void ClosePanel()
    {
        panelRoot.SetActive(false);
        ClearSlots();
    }

    private void ClearSlots()
    {
        foreach (var slot in activeSlots)
        {
            Destroy(slot);
        }
        activeSlots.Clear();
    }
}