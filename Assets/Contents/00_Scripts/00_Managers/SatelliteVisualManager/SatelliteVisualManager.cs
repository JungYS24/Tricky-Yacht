using UnityEngine;
using System.Collections.Generic;

public class SatelliteVisualManager : MonoBehaviour
{
    [Header("위성 스프라이트 (행성 이미지)")]
    public Sprite mercurySprite; // 수성
    public Sprite venusSprite;   // 금성
    public Sprite marsSprite;    // 화성
    public Sprite jupiterSprite; // 목성

    [Header("궤도 설정")]
    public float orbitSpeed = 3f;      // 공전 속도
    public float orbitWidth = 1.2f;    // 궤도 가로폭
    public float orbitHeight = 0.35f;  // 궤도 세로폭

    [Header("궤도 중심 영점 조절")]
    // 주사위 이미지의 기준점(Pivot) 차이로 인해 궤도가 쏠리는 현상을 보정합니다.
    // 인스펙터에서 X, Y 값을 조금씩 조절하며 정중앙을 맞춰보세요.
    public Vector3 centerOffset = new Vector3(0f, 0.2f, 0f);

    [Header("원근감(3D) 설정")]
    public float frontScale = 0.4f;    // 앞으로 올 때 크기
    public float backScale = 0.2f;     // 뒤로 갈 때 크기
    public float backDarkness = 0.4f;  // 뒤로 갈 때 어두워지는 정도

    [Header("꼬리(트레일) 이펙트 설정")]
    public Material trailMaterial;     // 꼬리에 쓰일 재질 (인스펙터 할당 권장)
    public float trailTime = 0.4f;     // 꼬리가 유지되는 시간 (길이)
    public float trailStartWidth = 0.15f; // 꼬리 시작 두께
    public float trailEndWidth = 0.0f;    // 꼬리 끝 두께

    private Dice dice;
    private SpriteRenderer diceSpriteRenderer;

    // 위성과 꼬리 이펙트를 함께 묶어서 관리하는 클래스
    private class SatelliteInstance
    {
        public SatelliteType type;
        public GameObject go;
        public SpriteRenderer sr;
        public TrailRenderer tr; // 트레일 렌더러 추가
        public float angle;
    }

    private List<SatelliteInstance> activeInstances = new List<SatelliteInstance>();

    void Awake()
    {
        dice = GetComponent<Dice>();
        diceSpriteRenderer = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        if (dice == null || dice.myData == null) return;

        if (dice.myData.activeSatellites != null &&
            dice.myData.activeSatellites.Count != activeInstances.Count)
        {
            RefreshSatellites();
        }

        UpdateOrbits();
    }

    private void RefreshSatellites()
    {
        foreach (var inst in activeInstances)
        {
            if (inst.go != null) Destroy(inst.go);
        }
        activeInstances.Clear();

        if (dice.myData.activeSatellites == null) return;

        for (int i = 0; i < dice.myData.activeSatellites.Count; i++)
        {
            SatelliteType type = dice.myData.activeSatellites[i];

            GameObject satGo = new GameObject("Satellite_" + type.ToString());
            satGo.transform.SetParent(transform);
            satGo.transform.localPosition = centerOffset; // 시작 위치를 오프셋으로 지정

            // 1. 스프라이트 렌더러 세팅
            SpriteRenderer sr = satGo.AddComponent<SpriteRenderer>();
            Color satColor = Color.white; // 꼬리 색상용 변수

            switch (type)
            {
                case SatelliteType.Mercury:
                    sr.sprite = mercurySprite;
                    satColor = new Color(0.3f, 0.8f, 1f); // 하늘색
                    break;
                case SatelliteType.Venus:
                    sr.sprite = venusSprite;
                    satColor = new Color(1f, 0.9f, 0.2f); // 노란색
                    break;
                case SatelliteType.Mars:
                    sr.sprite = marsSprite;
                    satColor = new Color(1f, 0.4f, 0.3f); // 붉은색
                    break;
                case SatelliteType.Jupiter:
                    sr.sprite = jupiterSprite;
                    satColor = new Color(0.8f, 0.6f, 0.4f); // 주황/갈색
                    break;
            }

            // 2. 트레일 렌더러(꼬리) 세팅
            TrailRenderer tr = satGo.AddComponent<TrailRenderer>();
            tr.time = trailTime;
            tr.startWidth = trailStartWidth;
            tr.endWidth = trailEndWidth;

            // 머티리얼이 비어있으면 유니티 기본 스프라이트 재질을 사용
            if (trailMaterial != null) tr.material = trailMaterial;
            else tr.material = new Material(Shader.Find("Sprites/Default"));

            // 꼬리의 색상이 서서히 투명해지도록 그라디언트 적용
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] { new GradientColorKey(satColor, 0.0f), new GradientColorKey(satColor, 1.0f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0.8f, 0.0f), new GradientAlphaKey(0.0f, 1.0f) }
            );
            tr.colorGradient = gradient;

            SatelliteInstance newInst = new SatelliteInstance
            {
                type = type,
                go = satGo,
                sr = sr,
                tr = tr,
                angle = Random.Range(0f, Mathf.PI * 2f)
            };

            activeInstances.Add(newInst);
        }
    }

    private void UpdateOrbits()
    {
        int baseOrder = diceSpriteRenderer.sortingOrder;

        foreach (var inst in activeInstances)
        {
            inst.angle += Time.deltaTime * orbitSpeed;

            float x = Mathf.Cos(inst.angle) * orbitWidth;
            float y = Mathf.Sin(inst.angle) * orbitHeight;
            float depth = Mathf.Sin(inst.angle);

            float finalX = x;
            float finalY = y;

            switch (inst.type)
            {
                case SatelliteType.Mercury: break;
                case SatelliteType.Venus:
                    // [방향 반전] 금성이 위에서 아래로 떨어지게 만듭니다
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

            // 계산된 최종 위치에 영점(centerOffset)을 더해줍니다.
            inst.go.transform.localPosition = new Vector3(finalX, finalY, 0f) + centerOffset;

            float depth01 = (depth + 1f) / 2f;
            float currentScale = Mathf.Lerp(frontScale, backScale, depth01);
            inst.go.transform.localScale = new Vector3(currentScale, currentScale, 1f);

            float colorMult = Mathf.Lerp(1f, backDarkness, depth01);
            inst.sr.color = new Color(colorMult, colorMult, colorMult, 1f);

            // 주사위 기준 앞/뒤 레이어 렌더링 정렬 (위성 본체와 꼬리 동시 적용)
            if (depth > 0)
            {
                inst.sr.sortingOrder = baseOrder - 1;
                if (inst.tr != null) inst.tr.sortingOrder = baseOrder - 2;
            }
            else
            {
                inst.sr.sortingOrder = baseOrder + 2;
                if (inst.tr != null) inst.tr.sortingOrder = baseOrder + 1;
            }
        }
    }
}