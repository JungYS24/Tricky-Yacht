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
    private bool isInitialized = false; // 추가됨: 생성 완료 여부 체크

    // Start() 함수는 완전히 삭제했습니다!

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

        // 추가됨: 패널을 열 때, 슬롯이 한 번도 생성 안 되었다면 지금 당장 42개를 만들어라!
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

        for (int i = 0; i < slotList.Count; i++)
        {
            if (i < myDeck.Count)
            {
                slotList[i].SetDice(myDeck[i]);
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