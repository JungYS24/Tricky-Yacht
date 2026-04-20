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

    [Header("주사위 타입별 이미지 (코팅용)")]
    public Sprite normalSprite;
    public Sprite prismSprite;
    public Sprite goldSprite;
    public Sprite blackSprite;
    public Sprite iceSprite;

    [Header("특수 주사위용 이미지 (추가)")]
    public Sprite[] fixedNumberSprites; // 고정 주사위용 1~6 아라비아 숫자 이미지

    private DiceData1 currentData;
    private int[] animFaces;
    private Sprite[] animSprites;
    private int currentAnimIndex;
    private float animTimer;
    private float animInterval = 0.6f;
    private bool isAnimating = false;

    // --- 특수 주사위 코팅 UI 제어 변수 ---
    private bool isPrismUI = false;
    private Color baseUIColor = Color.white;
    private bool isUsedState = false;

    public void SetEmpty()
    {
        emptyVisual.SetActive(true);
        filledVisual.SetActive(false);
        currentData = null;
        isAnimating = false;
        isPrismUI = false;
    }

    public void SetDice(DiceData1 data, bool isUsed)
    {
        currentData = data;
        isUsedState = isUsed;
        emptyVisual.SetActive(false);
        filledVisual.SetActive(true);

        isPrismUI = false;
        baseUIColor = Color.white;

        // 기존에 돌고 있던 애니메이션이 있다면 중지 (초기화)
        animTimer = 0f;
        currentAnimIndex = 0;

        // 주사위 눈금 중복 제거
        int[] uniqueFaces = data.faceValues.Distinct().ToArray();

        // 눈금 종류가 2개~5개 사이면 애니메이션 (홀/짝, 하이/로우 등)
        isAnimating = uniqueFaces.Length > 1 && uniqueFaces.Length < 6;

        // 특수 주사위 종류 판별 로직 
        bool isFixed = data.faceValues.All(f => f == data.faceValues[0]);
        bool isSpecialDie = isAnimating || isFixed || data.customDiceShell != null;

        // 특수 주사위 코팅 색상 설정
        if (isSpecialDie && data.isCoated)
        {
            if (data.type == DiceType.Prism)
            {
                isPrismUI = true;
            }
            else
            {
                baseUIColor = data.diceColor;
            }
        }

        UpdateDisplayColor(baseUIColor);

        if (isAnimating)
        {
            animFaces = uniqueFaces;

            // 커스텀 눈금이 있으면 그것을, 없으면 기본 숫자를 사용
            animSprites = (data.customFaceSprites != null && data.customFaceSprites.Length >= 6)
                          ? data.customFaceSprites : fixedNumberSprites;

            UpdateDisplaySprite();
        }
        else
        {
            // 애니메이션 대상이 아닐 때 (일반 주사위 및 고정 주사위)
            UpdateStaticDisplay(data, isFixed);
        }

        // 주사위 눈금 정보 텍스트 표시
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

    private void Update()
    {
        // 프리즘 주사위 UI 무지개색 애니메이션
        if (isPrismUI && diceIcon != null)
        {
            float hue = Mathf.Repeat(Time.unscaledTime * 0.6f, 1f);
            Color prismColor = Color.HSVToRGB(hue, 0.55f, 1f);
            UpdateDisplayColor(prismColor);
        }

        // 애니메이션 코루틴을 대체하는 애니메이션 업데이트 로직
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

    private void UpdateDisplayColor(Color c)
    {
        if (diceIcon != null)
        {
            Color finalColor = c;

            // 특수 주사위 코팅 가독성을 위한 파스텔 톤 보정
            if (!isPrismUI && c != Color.white)
            {
                float brightness = (c.r + c.g + c.b) / 3f;

                if (brightness < 0.3f)
                {
                    // 다크 코팅 등 너무 어두운 색상은 흰색을 섞어 가독성 확보
                    finalColor = Color.Lerp(Color.white, c, 0.5f);
                }
                else
                {
                    // 일반 색상들도 눈금을 가리지 않게 파스텔 톤으로 부드럽게 조정
                    finalColor = Color.Lerp(Color.white, c, 0.7f);
                }
            }

            // 사용된 주사위는 알파값(투명도)을 낮춰서 비활성화 표현
            finalColor.a = isUsedState ? 0.4f : 1f;
            diceIcon.color = finalColor;
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

    private void UpdateStaticDisplay(DiceData1 data, bool isFixed)
    {
        if (diceIcon == null) return;

        if (isFixed && fixedNumberSprites != null && fixedNumberSprites.Length >= 6)
        {
            // 고정 주사위라면 해당 숫자의 아라비아 숫자 이미지 표시
            diceIcon.sprite = fixedNumberSprites[data.faceValues[0] - 1];
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
    }
}