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
    //Z축을 양수(+50)로 설정해야 UI 뒤로 들어감
    public Vector3 vfxLocalOffset = new Vector3(0f, 0f, 50f);
    public float vfxScale = 2.5f;

    private SatelliteUIVisual satelliteVisual;
    private StampUIVisual stampVisual;

    private DiceData1 currentData;

    // LINQ 대체 및 배열 재할당 방지를 위한 고정 배열
    private int[] animFaces = new int[6];
    private int animUniqueCount = 0;
    private bool animUseFaceIndex = false;

    private Sprite[] animSprites;
    private int currentAnimIndex;
    private float animTimer;
    private float animInterval = 0.6f;
    private bool isAnimating = false;

    private bool isPrismUI = false;
    private Color baseUIColor = Color.white;
    private bool isUsedState = false;

    private int exactFaceValue = -1;
    private int exactFaceIndex = -1;
    private GameObject currentVFX;
    private DiceType lastVFXType = DiceType.Normal;



    public void SetEmpty()
    {
        emptyVisual.SetActive(true);
        filledVisual.SetActive(false);
        currentData = null;
        isAnimating = false;
        isPrismUI = false;
        exactFaceValue = -1;
        exactFaceIndex = -1;

        ClearVFX();

        // 빈 슬롯은 별 오브젝트를 숨겨 두고 다음 표시 때 재사용합니다.
        if (satelliteVisual != null) satelliteVisual.Bind(null);
        if (stampVisual != null) stampVisual.Bind(null);
    }

    public void SetDice(DiceData1 data, bool isUsed, int exactValue = -1, int faceIndex = -1)
    {
        currentData = data;
        isUsedState = isUsed;
        exactFaceValue = exactValue;
        exactFaceIndex = faceIndex;
        emptyVisual.SetActive(false);
        filledVisual.SetActive(true);

        isPrismUI = false;
        baseUIColor = Color.white;

        animTimer = 0f;
        currentAnimIndex = 0;
        animUseFaceIndex = false;

        bool hasPerFaceSprites = data.customFaceSprites != null && data.customFaceSprites.Length >= 6;
        bool completePerFace = DiceData1.HasCompleteFaceSprites(data.customFaceSprites);

        int minVal = data.faceValues[0];
        int maxVal = data.faceValues[0];
        bool isFixed = true;

        animFaces[0] = data.faceValues[0];
        animUniqueCount = 1;

        for (int i = 1; i < data.faceValues.Length; i++)
        {
            int val = data.faceValues[i];

            if (val < minVal) minVal = val;
            if (val > maxVal) maxVal = val;

            if (val != data.faceValues[0]) isFixed = false;

            bool isUnique = true;
            for (int j = 0; j < animUniqueCount; j++)
            {
                if (animFaces[j] == val)
                {
                    isUnique = false;
                    break;
                }
            }

            if (isUnique)
            {
                animFaces[animUniqueCount] = val;
                animUniqueCount++;
            }
        }

        // 6칸이 채워졌거나 로우·하이 면 구성이면 면 번호로 돌려 보여 준다.
        // 스프라이트가 비어 있는 구세이브·조우자 변환도 1·2·3 / 4·5·6 순서로 나온다.
        bool rollerFaces = SpecialDiceSprites.IsLowHigh(data);
        if ((completePerFace || rollerFaces) && exactFaceValue == -1 && exactFaceIndex < 0)
        {
            for (int i = 0; i < 6; i++) animFaces[i] = i;
            animUniqueCount = 6;
            animUseFaceIndex = true;
            isAnimating = true;
        }
        else
        {
            isAnimating = animUniqueCount > 1 && animUniqueCount < 6;
        }
        bool isSpecialDie = isAnimating || isFixed || data.customDiceShell != null || hasPerFaceSprites;

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
            if (SpecialDiceSprites.IsOddEven(data))
                animSprites = null;

            UpdateDisplaySprite();
        }
        else
        {
            UpdateStaticDisplay(data, isFixed);
        }

        // 파티클 재활용 최적화
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
            //가짜 주사위 일 떄 숫자 대신 Fake 출력
            if (data.diceName == "가짜 주사위")
                descText.text = LocalizationManager.GetUi("UI_FAKE_DICE", "Fake");
            else if (minVal == maxVal) descText.text = minVal.ToString();
            else descText.text = $"{minVal}~{maxVal}";
        }

        // 덱·강화 선택창이 같은 별 연출을 사용합니다. 프리팹 연결은 필요 없습니다.
        if (diceIcon != null)
        {
            if (satelliteVisual == null) satelliteVisual = diceIcon.GetComponent<SatelliteUIVisual>();
            if (satelliteVisual == null) satelliteVisual = diceIcon.gameObject.AddComponent<SatelliteUIVisual>();
            satelliteVisual.Bind(data);

            // 스탬프는 아이콘 가운데에 연한 자국으로 겹칩니다. 사용한 주사위는 아이콘처럼 더 흐리게.
            if (stampVisual == null) stampVisual = diceIcon.GetComponent<StampUIVisual>();
            if (stampVisual == null) stampVisual = diceIcon.gameObject.AddComponent<StampUIVisual>();
            stampVisual.Bind(data, isUsed ? 0.4f : 1f);
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

        // [핵심 최적화 & 순서 수정] diceIcon의 부모(filledVisual) 아래에 생성
        currentVFX = Instantiate(targetPrefab, filledVisual.transform);

        // 위치를 diceIcon과 동일하게 맞춘 후, Z축을 양수(+50)로 밀어서 뒤로 보냄
        currentVFX.transform.localPosition = diceIcon.transform.localPosition + vfxLocalOffset;
        currentVFX.transform.localRotation = Quaternion.identity;
        currentVFX.transform.localScale = Vector3.one * vfxScale;

        //하이라키 상에서 무조건 첫 번째(맨 위)로 올려서 렌더링 순서를 가장 뒤(주사위 밑)로 깔아버림
        currentVFX.transform.SetAsFirstSibling();

        lastVFXType = type;

        int uiLayer = LayerMask.NameToLayer("UI");
        Transform[] allChildren = currentVFX.GetComponentsInChildren<Transform>(true);
        foreach (var child in allChildren)
        {
            child.gameObject.layer = uiLayer;
        }

        ParticleSystem[] particleSystems = currentVFX.GetComponentsInChildren<ParticleSystem>(true);
        foreach (var ps in particleSystems)
        {
            var main = ps.main;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        }

        //부모 캔버스의 Sorting Order를 그대로 따라가게 얌전하게 설정
        Canvas parentCanvas = GetComponentInParent<Canvas>();
        int order = parentCanvas != null ? parentCanvas.sortingOrder : 0;
        string layerName = parentCanvas != null ? parentCanvas.sortingLayerName : "UI";

        ParticleSystemRenderer[] renderers = currentVFX.GetComponentsInChildren<ParticleSystemRenderer>(true);
        foreach (var r in renderers)
        {
            r.sortingLayerName = layerName;
            r.sortingOrder = order; // 캔버스와 동일한 순서를 주면, 위에서 설정한 Hierarchy(SetAsFirstSibling)와 Z축(+50)이 작동해서 완벽하게 뒤로 감!
        }
    }

    private void ClearVFX()
    {
        if (currentVFX != null)
        {
            // [버그 방지] UI 캔버스와 연결 끊고 에디터 감시망에서 제외
            currentVFX.hideFlags = HideFlags.HideAndDontSave;
            currentVFX.transform.SetParent(null);
            currentVFX.SetActive(false);

            if (Application.isPlaying)
            {
                Destroy(currentVFX);
            }
            else
            {
                DestroyImmediate(currentVFX);
            }
            currentVFX = null;
            lastVFXType = DiceType.Normal;
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

        if (isAnimating && (SpecialDiceSprites.IsOddEven(currentData) || (animSprites != null && animSprites.Length >= 6)))
        {
            animTimer += Time.deltaTime;
            if (animTimer >= animInterval)
            {
                animTimer = 0;
                currentAnimIndex = (currentAnimIndex + 1) % animUniqueCount;
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
        if (diceIcon == null) return;

        int faceValue = animFaces[currentAnimIndex];
        int key = animUseFaceIndex ? faceValue : faceValue - 1;
        if (SpecialDiceSprites.TryGetSpecialFaceSprite(currentData, key, out Sprite special) && special != null)
        {
            diceIcon.sprite = special;
            return;
        }

        if (animSprites != null && animSprites.Length > 0)
        {
            int safeIndex = animUseFaceIndex
                ? Mathf.Clamp(faceValue, 0, animSprites.Length - 1)
                : Mathf.Clamp(faceValue - 1, 0, animSprites.Length - 1);
            Sprite next = animSprites[safeIndex];
            if (next != null)
                diceIcon.sprite = next;
        }
    }

    private void UpdateStaticDisplay(DiceData1 data, bool isFixed)
    {
        if (diceIcon == null) return;

        int faceToShow = exactFaceValue != -1 ? exactFaceValue : data.faceValues[UnityEngine.Random.Range(0, data.faceValues.Length)];

        int specialKey = exactFaceIndex >= 0 ? exactFaceIndex : Mathf.Clamp(faceToShow - 1, 0, 5);
        if (SpecialDiceSprites.TryGetSpecialFaceSprite(data, specialKey, out Sprite special) && special != null)
        {
            diceIcon.sprite = special;
            return;
        }

        //기존 1~6 일반/고정 주사위 로직

        //하한선을 1에서 0으로 변경
        faceToShow = Mathf.Clamp(faceToShow, 0, 6);

        //0일 때 가짜 이미지 띄우기
        if (faceToShow == 0 && data.customFaceSprites != null && data.customFaceSprites.Length > 0)
        {
            diceIcon.sprite = data.customFaceSprites[0];
        }
        else if (isFixed && fixedNumberSprites != null && fixedNumberSprites.Length >= 6 && faceToShow > 0)
        {
            diceIcon.sprite = fixedNumberSprites[faceToShow - 1];
        }
        else if (defaultFaceSprites != null && defaultFaceSprites.Length >= 6 && faceToShow > 0)
        {
            diceIcon.sprite = defaultFaceSprites[faceToShow - 1];
        }
    }
}