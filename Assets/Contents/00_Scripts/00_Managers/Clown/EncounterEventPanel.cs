using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

// 조우자 종류를 구분하기 위한 Enum (데블 다이스 제외)
public enum EncounterType
{
    Clown, AbyssDealer, BlindFortuneTeller, Poacher,
    SacrificedGirl, Alchemist, WishWanderer, ForgottenExplorer,
    MadHatter, RustyCaptain
}

// 인스펙터에서 조우자 데이터를 쉽게 세팅하기 위한 클래스
[System.Serializable]
public class EncounterData
{
    public EncounterType type;
    public string encounterName;
    public List<BiomeType> appearBiomes; // 등장 가능한 바이옴 리스트

    [Header("조우자 외형 (이미지 & 애니메이션)")]
    public Sprite encounterSprite; // 조우자의 기본 일러스트
    public RuntimeAnimatorController animatorController; // 조우자 전용 애니메이터 컨트롤러

    [TextArea(2, 4)] public string[] dialogues; // 3~4줄의 대사 배열
    public string choiceAText; // 하이리스크 하이리턴 텍스트
    public string choiceBText; // 안전/거절 텍스트
}

public class EncounterEventPanel : MonoBehaviour
{
    [Header("참조 설정")]
    public DiceManager diceManager;

    [Header("조우자 데이터베이스")]
    public List<EncounterData> encounterDatabase = new List<EncounterData>();
    private EncounterData currentEncounter;

    [Header("전체 화면 및 대화 UI")]
    public GameObject fullScreenBackground;
    public Animator encounterAnimator;
    public GameObject dialogueRoot;
    public TextMeshProUGUI dialogueText;
    public Button nextDialogueButton;
    public SpriteRenderer encounterSpriteRenderer;

    [Header("선택지 UI")]
    public GameObject choiceRoot;
    public Button choiceAButton;
    public TextMeshProUGUI choiceAText;
    public Button choiceBButton;
    public TextMeshProUGUI choiceBText;

    private int dialogueIndex = 0;

    private void Awake()
    {
        if (nextDialogueButton != null) nextDialogueButton.onClick.AddListener(OnNextDialogue);
        if (choiceAButton != null) choiceAButton.onClick.AddListener(OnChoiceASelected);
        if (choiceBButton != null) choiceBButton.onClick.AddListener(OnChoiceBSelected);

        if (fullScreenBackground != null) fullScreenBackground.SetActive(false);
        if (dialogueRoot != null) dialogueRoot.SetActive(false);
        if (choiceRoot != null) choiceRoot.SetActive(false);
    }

    public void StartEvent(BiomeType currentBiome)
    {
        gameObject.SetActive(true);
        dialogueIndex = 0;

        // 현재 바이옴에 등장 가능한 조우자들만 필터링
        List<EncounterData> possibleEncounters = encounterDatabase
            .Where(e => e.appearBiomes.Contains(currentBiome))
            .ToList();

        if (possibleEncounters.Count > 0)
        {
            // 조건에 맞는 조우자 중 랜덤 1명 선택
            currentEncounter = possibleEncounters[Random.Range(0, possibleEncounters.Count)];
        }
        else
        {
            Debug.LogWarning("현재 바이옴에 등장 가능한 조우자가 없습니다!");
            EndEvent();
            return;
        }

        // 선택된 조우자의 외형으로 UI 교체
        if (encounterSpriteRenderer != null && currentEncounter.encounterSprite != null)
        {
            encounterSpriteRenderer.sprite = currentEncounter.encounterSprite;
        }

        if (encounterAnimator != null)
        {
            if (currentEncounter.animatorController != null)
            {
                // 전용 애니메이션이 있다면 컨트롤러를 갈아끼우고 재생
                encounterAnimator.runtimeAnimatorController = currentEncounter.animatorController;
                encounterAnimator.enabled = true;
                encounterAnimator.speed = 1f;
            }
            else
            {
                // 전용 애니메이션이 없다면 (정지 일러스트라면) 애니메이터 끄기
                encounterAnimator.enabled = false;
            }
        }

        if (fullScreenBackground != null) fullScreenBackground.SetActive(true);
        if (dialogueRoot != null) dialogueRoot.SetActive(true);
        if (choiceRoot != null) choiceRoot.SetActive(false);

        UpdateDialogue();
    }

