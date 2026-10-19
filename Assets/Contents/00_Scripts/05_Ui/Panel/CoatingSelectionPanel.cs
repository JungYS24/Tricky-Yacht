using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class CoatingSelectionPanel : MonoBehaviour
{
    public static bool IsPanelOpen { get; private set; } = false;

    public GameObject panelRoot;
    public Transform slotParent;
    public GameObject diceSlotPrefab;

    private DiceType pendingCoatingType;
    private float pendingMultiplier;
    private Color pendingColor;

    private List<GameObject> activeSlots = new List<GameObject>();
    private DiceManager diceManager;
    private bool isBusy;

    public void OpenSelection(DiceManager dm, DiceType type, float mult, Color color)
    {
        if (isBusy) return;
        IsPanelOpen = true;

        diceManager = dm;
        pendingCoatingType = type;
        pendingMultiplier = mult;
        pendingColor = color;


        //(GetRandomDiceForCoating 호출)
        List<DiceData1> options = dm.deckManager.GetRandomDiceForCoating(5);

        if (options.Count == 0)
        {
            Debug.Log("코팅할 수 있는 주사위가 덱에 없습니다!");
            if (ToastPopupController.Instance != null)
                ToastPopupController.Instance.ShowToast(LocalizationManager.GetSys("SYS_NO_EQUIPPABLE_DICE", "장착 가능한 주사위가 없습니다."));
            IsPanelOpen = false;
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
        if (diceManager == null || selectedDice == null || !diceManager.masterDeck.Contains(selectedDice)) return;
        isBusy = true;

        selectedDice.isCoated = true;
        selectedDice.type = pendingCoatingType;
        selectedDice.multiplier = pendingMultiplier;
        selectedDice.diceColor = pendingColor;

        Debug.Log($"{selectedDice.diceName}에 {pendingCoatingType} 코팅 적용 완료!");

        DiceSelectionFeedback.Get(this).PlayCoating(selectedSlot, activeSlots, pendingCoatingType, pendingColor, () =>
        {
            if (selectedSlot != null) selectedSlot.SetDice(selectedDice, false);
        }, () =>
        {
            isBusy = false;
            // 튜토리얼 중일 때 코팅 처리가 끝났음을 알림
            if (TutorialManager.Instance != null && TutorialManager.Instance.isTutorialActive)
            {
                TutorialManager.Instance.OnCoatingAppliedComplete();
            }

            ClosePanel();
            if (diceManager != null && diceManager.enemy != null) diceManager.ForceUpdateUI();
        });
    }

    public void ClosePanel()
    {
        if (isBusy) return;
        IsPanelOpen = false;

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
