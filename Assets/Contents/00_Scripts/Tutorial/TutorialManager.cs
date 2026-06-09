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

    [Header("새로운 패널 참조")]
    public LootSelectionPanel lootSelectionPanel;
    public CoatingSelectionPanel coatingSelectionPanel;
    public BiomeSelectionPanel biomeSelectionPanel;

    // 상점 및 아이템 체크용 변수
    private bool boughtHighRoller = false;
    private bool boughtFigure = false;
    private bool boughtPeppermint = false;
    private bool boughtGarnish = false;
    private bool boughtHeartDice = false; 
    private bool boughtCoating = false; 
    private bool usedPeppermint = false;
    private bool usedGarnish = false;
    private bool hasTransitionedTo22 = false;

    [Header("튜토리얼 강제 아이템 설정")]
    public BaseItemDataSO tutorialHighRollerDice;
    public BaseItemDataSO tutorialFigure;
    public BaseItemDataSO tutorialPeppermint;
    public BaseItemDataSO tutorialGarnish;
    public BaseItemDataSO tutorialHeartDice;
    public BaseItemDataSO tutorialCoating;

    [Header("튜토리얼 강제 몬스터 설정")]
    public MonsterDataSO tutorialMonster1;
    public MonsterDataSO tutorialMonster2;
    public MonsterDataSO tutorialBossMonster;

    [Header("첫 번째 상점 강제 아이템 6종")]
    public BaseItemDataSO tutFigure;
    public BaseItemDataSO tutSnack;
    public BaseItemDataSO tutCoating;
    public BaseItemDataSO tutDice;
    public BaseItemDataSO tutTicket;
    public BaseItemDataSO tutDummy;

    private List<GameObject> highlightedObjects = new List<GameObject>();

    // 주사위 하이라이트 관리용 변수
    private List<Dice> highlightedDice = new List<Dice>();
    private Dictionary<Dice, int> originalDiceSortingOrders = new Dictionary<Dice, int>();

    private void Awake() { Instance = this; }

    private void Start()
    {
        //에디터에서 테스트 중이거나, 튜토리얼 씬이라면 강제로 켜줍니다.
#if UNITY_EDITOR
        isTutorialActive = true;
#else
        int shouldRun = PlayerPrefs.GetInt("RunTutorial", 0);
        isTutorialActive = (shouldRun == 1);
#endif

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
        else
        {
            FinishTutorial();
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

        // 신규 대사 시트 인덱스 기준으로 하이라이트 분기 수정
        if (step >= 13 && step <= 17)
        {
            SetShopSlotsInteractable(false);
            if (step == 13) HighlightUI(shopManager.shopSlots[0].gameObject);      // 피규어
            else if (step == 14) HighlightUI(shopManager.shopSlots[1].gameObject); // 스낵
            else if (step == 15) HighlightUI(shopManager.shopSlots[2].gameObject); // 코팅
            else if (step == 16) HighlightUI(shopManager.shopSlots[4].gameObject); // 티켓
            else if (step == 17) HighlightUI(shopManager.shopSlots[3].gameObject); // 주사위 구매
        }
        else if (step == 18)
        {
            SetShopSlotsInteractable(false);
            // 5라는 고정 숫자 대신 상점 슬롯의 전체 개수만큼 반복하도록 변경!
            for (int i = 0; i < shopManager.shopSlots.Length; i++)
            {
                HighlightUI(shopManager.shopSlots[i].gameObject);
            }
        }
        else if (step == 33)
        {
            nextButton.gameObject.SetActive(false); // 대사 넘기기 버튼 숨김

            List<BiomeType> tutorialBiomes = new List<BiomeType> { BiomeType.Meadow, BiomeType.Jungle, BiomeType.Cave };
            if (biomeSelectionPanel != null)
            {
                biomeSelectionPanel.OpenPanel(diceManager, tutorialBiomes);

                // 바이옴 슬롯들은 누르지 못하게 비활성화
                foreach (var slot in biomeSelectionPanel.choiceSlots)
                {
                    slot.selectButton.interactable = false;
                }

                // 새로 만든 메인 메뉴 버튼을 하이라이트하고, 누르면 튜토리얼이 종료되게 연결!
                if (biomeSelectionPanel.mainMenuButton != null)
                {

                    biomeSelectionPanel.mainMenuButton.gameObject.SetActive(true);

                    HighlightUI(biomeSelectionPanel.mainMenuButton.gameObject);
                    biomeSelectionPanel.mainMenuButton.onClick.RemoveAllListeners();
                    biomeSelectionPanel.mainMenuButton.onClick.AddListener(() =>
                    {
                        FinishTutorial();
                        diceManager.GoToMainMenu(); // 로비로 이동
                    });
                }
            }
        }
        }

    

    //피규어 팝업이 뜨고 닫기 버튼을 누를 때까지 이벤트를 감시하는 코루틴
    private IEnumerator WaitForFigurePopupClose()
    {
        nextButton.gameObject.SetActive(false);

        // 피규어 상세 패널이 열릴 때까지 대기
        while (!FigureDetailPanel.IsPanelOpen) yield return null;

        // 팝업이 열리면 슬롯의 하이라이트는 끄고, 피규어 팝업창 자체를 가림막 위로 끌어올림!
        ClearHighlight();
        if (InventoryManager.Instance != null && InventoryManager.Instance.figureDetailPanel != null)
        {
            HighlightUI(InventoryManager.Instance.figureDetailPanel.panelRoot);
        }

        // 피규어 상세 패널이 닫힐 때까지 대기
        while (FigureDetailPanel.IsPanelOpen) yield return null;

        ShowStep(22);
    }

    //주사위를 가림막 위로 튀어나오게 하는 함수
    private void HighlightDice(Dice dice)
    {
        if (dice == null) return;

        SpriteRenderer sr = dice.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            // 원래 sortingOrder를 안전하게 저장
            if (!originalDiceSortingOrders.ContainsKey(dice))
            {
                originalDiceSortingOrders[dice] = sr.sortingOrder;
            }
            // 가림막 위로 렌더링되도록 숫자를 대폭 올림!
            sr.sortingOrder = 20000;
        }

        if (!highlightedDice.Contains(dice))
        {
            highlightedDice.Add(dice);
        }
    }

    public void ProceedTutorial()
    {
        if (NeedsActionToProceed(currentStepIndex))
        {
            StartWaitAction(currentStepIndex);
            return;
        }

        // 보스 스테이지 클리어 후 자유 모드 진입 조건 (신규 인덱스 기준 반영)
        if (currentStepIndex == 28)
        {
            ShowStep(29);
            return;
        }
        if (currentStepIndex == 29)
        {
            ShowStep(30);
            return;
        }
        if (currentStepIndex == 31)
        {
            StartFreePlay();
            return;
        }

        if (currentStepIndex >= 33) { FinishTutorial(); return; }

        ShowStep(currentStepIndex + 1);
    }

    private bool NeedsActionToProceed(int step)
    {
        // 신규 추가된 기믹들의 액션 단계 예외 처리 추가
        return step == 2 || step == 3 || step == 4 || step == 6 ||
               step == 7 || step == 8 || step == 9 || step == 11 ||
               step == 12 || step == 21 ||
               step == 19 || step == 20 || step == 22 ||
               step == 23 || step == 24 || step == 27 || step == 30;
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
                SetDialogPanelVisible(false);
                darkOverlay.SetActive(true); // 배경은 어둡게 유지
                if (diceManager.activeDiceList.Count >= 2)
                {
                    // 첫 번째, 두 번째 주사위만 빛나게!
                    HighlightDice(diceManager.activeDiceList[0]);
                    HighlightDice(diceManager.activeDiceList[1]);
                }
                break;
            case 8:
                SetDialogPanelVisible(false);
                darkOverlay.SetActive(true); // 배경은 어둡게 유지
                foreach (var d in diceManager.activeDiceList)
                {
                    if (d != null && !d.isKept) HighlightDice(d);
                }
                break;
            case 6:
                SetDialogPanelVisible(false);
                darkOverlay.SetActive(true); // 가림막 유지
                HighlightUI(uiManager.finishButton.gameObject);
                // 아직 위로 안 올라간 주사위들도 같이 빛나게!
                foreach (var d in diceManager.activeDiceList)
                {
                    if (d != null && !d.isKept) HighlightDice(d);
                }
                break;

            case 9:
                SetDialogPanelVisible(false);
                darkOverlay.SetActive(true);
                HighlightUI(uiManager.finishButton.gameObject);
                break;

            //전리품 선택창 등장 및 첫 번째 슬롯 강요
            case 11:
                SetDialogPanelVisible(false);
                darkOverlay.SetActive(true);
                if (lootSelectionPanel != null && lootSelectionPanel.choiceSlots.Length > 0)
                {
                    HighlightUI(lootSelectionPanel.choiceSlots[0].gameObject);
                }
                break;

            // [추가 기믹] 전리품 획득 후 상점 이동 유도
            case 12:
                SetDialogPanelVisible(false);
                darkOverlay.SetActive(true);
                if (uiManager.goShopButton != null) HighlightUI(uiManager.goShopButton.gameObject);
                break;

            case 19:
                SetDialogPanelVisible(false);
                darkOverlay.SetActive(true);
                HighlightUI(shopManager.shopSlots[0].gameObject);
                HighlightUI(shopManager.shopSlots[3].gameObject);
                EnableSpecificShopSlots(0, 3);
                break;
            case 20:
                SetDialogPanelVisible(false);
                darkOverlay.SetActive(true);
                HighlightUI(shopManager.nextStageButton.gameObject);
                break;
            case 21:
                SetDialogPanelVisible(false);
                darkOverlay.SetActive(true);
                if (InventoryManager.Instance != null && InventoryManager.Instance.figureSlotParent != null && InventoryManager.Instance.figureSlotParent.childCount > 0)
                {
                    HighlightUI(InventoryManager.Instance.figureSlotParent.GetChild(0).gameObject);
                }

                //피규어 상세 팝업을 열고 닫을 때까지 기다리는 코루틴 실행
                StartCoroutine(WaitForFigurePopupClose());
                break;


            case 22:
                SetDialogPanelVisible(false);
                darkOverlay.SetActive(false);
                HighlightUI(uiManager.finishButton.gameObject);
                break;

            //두 번째 상점: 4개 아이템 동시 활성화 및 코팅 마지막 구매 강요
            case 23:
                SetDialogPanelVisible(false);
                if (shopManager != null) shopManager.RefreshShop(false);

                UpdateShop2PurchaseState();
                break;

            // 주사위 코팅 선택창 연출 및 다음 스테이지 버튼 유도
            case 24:
                SetDialogPanelVisible(false);
                darkOverlay.SetActive(true);
                if (coatingSelectionPanel != null && coatingSelectionPanel.panelRoot.activeSelf)
                {
                    HighlightUI(coatingSelectionPanel.panelRoot);
                }
                else
                {
                    HighlightUI(shopManager.nextStageButton.gameObject);
                }
                break;

            //  보스전 주사위 고정 결과 후 끝내기 유도
            case 27:
                SetDialogPanelVisible(false);
                darkOverlay.SetActive(true);
                foreach (var d in diceManager.activeDiceList)
                {
                    if (d != null && !d.isKept) HighlightDice(d);
                }
                break;

            case 30:
                SetDialogPanelVisible(false);
                darkOverlay.SetActive(true);
                if (InventoryManager.Instance != null && InventoryManager.Instance.snackSlots.Length >= 4)
                {
                    HighlightUI(InventoryManager.Instance.snackSlots[2].gameObject);
                    HighlightUI(InventoryManager.Instance.snackSlots[3].gameObject);
                }
                break;
            case 32:
                SetDialogPanelVisible(false);
                darkOverlay.SetActive(true);
                if (lootSelectionPanel != null && lootSelectionPanel.choiceSlots.Length > 0)
                {
                    HighlightUI(lootSelectionPanel.choiceSlots[0].gameObject);
                }
                break;


            //바이옴 선택 패널 활성화
            case 33:
                SetDialogPanelVisible(false);
                darkOverlay.SetActive(true);
                if (biomeSelectionPanel != null)
                {
                    foreach (var slot in biomeSelectionPanel.choiceSlots)
                    {
                        if (slot.gameObject.activeSelf) HighlightUI(slot.gameObject);
                    }
                }
                break;
        }
    }

    // 두 번째 상점에서 구매 상태에 따라 버튼의 Interactable과 하이라이트를 실시간 제어하는 함수
    private void UpdateShop2PurchaseState()
    {
        SetShopSlotsInteractable(false);
        ClearHighlight();
        darkOverlay.SetActive(true);

        // 페퍼민트 -> 가니쉬 -> 하트 주사위 -> 코팅 순서로 하나씩 불이 들어오며 구매를 강제합니다.
        if (!boughtPeppermint)
        {
            HighlightUI(shopManager.shopSlots[0].gameObject);
            shopManager.shopSlots[0].buyButton.interactable = true;
        }
        else if (!boughtGarnish)
        {
            HighlightUI(shopManager.shopSlots[1].gameObject);
            shopManager.shopSlots[1].buyButton.interactable = true;
        }
        else if (!boughtHeartDice)
        {
            HighlightUI(shopManager.shopSlots[2].gameObject); // 세 번째 칸 (하트 주사위)
            shopManager.shopSlots[2].buyButton.interactable = true;
        }
        else if (!boughtCoating)
        {
            HighlightUI(shopManager.shopSlots[3].gameObject); // 네 번째 칸 (코팅)
            shopManager.shopSlots[3].buyButton.interactable = true;
        }
    }

    private void StartFreePlay()
    {
        // 1. 가장 중요: 튜토리얼 루트 자체를 꺼야 화면을 가로막는 투명 레이어가 완전히 사라집니다.
        tutorialRoot.SetActive(false);

        // 2. 가림막과 대사창 상태도 확실히 정리 (나중에 ShowStep에서 다시 켤 때를 대비)
        SetDialogPanelVisible(false);
        darkOverlay.SetActive(false);

        // 3. 게임 플레이를 위해 버튼들을 다시 활성화
        // 주의: DiceManager의 HandleDiceChanged가 실행되면 게임 로직에 따라 다시 꺼질 수 있습니다.
        uiManager.rollButton.interactable = true;
        uiManager.finishButton.interactable = true;

        // 4. 이번 전투 무조건 100% 박제(포획)를 위한 강제 보정
        if (diceManager != null)
        {
            diceManager.snackBonusFigureDropRate += 1.0f;
            // 버튼 상태를 강제로 갱신하도록 호출 (버튼이 안 눌리는 현상 방지)
            diceManager.ForceUpdateUI();
        }

        // 5. 유저가 보스 몬스터를 다 잡을 때까지 기다리는 코루틴 실행
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

        // 32번 대사 띄우기 (적 박제 완료 대사 안내)
        ShowStep(32);
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
        else if (currentStepIndex == 27) // [추가] 27번 스텝에서 주사위 5개를 다 킵하면 끝내기 버튼 활성화!
        {
            int keptCount = 0;
            foreach (var d in diceManager.activeDiceList) if (d != null && d.isKept) keptCount++;

            if (keptCount >= 5)
            {
                HighlightUI(uiManager.finishButton.gameObject);
            }
        }
    }
    private void OnFinishClicked()
    {
        if (!isTutorialActive) return;

        if (currentStepIndex == 6) ShowStep(7);
        else if (currentStepIndex == 9)
        {
            ClearHighlight();
            // 강제로 죽이는 코드를 삭제하고, 시스템이 창을 열어줄 때까지 기다리는 코루틴 실행
            StartCoroutine(WaitForFirstLootPanel());
        }
        else if (currentStepIndex == 22)
        {
            ClearHighlight();          
            StartCoroutine(WaitForSecondLootPanel());
        }
        else if (currentStepIndex == 27)
        {
            ClearHighlight();
            ShowStep(28); // 보스 반격 이벤트 대사로 연결
        }
    }

    //9번 스텝 종료 후 몬스터 사망 및 전리품 UI 등장 코루틴
    private IEnumerator WaitForFirstLootPanel()
    {
        while (!LootSelectionPanel.IsPanelOpen) yield return null;

        ShowStep(10); 
    }

    //전리품 선택 완료 시 호출될 훅 함수 (LootSelectionPanel이나 LootChoiceSlot에서 연동)
    public void OnLootSelectedComplete()
    {
        if (!isTutorialActive) return;

        if (currentStepIndex == 11)
        {
            ShowStep(12);
        }
        else if (currentStepIndex == 22)
        {
            ClearHighlight();
            darkOverlay.SetActive(true);
            // 수정한 코루틴 이름(HighlightButton)으로 적용
            StartCoroutine(HighlightButton(uiManager.goShopButton));
        }
        else if (currentStepIndex == 32) // [추가] 보스 처치 전리품을 얻었을 때
        {
            ClearHighlight();
            darkOverlay.SetActive(true);
            // 상점이 아니라 다음 스테이지 버튼을 강제 유도
            StartCoroutine(HighlightButton(uiManager.nextStageButton));
        }
    }

    private IEnumerator WaitForSecondLootPanel()
    {
        // 전리품 패널이 열릴 때까지 대기
        while (!LootSelectionPanel.IsPanelOpen) yield return null;

        darkOverlay.SetActive(true);

        // 첫 번째 전리품 슬롯을 가림막 위로 올려서 강제 하이라이트
        if (lootSelectionPanel != null && lootSelectionPanel.choiceSlots.Length > 0)
        {
            HighlightUI(lootSelectionPanel.choiceSlots[0].gameObject);
        }
    }

    //전리품 선택 후 상점가기, 다음스테이지 등 범용적으로 버튼을 하이라이트하는 코루틴
    private IEnumerator HighlightButton(Button btn)
    {
        yield return new WaitForSeconds(0.1f);
        if (btn != null && btn.gameObject.activeInHierarchy)
        {
            HighlightUI(btn.gameObject);
        }
    }

    private void OnGoShopClicked()
    {
        if (!isTutorialActive) return;

        ClearHighlight();
        if (uiManager.nextStageButton != null) uiManager.nextStageButton.gameObject.SetActive(true);

        if (currentStepIndex == 12) ShowStep(13);

        else if (currentStepIndex == 22) ShowStep(23); //상점에 입장하면서 23번 대사 호출
    }

    public void OnItemBought(string itemName)
    {
        if (!isTutorialActive) return;

        if (currentStepIndex == 19)
        {
            if (itemName == tutorialHighRollerDice.itemName) boughtHighRoller = true;
            if (itemName == tutorialFigure.itemName) boughtFigure = true;
            if (boughtHighRoller && boughtFigure) ShowStep(20);
        }
        else if (currentStepIndex == 23)
        {
            if (itemName == tutorialPeppermint.itemName) boughtPeppermint = true;
            else if (itemName == tutorialGarnish.itemName) boughtGarnish = true;
            else if (tutorialHeartDice != null && itemName == tutorialHeartDice.itemName) boughtHeartDice = true;
            else if (tutorialCoating != null && itemName == tutorialCoating.itemName) boughtCoating = true;

            // 실시간 상태 업데이트 적용 (차례대로 다음 아이템 활성화)
            UpdateShop2PurchaseState();

            // 4개를 모두 순서대로 샀다면 24번으로!
            if (boughtPeppermint && boughtGarnish && boughtHeartDice && boughtCoating)
            {
                ShowStep(24);
            }
    }
    }

    private void OnNextStageClicked()
    {
        if (!isTutorialActive) return;

        if (currentStepIndex == 20) ShowStep(21);
        else if (currentStepIndex == 24) ShowStep(25);
        else if (currentStepIndex == 30)
        {
            ClearHighlight();
            darkOverlay.SetActive(true);
        }
        else if (currentStepIndex == 32) //32번 스텝에서 다음 스테이지를 눌렀을 때
        {
            ClearHighlight();

            // 바이옴 3개를 튜토리얼용으로 임의 지정하여 선택창을 강제로 열어줍니다.
            List<BiomeType> tutorialBiomes = new List<BiomeType> { BiomeType.Meadow, BiomeType.Jungle, BiomeType.Cave };
            if (biomeSelectionPanel != null)
            {
                biomeSelectionPanel.OpenPanel(diceManager, tutorialBiomes);
            }

            ShowStep(33); // 바이옴 선택 대사 출력
        }
    }

    //주사위 코팅 처리가 완전히 끝났을 때 연동되는 함수
    public void OnCoatingAppliedComplete()
    {
        if (!isTutorialActive || currentStepIndex != 24) return;
        ClearHighlight();
        HighlightUI(shopManager.nextStageButton.gameObject);
    }

    public void OnItemUsed(string itemName)
    {
        if (!isTutorialActive || currentStepIndex != 30) return;

        if (itemName == tutorialPeppermint.itemName) usedPeppermint = true;
        if (itemName == tutorialGarnish.itemName) usedGarnish = true;

        if (usedPeppermint && usedGarnish && !hasTransitionedTo22)
        {
            hasTransitionedTo22 = true;
            ClearHighlight(); // 슬롯 하이라이트 해제
            ShowStep(31);     // 자유 전투(31) 시작 안내로 변경
        }
    }

    public int GetForcedDiceValue(int diceIndex)
    {
        if (!isTutorialActive) return -1;

        //2개를 킵하고 남은 3개를 굴리는 시점 (보통 4~5단계)
        // 0, 1번 주사위는 킵되어 있으므로 2, 3, 4번 주사위의 결과값만 강제함
        if (currentStepIndex == 4 || currentStepIndex == 5)
        {
            if (diceIndex == 2) return 1;
            if (diceIndex == 3) return 2;
            if (diceIndex == 4) return 3;
        }

        // 이후 단계 (족보 설명 등을 위해 다시 높은 숫자가 필요할 때)
        if (currentStepIndex >= 7 && currentStepIndex <= 9) return 5;
        if (currentStepIndex >= 17 && currentStepIndex <= 22) return 6;

        // 27단계 보스 조우 씬 진입 시 주사위 강제 2, 2, 2, 4, 5 설계
        if (currentStepIndex == 27)
        {
            if (diceIndex == 0) return 2;
            if (diceIndex == 1) return 2;
            if (diceIndex == 2) return 2;
            if (diceIndex == 3) return 4;
            if (diceIndex == 4) return 5;
        }

        return -1;
    }

    public bool IsDiceClickable(Dice targetDice)
    {
        if (!isTutorialActive) return true;

        //가림막이 켜져있더라도, '현재 하이라이트 된 주사위'라면 예외적으로 클릭 허용!
        if (darkOverlay != null && darkOverlay.activeSelf)
        {
            if (highlightedDice.Contains(targetDice)) return true;
            return false;
        }

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
            canvas.sortingOrder = 100;

            var raycaster = target.GetComponent<GraphicRaycaster>();
            if (raycaster == null) raycaster = target.AddComponent<GraphicRaycaster>();

            highlightedObjects.Add(target);
        }
    }

    private void ClearHighlight()
    {
        // 1. UI 버튼 하이라이트 해제 (이 부분이 지워졌을 가능성이 높습니다!)
        foreach (var obj in highlightedObjects)
        {
            if (obj != null)
            {
                // 강제로 달아줬던 레이캐스터와 캔버스를 파괴해서 원래 자리로 돌려보냄
                var raycaster = obj.GetComponent<GraphicRaycaster>();
                if (raycaster != null) Destroy(raycaster);

                var canvas = obj.GetComponent<Canvas>();
                if (canvas != null) Destroy(canvas);
            }
        }
        highlightedObjects.Clear();

        // 2. 주사위 하이라이트 원상복구
        foreach (var d in highlightedDice)
        {
            if (d != null)
            {
                SpriteRenderer sr = d.GetComponent<SpriteRenderer>();
                if (sr != null && originalDiceSortingOrders.ContainsKey(d))
                {
                    sr.sortingOrder = originalDiceSortingOrders[d]; // 원래 레이어로 복구
                }
            }
        }
        highlightedDice.Clear();
        originalDiceSortingOrders.Clear();
    }

    private void FinishTutorial()
    {
        isTutorialActive = false;
        tutorialRoot.SetActive(false);
        darkOverlay.SetActive(false);
        ClearHighlight();
        Debug.Log("튜토리얼 종료! 이제 자유롭게 플레이하세요.");
    }

    // 현재 스테이지(라운드) 번호를 받아서 그에 맞는 튜토리얼용 몬스터를 반환합니다.
    public MonsterDataSO GetTutorialMonster(int stageIndex)
    {
        if (!isTutorialActive) return null;

        if (stageIndex == 1) return tutorialMonster1;
        if (stageIndex == 2) return tutorialMonster2;
        if (stageIndex == 3) return tutorialBossMonster;

        return null;
    }
}