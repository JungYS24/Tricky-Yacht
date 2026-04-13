using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIManager : MonoBehaviour
{
    [Header("메인 게임 UI")]
    public TextMeshProUGUI stageText;
    public TextMeshProUGUI targetScoreText;
    public TextMeshProUGUI cumulativeScoreText;
    public TextMeshProUGUI roundPlaysText;

    [Header("데미지 계산 UI (통합)")]
    public TextMeshProUGUI scoringFormulaText; // ⬅️ 이거 하나만 사용합니다!

    [Header("버튼 및 패널")]
    public Button rollButton;
    public Button finishButton;
    public GameObject resultPanel;
    public TextMeshProUGUI resultDescription;
    public GameObject shopChoicePanel;
    public Button goShopButton;
    public Button nextStageButton;
    public TextMeshProUGUI goldText;

    [Header("확률 표시 UI")]
    public TextMeshProUGUI dropRateText; //박제 확률 텍스트

    public void ShowShopChoice() => shopChoicePanel.SetActive(true);
    public void HideShopChoice() => shopChoicePanel.SetActive(false);

    // 매개변수 끝자리에 combinedDamageText 하나만 받도록 수정
    public void UpdateGameUI(int stageNum, int currentHP, int maxHP, int playsMade, int maxPlays, int rerollsLeft, string combinedDamageText)
    {
        stageText.text = $"스테이지: {stageNum}";
        targetScoreText.text = $"<color=#FF5555>{currentHP}/{maxHP}</color>";
        cumulativeScoreText.text = "";
        roundPlaysText.text = $"라운드: {playsMade} / {maxPlays} | 남은 굴리기: {rerollsLeft}";

        // 텍스트 하나에 족보, 식, 결과를 세 줄로 띄워줍니다.
        if (scoringFormulaText != null)
        {
            scoringFormulaText.text = combinedDamageText;
        }
    }

    public void UpdateGoldUI(int currentGold) => goldText.text = currentGold.ToString("N0");
    public void SetRollButtonInteractable(bool state) => rollButton.interactable = state;
    public void SetFinishButtonInteractable(bool state) => finishButton.interactable = state;

    public void ShowResult(string colorHex, string description)
    {
        resultPanel.SetActive(true);
        resultDescription.text = $"<color={colorHex}></color>\n{description}";
    }

    public void UpdateDropRateUI(float baseRate, float bonusRate)
    {
        if (dropRateText == null) return;

        float totalRate = (baseRate + bonusRate) * 100f;

        // 가니쉬 버프가 있을 때는 청록색으로 강조하고 보너스 수치 표기
        if (bonusRate > 0)
        {
            dropRateText.text = $"박제 확률: <color=#00FFFF>{totalRate:F0}%</color>";
        }
        else
        {
            dropRateText.text = $"박제 확률: {totalRate:F0}%";
        }
    }

    public void HideResult() => resultPanel.SetActive(false);
}