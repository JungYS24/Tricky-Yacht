using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;

public enum CollectionBiomeFilter
{
    All = -1,
    Special = 0, Forest = 1, Meadow = 2, Temple = 3, Jungle = 4, Desert = 5,
    Ruins = 6, Cave = 7, Volcano = 8, Swamp = 9, Beach = 10, Ocean = 11,
    Abyss = 12, Snow = 13, Grave = 14, Circus = 15, Void = 16, Skyisland = 17
}
public enum CollectionStatusFilter { All, Unlocked, Locked }

public class CollectionBookManager : MonoBehaviour
{
    // 필터 버튼과 바이옴 좌우 넘김이 같이 쓰는 순서 (전체 → 바이옴 순)
    private static readonly int[] BiomeFilterOrder = { -1, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17 };

    [Header("도감 데이터")]
    public List<FigureItemSO> masterFigureDatabase;

    [Header("UI 연결")]
    public GameObject collectionPanelRoot;
    public Transform gridContentParent;
    public GameObject collectionSlotPrefab;
    public FigureDetailPanel detailPanel;

    [Header("필터창 UI")]
    public GameObject biomeFilterPanelRoot; // 어두운 배경을 포함한 전체 필터창

    [Header("필터 버튼 동적 생성")]
    public Transform filterGridParent;      // Grid Layout Group이 있는 부모
    public GameObject filterButtonPrefab;   // BiomeFilterSlot.cs가 달린 버튼 프리팹

    [Header("진행도 텍스트")]
    public TextMeshProUGUI progressText;

    [Header("현재 필터 상태")]
    public CollectionBiomeFilter currentBiomeFilter = CollectionBiomeFilter.All;
    public CollectionStatusFilter currentStatusFilter = CollectionStatusFilter.All;

    [Header("모달 (딤 클릭 시 닫기)")]
    public Button dimButton;

    [Header("책갈피 탭")]
    public Button figureTabButton;
    public Button synergyTabButton;
    [FormerlySerializedAs("achievementTabButton")] public Button ticketTabButton; // 예전 업적 탭 자리
    public TextMeshProUGUI figureTabLabel;
    public TextMeshProUGUI synergyTabLabel;
    [FormerlySerializedAs("achievementTabLabel")] public TextMeshProUGUI ticketTabLabel;
    public Color tabSelectedColor = Color.white;
    public Color tabUnselectedColor = new Color(0.72f, 0.72f, 0.72f, 1f);
    public float tabSelectedOffsetX = -16f; // 선택된 탭이 책 밖으로 더 나오는 거리

    [Header("페이지")]
    public GameObject figurePageRoot;
    public GameObject synergyPageRoot;
    public SynergyWikiDisplayer synergyWiki;
    public GameObject ticketPageRoot;
    public TicketBookPage ticketPage;

    [Header("피규어 페이지 헤더")]
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI biomeNameText;
    public Button prevBiomeButton;
    public Button nextBiomeButton;

    [Header("피규어 페이지 넘김")]
    public int slotsPerPage = 15;
    public Button prevPageButton;
    public Button nextPageButton;
    public TextMeshProUGUI pageText;

    private readonly List<CollectionSlot> slotPool = new List<CollectionSlot>();
    private List<FigureItemSO> currentFilteredList = new List<FigureItemSO>();
    private readonly Dictionary<Button, Vector2> tabBasePositions = new Dictionary<Button, Vector2>();
    private int currentPage;
    private bool isInitialized;
    private enum BookTab { Figure, Synergy, Ticket }
    private BookTab openTab;

    public TextMeshProUGUI currentFilterText;

    private static bool HasPersistentMethod(Button button, string methodName)
    {
        for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
        {
            if (button.onClick.GetPersistentMethodName(i) == methodName) return true;
        }
        return false;
    }

    private bool UsePaging => prevPageButton != null && nextPageButton != null && slotsPerPage > 0;
    private bool UseBiomeStepper => prevBiomeButton != null && nextBiomeButton != null;

    private void Start()
    {
        EnsureInitialized();

        if (biomeFilterPanelRoot != null) biomeFilterPanelRoot.SetActive(false);
        RefreshCollectionBoard();
    }

