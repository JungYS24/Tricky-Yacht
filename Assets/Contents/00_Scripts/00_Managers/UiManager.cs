using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIManager : MonoBehaviour
{
    [Header("메인 게임 UI (HUD)")]
    public TextMeshProUGUI stageText;
    public TextMeshProUGUI targetScoreText;
    public TextMeshProUGUI cumulativeScoreText;
    public TextMeshProUGUI roundPlaysText;
    public TextMeshProUGUI scoringFormulaText;

    [Header("버튼 객체 연결")]
    public Button rollButton;
    public Button finishButton;

    [Header("결과 화면 UI")]
    public GameObject resultPanel;
    public TextMeshProUGUI resultDescription;

    // 매개변수 중 baseSum을 boardSum(전체 합계)으로 용도 변경하여 받음.
    public void UpdateGameUI(int stageNum, int cumulativeScore, int targetScore, int playsMade, int maxPlays, int rerollsLeft, string handName, int boardSum, float multiplier)
    {
        stageText.text = $"스테이지: {stageNum}";
        targetScoreText.text = $"목표 점수: {targetScore}";
        cumulativeScoreText.text = $"누적 점수: {cumulativeScore}";
        roundPlaysText.text = $"라운드: {playsMade} / {maxPlays} | 남은 굴리기: {rerollsLeft}";

        // [변경됨] "주사위를 선택하세요" 문구를 없애고 항상 5개 기준 최대 점수를 보여줌.
        scoringFormulaText.text = $"<color=#FFD700>{handName}</color>\n";

        int finalScoreForTurn = Mathf.FloorToInt(boardSum * multiplier);
        scoringFormulaText.text += $"{boardSum} × <color=#ADD8E6>{multiplier}배</color>\n= <color=#FFD700>{finalScoreForTurn}점 예정</color>";
    }

    public void SetRollButtonInteractable(bool state) => rollButton.interactable = state;
    public void SetFinishButtonInteractable(bool state) => finishButton.interactable = state;

    public void ShowResult(string title, string colorHex, string description)
    {
        resultPanel.SetActive(true);
        resultDescription.text = $"<color={colorHex}>{title}</color>\n{description}";
    }

    public void HideResult() => resultPanel.SetActive(false);
}