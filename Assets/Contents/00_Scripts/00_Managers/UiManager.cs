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

    [Header("설정창(일시정지) UI")]
    public GameObject settingsPanel;   // 설정창 전체 패널 (어두운 배경 포함)
    public Button settingsOpenButton;  // 게임 화면 우측 상단의 톱니바퀴 버튼
    public Button resumeButton;        // 설정창 안의 '계속하기' 버튼

    private void Start()
    {
        // 시작할 때 버튼들에 함수를 자동으로 연결해 줍니다.
        if (settingsOpenButton != null)
            settingsOpenButton.onClick.AddListener(OpenSettings);

        if (resumeButton != null)
            resumeButton.onClick.AddListener(CloseSettings);

        // 시작 시 설정창은 무조건 꺼두기
        if (settingsPanel != null)
            settingsPanel.SetActive(false);
    }

    // 톱니바퀴 버튼을 눌렀을 때 실행될 함수
    public void OpenSettings()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(true); // 설정창 켜기

        Time.timeScale = 0f; //핵심: 게임 내 시간을 완전히 멈춤! (일시정지)
    }

    // 계속하기 버튼을 눌렀을 때 실행될 함수
    public void CloseSettings()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(false); // 설정창 끄기

        Time.timeScale = 1f; // 💡 핵심: 게임 내 시간을 다시 정상 속도로 돌림! (재개)
    }

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

        // [핵심 추가] 화면에 보여줄 보너스 확률 변수를 따로 만듭니다.
        float displayBonusRate = bonusRate;
        // 만약 보너스 확률이 1.0f(100%) 이상 들어왔다면 (튜토리얼 강제 보정이 들어간 상태라면)
        // 화면 표기용 수치에서만 1.0f(100%)를 몰래 빼줍니다.
        if (displayBonusRate >= 1.4f)
        {
            displayBonusRate -= 1.0f;
        }

        // 실제 계산은 눈속임용 수치로 진행합니다.
        float totalRate = (baseRate + displayBonusRate) * 100f;

        // 가니쉬 버프가 있을 때는 청록색으로 강조하고 보너스 수치 표기
        if (displayBonusRate > 0)
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