using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIManager : MonoBehaviour
{
    [Header("메인 게임 UI (HUD)")]
    public TextMeshProUGUI stageText;       // 스테이지
    public TextMeshProUGUI targetScoreText;     //목표 점수
    public TextMeshProUGUI cumulativeScoreText; //누적점수
    public TextMeshProUGUI roundPlaysText;      //라운드(총 3번을 던져서 목표점수 달성해야됨), 리롤횟수
    public TextMeshProUGUI scoringFormulaText;  //족보 적용시 점수

    [Header("버튼 객체 연결")]
    public Button rollButton;   //리롤
    public Button finishButton; //턴 끝내기

    [Header("결과 화면 UI")]
    public GameObject resultPanel;  //결과
    public TextMeshProUGUI resultDescription;   //결과 텍스트

    [Header("상점 진입 선택 UI")]
    public GameObject shopChoicePanel; // 상점을 갈지 묻는 팝업창 패널
    public Button goShopButton;        // 상점 가기 버튼
    public Button nextStageButton;     // 다음 스테이지 버튼

    [Header("재화 UI")]
    public TextMeshProUGUI goldText;

    public void ShowShopChoice() => shopChoicePanel.SetActive(true);
    public void HideShopChoice() => shopChoicePanel.SetActive(false);

    public void UpdateGameUI(int stageNum, int currentHP, int maxHP, int playsMade, int maxPlays, int rerollsLeft, string handName, int boardSum, float multiplier)
    {
        stageText.text = $"스테이지: {stageNum}";

        //목표 점수 대신 적 체력으로 표시
        targetScoreText.text = $"<color=#FF5555>{currentHP}/{maxHP}</color>";

        //  누적 점수는 이제 체력바가 대신하므로 텍스트를 비움
        cumulativeScoreText.text = "";

        roundPlaysText.text = $"라운드: {playsMade} / {maxPlays} | 남은 굴리기: {rerollsLeft}";

        scoringFormulaText.text = $"<color=#FFD700>{handName}</color>\n";

        int finalDamageForTurn = Mathf.FloorToInt(boardSum * multiplier);

        //  점수 예정 -> 데미지 예정으로 글귀 변경
        scoringFormulaText.text += $"{boardSum} × <color=#ADD8E6>{multiplier}배</color>\n= <color=#FF5555>{finalDamageForTurn} 데미지 예정</color>";
    }


    public void UpdateGoldUI(int currentGold)
    {
        
        goldText.text = currentGold.ToString("N0");
    }

    public void SetRollButtonInteractable(bool state) => rollButton.interactable = state;
    public void SetFinishButtonInteractable(bool state) => finishButton.interactable = state;

    public void ShowResult(string colorHex, string description)
    {
        resultPanel.SetActive(true);
        resultDescription.text = $"<color={colorHex}></color>\n{description}";
    }

    public void HideResult() => resultPanel.SetActive(false);
}