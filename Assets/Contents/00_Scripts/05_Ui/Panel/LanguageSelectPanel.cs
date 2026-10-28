using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 설정 패널의 언어 버튼으로 여는 나무 팝업. 한국어·영어·일본어·중국어(간체) 중 하나를 고르면 바로 바꾸고 저장한다.
public class LanguageSelectPanel : MonoBehaviour
{
    // LocalizationManager.SupportedLanguageCodes와 같은 순서
    static readonly string[] Codes = { "ko", "en", "ja", "zh-Hans" };

    public TextMeshProUGUI titleText;
    public Button[] optionButtons;
    public GameObject[] optionHighlights;
    public Button closeButton;

    private int selected = -1;

    private void Awake()
    {
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        for (int i = 0; i < optionButtons.Length && i < Codes.Length; i++)
        {
            int index = i;
            if (optionButtons[i] != null) optionButtons[i].onClick.AddListener(() => Select(index));
        }
    }

    public void Open()
    {
        if (titleText != null) titleText.text = LocalizationManager.GetUi("Language", "언어");
        string current = LocalizationManager.Instance != null ? LocalizationManager.Instance.CurrentLanguageCode : Codes[0];
        selected = System.Array.IndexOf(Codes, current);
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        Refresh();
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }

    private void Select(int index)
    {
        selected = index;
        if (LocalizationManager.Instance != null) LocalizationManager.Instance.SetLanguage(Codes[index]);
        Refresh();
    }

    // 지금 고른 언어 칸에만 금색 테두리를 켠다.
    private void Refresh()
    {
        for (int i = 0; i < optionHighlights.Length; i++)
        {
            if (optionHighlights[i] != null) optionHighlights[i].SetActive(i == selected);
        }
    }
}
