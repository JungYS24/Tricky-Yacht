using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using DG.Tweening;

public class UIManager : MonoBehaviour
{
    [Header("메인 게임 UI")]
    public TextMeshProUGUI stageText;
    public TextMeshProUGUI targetScoreText;
    public TextMeshProUGUI cumulativeScoreText;
    public TextMeshProUGUI roundPlaysText;

    // 기존 통합 UI 대신, 용도별로 완전히 분리된 텍스트들을 선언
    [Header("데미지 계산 UI (분리형)")]
    //public TextMeshProUGUI scoringFormulaText; // 기존 통합 텍스트는 이제 사용 안 함
    public TextMeshProUGUI handInfoText;     // 족보 이름 및 기본 코팅 보너스
    public TextMeshProUGUI chipsSumText;     // 합연산(덧셈) 전용 텍스트
    public TextMeshProUGUI multSumText;      // 곱연산(배수) 전용 텍스트
    public TextMeshProUGUI chipsLogText;     // 덧셈(칩) 머리 위에서 뜰 로그
    public TextMeshProUGUI multLogText;      // 곱셈(배수) 머리 위에서 뜰 로그
    public TextMeshProUGUI finalDamageText;  // = 대미지 예정 텍스트

    [Header("플레이어 체력 UI")]
    //플레이어 체력
    public TextMeshProUGUI heartText;

    //보호막 UI 연결용
    public GameObject shieldRoot;        
    public TextMeshProUGUI shieldText;   

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

    [Header("피규어 발동 아이콘 UI")]
    public Image[] activeFigureIcons; // 유니티 에디터에서 띄워줄 이미지 UI들을 연결할 배열

    //준비된 화염 틱딜 UI를 연결할 변수
    [Header("상태이상(화염) UI")]
    public GameObject flameStackRoot;  // 이미지와 텍스트를 모두 포함하는 최상위 부모 오브젝트
    public TextMeshProUGUI flameStackText; // 숫자가 표시될 텍스트

    [Header("결과창 설정")]
    public TMPro.TextMeshProUGUI resultText;
    public GameObject resultPanel;
    public void ShowShopChoice() => shopChoicePanel.SetActive(true);
    public void HideShopChoice() => shopChoicePanel.SetActive(false);

    [Header("설정창(일시정지) UI")]
    public GameObject settingsPanel;
    public Button settingsOpenButton;
    public Button resumeButton;

    //다이스 클릭방지
    public static bool IsSettingsOpen = false;

    private void Start()
    {
        if (settingsOpenButton != null)
            settingsOpenButton.onClick.AddListener(OpenSettings);

        if (resumeButton != null)
            resumeButton.onClick.AddListener(CloseSettings);

        CloseSettings();
    }

    public void OpenSettings()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(true);

        Time.timeScale = 0f;
        //Debug.Log("IsSettingsOpen");
        IsSettingsOpen = true; // 이제 다른 스크립트가 설정창이 열렸다는 걸 알 수 있음
        Debug.Log(IsSettingsOpen+" 아아");
    }

    public void CloseSettings()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        Time.timeScale = 1f;
        Debug.Log("닫힘");
        IsSettingsOpen = false; //설정창이 닫혔다고 알려줌
    }

    // 매개변수 맨 끝에 List<Sprite> activeSprites = null 을 추가해 줍니다.
    public void UpdateGameUI(string stageName, int currentHP, int maxHP, int playerHP, int playerMaxHP, int rerollsLeft, string combinedDamageText, string activeFigureString = "", List<Sprite> activeSprites = null)
    {
        stageText.text = stageName;
        targetScoreText.text = $"<color=#FF5555>{currentHP}/{maxHP}</color>";
        cumulativeScoreText.text = "";
        roundPlaysText.text = LocalizationManager.Instance != null
            ? LocalizationManager.Instance.GetLocalizedString(LocalizationManager.UiTable, "UI_REROLLS_LEFT", rerollsLeft)
            : $"남은 굴리기: {rerollsLeft}";

        if (heartText != null)
        {
            heartText.text = $"{playerHP}/{playerMaxHP}";
        }


        //발동된 피규어 아이콘 표시 로직
        if (activeFigureIcons != null)
        {
            // 1. 매번 갱신할 때마다 일단 모든 아이콘을 숨깁니다.
            foreach (var icon in activeFigureIcons)
            {
                if (icon != null)
                {
                    // DOTween 연출 중복 실행으로 인해 크기나 투명도가 꼬이지 않도록 초기화
                    icon.DOKill();
                    icon.transform.DOKill();
                    icon.gameObject.SetActive(false);
                }
            }

            // 전달받은 발동 피규어 아이콘이 있다면 앞에서부터 순서대로 켬
            if (activeSprites != null)
            {
                for (int i = 0; i < activeSprites.Count && i < activeFigureIcons.Length; i++)
                {
                    if (activeFigureIcons[i] != null)
                    {
                        activeFigureIcons[i].sprite = activeSprites[i];
                        activeFigureIcons[i].gameObject.SetActive(true);

                        //발동되는 순간 쫀득하게 튀어 오르는 타격감
                        activeFigureIcons[i].transform.localScale = Vector3.one;
                        activeFigureIcons[i].transform.DOPunchScale(new Vector3(0.3f, 0.3f, 0f), 0.4f, 2, 0.5f);

                        //상시 발동(연한 빛) 느낌을 위한 투명도 깜빡임(숨쉬기) 효과
                        activeFigureIcons[i].color = new Color(1f, 1f, 1f, 1f);
                        activeFigureIcons[i].DOFade(0.5f, 0.8f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);
                    }
                }
            }
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
    //화염 스택 갱신
    public void UpdateFlameStackUI(int flameDamage)
    {
        if (flameStackRoot != null)
        {
            if (flameDamage > 0)
            {
                flameStackRoot.SetActive(true);
                if (flameStackText != null) flameStackText.text = flameDamage.ToString();

                // 데미지가 누적될 때마다 아이콘 전체가 쫀득하게 튕기는 타격감
                flameStackRoot.transform.DOKill(true);
                flameStackRoot.transform.DOPunchScale(new Vector3(0.25f, 0.25f, 0f), 0.35f, 3, 0.5f);
            }
            else
            {
                flameStackRoot.SetActive(false); // 0이면 아예 숨김 처리
            }
        }
    }

    public void UpdateShieldUI(int shieldAmount)
    {
        if (shieldRoot != null)
        {
            if (shieldAmount > 0)
            {
                shieldRoot.SetActive(true);
                if (shieldText != null) shieldText.text = shieldAmount.ToString();

                // 획득하거나 깎일 때마다 타격감 연출
                shieldRoot.transform.DOKill(true);
                shieldRoot.transform.DOPunchScale(new Vector3(0.25f, 0.25f, 0f), 0.35f, 3, 0.5f);
            }
            else
            {
                shieldRoot.SetActive(false); // 보호막이 0이면 아예 숨김
            }
        }
    }

}