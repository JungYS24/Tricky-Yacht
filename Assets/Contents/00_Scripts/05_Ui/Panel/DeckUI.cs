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
    private int lastToggleFrame = -1;

    private void Awake()
    {
        if (closeButton != null) closeButton.onClick.AddListener(CloseDeckPanel);
        if (sortButton != null) sortButton.onClick.AddListener(SortDeck);

        // 버튼으로 연 직후 첫 활성화라면 닫지 않는다.
        if (lastToggleFrame != Time.frameCount)
            ForceClosed();
    }

    private void OnEnable()
    {
        DiceManager.OnDeckUpdateNeeded += RefreshDeckVisuals;
        AlignPanelWithOpenState();
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
        if (lastToggleFrame == Time.frameCount)
            return;
        lastToggleFrame = Time.frameCount;

        if (IsPanelOpen)
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
        if (deckPanel != null)
        {
            deckPanel.SetActive(true);
            deckPanel.transform.SetAsLastSibling();
        }
    }

    private void RefreshDeckVisuals()
    {
        if (IsPanelOpen)
            UpdateDeckUI();
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
        if (deckPanel != null)
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

    private void ForceClosed()
    {
        IsPanelOpen = false;
        if (deckPanel != null && deckPanel.activeSelf)
            deckPanel.SetActive(false);
    }

    // 꺼져 있다 켜져도 플래그와 패널 활성 상태를 맞춘다.
    private void AlignPanelWithOpenState()
    {
        if (deckPanel == null)
            return;

        if (IsPanelOpen)
        {
            if (!deckPanel.activeSelf)
                deckPanel.SetActive(true);
            return;
        }

        if (lastToggleFrame != Time.frameCount && deckPanel.activeSelf)
            deckPanel.SetActive(false);
    }
}
