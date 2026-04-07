using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIManager : MonoBehaviour
{

    [Header("매니저 참조")]
    public DiceManager diceManager;

    [Header("메인 게임 UI")]
    public TextMeshProUGUI stageText;
    public TextMeshProUGUI targetScoreText;
    public TextMeshProUGUI cumulativeScoreText;
    public TextMeshProUGUI roundPlaysText;
    public TextMeshProUGUI scoringFormulaText;

    [Header("버튼 및 패널")]
    public Button rollButton;
    public Button finishButton;
    public GameObject resultPanel;
    public TextMeshProUGUI resultDescription;
    public GameObject shopChoicePanel;
    public Button goShopButton;
    public Button nextStageButton;
    public TextMeshProUGUI goldText;

    public TMPro.TextMeshProUGUI totalText;
    public TMPro.TextMeshProUGUI normalText;
    public TMPro.TextMeshProUGUI prismText;
    public TMPro.TextMeshProUGUI goldenText;
    public TMPro.TextMeshProUGUI blackText;

    public GameObject deckPanel; // 덱 상태 팝업창 전체

    public void ShowShopChoice() => shopChoicePanel.SetActive(true);
    public void HideShopChoice() => shopChoicePanel.SetActive(false);

    public void UpdateGameUI(int stageNum, int currentHP, int maxHP, int playsMade, int maxPlays, int rerollsLeft, string handName, int boardSum, float multiplier)
    {
        stageText.text = $"스테이지: {stageNum}";
        targetScoreText.text = $"<color=#FF5555>{currentHP}/{maxHP}</color>";
        cumulativeScoreText.text = "";
        roundPlaysText.text = $"라운드: {playsMade} / {maxPlays} | 남은 굴리기: {rerollsLeft}";

        int finalDamageForTurn = Mathf.FloorToInt(boardSum * multiplier);
        scoringFormulaText.text = $"<color=#FFD700>{handName}</color>\n{boardSum} × <color=#ADD8E6>{multiplier}배</color>\n= <color=#FF5555>{finalDamageForTurn} 데미지 예정</color>";
    }

    public void UpdateGoldUI(int currentGold) => goldText.text = currentGold.ToString("N0");
    public void SetRollButtonInteractable(bool state) => rollButton.interactable = state;
    public void SetFinishButtonInteractable(bool state) => finishButton.interactable = state;

    public void ShowResult(string colorHex, string description)
    {
        resultPanel.SetActive(true);
        resultDescription.text = $"<color={colorHex}></color>\n{description}";
    }


    // 덱 버튼에 연결할 함수
    public void OnClickDeckButton()
    {
        // 1. 데이터 가져오기
        var status = diceManager.GetCurrentDeckStatus();

        // 2. UI 글자 업데이트
        totalText.text = status.totalCount.ToString();
        normalText.text = "x " + status.normalCount;
        prismText.text = "x " + status.prismCount;
        goldenText.text = "x " + status.goldCount;
        blackText.text = "x " + status.blackCount;

        // 3. 패널 열기
        deckPanel.SetActive(true);
    }
    public void HideResult() => resultPanel.SetActive(false);
}