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

    [Header("주사위 기본 눈금 이미지")]
    public Sprite[] defaultFaceSprites;

    [Header("주사위 타입별 이미지 (코팅용 백업)")]
    public Sprite normalSprite;
    public Sprite prismSprite;
    public Sprite goldSprite;
    public Sprite blackSprite;
    public Sprite iceSprite;

    [Header("특수 주사위용 이미지 (추가)")]
    public Sprite[] fixedNumberSprites;

    [Header("코팅 VFX 프리팹 (UI용)")]
    public GameObject iceVFXPrefab;
    public GameObject darkVFXPrefab;
    public GameObject goldVFXPrefab;
    public GameObject prismVFXPrefab;

    [Header("VFX 위치 및 크기 보정")]
    public Vector3 vfxLocalOffset = new Vector3(0f, 0f, -50f); // UI에서 파티클이 묻히지 않게 Z축을 확실히 앞으로 당김
    public float vfxScale = 100f; // ★ UI에서는 1이 1픽셀이므로 기본 크기를 100배로 확 키움!

    private DiceData1 currentData;
    private int[] animFaces;
    private Sprite[] animSprites;
    private int currentAnimIndex;
    private float animTimer;
    private float animInterval = 0.6f;
    private bool isAnimating = false;

    private bool isPrismUI = false;
    private Color baseUIColor = Color.white;
    private bool isUsedState = false;

    private int exactFaceValue = -1;
    private GameObject currentVFX; // 현재 띄워진 파티클 저장용
    private DiceType lastVFXType = DiceType.Normal; // 최적화: 현재 띄워진 파티클의 종류 기억

    public void SetEmpty()
    {
        emptyVisual.SetActive(true);
        filledVisual.SetActive(false);
        currentData = null;
        isAnimating = false;
        isPrismUI = false;
        exactFaceValue = -1;

        ClearVFX();
    }

    public void SetDice(DiceData1 data, bool isUsed, int exactValue = -1)
    {
        currentData = data;
        isUsedState = isUsed;
        exactFaceValue = exactValue;
        emptyVisual.SetActive(false);
        filledVisual.SetActive(true);

        isPrismUI = false;
        baseUIColor = Color.white;

        animTimer = 0f;
        currentAnimIndex = 0;

        // LINQ 최적화: 중복 제거 및 애니메이션 여부 판단
        bool isFixed = true;
        int firstFace = data.faceValues[0];
        int uniqueCount = 1;
        animFaces = new int[6]; // 최대 6개
        animFaces[0] = firstFace;

        for (int i = 1; i < data.faceValues.Length; i++)
        {
            if (data.faceValues[i] != firstFace) isFixed = false;

            // 중복되지 않은 숫자 찾기 (Distinct 대체)
            bool isUnique = true;
            for (int j = 0; j < uniqueCount; j++)
            {
                if (animFaces[j] == data.faceValues[i])
                {
                    isUnique = false;
                    break;
                }
            }
            if (isUnique)
            {
                animFaces[uniqueCount] = data.faceValues[i];
                uniqueCount++;
            }
        }

        System.Array.Resize(ref animFaces, uniqueCount); // 실제 고유 숫자 개수만큼 배열 자르기

        isAnimating = uniqueCount > 1 && uniqueCount < 6;
        bool isSpecialDie = isAnimating || isFixed || data.customDiceShell != null;

        // 코팅 색상 보정 로직 (파티클과 별개로 주사위 색상도 맞춤)
        if (data.isCoated)
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
            animSprites = (data.customFaceSprites != null && data.customFaceSprites.Length >= 6)
                          ? data.customFaceSprites : fixedNumberSprites;

            UpdateDisplaySprite();
        }
        else
        {
            UpdateStaticDisplay(data, isFixed);
        }

        // 최적화: 코팅 종류가 바뀌었을 때만 파티클 새로 고침
        if (data.isCoated)
        {
            if (currentVFX == null || lastVFXType != data.type)
            {
                ClearVFX();
                ApplyVFX(data.type);
            }
        }
        else
        {
            ClearVFX();
        }

        if (descText != null)
        {
            // LINQ 최적화: Min, Max 직접 계산
            int minVal = data.faceValues[0];
            int maxVal = data.faceValues[0];
            for (int i = 1; i < data.faceValues.Length; i++)
            {
                if (data.faceValues[i] < minVal) minVal = data.faceValues[i];
                if (data.faceValues[i] > maxVal) maxVal = data.faceValues[i];
            }

            if (minVal == maxVal)
                descText.text = minVal.ToString();
            else
                descText.text = $"{minVal}~{maxVal}";
        }
    }

    private void ApplyVFX(DiceType type)
    {
        GameObject targetPrefab = null;

        switch (type)
        {
            case DiceType.Prism: targetPrefab = prismVFXPrefab; break;
            case DiceType.Gold: targetPrefab = goldVFXPrefab; break;
            case DiceType.Dark: targetPrefab = darkVFXPrefab; break;
            case DiceType.Ice: targetPrefab = iceVFXPrefab; break;
        }

        if (targetPrefab == null) return;

        // diceIcon을 부모로 삼아 UI 위치에 생성
        currentVFX = Instantiate(targetPrefab, diceIcon.transform);

        // Z축만 살짝 앞으로 당기고, 크기는 인스펙터의 vfxScale 변수를 따르도록 수정
        currentVFX.transform.localPosition = new Vector3(0f, 0f, -50f);
        currentVFX.transform.localRotation = Quaternion.identity;
        currentVFX.transform.localScale = Vector3.one * vfxScale; // 100 고정 삭제!

        lastVFXType = type;

        // 생성된 파티클과 그 자식들의 레이어를 모두 'UI'로 변경
        int uiLayer = LayerMask.NameToLayer("UI");
        Transform[] allChildren = currentVFX.GetComponentsInChildren<Transform>(true);
        foreach (var child in allChildren)
        {
            child.gameObject.layer = uiLayer;
        }

        // 파티클이 UI 슬롯(Canvas) 크기에 맞춰서 얌전히 작아지도록 Hierarchy 모드로 강제
        ParticleSystem[] particleSystems = currentVFX.GetComponentsInChildren<ParticleSystem>(true);
        foreach (var ps in particleSystems)
        {
            var main = ps.main;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        }

        // 파티클이 UI 창(Canvas) 뒤로 숨지 않게 렌더링 순서만 끌어올림
        ParticleSystemRenderer[] renderers = currentVFX.GetComponentsInChildren<ParticleSystemRenderer>(true);
        foreach (var r in renderers)
        {
            r.sortingLayerName = "UI";
            r.sortingOrder = 30000;
        }
    }

    private void ClearVFX()
    {
        if (currentVFX != null)
        {
            if (Application.isPlaying)
            {
                Destroy(currentVFX);
            }
            else
            {
                DestroyImmediate(currentVFX);
            }
            currentVFX = null;
            lastVFXType = DiceType.Normal; // 타입 초기화
        }
    }

    private void Update()
    {
        if (isPrismUI && diceIcon != null)
        {
            float hue = Mathf.Repeat(Time.unscaledTime * 0.6f, 1f);
            Color prismColor = Color.HSVToRGB(hue, 0.55f, 1f);
            UpdateDisplayColor(prismColor);
        }

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

            if (!isPrismUI && c != Color.white)
            {
                float brightness = (c.r + c.g + c.b) / 3f;
                finalColor = brightness < 0.3f ? Color.Lerp(Color.white, c, 0.5f) : Color.Lerp(Color.white, c, 0.7f);
            }

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

        int faceToShow = exactFaceValue != -1 ? exactFaceValue : data.faceValues[UnityEngine.Random.Range(0, data.faceValues.Length)];
        faceToShow = Mathf.Clamp(faceToShow, 1, 6);

        if (isFixed && fixedNumberSprites != null && fixedNumberSprites.Length >= 6)
        {
            diceIcon.sprite = fixedNumberSprites[faceToShow - 1];
        }
        else if (data.customFaceSprites != null && data.customFaceSprites.Length >= 6)
        {
            diceIcon.sprite = data.customFaceSprites[faceToShow - 1];
        }
        else if (defaultFaceSprites != null && defaultFaceSprites.Length >= 6)
        {
            // VFX가 덧씌워질 것이므로 배경은 항상 투명한 기본 주사위 눈금을 씀
            diceIcon.sprite = defaultFaceSprites[faceToShow - 1];
        }
    }
}