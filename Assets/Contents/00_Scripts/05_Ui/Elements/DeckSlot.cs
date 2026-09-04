using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

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

    // 위성 UI 전용
    [Header("위성 UI 설정")]
    public Sprite mercurySprite;
    public Sprite venusSprite;
    public Sprite marsSprite;
    public Sprite jupiterSprite;

    public float satOrbitSpeed = 3f;
    public float satOrbitWidth = 45f;   // UI 픽셀 단위 궤도 가로폭
    public float satOrbitHeight = 15f;  // UI 픽셀 단위 궤도 세로폭
    public float satFrontScale = 0.8f;  // UI에서 앞으로 올 때 크기
    public float satBackScale = 0.4f;   // UI에서 뒤로 갈 때 크기
    public float satBackDarkness = 0.4f;

    public Vector3 satCenterOffset = Vector3.zero;

    private class UISatellite
    {
        public SatelliteType type;
        public GameObject go;
        public Image image;
        public float angle;
    }
    private List<UISatellite> activeSatellites = new List<UISatellite>();

    private DiceData1 currentData;

    // LINQ 대체 및 배열 재할당 방지를 위한 고정 배열
    private int[] animFaces = new int[6];
    private int animUniqueCount = 0;

    private Sprite[] animSprites;
    private int currentAnimIndex;
    private float animTimer;
    private float animInterval = 0.6f;
    private bool isAnimating = false;

    private bool isPrismUI = false;
    private Color baseUIColor = Color.white;
    private bool isUsedState = false;

    private int exactFaceValue = -1;
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

        ClearVFX();

        //파괴하는 대신 빈 리스트를 넘겨서 위성들을 모두 숨김 처리
        UpdateSatellites(new List<SatelliteType>());
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

        //LINQ 제거 및 단일 반복문 최적화
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

        isAnimating = animUniqueCount > 1 && animUniqueCount < 6;
        bool isSpecialDie = isAnimating || isFixed || data.customDiceShell != null;

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
            if (data.diceName == "가짜 주사위") descText.text = "Fake";
            else if (minVal == maxVal) descText.text = minVal.ToString();
            else descText.text = $"{minVal}~{maxVal}";
        }

        //위성 UI 생성
        if (data.activeSatellites == null)
            data.activeSatellites = new List<SatelliteType>();

        UpdateSatellites(data.activeSatellites);
    }

    private void UpdateSatellites(List<SatelliteType> satTypes)
    {
        // 필요한 개수보다 모자라면 껍데기를 추가 생성
        while (activeSatellites.Count < satTypes.Count)
        {
            CreateUISatellitePooled();
        }

        // 만들어둔 위성들을 꺼내서 현재 상태에 맞게 옷을 갈아입히거나 끕니다
        for (int i = 0; i < activeSatellites.Count; i++)
        {
            UISatellite sat = activeSatellites[i];

            if (i < satTypes.Count)
            {
                sat.go.SetActive(true); // 켜기
                sat.type = satTypes[i];

                switch (sat.type)
                {
                    case SatelliteType.Mercury: sat.image.sprite = mercurySprite; break;
                    case SatelliteType.Venus: sat.image.sprite = venusSprite; break;
                    case SatelliteType.Mars: sat.image.sprite = marsSprite; break;
                    case SatelliteType.Jupiter: sat.image.sprite = jupiterSprite; break;
                }

                if (sat.image.sprite != null) sat.image.SetNativeSize();
            }
            else
            {
                sat.go.SetActive(false); // [핵심] 안 쓰는 위성은 파괴하지 않고 숨김 처리
            }
        }
    }

    private void CreateUISatellitePooled()
    {
        // 파괴되지 않고 재사용될 빈 껍데기만 생성
        GameObject satGo = new GameObject("UISatellite_Pooled");

        // [버그 원천 차단] 유니티 에디터 인스펙터 충돌 방지용 투명망토
        satGo.hideFlags = HideFlags.HideAndDontSave;

        satGo.transform.SetParent(filledVisual.transform, false);

        Image img = satGo.AddComponent<Image>();
        img.raycastTarget = false;

        UISatellite newSat = new UISatellite
        {
            go = satGo,
            image = img,
            angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f)
        };

        satGo.transform.localScale = Vector3.one * satFrontScale; // 초기 크기 지정
        activeSatellites.Add(newSat);
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

        if (isAnimating && animSprites != null && animSprites.Length >= 6)
        {
            animTimer += Time.deltaTime;
            if (animTimer >= animInterval)
            {
                animTimer = 0;
                currentAnimIndex = (currentAnimIndex + 1) % animUniqueCount;
                UpdateDisplaySprite();
            }
        }
        //UI 위성 회전 연산
        if (activeSatellites.Count > 0 && diceIcon != null)
        {
            foreach (var sat in activeSatellites)
            {
                if (!sat.go.activeSelf) continue;

                sat.angle += Time.unscaledDeltaTime * satOrbitSpeed;

                float x = Mathf.Cos(sat.angle) * satOrbitWidth;
                float y = Mathf.Sin(sat.angle) * satOrbitHeight;
                float depth = Mathf.Sin(sat.angle);

                float finalX = x;
                float finalY = y;

                switch (sat.type)
                {
                    case SatelliteType.Mercury: break;
                    case SatelliteType.Venus:
                        //UI에서도 좌우 흔들림(finalX)을 0으로 완벽 차단! 위에서 아래로만 떨어짐!
                        finalX = 0f;
                        finalY = -x;
                        break;
                    case SatelliteType.Mars:
                        float cos45 = 0.7071f;
                        finalX = x * cos45 - y * cos45;
                        finalY = x * cos45 + y * cos45;
                        break;
                    case SatelliteType.Jupiter:
                        float cosM45 = 0.7071f; float sinM45 = -0.7071f;
                        finalX = x * cosM45 - y * sinM45;
                        finalY = x * sinM45 + y * cosM45;
                        break;
                }

                // [중심 맞추기] 위치 적용 시 중심점(baseCenter)과 오프셋(satCenterOffset)을 더해줌
                Vector3 baseCenter = diceIcon.transform.localPosition;
                sat.go.transform.localPosition = new Vector3(finalX, finalY, 0f) + baseCenter + satCenterOffset;

                float depth01 = (depth + 1f) / 2f;
                float currentScale = Mathf.Lerp(satFrontScale, satBackScale, depth01);
                sat.go.transform.localScale = new Vector3(currentScale, currentScale, 1f);

                float colorMult = Mathf.Lerp(1f, satBackDarkness, depth01);
                sat.image.color = new Color(colorMult, colorMult, colorMult, 1f);

                // Hierarchy 순서를 변경하여 주사위 이미지(diceIcon) 앞/뒤를 교차
                if (depth > 0) // 뒤로 갈 때
                {
                    if (sat.go.transform.GetSiblingIndex() > diceIcon.transform.GetSiblingIndex())
                    {
                        sat.go.transform.SetSiblingIndex(diceIcon.transform.GetSiblingIndex());
                    }
                }
                else // 앞으로 올 때
                {
                    if (sat.go.transform.GetSiblingIndex() < diceIcon.transform.GetSiblingIndex())
                    {
                        sat.go.transform.SetAsLastSibling();
                    }
                }
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
        if (diceIcon != null && animSprites != null && animSprites.Length > 0)
        {
            int faceValue = animFaces[currentAnimIndex];
            // 배열 크기를 넘어서는 눈금(예: 88)이 들어와도 에러가 나지 않도록 안전하게 묶어줌
            int safeIndex = Mathf.Clamp(faceValue - 1, 0, animSprites.Length - 1);
            diceIcon.sprite = animSprites[safeIndex];
        }
    }

    private void UpdateStaticDisplay(DiceData1 data, bool isFixed)
    {
        if (diceIcon == null) return;

        int faceToShow = exactFaceValue != -1 ? exactFaceValue : data.faceValues[UnityEngine.Random.Range(0, data.faceValues.Length)];

        //커스텀 이미지가 설정되어 있다면 무조건 최우선으로 띄워줌 (88 주사위 완벽 대응)
        if (data.customFaceSprites != null && data.customFaceSprites.Length > 0)
        {
            // 커스텀 이미지가 1개밖에 없다면 무조건 0번째 이미지를 띄움 (에러 방지)
            int safeIndex = (data.customFaceSprites.Length == 1) ? 0 : Mathf.Clamp(faceToShow - 1, 0, data.customFaceSprites.Length - 1);
            diceIcon.sprite = data.customFaceSprites[safeIndex];
            return; // 여기서 함수를 끝내서 숫자 6으로 바뀌는 현상을 막음
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