    private void UpdateDialogue()
    {
        if (currentEncounter != null && dialogueIndex < currentEncounter.dialogues.Length)
        {
            dialogueText.text = currentEncounter.dialogues[dialogueIndex];
        }
        else
        {
            if (dialogueRoot != null) dialogueRoot.SetActive(false);
            ShowChoices();
        }
    }

    private void OnNextDialogue()
    {
        dialogueIndex++;
        UpdateDialogue();
    }

    private void ShowChoices()
    {
        if (encounterAnimator != null && encounterAnimator.enabled)
            encounterAnimator.speed = 0f; // 대화가 끝나고 선택지가 나오면 애니메이션 정지

        if (choiceAText != null) choiceAText.text = currentEncounter.choiceAText;
        if (choiceBText != null) choiceBText.text = currentEncounter.choiceBText;

        choiceAButton.interactable = CheckChoiceACondition(currentEncounter.type);
        choiceBButton.interactable = true;

        if (choiceRoot != null) choiceRoot.SetActive(true);
    }

    private bool CheckChoiceACondition(EncounterType type)
    {
        switch (type)
        {
            case EncounterType.Clown: return diceManager.masterDeck.Count >= 4;
            case EncounterType.SacrificedGirl: return diceManager.masterDeck.Count >= 2;
            default: return true;
        }
    }

