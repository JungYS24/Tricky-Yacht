using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using TMPro;

public enum CollectionBiomeFilter { All = -1, Forest = 0, Meadow = 1, Temple = 2, Jungle = 3, Desert = 4, Ruins = 5, Cave = 6, Volcano = 7, Swamp = 8, Beach = 9, Ocean = 10, Abyss = 11, Snow = 12, Grave = 13, Circus = 14, Void = 15 }
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

    [Header("진행도 텍스트")]
    public TextMeshProUGUI progressText;

    [Header("현재 필터 상태")]
    public CollectionBiomeFilter currentBiomeFilter = CollectionBiomeFilter.All;
    public CollectionStatusFilter currentStatusFilter = CollectionStatusFilter.All;

    private List<GameObject> activeSlots = new List<GameObject>();
    private List<FigureItemSO> currentFilteredList = new List<FigureItemSO>();

    private void Start()
    {
        // 숲(0)부터 공허(15)까지 바이옴 순서로 기본 정렬
        masterFigureDatabase = masterFigureDatabase.OrderBy(f => (int)f.sourceBiome).ToList();
        RefreshCollectionBoard();
    }

    public void OpenCollectionBook()
    {
        collectionPanelRoot.SetActive(true);
        RefreshCollectionBoard();
    }

    public void CloseCollectionBook()
    {
        collectionPanelRoot.SetActive(false);
    }

    // 드롭다운이나 탭 버튼에서 호출할 필터 변경 함수들
    public void ChangeBiomeFilter(int biomeFilterIndex)
    {
        currentBiomeFilter = (CollectionBiomeFilter)biomeFilterIndex;
        RefreshCollectionBoard();
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
            // 1. 바이옴 필터 검사
            if (currentBiomeFilter != CollectionBiomeFilter.All && figure.sourceBiome != (BiomeType)currentBiomeFilter)
                continue;

            // 2. 해금 상태 검사
            bool isUnlocked = PlayerPrefs.GetInt("Collection_Unlocked_" + figure.itemName, 0) == 1;

            if (currentStatusFilter == CollectionStatusFilter.Unlocked && !isUnlocked) continue;
            if (currentStatusFilter == CollectionStatusFilter.Locked && isUnlocked) continue;

            // 조건에 맞는 피규어 슬롯 생성
            GameObject slotGo = Instantiate(collectionSlotPrefab, gridContentParent);
            CollectionSlot slot = slotGo.GetComponent<CollectionSlot>();
            slot.Setup(figure, isUnlocked, this);
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
    }

    public void OpenFigureDetail(FigureItemSO figure)
    {
        if (detailPanel != null)
        {
            detailPanel.OpenPanel(currentFilteredList, figure);
        }
    }
}