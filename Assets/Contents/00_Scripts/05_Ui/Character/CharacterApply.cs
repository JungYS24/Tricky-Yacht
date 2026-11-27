using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

// 선택한 캐릭터로 게임을 시작하거나 로비로 나간다. 화면 표시는 CharacterSelectListView / CharacterDetailView가 맡는다.
public class CharacterApply : MonoBehaviour
{
    [SerializeField] private GameObject finalCheckPanel;

    [SerializeField] private TextMeshProUGUI confirmText;

    [Header("로컬라이즈 라벨 (확인 창 버튼)")]
    [SerializeField] private TextMeshProUGUI[] cancelLabels;
    [SerializeField] private TextMeshProUGUI[] startLabels;

    private CharacterData data;
    private int selectedIdx = -1;

    public bool IsConfirmOpen => finalCheckPanel != null && finalCheckPanel.activeSelf;

    private void Start()
    {
        ApplyLabels();
        finalCheckPanel.SetActive(false);
    }

    public void SetCharacter(CharacterData character)
    {
        data = character;
    }

    public void SetDedicateFigure(int idx)
    {
        if (idx < data.DedicateFigureDatas.Length)
            selectedIdx = idx;
    }

    // 잠긴 캐릭터는 정보만 볼 수 있고 시작은 못 한다.
    public void OnApply()
    {
        if (data == null || !data.IsUnlocked)
            return;

        if (confirmText != null)
            confirmText.text = LocalizationManager.GetUi("UI_CHARSELECT_CONFIRM", "{0}(으)로 시작할까요?", LocalizationManager.GetCharacterName(data));

        finalCheckPanel.SetActive(true);
    }

    public void OnFinalApply(bool apply)
    {
        if (apply)
        {
            if (data == null || !data.IsUnlocked || selectedIdx == -1)
                return;

            CharacterStatus.Selected = new(data.CharacterName, data.InitialHP, data.InitialGold, data.DedicateDiceData, data.DedicateFigureDatas[selectedIdx]);
            SceneManager.LoadScene("MainScene_Map");
        }
        else
        {
            finalCheckPanel.SetActive(false);
        }
    }

    // 목록 화면 취소 버튼: 캐릭터 선택을 그만두고 로비로 돌아간다.
    public void OnBackToLobby()
    {
        SceneManager.LoadScene("Lobby");
    }

    private void ApplyLabels()
    {
        if (cancelLabels != null)
            foreach (TextMeshProUGUI label in cancelLabels)
                if (label != null) label.text = LocalizationManager.GetUi("UI_CHARSELECT_CANCEL", "취소");

        if (startLabels != null)
            foreach (TextMeshProUGUI label in startLabels)
                if (label != null) label.text = LocalizationManager.GetUi("UI_CHARSELECT_START", "시작");
    }
}
