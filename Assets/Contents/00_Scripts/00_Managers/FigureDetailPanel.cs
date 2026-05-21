using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class FigureDetailPanel : MonoBehaviour
{
    //다른 스크립트(주사위, 매니저 등)에서 패널 오픈 여부를 확인할 수 있는 정적 변수
    public static bool IsPanelOpen { get; private set; } = false;

    public GameObject panelRoot;
    public Image figureImage;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI descText;

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

    private void UpdateUI()
    {
        if (currentIndex < 0 || currentIndex >= currentOwnedFigures.Count) return;

        FigureItemSO currentFigure = currentOwnedFigures[currentIndex];

        if (figureImage != null) figureImage.sprite = currentFigure.icon;
        if (nameText != null) nameText.text = currentFigure.itemName;
        if (descText != null) descText.text = currentFigure.description;

        // 첫 번째거나 마지막 피규어면 화살표 비활성화
        leftButton.interactable = (currentIndex > 0);
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