using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class StampSelectionPanel : MonoBehaviour
{
    public static bool IsPanelOpen { get; private set; } = false;

    public GameObject panelRoot;
    public Transform slotParent;
    public GameObject diceSlotPrefab;

    private StampType pendingStampType;
    private Sprite pendingSealImage;

    private List<GameObject> activeSlots = new List<GameObject>();
    private DiceManager diceManager;

    public void OpenSelection(DiceManager dm, StampType type, Sprite image)
    {
        IsPanelOpen = true;

        diceManager = dm;
        pendingStampType = type;
        pendingSealImage = image;

        List<DiceData1> options = dm.deckManager.GetRandomDiceForCoating(5);

        if (options.Count == 0)
        {
            Debug.Log("스탬프를 찍을 수 있는 주사위가 덱에 없습니다!");
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
       

        ClosePanel();
    }

    public void ClosePanel()
    {
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