using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;

public class DeckUI : MonoBehaviour
{
    public static bool IsPanelOpen { get; private set; } = false;

    public DiceManager diceManager;
    public GameObject deckPanel;

    [Header("인벤토리 UI 설정")]
    public Transform slotParent;
    public GameObject deckSlotPrefab;

    [Header("버튼 설정")]
    public Button closeButton;
    public Button sortButton;

    private List<DeckSlot> slotList = new List<DeckSlot>();
    private bool isInitialized = false;

    private void Awake()
    {
        if (closeButton != null) closeButton.onClick.AddListener(CloseDeckPanel);
        if (sortButton != null) sortButton.onClick.AddListener(SortDeck);
        IsPanelOpen = false;
    }
    private void Start()
    {
        // 에디터에서 팝업창을 켜둔 채로 시작하더라도, 게임 시작 시 강제로 닫아서 코드와 상태를 일치시킵니다.
        if (deckPanel != null && deckPanel.activeSelf)
        {
            deckPanel.SetActive(false);
            IsPanelOpen = false;
        }
    }

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

        IsPanelOpen = true;
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
        IsPanelOpen = false;
        deckPanel.SetActive(false);
    }

    public void SortDeck()
    {
        if (diceManager == null || diceManager.masterDeck == null) return;

        diceManager.masterDeck = diceManager.masterDeck
            .OrderByDescending(d => d.isCoated)
            .ThenBy(d => d.type)
            .ThenBy(d => d.diceName)
            .ToList();

        UpdateDeckUI();
    }
}