    // 패널이 꺼진 채로 시작하므로 Start보다 OpenCollectionBook이 먼저 불릴 수 있다.
    private void EnsureInitialized()
    {
        if (isInitialized) return;
        isInitialized = true;

        masterFigureDatabase = masterFigureDatabase.OrderBy(f => (int)f.sourceBiomes.FirstOrDefault()).ToList();

        GenerateFilterButtons(); // 시작할 때 필터 버튼 자동 생성

        if (dimButton != null) dimButton.onClick.AddListener(CloseCollectionBook);
        // 씬에 저장된 호출이 있으면 런타임에 또 붙이지 않는다. 없으면 예전 씬도 동작하게 붙인다.
        if (figureTabButton != null && !HasPersistentMethod(figureTabButton, nameof(ShowFigurePage)))
            figureTabButton.onClick.AddListener(ShowFigurePage);
        if (synergyTabButton != null && !HasPersistentMethod(synergyTabButton, nameof(ShowSynergyPage)))
            synergyTabButton.onClick.AddListener(ShowSynergyPage);
        if (ticketTabButton != null && !HasPersistentMethod(ticketTabButton, nameof(ShowTicketPage)))
            ticketTabButton.onClick.AddListener(ShowTicketPage);
        if (prevBiomeButton != null) prevBiomeButton.onClick.AddListener(() => StepBiomeFilter(-1));
        if (nextBiomeButton != null) nextBiomeButton.onClick.AddListener(() => StepBiomeFilter(1));
        if (prevPageButton != null) prevPageButton.onClick.AddListener(() => ChangePage(-1));
        if (nextPageButton != null) nextPageButton.onClick.AddListener(() => ChangePage(1));
        if (detailPanel != null) detailPanel.synergyClicked = ShowSynergy;

        foreach (var tab in new[] { figureTabButton, synergyTabButton, ticketTabButton })
        {
            if (tab != null) tabBasePositions[tab] = ((RectTransform)tab.transform).anchoredPosition;
        }
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame) return;

