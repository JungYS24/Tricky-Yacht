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
    public TextMeshProUGUI scoringFormulaText;
    public TextMeshProUGUI activeFigureText; // 피규어 발동 텍스트변수

    [Header("플레이어 체력 UI")]
    //플레이어 체력
    public TextMeshProUGUI heartText;

    [Header("버튼 및 패널")]
    public Button rollButton;
    public Button finishButton;
    //public TextMeshProUGUI resultDescription;
    public GameObject shopChoicePanel;
    public Button goShopButton;
    public Button nextStageButton;
    public TextMeshProUGUI goldText;

    [Header("확률 표시 UI")]
    public TextMeshProUGUI dropRateText;

    [Header("결과창 설정")]
    public TMPro.TextMeshProUGUI resultText;
    public GameObject resultPanel;
    public void ShowShopChoice() => shopChoicePanel.SetActive(true);
    public void HideShopChoice() => shopChoicePanel.SetActive(false);

    [Header("설정창(일시정지) UI")]
    public GameObject settingsPanel;
    public Button settingsOpenButton;
    public Button resumeButton;

    private void Start()
    {
        if (settingsOpenButton != null)
            settingsOpenButton.onClick.AddListener(OpenSettings);

        if (resumeButton != null)
            resumeButton.onClick.AddListener(CloseSettings);

        if (settingsPanel != null)
            settingsPanel.SetActive(false);
    }

    public void OpenSettings()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(true);

        Time.timeScale = 0f;
    }

    public void CloseSettings()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        Time.timeScale = 1f;
    }

    public void UpdateGameUI(string stageName, int currentHP, int maxHP, int playerHP, int playerMaxHP, int rerollsLeft, string combinedDamageText, string activeFigureString = "")
    {
        stageText.text = stageName;

        targetScoreText.text = $"<color=#FF5555>{currentHP}/{maxHP}</color>";
        cumulativeScoreText.text = "";

        roundPlaysText.text = $"남은 굴리기: {rerollsLeft}";

        if (heartText != null)
        {
            heartText.text = $"{playerHP}/{playerMaxHP}";
        }

        if (scoringFormulaText != null)
        {
            scoringFormulaText.text = combinedDamageText;
        }

        if (activeFigureText != null)
        {
            activeFigureText.text = activeFigureString;
        }
    }

    public void UpdateGoldUI(int currentGold) => goldText.text = currentGold.ToString("N0");
    public void SetRollButtonInteractable(bool state) => rollButton.interactable = state;
    public void SetFinishButtonInteractable(bool state) => finishButton.interactable = state;

    public void ShowResult(string colorHex, string message)
    {
        if (resultPanel != null)
        {
            resultPanel.SetActive(true);
        }

        if (resultText != null)
        {
            // DiceManager가 보내준 색상(Hex)과 문구("게임 오버")를 서치해서 반영해줍니다.
            resultText.text = $"<color={colorHex}>{message}</color>";
        }
    }

    public void UpdateDropRateUI(float baseRate, float bonusRate)
    {
        if (dropRateText == null) return;

        float displayBonusRate = bonusRate;
        if (displayBonusRate >= 1.0f)
        {
            displayBonusRate -= 1.0f;
        }

        float totalRate = (baseRate + displayBonusRate) * 100f;

        if (displayBonusRate > 0)
        {
            dropRateText.text = $"<color=#00FFFF>{totalRate:F0}%</color>";
        }
        else
        {
            dropRateText.text = $"{totalRate:F0}%";
        }
    }

    public void HideResult() => resultPanel.SetActive(false);
}