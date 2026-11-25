using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 설정창의 쿠폰 입력 팝업. 코드 확인은 CouponService가 맡는다.
/// </summary>
public class CouponPopup : MonoBehaviour
{
    [SerializeField] private TMP_InputField codeInput;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI placeholderText;
    [SerializeField] private TextMeshProUGUI confirmText;
    [SerializeField] private TextMeshProUGUI cancelText;
    [SerializeField] private TextMeshProUGUI messageText;

    [SerializeField] private Color normalMessageColor = new Color32(0xF6, 0xE4, 0xC2, 0xFF);
    [SerializeField] private Color errorMessageColor = new Color32(0xE8, 0x8A, 0x6A, 0xFF);

    private void Awake()
    {
        if (confirmButton != null) confirmButton.onClick.AddListener(Submit);
        if (cancelButton != null) cancelButton.onClick.AddListener(Close);
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (codeInput != null) codeInput.onSubmit.AddListener(_ => Submit());
    }

    public void Open()
    {
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        RefreshTexts();
        if (codeInput != null)
        {
            codeInput.text = string.Empty;
            codeInput.ActivateInputField();
        }
        SetMessage(string.Empty, false);
    }

    public void Close()
    {
        if (codeInput != null) codeInput.DeactivateInputField();
        gameObject.SetActive(false);
    }

    private void RefreshTexts()
    {
        if (titleText != null) titleText.text = LocalizationManager.GetUi("UI_COUPON_TITLE", "쿠폰 입력");
        if (placeholderText != null) placeholderText.text = LocalizationManager.GetUi("UI_COUPON_PLACEHOLDER", "쿠폰 코드를 입력하세요");
        if (confirmText != null) confirmText.text = LocalizationManager.GetUi("UI_COUPON_CONFIRM", "확인");
        if (cancelText != null) cancelText.text = LocalizationManager.GetUi("UI_COUPON_CANCEL", "취소");
    }

    private void Submit()
    {
        string code = codeInput != null ? codeInput.text.Trim() : string.Empty;
        if (string.IsNullOrEmpty(code))
        {
            SetMessage(LocalizationManager.GetSys("SYS_COUPON_EMPTY", "쿠폰 코드를 입력해 주세요."), true);
            return;
        }

        bool ok = CouponService.TryRedeem(code, out string message);
        SetMessage(message, !ok);
        if (ok && codeInput != null) codeInput.text = string.Empty;
    }

    private void SetMessage(string message, bool isError)
    {
        if (messageText == null) return;
        messageText.text = message;
        messageText.color = isError ? errorMessageColor : normalMessageColor;
    }
}

/// <summary>
/// 쿠폰 코드 확인. 서버나 쿠폰 표가 생기면 여기만 바꾼다. 지금은 등록된 쿠폰이 없다.
/// </summary>
public static class CouponService
{
    public static bool TryRedeem(string code, out string message)
    {
        message = LocalizationManager.GetSys("SYS_COUPON_INVALID", "유효하지 않은 쿠폰 코드입니다.");
        return false;
    }
}
