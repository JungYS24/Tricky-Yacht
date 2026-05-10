using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
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
    private bool boughtPeppermint = false;
    private bool boughtGarnish = false;
    private bool usedPeppermint = false;
    private bool usedGarnish = false;
    private bool hasTransitionedTo22 = false; // 중복 스킵 방지용

    [Header("튜토리얼 강제 아이템 설정")]
    public BaseItemDataSO tutorialHighRollerDice;
    public BaseItemDataSO tutorialFigure;
    public BaseItemDataSO tutorialPeppermint;
    public BaseItemDataSO tutorialGarnish;

    [Header("첫 번째 상점 강제 아이템 5종")]
    public BaseItemDataSO tutFigure;
    public BaseItemDataSO tutSnack;
    public BaseItemDataSO tutCoating;
    public BaseItemDataSO tutDice;
    public BaseItemDataSO tutTicket;

    private List<GameObject> highlightedObjects = new List<GameObject>();

    private void Awake() { Instance = this; }

    private void Start()
    {
        if (isTutorialActive)
        {
            nextButton.onClick.AddListener(ProceedTutorial);
            uiManager.rollButton.onClick.AddListener(OnRollClicked);
            uiManager.finishButton.onClick.AddListener(OnFinishClicked);
            uiManager.goShopButton.onClick.AddListener(OnGoShopClicked);
            shopManager.nextStageButton.onClick.AddListener(OnNextStageClicked);
            Dice.OnDiceStateChanged += OnDiceKept;

            FixTooltipSorting(shopManager.tooltipPanel);
            if (InventoryManager.Instance != null)
                FixTooltipSorting(InventoryManager.Instance.tooltipPanel);

            ShowStep(1);
        }
    }

    private void FixTooltipSorting(GameObject tooltip)
    {
        if (tooltip == null) return;
        Canvas c = tooltip.GetComponent<Canvas>();
        if (c == null) c = tooltip.AddComponent<Canvas>();
        c.overrideSorting = true;
        c.sortingOrder = 30000;

        GraphicRaycaster gr = tooltip.GetComponent<GraphicRaycaster>();
        if (gr != null) Destroy(gr);
    }

    private void ShowStep(int step)
    {
        currentStepIndex = step;
        string key = "Tut_" + step.ToString("D2");
        dialogText.text = DialogueManager.Instance.GetText(key);

        tutorialRoot.SetActive(true);
        SetDialogPanelVisible(true);
        nextButton.gameObject.SetActive(true);
        darkOverlay.SetActive(true);
        ClearHighlight();

        if (step >= 10 && step <= 14)
        {
            SetShopSlotsInteractable(false);
            if (step == 10) HighlightUI(shopManager.shopSlots[0].gameObject);
            else if (step == 11) HighlightUI(shopManager.shopSlots[1].gameObject);
            else if (step == 12) HighlightUI(shopManager.shopSlots[2].gameObject);
            else if (step == 13) HighlightUI(shopManager.shopSlots[4].gameObject);
            else if (step == 14) HighlightUI(shopManager.shopSlots[3].gameObject);
        }
        else if (step == 15)
        {
            SetShopSlotsInteractable(false);
            for (int i = 0; i < 5; i++) HighlightUI(shopManager.shopSlots[i].gameObject);
        }
        else if (step == 18)
        {
            StartCoroutine(ForceSpawnTutorialDice());
        }
    }

    private IEnumerator ForceSpawnTutorialDice()
    {
        yield return new WaitForSeconds(0.2f);

        if (tutorialHighRollerDice is DiceItemSO hrData && diceManager.activeDiceList.Count >= 5)
        {
            int highRollerCount = 0;

            for (int i = 0; i < 5; i++)
            {
                Dice d = diceManager.activeDiceList[i];
                if (d == null) continue;

                if (d.myData.diceName == hrData.itemName)
                {
                    highRollerCount++;
                    if (highRollerCount > 1) d.SetData(diceManager.masterDeck[0], 6);
                    else d.SetData(d.myData, 6);
                }
            }

            if (highRollerCount == 0)
            {
                Dice targetDice = diceManager.activeDiceList[0];
                DiceData1 hrDiceData = new DiceData1(hrData.itemName, hrData.customFaces);
                hrDiceData.customDiceShell = hrData.customDiceShell;
                hrDiceData.customFaceSprites = hrData.customFaceSprites;
                targetDice.SetData(hrDiceData, 6);
            }

            foreach (var d in diceManager.activeDiceList)
            {
                if (d != null) d.SetData(d.myData, 6);
            }

            diceManager.ForceUpdateUI();
        }
    }

    public void ProceedTutorial()
    {
        if (NeedsActionToProceed(currentStepIndex))
        {
            StartWaitAction(currentStepIndex);
            return;
        }

        // 22번 대사("자 이제 자유롭게...")를 읽고 '다음'을 누르면 자유 플레이 진입!
        if (currentStepIndex == 22)
        {
            StartFreePlay();
            return;
        }

        // 24번 대사까지 읽고 다음을 누르면 튜토리얼 종료
        if (currentStepIndex >= 24) { FinishTutorial(); return; }

        ShowStep(currentStepIndex + 1);
    }

    private bool NeedsActionToProceed(int step)
    {
        // 22, 23, 24번은 대사만 읽고 '다음'을 누르는 구조이므로 여기 포함시키지 않습니다.
        return step == 2 || step == 3 || step == 4 || step == 6 ||
               step == 7 || step == 8 || step == 9 || step == 16 ||
               step == 17 || step == 19 || step == 20 || step == 21;
    }

    private void StartWaitAction(int step)
    {
        nextButton.gameObject.SetActive(false);

        switch (step)
        {
            case 2:
            case 4:
            case 7:
                HighlightUI(uiManager.rollButton.gameObject);
                break;
            case 3:
            case 8:
                darkOverlay.SetActive(false);
                break;
            case 6:
            case 9:
            case 19:
                SetDialogPanelVisible(false);
                darkOverlay.SetActive(true);
                HighlightUI(uiManager.finishButton.gameObject);
                break;
            case 16:
                SetDialogPanelVisible(false);
                darkOverlay.SetActive(true);
                HighlightUI(shopManager.shopSlots[0].gameObject);
                HighlightUI(shopManager.shopSlots[3].gameObject);
                EnableSpecificShopSlots(0, 3);
                break;
            case 17:
                SetDialogPanelVisible(false);
                darkOverlay.SetActive(true);
                HighlightUI(shopManager.nextStageButton.gameObject);
                break;
            case 20:
                SetDialogPanelVisible(false);
                darkOverlay.SetActive(true);
                HighlightUI(shopManager.shopSlots[0].gameObject);
                HighlightUI(shopManager.shopSlots[1].gameObject);
                EnableSpecificShopSlots(0, 1);
                break;
            case 21:
                SetDialogPanelVisible(false);
                darkOverlay.SetActive(true);
                HighlightUI(shopManager.nextStageButton.gameObject);
                break;
        }
    }

    private void StartFreePlay()
    {
        // 가림막 및 대사창 완전히 해제
        SetDialogPanelVisible(false);
        darkOverlay.SetActive(false);
        uiManager.rollButton.interactable = true;
        uiManager.finishButton.interactable = true;

        // 이번 전투 무조건 100% 박제(포획)를 위한 강제 보정!
        if (diceManager != null) diceManager.snackBonusFigureDropRate += 1.0f;

        // 유저가 몬스터를 다 잡을 때까지 몰래 기다리는 코루틴 실행
        StartCoroutine(WaitForMonsterDefeat());
    }

    private IEnumerator WaitForMonsterDefeat()
    {
        // 몬스터가 죽을 때까지 대기
        while (diceManager.enemy == null || !diceManager.enemy.IsDead)
        {
            yield return null;
        }

        // 몬스터가 죽고, 박제 연출이 모두 끝날 때까지 여유롭게 대기 (3.5초)
        yield return new WaitForSeconds(3.5f);

        // 23번 대사 띄우기
        ShowStep(23);
    }

    private void SetDialogPanelVisible(bool isVisible)
    {
        if (dialogText != null && dialogText.transform.parent != null)
            dialogText.transform.parent.gameObject.SetActive(isVisible);
    }

    private void SetShopSlotsInteractable(bool state)
    {
        if (shopManager == null || shopManager.shopSlots == null) return;
        foreach (var slot in shopManager.shopSlots)
            if (slot != null && slot.buyButton != null) slot.buyButton.interactable = state;
    }

    private void EnableSpecificShopSlots(params int[] indices)
    {
        SetShopSlotsInteractable(false);
        foreach (int i in indices)
            if (i < shopManager.shopSlots.Length && shopManager.shopSlots[i] != null)
                shopManager.shopSlots[i].buyButton.interactable = true;
    }

    private void OnRollClicked()
    {
        if (!isTutorialActive) return;
        if (currentStepIndex == 2) ShowStep(3);
        else if (currentStepIndex == 4) ShowStep(5);
        else if (currentStepIndex == 7) ShowStep(8);
    }

    private void OnDiceKept()
    {
        if (!isTutorialActive) return;

        if (currentStepIndex == 3)
        {
            if (diceManager.activeDiceList[0].isKept && diceManager.activeDiceList[1].isKept) ShowStep(4);
        }
        else if (currentStepIndex == 8)
        {
            int keptCount = 0;
            foreach (var d in diceManager.activeDiceList) if (d != null && d.isKept) keptCount++;
            if (keptCount >= 5) ShowStep(9);
        }
    }

    private void OnFinishClicked()
    {
        if (!isTutorialActive) return;

        if (currentStepIndex == 6) ShowStep(7);
        else if (currentStepIndex == 9 || currentStepIndex == 19)
        {
            ClearHighlight();
            StartCoroutine(WaitAndHighlightShop());
        }
    }

    private IEnumerator WaitAndHighlightShop()
    {
        yield return new WaitForSeconds(3.0f);
        if (uiManager.nextStageButton != null) uiManager.nextStageButton.gameObject.SetActive(false);
        if (uiManager.goShopButton != null) HighlightUI(uiManager.goShopButton.gameObject);
    }

    private void OnGoShopClicked()
    {
        if (!isTutorialActive) return;

        ClearHighlight();
        if (uiManager.nextStageButton != null) uiManager.nextStageButton.gameObject.SetActive(true);

        if (currentStepIndex == 9) ShowStep(10);
        if (currentStepIndex == 19) ShowStep(20);
    }

    public void OnItemBought(string itemName)
    {
        if (!isTutorialActive) return;

        if (currentStepIndex == 16)
        {
            if (itemName == tutorialHighRollerDice.itemName) boughtHighRoller = true;
            if (itemName == tutorialFigure.itemName) boughtFigure = true;
            if (boughtHighRoller && boughtFigure) ShowStep(17);
        }
        else if (currentStepIndex == 20)
        {
            if (itemName == tutorialPeppermint.itemName) boughtPeppermint = true;
            if (itemName == tutorialGarnish.itemName) boughtGarnish = true;
            if (boughtPeppermint && boughtGarnish) ShowStep(21);
        }
    }

    private void OnNextStageClicked()
    {
        if (!isTutorialActive) return;

        if (currentStepIndex == 17) ShowStep(18);
        else if (currentStepIndex == 21)
        {
            ClearHighlight();
            darkOverlay.SetActive(true);

            // 인벤토리의 페퍼민트와 가니쉬 스낵 슬롯만 밝게 하이라이트!
            if (InventoryManager.Instance != null && InventoryManager.Instance.snackSlots.Length >= 2)
            {
                HighlightUI(InventoryManager.Instance.snackSlots[0].gameObject);
                HighlightUI(InventoryManager.Instance.snackSlots[1].gameObject);
            }
        }
    }

    public void OnItemUsed(string itemName)
    {
        if (!isTutorialActive || currentStepIndex != 21) return;

        if (itemName == tutorialPeppermint.itemName) usedPeppermint = true;
        if (itemName == tutorialGarnish.itemName) usedGarnish = true;

        if (usedPeppermint && usedGarnish && !hasTransitionedTo22)
        {
            hasTransitionedTo22 = true;
            ClearHighlight(); // 슬롯 하이라이트 해제
            ShowStep(22);     // 바로 22번 대사("자 이제 자유롭게...") 출력
        }
    }

    public int GetForcedDiceValue(int diceIndex)
    {
        if (!isTutorialActive) return -1;
        if (currentStepIndex >= 7 && currentStepIndex <= 9) return 5;
        if (currentStepIndex >= 17 && currentStepIndex <= 20) return 6;
        return -1;
    }

    public bool IsDiceClickable(Dice targetDice)
    {
        if (!isTutorialActive) return true;
        int idx = diceManager.activeDiceList.IndexOf(targetDice);
        if (currentStepIndex == 3) return (idx == 0 || idx == 1);
        return true;
    }

    private void HighlightUI(GameObject target)
    {
        if (target == null) return;
        if (!highlightedObjects.Contains(target))
        {
            var canvas = target.GetComponent<Canvas>();
            if (canvas == null) canvas = target.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 20000;

            var raycaster = target.GetComponent<GraphicRaycaster>();
            if (raycaster == null) raycaster = target.AddComponent<GraphicRaycaster>();

            highlightedObjects.Add(target);
        }
    }

    private void ClearHighlight()
    {
        foreach (var obj in highlightedObjects)
        {
            if (obj != null)
            {
                var raycaster = obj.GetComponent<GraphicRaycaster>();
                if (raycaster != null) Destroy(raycaster);

                var canvas = obj.GetComponent<Canvas>();
                if (canvas != null) Destroy(canvas);
            }
        }
        highlightedObjects.Clear();
    }

    private void FinishTutorial()
    {
        isTutorialActive = false;
        tutorialRoot.SetActive(false);
        darkOverlay.SetActive(false);
        ClearHighlight();
        Debug.Log("튜토리얼 종료! 이제 자유롭게 플레이하세요.");
    }
}