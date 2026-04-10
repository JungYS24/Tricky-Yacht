using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DeckSlot : MonoBehaviour
{
    [Header("UI 연결")]
    public GameObject emptyVisual;
    public GameObject filledVisual;
    public Image diceIcon;
    public TextMeshProUGUI descText;

    [Header("주사위 타입별 이미지")]
    public Sprite normalSprite;
    public Sprite prismSprite;
    public Sprite goldSprite;
    public Sprite blackSprite; 
    public Sprite iceSprite;   

    public void SetEmpty()
    {
        emptyVisual.SetActive(true);
        filledVisual.SetActive(false);
    }

    public void SetDice(DiceData1 data, bool isUsed)
    {
        emptyVisual.SetActive(false);
        filledVisual.SetActive(true);

        if (diceIcon != null)
        {

            switch (data.type)
            {
                case DiceType.Normal: diceIcon.sprite = normalSprite; break;
                case DiceType.Prism: diceIcon.sprite = prismSprite; break;
                case DiceType.Gold: diceIcon.sprite = goldSprite; break;
                case DiceType.Dark: diceIcon.sprite = blackSprite; break;
                case DiceType.Ice: diceIcon.sprite = iceSprite; break; 
            }

            Color finalColor = Color.white;

            if (isUsed)
            {
                finalColor.a = 0.4f;
            }
            diceIcon.color = finalColor;
        }

        if (descText != null)
        {
            descText.text = $"{data.minRoll}~{data.maxRoll}";
        }
    }
}