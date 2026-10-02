using UnityEngine;

// 모든 주사위가 공유하는 위성 외형/연출 설정. 기본 에셋은 Resources에서 한 번 읽습니다.
[CreateAssetMenu(fileName = "SatelliteVisualProfile", menuName = "TrickYacht/Visuals/Satellite Profile")]
public sealed class SatelliteVisualProfile : ScriptableObject
{
    [Header("별 PNG (종류별 원본 색상 유지)")]
    public Sprite mercuryStar;
    public Sprite venusStar;
    public Sprite marsStar;
    public Sprite jupiterStar;

    [Header("주사위 크기에 비례한 공통 궤도")]
    [Min(0f)] public float clockwiseDegreesPerSecond = 55f;
    public Vector2 radiusRatio = new Vector2(0.68f, 0.62f);
    // 주사위 이미지의 기준점(Pivot) 차이는 Sprite bounds의 중심으로 보정합니다.
    public Vector2 centerOffsetRatio = Vector2.zero;
    [Range(0.1f, 0.7f)] public float starSizeRatio = 0.32f;

    [Header("뒤쪽 빛과 반짝임 (별 본체 색상은 바꾸지 않음)")]
    [Range(1f, 4f)] public float glowSize = 1.9f;
    [Range(0f, 1f)] public float glowAlpha = 0.48f;
    [Min(0.2f)] public float twinkleSeconds = 1.5f;
    [Range(0f, 0.2f)] public float starPulse = 0.035f;

    [Header("작은 점선 잔상")]
    [Range(0, 24)] public int trailDotCount = 14;
    [Range(0f, 140f)] public float trailArcDegrees = 78f;
    [Range(0.005f, 0.08f)] public float dotSizeRatio = 0.026f;
    [Range(0f, 1f)] public float trailAlpha = 0.7f;

    [Header("빛/잔상 색 (PNG는 흰색 틴트로 원본 유지)")]
    public Color mercuryLight = new Color(0.2f, 0.88f, 1f);
    public Color venusLight = new Color(1f, 0.72f, 0.12f);
    public Color marsLight = new Color(1f, 0.24f, 0.15f);
    public Color jupiterLight = new Color(1f, 0.88f, 0.58f);

    public Sprite GetStar(SatelliteType type)
    {
        switch (type)
        {
            case SatelliteType.Mercury: return mercuryStar;
            case SatelliteType.Venus: return venusStar;
            case SatelliteType.Mars: return marsStar;
            case SatelliteType.Jupiter: return jupiterStar;
            default: return null;
        }
    }

    public Color GetLight(SatelliteType type)
    {
        switch (type)
        {
            case SatelliteType.Mercury: return mercuryLight;
            case SatelliteType.Venus: return venusLight;
            case SatelliteType.Mars: return marsLight;
            case SatelliteType.Jupiter: return jupiterLight;
            default: return Color.white;
        }
    }
}
