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

    public Sprite[] fixedNumberSprites; // 고정 주사위용 1~6 아라비아 숫자 이미지

    private Coroutine animCoroutine;//주사위 애니메이션(홀수 ,짝수 ,하이, 로우)

    public void SetEmpty()
    {
        emptyVisual.SetActive(true);
        filledVisual.SetActive(false);
    }

    public void SetDice(DiceData1 data, bool isUsed)
    {
        emptyVisual.SetActive(false);
        filledVisual.SetActive(true);

        // 기존에 돌고 있던 애니메이션이 있다면 중지
        if (animCoroutine != null)
        {
            StopCoroutine(animCoroutine);
            animCoroutine = null;
        }

        // 주사위 눈금 중복 제거
        int[] uniqueFaces = data.faceValues.Distinct().ToArray();

        // 눈금 종류가 2개~5개 사이면 애니메이션 (홀/짝, 하이/로우 등)
        bool shouldAnimate = uniqueFaces.Length > 1 && uniqueFaces.Length < 6;


        if (shouldAnimate)
        {
            //커스텀 눈금이 있으면 그것을, 없으면 기본 숫자를 사용
            Sprite[] targetSprites = (data.customFaceSprites != null && data.customFaceSprites.Length >= 6) ? data.customFaceSprites : fixedNumberSprites;

            if (targetSprites != null && targetSprites.Length >= 6)
            {
                animCoroutine = StartCoroutine(AnimateDiceUI(uniqueFaces, targetSprites));
            }
        }

        else
        {
            //애니메이션 대상이 아닐 때 (일반 주사위 및 고정 주사위)
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

                else if (data.customDiceShell != null)
                {
                    diceIcon.sprite = data.customDiceShell;
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

    // 애니메이션 코루틴
    private System.Collections.IEnumerator AnimateDiceUI(int[] faces, Sprite[] spriteArray)
    {
        int index = 0;
        while (true)
        {
            diceIcon.sprite = spriteArray[faces[index] - 1]; // 1, 3, 5 순서대로 이미지 변경
            index = (index + 1) % faces.Length;
            yield return new WaitForSeconds(0.7f); // 0.4초마다 이미지 교체 (속도 조절 가능)
        }
    }
}