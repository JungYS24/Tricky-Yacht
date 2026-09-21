using UnityEngine;
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

        GenerateFilterButtons(); // 시작할 때 필터 버튼 자동 생성

        if (biomeFilterPanelRoot != null) biomeFilterPanelRoot.SetActive(false);
        RefreshCollectionBoard();
    }

    // 필터 버튼들을 생성해주는 함수
    private void GenerateFilterButtons()
    {
        if (filterGridParent == null || filterButtonPrefab == null) return;

        string[] filterNames =
        {
            LocalizationManager.GetUi("UI_FILTER_ALL", "전체"),
            LocalizationManager.GetBiomeDisplayName(BiomeType.Special),
            LocalizationManager.GetBiomeDisplayName(BiomeType.Forest),
            LocalizationManager.GetBiomeDisplayName(BiomeType.Meadow),
            LocalizationManager.GetBiomeDisplayName(BiomeType.Temple),
            LocalizationManager.GetBiomeDisplayName(BiomeType.Jungle),
            LocalizationManager.GetBiomeDisplayName(BiomeType.Desert),
            LocalizationManager.GetBiomeDisplayName(BiomeType.Ruins),
            LocalizationManager.GetBiomeDisplayName(BiomeType.Cave),
            LocalizationManager.GetBiomeDisplayName(BiomeType.Volcano),
            LocalizationManager.GetBiomeDisplayName(BiomeType.Swamp),
            LocalizationManager.GetBiomeDisplayName(BiomeType.Beach),
            LocalizationManager.GetBiomeDisplayName(BiomeType.Ocean),
            LocalizationManager.GetBiomeDisplayName(BiomeType.Abyss),
            LocalizationManager.GetBiomeDisplayName(BiomeType.Snow),
            LocalizationManager.GetBiomeDisplayName(BiomeType.Grave),
            LocalizationManager.GetBiomeDisplayName(BiomeType.Circus),
            LocalizationManager.GetBiomeDisplayName(BiomeType.Void),
            LocalizationManager.GetBiomeDisplayName(BiomeType.Skyisland)
        };
        int[] filterValues = { -1, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17 };

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

            //하드디스크 대신 매니저의 딕셔너리에서 상태(0, 1, 2)를 한 번에 가져옴
            int figureState = CollectionDataManager.Instance.GetFigureState(figure);
            bool isUnlocked = (figureState == 2);         // 2번이면 완전 해금
            bool isEncountered = (figureState >= 1);      // 1번 이상(1, 2)이면 마주친 적 있음

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
            progressText.text = LocalizationManager.GetUi(
                "UI_COLLECTION_PROGRESS",
                "{0} : {1} / {2}",
                GetCurrentFilterDisplayName(),
                unlockedCount,
                totalCount);
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
        currentFilterText.text = LocalizationManager.GetUi("UI_BIOME_FILTER", "바이옴 : {0}", GetCurrentFilterDisplayName());
    }

    private string GetCurrentFilterDisplayName()
    {
        if (currentBiomeFilter == CollectionBiomeFilter.All)
            return LocalizationManager.GetUi("UI_FILTER_ALL", "전체");

        return LocalizationManager.GetBiomeDisplayName((BiomeType)(int)currentBiomeFilter);
    }
}