        // 정렬 드롭다운 → 상세 → 필터창 → 도감 순서로 하나씩 닫는다.
        if (openTab == BookTab.Synergy && synergyWiki != null && synergyWiki.IsOrderDropdownExpanded)
            synergyWiki.CloseOrderDropdown();
        else if (detailPanel != null && detailPanel.panelRoot != null && detailPanel.panelRoot.activeSelf)
            detailPanel.ClosePanel();
        else if (biomeFilterPanelRoot != null && biomeFilterPanelRoot.activeSelf)
            CloseBiomeFilterPanel();
        else
            CloseCollectionBook();
    }

    // 필터 버튼들을 생성해주는 함수
    private void GenerateFilterButtons()
    {
        if (filterGridParent == null || filterButtonPrefab == null) return;

        foreach (int filterValue in BiomeFilterOrder)
        {
            GameObject btnGo = Instantiate(filterButtonPrefab, filterGridParent);
            BiomeFilterSlot slot = btnGo.GetComponent<BiomeFilterSlot>();

            if (slot != null)
            {
                slot.Setup(GetFilterDisplayName(filterValue), filterValue, this);
            }
        }
    }

    public void OpenCollectionBook()
    {
        if (collectionPanelRoot != null && collectionPanelRoot.activeSelf)
        {
            CloseCollectionBook();
            return;
        }

        EnsureInitialized();

        if (UseBiomeStepper)
        {
            currentBiomeFilter = GetDefaultBiomeFilter();
            currentPage = 0;
        }

        collectionPanelRoot.SetActive(true);
        RefreshTabLabels();
        ShowFigurePage();
    }

    public void CloseCollectionBook()
    {
        if (detailPanel != null) detailPanel.ClosePanel();
        if (biomeFilterPanelRoot != null) biomeFilterPanelRoot.SetActive(false);
        collectionPanelRoot.SetActive(false);
    }

    public void ShowFigurePage()
    {
#if UNITY_EDITOR
        Debug.Log("[CollectionBook] tab -> figure");
#endif
        openTab = BookTab.Figure;
        if (figurePageRoot != null) figurePageRoot.SetActive(true);
        if (synergyPageRoot != null) synergyPageRoot.SetActive(false);
        if (ticketPageRoot != null) ticketPageRoot.SetActive(false);
        UpdateTabVisuals();
        RefreshCollectionBoard();
    }

    public void ShowSynergyPage()
    {
#if UNITY_EDITOR
        Debug.Log("[CollectionBook] tab -> synergy");
#endif
        if (synergyPageRoot == null) return;

        openTab = BookTab.Synergy;
        // 상세창과 바이옴 필터는 피규어 페이지 전용
        if (detailPanel != null) detailPanel.ClosePanel();
        CloseBiomeFilterPanel();

        if (figurePageRoot != null) figurePageRoot.SetActive(false);
        if (ticketPageRoot != null) ticketPageRoot.SetActive(false);
        SetPagerVisible(false);
        synergyPageRoot.SetActive(true);
        if (synergyWiki != null) synergyWiki.Refresh();
        UpdateTabVisuals();
    }

    public void ShowTicketPage()
    {
#if UNITY_EDITOR
        Debug.Log("[CollectionBook] tab -> ticket");
#endif
        if (ticketPageRoot == null) return;

        openTab = BookTab.Ticket;
        if (detailPanel != null) detailPanel.ClosePanel();
        CloseBiomeFilterPanel();

        if (figurePageRoot != null) figurePageRoot.SetActive(false);
        if (synergyPageRoot != null) synergyPageRoot.SetActive(false);
        SetPagerVisible(false);
        ticketPageRoot.SetActive(true);
        if (ticketPage != null) ticketPage.Refresh();
        UpdateTabVisuals();
    }

    // 관련 시너지 카드에서 들어온다. 피규어 페이지의 필터와 페이지는 바꾸지 않는다.
    public void ShowSynergy(SynergyData synergy)
    {
        if (synergyWiki != null) synergyWiki.ArmFocus(synergy);
        ShowSynergyPage();
    }

    public void OpenBiomeFilterPanel()
    {
        if (biomeFilterPanelRoot != null) biomeFilterPanelRoot.SetActive(true);
    }

    public void CloseBiomeFilterPanel()
    {
        if (biomeFilterPanelRoot != null) biomeFilterPanelRoot.SetActive(false);
    }

    public void ChangeBiomeFilter(int biomeFilterIndex)
    {
        currentBiomeFilter = (CollectionBiomeFilter)biomeFilterIndex;
        currentPage = 0;
        // 필터가 바뀌면 상세창이 보던 목록이 달라지므로 닫는다.
        if (detailPanel != null) detailPanel.ClosePanel();
        RefreshCollectionBoard();
        CloseBiomeFilterPanel(); // 누르면 자동으로 닫힘
    }

    public void ChangeStatusFilter(int statusFilterIndex)
    {
        currentStatusFilter = (CollectionStatusFilter)statusFilterIndex;
        currentPage = 0;
        RefreshCollectionBoard();
    }

    public void StepBiomeFilter(int direction)
    {
        int count = BiomeFilterOrder.Length;
        int index = Array.IndexOf(BiomeFilterOrder, (int)currentBiomeFilter);
        if (index < 0) index = 0;
        index = ((index + direction) % count + count) % count;
        ChangeBiomeFilter(BiomeFilterOrder[index]);
    }

    public void ChangePage(int direction)
    {
        int nextPage = Mathf.Clamp(currentPage + direction, 0, GetPageCount() - 1);
        if (nextPage == currentPage) return;

        currentPage = nextPage;
        RefreshCollectionBoard();
    }

    public void RefreshCollectionBoard()
    {
        currentFilteredList.Clear();

        int totalCount = 0;
        int unlockedCount = 0;

        foreach (var figure in masterFigureDatabase)
        {
            if (currentBiomeFilter != CollectionBiomeFilter.All && !figure.sourceBiomes.Contains((BiomeType)currentBiomeFilter))
                continue;

            //하드디스크 대신 매니저의 딕셔너리에서 상태(0, 1, 2)를 한 번에 가져옴
            int figureState = GetFigureState(figure);
            bool isUnlocked = (figureState == 2);         // 2번이면 완전 해금

            if (currentStatusFilter == CollectionStatusFilter.Unlocked && !isUnlocked) continue;
            if (currentStatusFilter == CollectionStatusFilter.Locked && isUnlocked) continue;

            currentFilteredList.Add(figure);

            totalCount++;
            if (isUnlocked) unlockedCount++;
        }

        int pageCount = GetPageCount();
        currentPage = Mathf.Clamp(currentPage, 0, pageCount - 1);
        int startIndex = UsePaging ? currentPage * slotsPerPage : 0;
        int visibleCount = UsePaging
            ? Mathf.Min(slotsPerPage, currentFilteredList.Count - startIndex)
            : currentFilteredList.Count;

        EnsureSlotPool(visibleCount);
        for (int i = 0; i < slotPool.Count; i++)
        {
            CollectionSlot slot = slotPool[i];
            bool visible = i < visibleCount;
            slot.gameObject.SetActive(visible);
            if (!visible) continue;

            FigureItemSO figure = currentFilteredList[startIndex + i];
            int figureState = GetFigureState(figure);
            slot.Setup(figure, figureState == 2, figureState >= 1, this);
        }

        if (progressText != null)
        {
            progressText.text = UseBiomeStepper
                ? LocalizationManager.GetUi("UI_COLLECTION_COUNT", "{0} / {1}", unlockedCount, totalCount)
                : LocalizationManager.GetUi(
                    "UI_COLLECTION_PROGRESS",
                    "{0} : {1} / {2}",
                    GetCurrentFilterDisplayName(),
                    unlockedCount,
                    totalCount);
        }

        if (titleText != null)
            titleText.text = LocalizationManager.GetUi("UI_COLLECTION_TITLE", "피규어 도감");

        UpdatePager(pageCount);
        UpdateFilterText();
    }

    public void OpenFigureDetail(FigureItemSO figure)
    {
        if (detailPanel != null) detailPanel.OpenPanel(currentFilteredList, figure);
    }

    private void EnsureSlotPool(int count)
    {
        while (slotPool.Count < count)
        {
            GameObject slotGo = Instantiate(collectionSlotPrefab, gridContentParent);
            slotPool.Add(slotGo.GetComponent<CollectionSlot>());
        }
    }

    private int GetPageCount()
    {
        if (!UsePaging) return 1;
        return Mathf.Max(1, Mathf.CeilToInt(currentFilteredList.Count / (float)slotsPerPage));
    }

    private void UpdatePager(int pageCount)
    {
        bool show = pageCount > 1;
        SetPagerVisible(show);
        if (!show) return;
        if (pageText != null)
            pageText.text = LocalizationManager.GetUi("UI_PAGE_INDICATOR", "{0} / {1}", currentPage + 1, pageCount);
        if (prevPageButton != null) prevPageButton.interactable = currentPage > 0;
        if (nextPageButton != null) nextPageButton.interactable = currentPage < pageCount - 1;
    }

    private void SetPagerVisible(bool visible)
    {
        if (prevPageButton != null) prevPageButton.gameObject.SetActive(visible);
        if (nextPageButton != null) nextPageButton.gameObject.SetActive(visible);
        if (pageText != null) pageText.gameObject.SetActive(visible);
    }

    private void UpdateTabVisuals()
    {
        SetTabSelected(figureTabButton, openTab == BookTab.Figure);
        SetTabSelected(synergyTabButton, openTab == BookTab.Synergy);
        SetTabSelected(ticketTabButton, openTab == BookTab.Ticket);
    }

    private void SetTabSelected(Button tab, bool selected)
    {
        if (tab == null) return;

        if (tab.targetGraphic != null)
            tab.targetGraphic.color = selected ? tabSelectedColor : tabUnselectedColor;

        if (tabBasePositions.TryGetValue(tab, out Vector2 basePosition))
        {
            ((RectTransform)tab.transform).anchoredPosition =
                selected ? basePosition + new Vector2(tabSelectedOffsetX, 0f) : basePosition;
        }
    }

    private void RefreshTabLabels()
    {
        if (figureTabLabel != null) figureTabLabel.text = LocalizationManager.GetUi("UI_TAB_FIGURE", "피규어");
        if (synergyTabLabel != null) synergyTabLabel.text = LocalizationManager.GetUi("UI_TAB_SYNERGY", "시너지");
        if (ticketTabLabel != null) ticketTabLabel.text = LocalizationManager.GetUi("UI_TAB_TICKET", "티켓");
    }

    // 런 중이면 현재 바이옴, 아니면 전체
    private static CollectionBiomeFilter GetDefaultBiomeFilter()
    {
        DiceManager diceManager = DiceManager.Instance;
        if (diceManager != null && diceManager.currentBiome != null)
            return (CollectionBiomeFilter)(int)diceManager.currentBiome.biomeType;

        return CollectionBiomeFilter.All;
    }

    private static int GetFigureState(FigureItemSO figure)
    {
        return CollectionDataManager.Instance != null ? CollectionDataManager.Instance.GetFigureState(figure) : 0;
    }

    private void UpdateFilterText()
    {
        if (biomeNameText != null)
            biomeNameText.text = GetCurrentFilterDisplayName();

        if (currentFilterText == null) return;
        currentFilterText.text = LocalizationManager.GetUi("UI_BIOME_FILTER", "바이옴 : {0}", GetCurrentFilterDisplayName());
    }

    private string GetCurrentFilterDisplayName()
    {
        return GetFilterDisplayName((int)currentBiomeFilter);
    }

    private static string GetFilterDisplayName(int filterValue)
    {
        if (filterValue == (int)CollectionBiomeFilter.All)
            return LocalizationManager.GetUi("UI_FILTER_ALL", "전체");

        return LocalizationManager.GetBiomeDisplayName((BiomeType)filterValue);
    }
}
