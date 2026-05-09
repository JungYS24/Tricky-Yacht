using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance;

    public bool isTutorialActive = true;
    public int currentStepIndex = 1;

    [Header("UI 연결")]
    public GameObject tutorialRoot;
    public GameObject darkOverlay;
    public TextMeshProUGUI dialogText;
    public Button nextButton;

    [Header("매니저 참조")]
    public DiceManager diceManager;
    public ShopManager shopManager;
    public UIManager uiManager;

    // 상점 및 아이템 체크용 변수
    private bool boughtHighRoller = false;
    private bool boughtFigure = false;
    private bool usedPeppermint = false;
    private bool usedGarnish = false;

    // ... 기존 변수들 아래에 ...

    [Header("튜토리얼 강제 아이템 설정")]
    public BaseItemDataSO tutorialHighRollerDice;
    public BaseItemDataSO tutorialFigure;
    public BaseItemDataSO tutorialPeppermint; // 이 이름이 ShopManager 101행과 연결됨
    public BaseItemDataSO tutorialGarnish;    // 이 이름이 ShopManager 103행과 연결됨

    [Header("첫 번째 상점 강제 아이템 5종")]
    public BaseItemDataSO tutFigure;   // ShopManager 89행
    public BaseItemDataSO tutSnack;    // ShopManager 90행
    public BaseItemDataSO tutCoating;  // ShopManager 91행
    public BaseItemDataSO tutDice;     // ShopManager 92행
    public BaseItemDataSO tutTicket;   // ShopManager 93행

    private Canvas activeCanvas;
    private GraphicRaycaster activeRaycaster;

    private void Awake() { Instance = this; }

    private void Start()
    {
        if (isTutorialActive)
        {
            // 이벤트 연결
            nextButton.onClick.AddListener(ProceedTutorial);
            uiManager.rollButton.onClick.AddListener(OnRollClicked);
            uiManager.finishButton.onClick.AddListener(OnFinishClicked);
            Dice.OnDiceStateChanged += OnDiceKept;

            ShowStep(1);
        }
    }

    private void ShowStep(int step)
    {
        currentStepIndex = step;
        string key = "Tut_" + step.ToString("D2");
        dialogText.text = DialogueManager.Instance.GetText(key);

        nextButton.gameObject.SetActive(true);
        darkOverlay.SetActive(true);
        ClearHighlight();

        // [시나리오 핵심 로직] 단계별 UI 제어
        switch (step)
        {
            case 2:
            case 4:
            case 7: // 굴리기 버튼 클릭 대기
                nextButton.gameObject.SetActive(false);
                HighlightUI(uiManager.rollButton.gameObject);
                break;

            case 3: // 1, 2번 주사위 킵 대기
                nextButton.gameObject.SetActive(false);
                break;

            case 6: // 끝내기 버튼 클릭 대기
                nextButton.gameObject.SetActive(false);
                HighlightUI(uiManager.finishButton.gameObject);
                break;

            case 9: // 상점 진입 버튼 대기
                nextButton.gameObject.SetActive(false);
                HighlightUI(uiManager.goShopButton.gameObject);
                break;

            case 16: // 하이롤러, 피규어 구매 대기
                nextButton.gameObject.SetActive(false);
                darkOverlay.SetActive(false); // 상점 이용을 위해 오버레이 끔
                break;

            case 17: // 다음 스테이지 버튼 대기
                nextButton.gameObject.SetActive(false);
                HighlightUI(shopManager.nextStageButton.gameObject);
                break;

            case 21: // 페퍼민트, 가니쉬 사용 대기
                nextButton.gameObject.SetActive(false);
                darkOverlay.SetActive(false);
                break;
        }
    }

    // "다음" 버튼 클릭 시 (대사만 있는 단계에서만 작동)
    public void ProceedTutorial()
    {
        // 특정 행동이 필요한 단계라면 '다음' 버튼으로 못 넘어감
        if (IsWaitStep(currentStepIndex)) return;

        if (currentStepIndex >= 22) { FinishTutorial(); return; }
        ShowStep(currentStepIndex + 1);
    }

    private bool IsWaitStep(int step) => (step == 2 || step == 3 || step == 4 || step == 6 || step == 7 || step == 9 || step == 16 || step == 17 || step == 21);

    // 1. 굴리기 클릭 시 처리
    private void OnRollClicked()
    {
        if (currentStepIndex == 2) ShowStep(3);
        else if (currentStepIndex == 4) ShowStep(5);
        else if (currentStepIndex == 7) ShowStep(8); // 무조건 야추 나오는 단계
    }

    // 2. 주사위 킵 처리
    private void OnDiceKept()
    {
        int keptCount = 0;
        bool dice1 = diceManager.activeDiceList[0].isKept;
        bool dice2 = diceManager.activeDiceList[1].isKept;

        foreach (var d in diceManager.activeDiceList) if (d.isKept) keptCount++;

        if (currentStepIndex == 3 && dice1 && dice2) ShowStep(4);
    }

    // 3. 끝내기 클릭 처리
    private void OnFinishClicked()
    {
        if (currentStepIndex == 6) ShowStep(7);
    }

    // 4. 상점 구매 체크 (ShopManager에서 구매 시 호출해줘야 함)
    public void OnItemBought(string itemName)
    {
        if (currentStepIndex != 16) return;
        if (itemName == "HighRoller") boughtHighRoller = true;
        if (itemName == "Figure") boughtFigure = true;

        if (boughtHighRoller && boughtFigure) ShowStep(17);
    }

    // 5. 다음 스테이지 버튼 클릭 처리
    private void OnNextStageClicked()
    {
        if (currentStepIndex == 17) ShowStep(18);
    }

    // 6. 아이템 사용 체크 (인벤토리에서 아이템 사용 시 호출)
    public void OnItemUsed(string itemName)
    {
        if (currentStepIndex != 21) return;
        if (itemName == "Peppermint") usedPeppermint = true;
        if (itemName == "Garnish") usedGarnish = true;

        if (usedPeppermint && usedGarnish) ShowStep(22);
    }

    // [중요] 주사위 눈금 강제 조정
    public int GetForcedDiceValue(int diceIndex)
    {
        if (!isTutorialActive) return -1;

        // 7~9단계: 무조건 야추 (5, 5, 5, 5, 5)
        if (currentStepIndex >= 7 && currentStepIndex <= 9) return 5;
        // 18단계 이후: 하이롤러 효과로 야추 (6, 6, 6, 6, 6)
        if (currentStepIndex >= 18) return 6;

        return -1;
    }

    // [중요] 주사위 클릭 제한
    public bool IsDiceClickable(Dice targetDice)
    {
        if (!isTutorialActive) return true;
        int idx = diceManager.activeDiceList.IndexOf(targetDice);
        if (currentStepIndex == 3) return (idx == 0 || idx == 1); // 1, 2번만 클릭 가능
        return true;
    }

    private void HighlightUI(GameObject target)
    {
        activeCanvas = target.AddComponent<Canvas>();
        activeCanvas.overrideSorting = true;
        activeCanvas.sortingOrder = 100;
        activeRaycaster = target.AddComponent<GraphicRaycaster>();
    }

    private void ClearHighlight()
    {
        if (activeRaycaster != null) Destroy(activeRaycaster);
        if (activeCanvas != null) Destroy(activeCanvas);
    }

    private void FinishTutorial()
    {
        isTutorialActive = false;
        tutorialRoot.SetActive(false);
    }
}