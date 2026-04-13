using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq; // [필수] Min, Max 함수를 사용하기 위해 추가

public class DeckSlot : MonoBehaviour
{
    [Header("UI 연결")]
    public GameObject emptyVisual;
    public GameObject filledVisual;
    public Image diceIcon;
    public TextMeshProUGUI descText;

    [Header("주사위 타입별 이미지 (코팅용)")]
    public Sprite normalSprite;
    public Sprite prismSprite;
    public Sprite goldSprite;
    public Sprite blackSprite;
    public Sprite iceSprite;

    [Header("특수 주사위용 이미지 (추가)")]
    public Sprite oddDiceSprite;       // 홀수 주사위 아이콘
    public Sprite evenDiceSprite;      // 짝수 주사위 아이콘
    public Sprite[] fixedNumberSprites; // 고정 주사위용 1~6 아라비아 숫자 이미지


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
            // --- 1. 특수 주사위 종류 판별 로직 ---
            bool isFixed = data.faceValues.All(f => f == data.faceValues[0]);

            if (isFixed && fixedNumberSprites != null && fixedNumberSprites.Length >= 6)
            {
                // 고정 주사위라면 해당 숫자의 아라비아 숫자 이미지 표시
                int val = data.faceValues[0];
                diceIcon.sprite = fixedNumberSprites[val - 1];
            }

            else if (!string.IsNullOrEmpty(data.diceName) && data.diceName.Contains("홀수") && oddDiceSprite != null)
            {
                diceIcon.sprite = oddDiceSprite;
            }
            else if (!string.IsNullOrEmpty(data.diceName) && data.diceName.Contains("짝수") && evenDiceSprite != null)
            {
                diceIcon.sprite = evenDiceSprite;
            }
            else
            {
                // 일반 주사위이거나 코팅된 주사위라면 기존 코팅 타입별 이미지 사용
                switch (data.type)
                {
                    case DiceType.Normal: diceIcon.sprite = normalSprite; break;
                    case DiceType.Prism: diceIcon.sprite = prismSprite; break;
                    case DiceType.Gold: diceIcon.sprite = goldSprite; break;
                    case DiceType.Dark: diceIcon.sprite = blackSprite; break;
                    case DiceType.Ice: diceIcon.sprite = iceSprite; break;
                }
            }

            Color finalColor = Color.white;
            if (isUsed) finalColor.a = 0.4f;
            diceIcon.color = finalColor;
        }

        // --- 2. 주사위 눈금 정보 텍스트 표시 ---
        if (descText != null)
        {
            int minVal = data.faceValues.Min();
            int maxVal = data.faceValues.Max();

            // 최소와 최대가 같으면 숫자 하나만 표시, 다르면 범위(1~6 등) 표시
            if (minVal == maxVal)
                descText.text = minVal.ToString();
            else
                descText.text = $"{minVal}~{maxVal}";
        }
    }
}