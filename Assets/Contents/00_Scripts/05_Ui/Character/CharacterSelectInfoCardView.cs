using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 상세 화면의 고유 주사위 카드 / 시작 피규어 카드 한 장. 아이콘이 없으면 "?" 점선 칸을 보여 준다.
public class CharacterSelectInfoCardView : MonoBehaviour
{
    [SerializeField] private Image accentBar;
    [SerializeField] private TextMeshProUGUI letterText;
    [SerializeField] private Image icon;
    [SerializeField] private GameObject unknownIcon;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private GameObject provisionalTag;
    [SerializeField] private TextMeshProUGUI provisionalText;
    [SerializeField] private TextMeshProUGUI descriptionText;

    // isDice: 고유 주사위 카드면 아이콘에 게임 주사위와 같은 떨림을 준다(피규어 카드는 그대로).
    public void Bind(CharacterSelectInfoCard card, Color accent, string letter, bool isDice = false)
    {
        Sprite sprite = card != null ? card.ResolveIcon() : null;

        if (accentBar != null) accentBar.color = accent;
        if (letterText != null)
        {
            letterText.gameObject.SetActive(!string.IsNullOrEmpty(letter));
            letterText.text = letter;
            letterText.color = accent;
        }

        if (isDice)
            CharacterSelectUi.SetDiceIcon(icon, sprite);
        else if (icon != null)
        {
            icon.sprite = sprite;
            icon.enabled = sprite != null;
        }
        if (unknownIcon != null) unknownIcon.SetActive(sprite == null);

        if (nameText != null) nameText.text = card != null ? card.name.Get() : string.Empty;
        if (descriptionText != null) descriptionText.text = card != null ? card.description.Get() : string.Empty;

        bool provisional = card != null && card.provisional;
        if (provisionalTag != null) provisionalTag.SetActive(provisional);
        if (provisionalText != null) provisionalText.text = CharacterSelectLabels.Provisional.Get();
    }
}
