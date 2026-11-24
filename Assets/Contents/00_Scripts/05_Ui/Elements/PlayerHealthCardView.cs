using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 왼쪽 패널 체력 칸(HeartPanel)에 붙는 표시 담당입니다.
// 체력 바 길이를 현재 체력에 맞추고, 보호막이 있는 동안에는 하트를 방패로, 바와 체력 숫자를 회색으로 바꿉니다.
// 숫자 글자(예: 100(4)/100)는 UIManager가 쓰고, 이 스크립트는 그림과 색만 바꿉니다.
// 씬 연결은 Tools/UI/Setup Calc Panel (Navy) 메뉴가 합니다.
public sealed class PlayerHealthCardView : MonoBehaviour
{
    [SerializeField] Image icon;
    [SerializeField] Sprite shieldSprite;
    [SerializeField] RectTransform fill;
    [SerializeField] Image fillImage;
    [SerializeField] Sprite redFill;
    [SerializeField] Sprite grayFill;
    [SerializeField] TMP_Text hpText;
    [SerializeField] Color shieldTextColor = new Color(0.682f, 0.722f, 0.769f, 1f);
    [Tooltip("체력 바가 목표 길이로 따라가는 빠르기")]
    [SerializeField] float fillSharpness = 12f;

    Sprite heartSprite;
    Color heartTextColor;
    bool cached;
    bool shieldShown;
    float shownRatio = -1f;

    void Awake() => Cache();

    // 체력 바 칸(빨간 바). 악마주사위 간식 시간 연출(DebuffFeedback.PlayDevilSnack)이 바 끝을 찾을 때 쓴다.
    internal RectTransform Fill => fill;

    // 다음 프레임에 바를 따라가지 않고 현재 체력 길이로 바로 맞춘다(간식 시간에 바 끝이 한 번에 물려 나가게).
    internal void JumpToCurrent() => shownRatio = -1f;

    void Cache()
    {
        if (cached) return;
        cached = true;
        if (icon != null) heartSprite = icon.sprite;
        if (hpText != null) heartTextColor = hpText.color;
    }

    void LateUpdate()
    {
        DiceManager dm = DiceManager.Instance;
        if (dm == null) return;
        Cache();

        float target = dm.playerMaxHP > 0 ? Mathf.Clamp01((float)dm.currentPlayerHP / dm.playerMaxHP) : 0f;
        if (shownRatio < 0f) shownRatio = target;
        else shownRatio = Mathf.Lerp(shownRatio, target, 1f - Mathf.Exp(-fillSharpness * Time.unscaledDeltaTime));
        if (Mathf.Abs(shownRatio - target) < 0.001f) shownRatio = target;
        if (fill != null)
        {
            fill.anchorMax = new Vector2(shownRatio, 1f);
            bool visible = shownRatio > 0.001f;
            if (fill.gameObject.activeSelf != visible) fill.gameObject.SetActive(visible);
        }

        bool shield = dm.currentShield > 0;
        if (shield != shieldShown) Apply(shield);
    }

    void Apply(bool shield)
    {
        shieldShown = shield;
        if (icon != null) icon.sprite = shield && shieldSprite != null ? shieldSprite : heartSprite;
        if (fillImage != null)
        {
            Sprite s = shield ? grayFill : redFill;
            if (s != null) fillImage.sprite = s;
        }
        if (hpText != null) hpText.color = shield ? shieldTextColor : heartTextColor;
    }
}
