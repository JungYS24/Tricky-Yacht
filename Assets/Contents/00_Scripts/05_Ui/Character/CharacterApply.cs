using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class CharacterApply : MonoBehaviour
{
    [SerializeField] private GameObject applyButton;

    [SerializeField] private GameObject finalCheckPanel;

    [SerializeField] private TextMeshProUGUI nameText;

    [Header("정보 패널")]
    [SerializeField] private GameObject guideRoot;
    [SerializeField] private GameObject infoRoot;
    [SerializeField] private Image portraitImage;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private TextMeshProUGUI hpValueText;
    [SerializeField] private Image diceIcon;
    [SerializeField] private TextMeshProUGUI diceEmptyText;
    [SerializeField] private Image figureIcon;
    [SerializeField] private TextMeshProUGUI figureEmptyText;
    [SerializeField] private TextMeshProUGUI confirmText;
    [SerializeField] private Color disabledTint = new Color(0.45f, 0.45f, 0.45f, 1f);

    [Header("로컬라이즈 라벨")]
    [SerializeField] private TextMeshProUGUI titleLabel;
    [SerializeField] private TextMeshProUGUI guideLabel;
    [SerializeField] private TextMeshProUGUI diceLabel;
    [SerializeField] private TextMeshProUGUI figureLabel;
    [SerializeField] private TextMeshProUGUI[] cancelLabels;
    [SerializeField] private TextMeshProUGUI[] startLabels;

    private CharacterData data;

    private void Start()
    {
        ApplyLabels();
        SetApplyInteractable(false);
        finalCheckPanel.SetActive(false);

        if (guideRoot != null) guideRoot.SetActive(true);
        if (infoRoot != null) infoRoot.SetActive(false);
    }

    public void SetCharacter(CharacterData character)
    {
        data = character;
        nameText.text = data.CharacterName;
        SetApplyInteractable(true);
        RefreshInfo();
    }

    public void OnApply()
    {
        if (data == null)
            return;

        if (confirmText != null)
            confirmText.text = LocalizationManager.GetUi("UI_CHARSELECT_CONFIRM", "{0}(으)로 시작할까요?", data.CharacterName);

        finalCheckPanel.SetActive(true);
    }

    public void OnFinalApply(bool apply)
    {
        if (apply)
        {
            CharacterStatus status = new(data.CharacterName, data.InitialHP, data.DedicateDiceData, data.DedicateFigureData);
            SceneManager.LoadScene("MainScene_Map");
        }
        else
        {
            finalCheckPanel.SetActive(false);
        }
    }

    // 하단 취소 버튼: 캐릭터 선택을 그만두고 로비로 돌아간다.
    public void OnBackToLobby()
    {
        SceneManager.LoadScene("Lobby");
    }

    private void RefreshInfo()
    {
        if (guideRoot != null) guideRoot.SetActive(false);
        if (infoRoot != null) infoRoot.SetActive(true);

        // 초상이 없으면 프레임의 어두운 바탕만 보이게 둔다.
        if (portraitImage != null)
        {
            portraitImage.sprite = data.Portrait;
            portraitImage.enabled = data.Portrait != null;
        }

        if (descriptionText != null) descriptionText.text = data.Description;
        if (hpValueText != null) hpValueText.text = data.InitialHP.ToString();

        SetItemIcon(diceIcon, diceEmptyText, data.DedicateDiceData);
        SetItemIcon(figureIcon, figureEmptyText, data.DedicateFigureData);
    }

    private static void SetItemIcon(Image icon, TextMeshProUGUI emptyText, BaseItemDataSO item)
    {
        Sprite sprite = item != null ? item.icon : null;

        if (icon != null)
        {
            icon.sprite = sprite;
            icon.enabled = sprite != null;
        }

        if (emptyText != null)
            emptyText.gameObject.SetActive(sprite == null);
    }

    // 시작 버튼은 Sprite Swap이라 색 틴트가 먹지 않으므로 직접 어둡게 한다.
    private void SetApplyInteractable(bool interactable)
    {
        if (applyButton.TryGetComponent(out Button button))
            button.interactable = interactable;

        Color tint = interactable ? Color.white : disabledTint;
        foreach (Graphic graphic in applyButton.GetComponentsInChildren<Graphic>(true))
            graphic.color = tint;
    }

    private void ApplyLabels()
    {
        SetLabel(titleLabel, "UI_CHARSELECT_TITLE", "캐릭터 선택");
        SetLabel(guideLabel, "UI_CHARSELECT_GUIDE", "캐릭터를 선택하세요");
        SetLabel(diceLabel, "UI_CHARSELECT_DEDICATED_DICE", "전용 주사위");
        SetLabel(figureLabel, "UI_CHARSELECT_DEDICATED_FIGURE", "전용 피규어");

        if (cancelLabels != null)
            foreach (TextMeshProUGUI label in cancelLabels)
                SetLabel(label, "UI_CHARSELECT_CANCEL", "취소");

        if (startLabels != null)
            foreach (TextMeshProUGUI label in startLabels)
                SetLabel(label, "UI_CHARSELECT_START", "시작");
    }

    private static void SetLabel(TextMeshProUGUI label, string key, string fallback)
    {
        if (label != null)
            label.text = LocalizationManager.GetUi(key, fallback);
    }
}
