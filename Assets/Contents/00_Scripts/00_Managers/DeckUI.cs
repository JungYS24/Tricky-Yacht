using UnityEngine;
using System.Collections.Generic;

public class DeckUI : MonoBehaviour
{
    public DiceManager diceManager;
    public GameObject deckPanel;

    [Header("인벤토리 UI 설정")]
    public Transform slotParent;        
    public GameObject deckSlotPrefab;   

    private List<DeckSlot> slotList = new List<DeckSlot>();
    private bool isInitialized = false; 

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

    private void UpdateDeckUI()
    {
        List<DiceData1> myDeck = diceManager.masterDeck;
        List<DiceData1> currentDrawPile = diceManager.drawPile; 

        for (int i = 0; i < slotList.Count; i++)
        {
            if (i < myDeck.Count)
            {
                bool isUsed = false;

                if (currentDrawPile != null)
                {
                    isUsed = !currentDrawPile.Contains(myDeck[i]);
                }
                slotList[i].SetDice(myDeck[i], isUsed);
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