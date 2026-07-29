using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;

public class FigureDetailPanel : MonoBehaviour
{
    //다른 스크립트(주사위, 매니저 등)에서 패널 오픈 여부를 확인할 수 있는 정적 변수
    public static bool IsPanelOpen { get; private set; } = false;

    public GameObject panelRoot;
    public Image figureImage;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI descText;

    [Header("도감용 UI")]
    public TextMeshProUGUI locationText;

    public Button leftButton;
    public Button rightButton;
    public Button closeButton;

    // 보유 중인 피규어 리스트와 현재 보고 있는 피규어 인덱스
    private List<FigureItemSO> currentOwnedFigures;
    private int currentIndex;

    private void Awake()
    {
        if (closeButton != null) closeButton.onClick.AddListener(ClosePanel);
        if (leftButton != null) leftButton.onClick.AddListener(ShowPrevious);
        if (rightButton != null) rightButton.onClick.AddListener(ShowNext);

        panelRoot.SetActive(false); // 시작 시 패널 숨기기
        IsPanelOpen = false;        // 상태 초기화
    }

    public void OpenPanel(List<FigureItemSO> ownedFigures, FigureItemSO selectedFigure)
    {
        if (ownedFigures == null || ownedFigures.Count == 0) return;

        currentOwnedFigures = ownedFigures;
        currentIndex = currentOwnedFigures.IndexOf(selectedFigure);

        IsPanelOpen = true; // 패널이 열렸음을 알림 (주사위/버튼 조작 차단 시작)
        UpdateUI();
        panelRoot.SetActive(true);
    }

    private string GetBiomeKoreanName(BiomeType biome)
    {
        switch (biome)
        {
            case BiomeType.Forest: return "숲";
            case BiomeType.Meadow: return "초원";
            case BiomeType.Temple: return "신전";
            case BiomeType.Jungle: return "정글";
            case BiomeType.Desert: return "사막";
            case BiomeType.Ruins: return "유적";
            case BiomeType.Cave: return "동굴";
            case BiomeType.Volcano: return "화산";
            case BiomeType.Swamp: return "늪";
            case BiomeType.Beach: return "해변";
            case BiomeType.Ocean: return "바다";
            case BiomeType.Abyss: return "심연";
            case BiomeType.Snow: return "설원";
            case BiomeType.Grave: return "무덤";
            case BiomeType.Circus: return "서커스";
            case BiomeType.Void: return "공허";
            case BiomeType.Shop: return "상점";
            default: return "알 수 없음";
        }
    }


    private void UpdateUI()
    {
        if (currentIndex < 0 || currentIndex >= currentOwnedFigures.Count) return;

        FigureItemSO currentFigure = currentOwnedFigures[currentIndex];
        bool isUnlocked = PlayerPrefs.GetInt("Collection_Unlocked_" + currentFigure.itemName, 0) == 1;
        bool isEncountered = PlayerPrefs.GetInt("Collection_Encountered_" + currentFigure.itemName, 0) == 1;

        string biomeNames = string.Join(", ", currentFigure.sourceBiomes.Select(b => GetBiomeKoreanName(b)));
        string biomeText = $"획득 바이옴 : {biomeNames}";

        if (isUnlocked)
        {
            if (figureImage != null)
            {
                figureImage.sprite = currentFigure.icon;
                figureImage.color = Color.white;
            }
            if (nameText != null)
            {
                nameText.text = currentFigure.itemName;
                nameText.color = Color.white;
            }
            if (descText != null)
            {
                descText.text = currentFigure.description;
                descText.color = Color.white;
            }
            if (locationText != null)
            {
                locationText.text = biomeText;
                locationText.color = Color.green;
            }
        }
        else if (isEncountered)
        {
            if (figureImage != null)
            {
                figureImage.sprite = currentFigure.icon;
                figureImage.color = Color.gray;
            }
            if (nameText != null)
            {
                nameText.text = currentFigure.itemName;
                nameText.color = Color.green;
            }
            if (descText != null)
            {
                descText.text = currentFigure.description;
                descText.color = Color.white;
            }
            if (locationText != null)
            {
                locationText.text = biomeText;
                locationText.color = Color.green;
            }
        }
        else
        {
            if (figureImage != null)
            {
                figureImage.sprite = currentFigure.icon;
                figureImage.color = new Color(0f, 0f, 0f, 0.7f);
            }
            if (nameText != null)
            {
                nameText.text = "???";
                nameText.color = new Color(0.4f, 0.4f, 0.4f, 1f);
            }
            if (descText != null)
            {
                descText.text = "???";
                descText.color = new Color(0.4f, 0.4f, 0.4f, 1f);
            }
            if (locationText != null)
            {
                locationText.text = biomeText;
                locationText.color = new Color(0.4f, 0.4f, 0.4f, 1f);
            }
        }

        // 첫 번째거나 마지막 피규어면 화살표 비활성화 (버튼이 있을 때만 실행)
        if (leftButton != null)
            leftButton.interactable = (currentIndex > 0);

        if (rightButton != null)
            rightButton.interactable = (currentIndex < currentOwnedFigures.Count - 1);
    }

    // 이전 피규어 보기
    private void ShowPrevious()
    {
        if (currentIndex > 0)
        {
            currentIndex--;
            UpdateUI();
        }
    }

    // 다음 피규어 보기
    private void ShowNext()
    {
        if (currentIndex < currentOwnedFigures.Count - 1)
        {
            currentIndex++;
            UpdateUI();
        }
    }

    public void ClosePanel()
    {
        IsPanelOpen = false; // 패널이 닫혔음을 알림 (조작 차단 해제)
        panelRoot.SetActive(false);
    }
}