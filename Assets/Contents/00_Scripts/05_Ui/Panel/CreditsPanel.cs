using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 설정 패널의 크레딧 버튼으로 여는 나무 팝업. 제목은 UI 테이블의 Credit 키를 쓴다.
public class CreditsPanel : MonoBehaviour
{
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI bodyText;
    public Button closeButton;

    private string bodyFallback;

    private void Awake()
    {
        if (closeButton != null) closeButton.onClick.AddListener(Close);
    }

    public void Open()
    {
        if (titleText != null) titleText.text = LocalizationManager.GetUi("Credit", "크레딧");
        // 본문은 UI_CREDITS_BODY 키를 쓰고, 키가 없으면 씬에 적힌 글을 그대로 둔다.
        if (bodyText != null)
        {
            if (bodyFallback == null) bodyFallback = bodyText.text;
            bodyText.text = LocalizationManager.GetUi("UI_CREDITS_BODY", bodyFallback);
        }
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }
}
