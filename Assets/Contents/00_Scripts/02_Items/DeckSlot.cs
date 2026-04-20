using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;

public class DeckSlot : MonoBehaviour
{
    [Header("UI 연결")]
    public GameObject emptyVisual;
    public GameObject filledVisual;
    public Image diceIcon;
    public TextMeshProUGUI descText;

    [Header("주사위 타입별 기본 이미지")]
    public Sprite normalSprite;
    public Sprite prismSprite;
    public Sprite goldSprite;
    public Sprite blackSprite;
    public Sprite iceSprite;

    [Header("특수 주사위용 숫자 이미지")]
    public Sprite[] fixedNumberSprites;

    // 애니메이션 제어용 변수
    private DiceData1 currentData;
    private int[] animFaces;
    private Sprite[] animSprites;
    private int currentAnimIndex;
    private float animTimer;
    private float animInterval = 0.7f; // 깜빡이는 속도 (초)
    private bool isAnimating = false;

    public void SetEmpty()
    {
        emptyVisual.SetActive(true);
        filledVisual.SetActive(false);
        currentData = null;
        isAnimating = false;
    }

    public void SetDice(DiceData1 data, bool isUsed)
    {
        currentData = data;
        emptyVisual.SetActive(false);
        filledVisual.SetActive(true);

        // 투명도 설정 (사용 중인 주사위는 흐리게)
        if (diceIcon != null)
        {
            Color finalColor = Color.white;
            if (isUsed) finalColor.a = 0.4f;
            diceIcon.color = finalColor;
        }

        // --- 애니메이션 데이터 분석 ---
        int[] uniqueFaces = data.faceValues.Distinct().ToArray();
        // 눈금이 2개 이상 5개 이하일 때만 애니메이션 실행 (홀짝, 하이로우 등)
        isAnimating = uniqueFaces.Length > 1 && uniqueFaces.Length < 6;

        if (isAnimating)
        {
            animFaces = uniqueFaces;
            animSprites = (data.customFaceSprites != null && data.customFaceSprites.Length >= 6)
                          ? data.customFaceSprites : fixedNumberSprites;

            currentAnimIndex = 0;
            animTimer = 0;
            UpdateDisplaySprite(); // 첫 프레임 즉시 적용
        }
        else
        {
            // 애니메이션이 아닌 경우 (일반 또는 6면 동일 고정 주사위)
            UpdateStaticDisplay(data);
        }

        // 하단 텍스트 (범위 표시: 예 1~3)
        if (descText != null)
        {
            int minVal = data.faceValues.Min();
            int maxVal = data.faceValues.Max();
            descText.text = (minVal == maxVal) ? minVal.ToString() : $"{minVal}~{maxVal}";
        }
    }

    private void Update()
    {
        // 덱 슬롯이 활성화되어 있고 애니메이션 대상일 때만 실행
        if (isAnimating && animSprites != null && animSprites.Length >= 6)
        {
            animTimer += Time.deltaTime;
            if (animTimer >= animInterval)
            {
                animTimer = 0;
                currentAnimIndex = (currentAnimIndex + 1) % animFaces.Length;
                UpdateDisplaySprite();
            }
        }
    }

    private void UpdateDisplaySprite()
    {
        if (diceIcon != null && animSprites != null)
        {
            int faceValue = animFaces[currentAnimIndex];
            diceIcon.sprite = animSprites[faceValue - 1];
        }
    }

    private void UpdateStaticDisplay(DiceData1 data)
    {
        if (diceIcon == null) return;

        bool isFixed = data.faceValues.All(f => f == data.faceValues[0]);

        if (isFixed && fixedNumberSprites != null && fixedNumberSprites.Length >= 6)
        {
            diceIcon.sprite = fixedNumberSprites[data.faceValues[0] - 1];
        }
        else if (data.customDiceShell != null)
        {
            diceIcon.sprite = data.customDiceShell;
        }
        else
        {
            // 코팅 타입에 따른 스프라이트 설정
            switch (data.type)
            {
                case DiceType.Normal: diceIcon.sprite = normalSprite; break;
                case DiceType.Prism: diceIcon.sprite = prismSprite; break;
                case DiceType.Gold: diceIcon.sprite = goldSprite; break;
                case DiceType.Dark: diceIcon.sprite = blackSprite; break;
                case DiceType.Ice: diceIcon.sprite = iceSprite; break;
            }
        }
    }
}