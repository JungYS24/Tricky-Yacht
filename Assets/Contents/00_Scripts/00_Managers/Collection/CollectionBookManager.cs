using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using TMPro;

public enum CollectionBiomeFilter
{
    All = -1, Forest = 0, Meadow = 1, Temple = 2, Jungle = 3, Desert = 4,
    Ruins = 5, Cave = 6, Volcano = 7, Swamp = 8, Beach = 9, Ocean = 10,
    Abyss = 11, Snow = 12, Grave = 13, Circus = 14, Void = 15, Shop = 16
}
public enum CollectionStatusFilter { All, Unlocked, Locked }

public class CollectionBookManager : MonoBehaviour
{
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

    private List<GameObject> activeSlots = new List<GameObject>();
    private List<FigureItemSO> currentFilteredList = new List<FigureItemSO>();

    public TextMeshProUGUI currentFilterText;
    private void Start()
    {
        masterFigureDatabase = masterFigureDatabase.OrderBy(f => (int)f.sourceBiomes.FirstOrDefault()).ToList();

        GenerateFilterButtons(); // 시작할 때 필터 버튼 18개 자동 생성

        if (biomeFilterPanelRoot != null) biomeFilterPanelRoot.SetActive(false);
        RefreshCollectionBoard();
    }

    // 필터 버튼들을 생성해주는 함수
    private void GenerateFilterButtons()
    {
        if (filterGridParent == null || filterButtonPrefab == null) return;

        string[] filterNames = { "전체", "숲", "초원", "신전", "정글", "사막", "유적", "동굴", "화산", "늪", "해변", "바다", "심연", "설원", "무덤", "서커스", "공허", "상점" };
        int[] filterValues = { -1, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 };

        for (int i = 0; i < filterNames.Length; i++)
        {
            GameObject btnGo = Instantiate(filterButtonPrefab, filterGridParent);
            BiomeFilterSlot slot = btnGo.GetComponent<BiomeFilterSlot>();

            if (slot != null)
            {
                slot.Setup(filterNames[i], filterValues[i], this);
            }
        }
    }

    public void OpenCollectionBook()
    {
        collectionPanelRoot.SetActive(true);
        RefreshCollectionBoard();
    }

    public void CloseCollectionBook()
    {
        if (detailPanel != null) detailPanel.ClosePanel();
        if (biomeFilterPanelRoot != null) biomeFilterPanelRoot.SetActive(false);
        collectionPanelRoot.SetActive(false);
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
        RefreshCollectionBoard();
        CloseBiomeFilterPanel(); // 누르면 자동으로 닫힘
    }

    public void ChangeStatusFilter(int statusFilterIndex)
    {
        currentStatusFilter = (CollectionStatusFilter)statusFilterIndex;
        RefreshCollectionBoard();
    }

    public void RefreshCollectionBoard()
    {
        foreach (var slot in activeSlots) Destroy(slot);
        activeSlots.Clear();
        currentFilteredList.Clear();

        int totalCount = 0;
        int unlockedCount = 0;

        foreach (var figure in masterFigureDatabase)
        {
            if (currentBiomeFilter != CollectionBiomeFilter.All && !figure.sourceBiomes.Contains((BiomeType)currentBiomeFilter))
                continue;

            bool isUnlocked = PlayerPrefs.GetInt("Collection_Unlocked_" + figure.itemName, 0) == 1;
            bool isEncountered = PlayerPrefs.GetInt("Collection_Encountered_" + figure.itemName, 0) == 1;

            if (currentStatusFilter == CollectionStatusFilter.Unlocked && !isUnlocked) continue;
            if (currentStatusFilter == CollectionStatusFilter.Locked && isUnlocked) continue;

            GameObject slotGo = Instantiate(collectionSlotPrefab, gridContentParent);
            CollectionSlot slot = slotGo.GetComponent<CollectionSlot>();

            slot.Setup(figure, isUnlocked, isEncountered, this);

            activeSlots.Add(slotGo);
            currentFilteredList.Add(figure);

            totalCount++;
            if (isUnlocked) unlockedCount++;
        }

        if (progressText != null)
        {
            string biomeName = currentBiomeFilter == CollectionBiomeFilter.All ? "All" : currentBiomeFilter.ToString();
            progressText.text = $"{biomeName} Biome Figures Collected: {unlockedCount} / {totalCount}";
        }

        UpdateFilterText();
    }

    public void OpenFigureDetail(FigureItemSO figure)
    {
        if (detailPanel != null) detailPanel.OpenPanel(currentFilteredList, figure);
    }

    private void UpdateFilterText()
    {
        if (currentFilterText == null) return;

        string filterName = "";
        switch (currentBiomeFilter)
        {
            case CollectionBiomeFilter.All: filterName = "전체"; break;
            case CollectionBiomeFilter.Forest: filterName = "숲"; break;
            case CollectionBiomeFilter.Meadow: filterName = "초원"; break;
            case CollectionBiomeFilter.Temple: filterName = "신전"; break;
            case CollectionBiomeFilter.Jungle: filterName = "정글"; break;
            case CollectionBiomeFilter.Desert: filterName = "사막"; break;
            case CollectionBiomeFilter.Ruins: filterName = "유적"; break;
            case CollectionBiomeFilter.Cave: filterName = "동굴"; break;
            case CollectionBiomeFilter.Volcano: filterName = "화산"; break;
            case CollectionBiomeFilter.Swamp: filterName = "늪"; break;
            case CollectionBiomeFilter.Beach: filterName = "해변"; break;
            case CollectionBiomeFilter.Ocean: filterName = "바다"; break;
            case CollectionBiomeFilter.Abyss: filterName = "심연"; break;
            case CollectionBiomeFilter.Snow: filterName = "설원"; break;
            case CollectionBiomeFilter.Grave: filterName = "무덤"; break;
            case CollectionBiomeFilter.Circus: filterName = "서커스"; break;
            case CollectionBiomeFilter.Void: filterName = "공허"; break;
            case CollectionBiomeFilter.Shop: filterName = "상점"; break;
        }

        currentFilterText.text = $"바이옴 : {filterName}";
    }
}