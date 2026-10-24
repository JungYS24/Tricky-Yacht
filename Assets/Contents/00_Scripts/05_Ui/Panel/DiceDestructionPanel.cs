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
    private bool isBusy;
    private DiceChoiceWindow window;

    public void OpenSelection(DiceManager dm)
    {
        if (isBusy) return;
        diceManager = dm;

        // 덱에서 주사위를 랜덤으로 최대 5개 추출 (여정 주사위는 파괴 후보에서 제외)
        List<DiceData1> options = dm.deckManager.GetRandomDiceForDestruction(5);

        if (options.Count == 0)
        {
            Debug.Log("파괴할 수 있는 주사위가 덱에 없습니다!");
            if (ToastPopupController.Instance != null)
                ToastPopupController.Instance.ShowToast(LocalizationManager.GetSys("SYS_NO_EQUIPPABLE_DICE", "장착 가능한 주사위가 없습니다."));
            return;
        }

        panelRoot.SetActive(true);
        window = DiceChoiceWindow.Attach(panelRoot.transform as RectTransform);
        if (window != null)
        {
            // 덱 주사위 카드 5장 + 리롤(지금 보이는 주사위를 빼고 다른 주사위로, 여정 주사위는 계속 제외)
            window.Open(DiceChoiceWindow.DestructionHeader(dm.shopManager), options, diceSlotPrefab, OnDiceSelected,
                dm.shopManager, dm.deckManager.GetDestructionCandidates, () => isBusy);
        }
        else ShowOptions(options);
    }

    // 선택 연출이 흐리게 만들 카드(창이 없으면 예전 주사위 칸)
    private List<GameObject> FeedbackSlots => window != null ? window.Cards : activeSlots;

    private void ShowOptions(List<DiceData1> options)
    {
        ClearSlots();

        foreach (var dice in options)
        {
            GameObject slotGo = Instantiate(diceSlotPrefab, slotParent);
            activeSlots.Add(slotGo);

            Button btn = slotGo.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.AddListener(() => OnDiceSelected(dice, slotGo.GetComponent<DeckSlot>()));
            }

            DeckSlot deckSlot = slotGo.GetComponent<DeckSlot>();
            if (deckSlot != null)
            {
                deckSlot.SetDice(dice, false);
            }
        }
    }

    private void OnDiceSelected(DiceData1 selectedDice, DeckSlot selectedSlot)
    {
        if (isBusy) return;
        if (diceManager == null || selectedDice == null) return;

        // 여정 주사위는 어떤 경로로도 파괴할 수 없다(방어 코드).
        if (DeckManager.IsProtected(selectedDice)) return;

        // 선택한 주사위를 덱(masterDeck)에서 영구 삭제
        if (diceManager.masterDeck.Remove(selectedDice))
        {
            isBusy = true;

            // 뽑기 더미와 버린 더미에 남아 있는 같은 주사위도 제거
            diceManager.deckManager.drawPile.RemoveAll(
                dice => ReferenceEquals(dice, selectedDice));

            diceManager.deckManager.discardPile.RemoveAll(
                dice => ReferenceEquals(dice, selectedDice));

            Debug.Log($"{selectedDice.diceName} 주사위가 덱에서 영구히 파괴되었습니다! 남은 주사위: {diceManager.masterDeck.Count}");

            Sfx.Play(SfxId.Dice_Burn_Vanish);
            DiceSelectionFeedback.Get(this).PlayDestruction(selectedSlot, FeedbackSlots, () =>
            {
                isBusy = false;
                // 파괴 선택창을 닫은 뒤 주사위 파괴 피규어 효과 발동
                ClosePanel();

                if (diceManager == null) return;
                FigureEffectManager.Instance?.EvaluateDiceDestroyedTriggers(
                    diceManager,
                    diceManager.shopManager);

                if (diceManager.enemy != null) diceManager.ForceUpdateUI();
            });
            return;
        }

        ClosePanel();
    }

    public System.Action onClosed;

    public void ClosePanel()
    {
        if (isBusy) return;
        if (panelRoot != null) panelRoot.SetActive(false);
        ClearSlots();
        System.Action callback = onClosed;
        onClosed = null;
        callback?.Invoke();
    }

    private void ClearSlots()
    {
        foreach (var slot in activeSlots)
        {
            Destroy(slot);
        }
        activeSlots.Clear();
        if (window != null) window.Clear();
    }
}