    private void OnChoiceASelected()
    {
        bool waitCoroutine = false; // 코루틴 대기를 위한 플래그

        switch (currentEncounter.type)
        {
            case EncounterType.Clown:
                // 체력 30% 지불
                diceManager.currentPlayerHP -= Mathf.FloorToInt(diceManager.currentPlayerHP * 0.3f);

                // 선택창을 4번 연속 띄우기 위해 코루틴 실행
                waitCoroutine = true;
                StartCoroutine(ClownCoatingRoutine());
                break;

            case EncounterType.AbyssDealer:
                // 족보 초기화 (기본 배수로) 및 골드 획득
                diceManager.multHighCard = 1.0f; diceManager.multOnePair = 1.2f; diceManager.multTwoPair = 1.4f;
                diceManager.multTriple = 1.5f; diceManager.multFullHouse = 1.7f; diceManager.multFourOfAKind = 1.8f;
                diceManager.multStraight = 2.0f; diceManager.multYacht = 2.5f;
                diceManager.shopManager.currentGold += (1000 * diceManager.currentStage);

                // InventoryManager에 추가한 티켓 및 UI 완전 삭제 함수 호출
                if (InventoryManager.Instance != null) InventoryManager.Instance.ClearAllTickets();
                break;

            case EncounterType.BlindFortuneTeller:
                // 다음 적 체력 100% 증가 플래그
                diceManager.isNextEnemyHPBoosted = true;
                // [TODO: 88주사위 ScriptableObject 완성 시 연동]
                // InventoryManager.Instance.AddItem(EightyEightDiceSO); 
                break;

            case EncounterType.RustyCaptain:
                // 최대 체력 20% 영구 감소 및 다음 상점 모든 아이템 0원
                diceManager.playerMaxHP -= Mathf.FloorToInt(diceManager.playerMaxHP * 0.2f);
                if (diceManager.currentPlayerHP > diceManager.playerMaxHP)
                    diceManager.currentPlayerHP = diceManager.playerMaxHP;

                diceManager.isNextShopFree = true; // 버프 활성화
                break;

            case EncounterType.Poacher:
                // 상점 슬롯 1개 영구 봉쇄
                diceManager.extraShopSlots -= 1;

                // 보유하지 않은 피규어만 필터링하여 랜덤 3개 지급
                if (diceManager.shopManager != null && diceManager.shopManager.allItemsPool != null)
                {
                    List<FigureItemSO> unownedFigures = diceManager.shopManager.allItemsPool
                        .OfType<FigureItemSO>()
                        .Where(f => !InventoryManager.Instance.ownedFigures.Contains(f))
                        .OrderBy(x => Random.value)
                        .ToList();

                    int getCount = Mathf.Min(3, unownedFigures.Count);
                    for (int i = 0; i < getCount; i++)
                    {
                        InventoryManager.Instance.AddItem(unownedFigures[i]);
                    }
                }
                break;

            case EncounterType.SacrificedGirl:
                // 덱에서 무작위 주사위 2개 소모 (코팅 여부 무관)
                if (diceManager.masterDeck.Count >= 2)
                {
                    diceManager.masterDeck.RemoveAt(Random.Range(0, diceManager.masterDeck.Count));
                    diceManager.masterDeck.RemoveAt(Random.Range(0, diceManager.masterDeck.Count));
                }
                // [TODO: 전용 피규어 즉시 획득]
                // InventoryManager.Instance.AddItem(SacrificedGirlFigureSO);
                break;

            case EncounterType.Alchemist:
                // 내 덱의 모든 주사위를 코팅 및 눈금 무작위 변환시키고 3000골드 획득
                foreach (var d in diceManager.masterDeck)
                {
                    // 1. 코팅 랜덤
                    d.isCoated = true;
                    d.type = (DiceType)Random.Range(1, 5); // 1:Prism, 2:Gold, 3:Dark, 4:Ice

                    switch (d.type)
                    {
                        case DiceType.Prism: d.diceColor = Color.white; break;
                        case DiceType.Gold: d.diceColor = Color.yellow; break;
                        case DiceType.Dark: d.diceColor = new Color32(43, 42, 26, 255); break;
                        case DiceType.Ice: d.diceColor = Color.cyan; break;
                    }

                    // 2. 주사위 눈금 종류 랜덤 (로우, 하이, 홀수, 짝수, 고정, 기본)
                    int diceStyle = Random.Range(0, 6);
                    switch (diceStyle)
                    {
                        case 0: // 기본 주사위
                            d.faceValues = new int[] { 1, 2, 3, 4, 5, 6 };
                            d.specialEffect = SpecialDieEffect.None;
                            break;
                        case 1: // 로우 주사위
                            d.faceValues = new int[] { 1, 1, 2, 2, 3, 3 };
                            d.specialEffect = SpecialDieEffect.None;
                            break;
                        case 2: // 하이 주사위
                            d.faceValues = new int[] { 4, 4, 5, 5, 6, 6 };
                            d.specialEffect = SpecialDieEffect.None;
                            break;
                        case 3: // 짝수 주사위
                            d.faceValues = new int[] { 2, 2, 4, 4, 6, 6 };
                            d.specialEffect = SpecialDieEffect.Even;
                            break;
                        case 4: // 홀수 주사위
                            d.faceValues = new int[] { 1, 1, 3, 3, 5, 5 };
                            d.specialEffect = SpecialDieEffect.Odd;
                            break;
                        case 5: // 고정 주사위
                            int fixedNum = Random.Range(1, 7);
                            d.faceValues = new int[] { fixedNum, fixedNum, fixedNum, fixedNum, fixedNum, fixedNum };
                            d.specialEffect = SpecialDieEffect.None;
                            break;
                    }

                    // 기존에 씌워져있던 스킨(가짜주사위 등)이 있으면 숫자가 안 보일 수 있으므로 초기화
                    d.customDiceShell = null;
                    d.customFaceSprites = null;
                }
                diceManager.shopManager.currentGold += 3000;

                // 필드 파티클 강제 새로고침
                foreach (var activeDice in diceManager.activeDiceList)
                {
                    if (activeDice != null && activeDice.myData != null)
                    {
                        //바뀐 주사위 타입에 맞는 새로운 눈금 하나를 랜덤으로 뽑음 (예: 로우 주사위가 됐으면 1~3 중 하나)
                        int newValidValue = activeDice.myData.faceValues[Random.Range(0, 6)];

                        // 바뀐 데이터와 새 눈금을 바탕으로 필드 주사위의 이미지를 즉시 다시 그리도록 강제!
                        activeDice.SetData(activeDice.myData, newValidValue);

                        // 파티클 이펙트 갱신
                        DiceCoatingVFX vfx = activeDice.GetComponent<DiceCoatingVFX>();
                        if (vfx != null) vfx.ForceRefresh();
                    }
                }
                break;

            case EncounterType.WishWanderer:
                diceManager.isNextCombatHPTiedToOne = true;
                // [TODO: 전용 피규어 즉시 획득]
                // InventoryManager.Instance.AddItem(WishWandererFigureSO);
                break;

            case EncounterType.ForgottenExplorer:
                // 스낵 모두 파괴 및 5000골드 즉시 획득
                foreach (var slot in InventoryManager.Instance.snackSlots) slot.ClearSlot();
                diceManager.shopManager.currentGold += 5000;
                break;

            case EncounterType.MadHatter:
                // 스낵 슬롯을 완벽히 지우고 스테이크/팬케이크로 덮어씌움
                if (diceManager.shopManager != null && diceManager.shopManager.allItemsPool != null)
                {
                    List<SnackItemSO> allSnacks = diceManager.shopManager.allItemsPool.OfType<SnackItemSO>().ToList();
                    SnackItemSO steak = allSnacks.Find(s => s.snackType == SnackType.Steak);
                    SnackItemSO pancake = allSnacks.Find(s => s.snackType == SnackType.Pancake);

                    foreach (var slot in InventoryManager.Instance.snackSlots)
                    {
                        slot.ClearSlot(); // 기존 스낵 완벽하게 파괴
                        SnackItemSO randomSnack = (Random.value > 0.5f) ? steak : pancake;
                        if (randomSnack != null) slot.SetItem(randomSnack);
                    }
                }
                break;
        }

        // 코루틴으로 대기해야 하는 조우자(어릿광대)가 아닐 때만 즉시 UI 갱신 및 이벤트 종료
        if (!waitCoroutine)
        {
            UpdateUIAfterChoice();
            EndEvent();
        }
    }

