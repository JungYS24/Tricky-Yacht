using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class DeckUI : MonoBehaviour
{
    public DiceManager diceManager;
    public GameObject deckPanel;

    [Header("인벤토리 UI 설정")]
    public Transform slotParent;
    public GameObject deckSlotPrefab;

    private List<DeckSlot> slotList = new List<DeckSlot>();
    private bool isInitialized = false;

    private void OnEnable()
    {
        DiceManager.OnDeckUpdateNeeded += RefreshDeckVisuals;
    }

    private void OnDisable()
    {
        DiceManager.OnDeckUpdateNeeded -= RefreshDeckVisuals;
    }

    void InitializeSlots()
    {
        int maxCapacity = 42;

        for (int i = 0; i < maxCapacity; i++)
        {
            GameObject go = Instantiate(deckSlotPrefab, slotParent);
            DeckSlot slot = go.GetComponent<DeckSlot>();
            slotList.Add(slot);
        }
    }

    public void OnClickDeckButton()
    {
        if (deckPanel.activeSelf)
        {
            CloseDeckPanel();
            return;
        }

        if (!isInitialized)
        {
            InitializeSlots();
            isInitialized = true;
        }

        UpdateDeckUI();
        deckPanel.SetActive(true);
    }

    private void RefreshDeckVisuals()
    {
        if (deckPanel != null && deckPanel.activeSelf)
        {
            UpdateDeckUI();
        }
    }

    private void UpdateDeckUI()
    {
        List<DiceData1> myDeck = diceManager.masterDeck;
        List<DiceData1> currentDrawPile = diceManager.drawPile;
        List<Dice> activeDice = diceManager.activeDiceList;

        for (int i = 0; i < slotList.Count; i++)
        {
            if (i < myDeck.Count)
            {
                DiceData1 diceData = myDeck[i];
                bool isUsed = false;

                if (currentDrawPile != null)
                {
                    isUsed = !currentDrawPile.Contains(diceData);
                }

                int exactValue = -1;
                if (isUsed && activeDice != null)
                {
                    Dice fieldDice = activeDice.FirstOrDefault(d => d != null && d.myData == diceData);
                    if (fieldDice != null)
                    {
                        exactValue = fieldDice.currentValue;
                    }
                }

                slotList[i].SetDice(diceData, isUsed, exactValue);
            }
            else
            {
                slotList[i].SetEmpty();
            }
        }
    }

    public void CloseDeckPanel()
    {
        deckPanel.SetActive(false);
    }
}