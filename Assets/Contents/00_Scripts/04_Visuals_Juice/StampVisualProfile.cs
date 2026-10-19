using UnityEngine;

// 스탬프 표시(주사위 가운데 연한 자국)와 도장 찍기 연출에 쓰는 이미지·수치. 기본 에셋은 Resources에서 한 번 읽습니다.
// 이미지가 비어 있으면 그 표시·연출만 건너뛰고 효과는 그대로입니다.
[CreateAssetMenu(fileName = "StampVisualProfile", menuName = "TrickYacht/Visuals/Stamp Profile")]
public sealed class StampVisualProfile : ScriptableObject
{
    [Header("스탬프 문양")]
    public Sprite heart;
    public Sprite clover;
    public Sprite diamond;
    public Sprite spade;

    [Header("주사위 가운데 연한 자국 (테이블 위 주사위·덱 화면·선택창 공통)")]
    [Tooltip("자국 그림 크기. 주사위 그림을 1로 본 비율. 0.7이면 동그라미가 주사위 몸체 폭의 약 70%. 0.5·1이 픽셀이 가장 고르게 보임")]
    [Range(0.2f, 1f)] public float markSize = 0.7f;
    [Tooltip("0이면 안 보이고 1이면 원래 색 그대로")]
    [Range(0f, 1f)] public float markOpacity = 0.5f;
    [Tooltip("주사위 가운데에서 옮기는 거리(주사위 크기 비율). 기본은 정가운데")]
    public Vector2 markOffset = Vector2.zero;

    [Header("자국 그리는 방식")]
    [Tooltip("도장 잉크처럼 바탕에 곱해서 칠하는 머티리얼(M_StampInk). 주사위 눈이 가려지지 않음. 비우면 일반 반투명으로 덮음")]
    public Material inkMaterial;
    [Tooltip("문양 바깥쪽 테두리만 그리는 머티리얼(M_StampOutline). 비우면 테두리 없음")]
    public Material outlineMaterial;

    [Header("자국 밝은 테두리 (코팅으로 어둡거나 색이 있는 주사위에서도 모양이 보이게)")]
    [Tooltip("문양 바깥쪽 테두리 진하기. 0이면 끔")]
    [Range(0f, 1f)] public float edgeOpacity = 0.5f;
    [Tooltip("테두리 두께(주사위 그림 픽셀)")]
    [Range(0.5f, 3f)] public float edgeWidth = 1f;
    [Tooltip("테두리 색")]
    public Color edgeColor = new Color(1f, 0.98f, 0.9f, 1f);

    [Header("도장 찍기 연출 (스탬프 선택창)")]
    [Tooltip("모든 연출 시간에 곱하는 값. 1이면 기본(약 1.5초), 클수록 천천히")]
    [Min(0.2f)] public float durationScale = 1f;
    [Tooltip("내려찍는 도장 그림 (정면 모습, 찍는 면이 아래)")]
    public Sprite sealTool;
    [Tooltip("도장 크기. 주사위 아이콘을 1로 본 비율. 찍는 면 폭이 연한 자국 동그라미와 비슷해지는 값")]
    public float sealSize = 1.09f;
    [Tooltip("도장 그림 가운데에서 찍는 면 바닥까지의 거리 (도장 그림 크기 비율). 그림을 바꾸면 맞춰 줄 것")]
    public float sealFaceOffset = 0.406f;
    [Tooltip("찍는 면 폭 (도장 그림 크기 비율). 잉크 줄과 그림자 폭에 쓴다")]
    public float sealFaceWidth = 0.5625f;
    [Tooltip("나타날 때 기울기(도). 조준하며 흔들리다 똑바로 서서 내려찍는다")]
    public float liftAngle = -10f;
    [Tooltip("도장이 나타나 떠오르는 시간(초)")]
    public float liftDuration = 0.35f;
    [Tooltip("떠서 조준하듯 흔들리며 멈추는 시간(초)")]
    public float aimDuration = 0.15f;
    [Tooltip("내려찍는 시간(초). 너무 길면 쾅 하는 느낌이 줄어듦")]
    public float slamDuration = 0.12f;
    [Tooltip("찍은 채로 누르고 있는 시간(초)")]
    public float pressHold = 0.25f;
    [Tooltip("도장을 떼고 사라지는 시간(초)")]
    public float releaseDuration = 0.35f;
    [Tooltip("갓 찍힌 진한 잉크가 연한 자국으로 마르는 시간(초)")]
    public float inkDryDuration = 0.6f;
    [Tooltip("찍는 순간 도장이 눌리는 세로 배율")]
    [Range(0.6f, 1f)] public float sealSquash = 0.85f;
    [Tooltip("찍는 순간 주사위가 눌리는 세로 배율")]
    [Range(0.6f, 1f)] public float squash = 0.92f;
    [Tooltip("찍는 순간 선택창이 흔들리는 거리(px)")]
    public float shake = 6f;

    [Header("잉크 색 (찍을 때 튀는 잉크 방울)")]
    public Color heartInk = new Color32(199, 2, 5, 255);
    public Color cloverInk = new Color32(15, 179, 66, 255);
    public Color diamondInk = new Color32(17, 66, 143, 255);
    public Color spadeInk = new Color32(55, 53, 51, 255);

    static StampVisualProfile profile;
    static bool loaded;
    static Material outlineInstance;

    // 테두리 두께를 넣은 테두리 머티리얼. 에셋 원본을 바꾸지 않도록 실행 중에는 복사본을 쓴다.
    // 스탬프 그림은 64px 한 장(아틀라스 아님)이라 주사위 그림 1픽셀 = 1 / (64 × markSize) UV.
    public Material GetOutlineMaterial()
    {
        if (outlineMaterial == null) return null;
        if (outlineInstance == null || outlineInstance.shader != outlineMaterial.shader)
            outlineInstance = new Material(outlineMaterial) { name = outlineMaterial.name + " (Runtime)", hideFlags = HideFlags.DontSave };
        outlineInstance.SetFloat("_OutlineUV", edgeWidth / (64f * Mathf.Max(0.05f, markSize)));
        return outlineInstance;
    }

    public static StampVisualProfile Profile
    {
        get
        {
            if (!loaded)
            {
                profile = Resources.Load<StampVisualProfile>("StampVisualProfile");
                loaded = true;
            }
            return profile;
        }
    }

    public static Sprite GetSprite(StampType type)
    {
        StampVisualProfile p = Profile;
        if (p == null) return null;
        switch (type)
        {
            case StampType.Heart: return p.heart;
            case StampType.Clover: return p.clover;
            case StampType.Diamond: return p.diamond;
            case StampType.Spade: return p.spade;
            default: return null;
        }
    }

    public Color GetInk(StampType type)
    {
        switch (type)
        {
            case StampType.Heart: return heartInk;
            case StampType.Clover: return cloverInk;
            case StampType.Diamond: return diamondInk;
            case StampType.Spade: return spadeInk;
            default: return Color.white;
        }
    }
}