    private void OnChoiceBSelected()
    {
        

        switch (currentEncounter.type)
        {
            case EncounterType.AbyssDealer:
                diceManager.maxRerolls++;
                break;
            case EncounterType.BlindFortuneTeller:
                diceManager.currentPlayerHP += Mathf.FloorToInt(diceManager.playerMaxHP * 0.05f);
                break;
            case EncounterType.SacrificedGirl:
                diceManager.shopManager.currentGold += (100 * diceManager.currentStage);
                break;
            case EncounterType.RustyCaptain:
                //전투 없이 그냥 이동
                break;
            case EncounterType.Alchemist:
                diceManager.currentPlayerHP += Mathf.FloorToInt((diceManager.playerMaxHP - diceManager.currentPlayerHP) * 0.1f);
                break;
            case EncounterType.ForgottenExplorer:
                // [TODO: 골드 일부 소모 후 주사위 부품 획득 구현]
                break;
            case EncounterType.MadHatter:
                // 스낵 슬롯을 완벽히 지우고 페퍼민트/체리로 덮어씌움
                if (diceManager.shopManager != null && diceManager.shopManager.allItemsPool != null)
                {
                    List<SnackItemSO> allSnacks = diceManager.shopManager.allItemsPool.OfType<SnackItemSO>().ToList();
                    SnackItemSO peppermint = allSnacks.Find(s => s.snackType == SnackType.Peppermint);
                    SnackItemSO cherry = allSnacks.Find(s => s.snackType == SnackType.Cherry);

                    foreach (var slot in InventoryManager.Instance.snackSlots)
                    {
                        slot.ClearSlot(); // 기존 스낵 완벽하게 파괴
                        SnackItemSO randomSnack = (Random.value > 0.5f) ? peppermint : cherry;
                        if (randomSnack != null) slot.SetItem(randomSnack);
                    }
                }
                break;
        }

        UpdateUIAfterChoice();
        EndEvent();
    }

    // 어릿광대용 코팅 4번 띄우기 코루틴
    private IEnumerator ClownCoatingRoutine()
    {
        // 상점에서처럼 유저가 직접 다크 코팅 대상을 4번 고르도록 띄움
        for (int i = 0; i < 4; i++)
        {
            // 코팅창 열기 (다크, 배수 1배, 다크 색상)
            diceManager.shopManager.ShowCoatingSelection(DiceType.Dark, 1.0f, new Color32(43, 42, 26, 255));

            // 코팅창이 화면에 띄워져 있는 동안 다음 코드로 넘어가지 않고 무한 대기
            while (diceManager.shopManager.coatingSelectionPanel.panelRoot.activeSelf)
            {
                yield return null;
            }
        }

        // 4번의 선택이 모두 끝났으므로 UI 갱신 및 이벤트 최종 종료
        UpdateUIAfterChoice();
        EndEvent();
    }

    private void UpdateUIAfterChoice()
    {
        if (diceManager.currentPlayerHP > diceManager.playerMaxHP)
            diceManager.currentPlayerHP = diceManager.playerMaxHP;

        if (diceManager.ui != null)
            diceManager.ui.UpdateGoldUI(diceManager.shopManager.currentGold);
        if (GoldCounter.Instance != null)
            GoldCounter.Instance.SetGold(diceManager.shopManager.currentGold);

        diceManager.ForceUpdateUI();
    }

    private void EndEvent()
    {
        if (fullScreenBackground != null) fullScreenBackground.SetActive(false);
        if (choiceRoot != null) choiceRoot.SetActive(false);
        gameObject.SetActive(false);

        diceManager.ShowLootSelection();
    }
}