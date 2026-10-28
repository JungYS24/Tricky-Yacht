using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 설정 패널의 크레딧 버튼으로 여는 나무 팝업. 제목은 UI 테이블의 Credit 키를 쓴다.
public class CreditsPanel : MonoBehaviour
{
    public TextMeshProUGUI titleText;
    public Button closeButton;

    private void Awake()
    {
        if (closeButton != null) closeButton.onClick.AddListener(Close);
    }

    public void Open()
    {
        if (titleText != null) titleText.text = LocalizationManager.GetUi("Credit", "크레딧");
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }
}
