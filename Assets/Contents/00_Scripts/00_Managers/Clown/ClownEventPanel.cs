using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class ClownEventPanel : MonoBehaviour
{
    [Header("참조 설정")]
    public DiceManager diceManager;

    [Header("전체 화면 및 대화 UI")]
    public GameObject fullScreenBackground; // 전체 화면을 덮는 광대 배경 이미지
    public Animator clownAnimator;
    public GameObject dialogueRoot;         // 화면 하단의 대화창 루트
    public TextMeshProUGUI dialogueText;    // 대화 텍스트
    public Button nextDialogueButton;       // 다음 대화로 넘어가는 버튼

    [Header("선택지 UI (광대 화면 위)")]
    public GameObject choiceRoot;           // 광대 화면 위에 뜰 선택지 창 루트
    public Button choice1Button;
    public TextMeshProUGUI choice1Text;
    public Button choice2Button;
    public TextMeshProUGUI choice2Text;
    public Button skipButton;               // 그냥 넘기기 버튼

    [Header("광대 이벤트 수치 조절")]
    public int minGold = 1500;
    public int maxGold = 2501;
    public int minHpCost = 30;
    public int maxHpCost = 41;

    private int dialogueIndex = 0;
    private string[] currentDialogues = new string[]
    {
        "히히히! 용케 여기까지 살아남았군!",
        "하지만 앞으로의 길은 더 험난할 텐데...",
        "내가 아주 재미있는 거래를 하나 제안하지.",
        "네 피를 조금 깎는 대신, 달콤한 보상을 주마. 어때?"
    };

    private int choice1HPCost;
    private int choice2HPCost;
    private int choice1GoldReward;
    private FigureItemSO choice2FigureReward;

    private void Awake()
    {
        if (nextDialogueButton != null) nextDialogueButton.onClick.AddListener(OnNextDialogue);
        if (choice1Button != null) choice1Button.onClick.AddListener(OnChoice1Selected);
        if (choice2Button != null) choice2Button.onClick.AddListener(OnChoice2Selected);
        if (skipButton != null) skipButton.onClick.AddListener(OnSkipSelected);

        // 시작할 때는 모든 이벤트 UI를 숨겨둡니다.
        if (fullScreenBackground != null) fullScreenBackground.SetActive(false);
        if (dialogueRoot != null) dialogueRoot.SetActive(false);
        if (choiceRoot != null) choiceRoot.SetActive(false);
    }

    public void StartEvent()
    {
        // 유니티 에디터에서 부모 오브젝트가 꺼져있어도 강제로 켜줍니다.
        gameObject.SetActive(true);

        dialogueIndex = 0;

        if (clownAnimator != null)
        {
            clownAnimator.speed = 1f;
        }

        // 이벤트가 시작되면 전체 화면 배경과 대화창을 켭니다.
        if (fullScreenBackground != null) fullScreenBackground.SetActive(true);
        if (dialogueRoot != null) dialogueRoot.SetActive(true);
        if (choiceRoot != null) choiceRoot.SetActive(false);

        UpdateDialogue();
    }

    private void UpdateDialogue()
    {
        if (dialogueIndex < currentDialogues.Length)
        {
            dialogueText.text = currentDialogues[dialogueIndex];
        }
        else
        {
            // 4번의 대화가 모두 끝나면 '대화창'만 끕니다. 광대 배경은 유지합니다.
            if (dialogueRoot != null) dialogueRoot.SetActive(false);

            // 광대 이미지 위에 선택지 창을 띄웁니다.
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

        //애니메이션을 첫 프레임으로 되돌리고 속도를 0으로 만들어 정지시킴
        if (clownAnimator != null)
        {
            clownAnimator.Play("AC_Clown_SS", 0, 0f); 
            clownAnimator.speed = 0f; 
        }
        // 랜덤 조건 생성
        choice1HPCost = Random.Range(minHpCost, maxHpCost);
        choice1GoldReward = Random.Range(minGold, maxGold);

        choice2HPCost = Random.Range(minHpCost, maxHpCost);

        List<FigureItemSO> availableFigures = new List<FigureItemSO>();
        if (diceManager.shopManager != null && diceManager.shopManager.allItemsPool != null)
        {
            foreach (var item in diceManager.shopManager.allItemsPool)
            {
                if (item is FigureItemSO fig && !InventoryManager.Instance.ownedFigures.Contains(fig))
                {
                    availableFigures.Add(fig);
                }
            }
        }

        if (availableFigures.Count > 0)
        {
            choice2FigureReward = availableFigures[Random.Range(0, availableFigures.Count)];
        }
        else
        {
            choice2FigureReward = null;
        }

        // 텍스트 반영
        if (choice1Text != null)
            choice1Text.text = $"체력 {choice1HPCost} 감소\n골드 {choice1GoldReward} 획득";

        if (choice2Text != null)
        {
            if (choice2FigureReward != null)
                choice2Text.text = $"체력 {choice2HPCost} 감소\n랜덤 피규어 획득\n({choice2FigureReward.itemName})";
            else
                choice2Text.text = $"체력 {choice2HPCost} 감소\n획득할 피규어 없음";
        }

        // 플레이어 체력이 부족하면 해당 버튼 비활성화 (죽는 것 방지)
        choice1Button.interactable = (diceManager.currentPlayerHP > choice1HPCost);
        choice2Button.interactable = (diceManager.currentPlayerHP > choice2HPCost && choice2FigureReward != null);

        if (choiceRoot != null) choiceRoot.SetActive(true);
    }

    private void OnChoice1Selected()
    {
        diceManager.currentPlayerHP -= choice1HPCost;
        if (diceManager.shopManager != null)
        {
            diceManager.shopManager.currentGold += choice1GoldReward;
            diceManager.ui?.UpdateGoldUI(diceManager.shopManager.currentGold);
            if (GoldCounter.Instance != null) GoldCounter.Instance.SetGold(diceManager.shopManager.currentGold);
        }
        EndEvent();
    }

    private void OnChoice2Selected()
    {
        diceManager.currentPlayerHP -= choice2HPCost;
        if (choice2FigureReward != null)
        {
            InventoryManager.Instance.AddItem(choice2FigureReward);
        }
        EndEvent();
    }

    private void OnSkipSelected()
    {
        EndEvent(); // 아무것도 깎이지 않고, 보상도 없이 종료
    }

    private void EndEvent()
    {
        // 보상을 선택하면 그제서야 광대 배경과 선택지 창을 모두 끄고 게임 화면으로 돌아갑니다.
        if (fullScreenBackground != null) fullScreenBackground.SetActive(false);
        if (choiceRoot != null) choiceRoot.SetActive(false);
        gameObject.SetActive(false);

        // UI 갱신 및 전리품(Loot) 팝업 열기
        diceManager.ForceUpdateUI();
        diceManager.ShowLootSelection();
    }
}