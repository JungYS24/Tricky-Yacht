using UnityEngine;

// 스탬프 표시(주사위 그림에 새긴 문양)와 도장 찍기 연출에 쓰는 이미지·수치. 기본 에셋은 Resources에서 한 번 읽습니다.
// 새김 문양이 비어 있으면 그 표시·연출만 건너뛰고 효과는 그대로입니다.
[CreateAssetMenu(fileName = "StampVisualProfile", menuName = "TrickYacht/Visuals/Stamp Profile")]
public sealed class StampVisualProfile : ScriptableObject
{
    [Header("스탬프 문양 (64px 원본 그림. 주사위 표시는 아래 새김 문양을 쓴다)")]
    public Sprite heart;
    public Sprite clover;
    public Sprite diamond;
    public Sprite spade;

    [Header("주사위 그림에 새기는 문양 (26×26, 흰색 = 문양 픽셀). 64px 주사위 기준, 128px이면 2배로 키운다")]
    public Sprite engraveHeart;
    public Sprite engraveClover;
    public Sprite engraveDiamond;
    public Sprite engraveSpade;

    [Header("새길 몸체 고르기 (64px 기준 픽셀·0~255 밝기)")]
    [Tooltip("몸체색과 밝기 차이가 이보다 작아야 몸체")]
    public float bodyLumaTolerance = 55f;
    [Tooltip("몸체색과 RGB 차이 합이 이보다 작아야 몸체")]
    public float bodyColorTolerance = 140f;
    [Tooltip("몸체색 밝기가 이보다 크면 밝은 주사위로 보고 흰 하이라이트도 몸체로 친다")]
    public float highlightBodyLuma = 150f;
    [Tooltip("밝은 주사위에서 몸체색보다 이만큼 어두운 것까지 하이라이트로 친다")]
    public float highlightLumaMargin = 10f;
    [Tooltip("하이라이트로 칠 최대 채도(RGB 최대-최소)")]
    public float highlightMaxSaturation = 60f;
    [Tooltip("몸체색을 찾는 가운데 영역 [min, max) 픽셀")]
    public int bodySampleMin = 8;
    public int bodySampleMax = 56;
    [Tooltip("테두리를 피하려고 알파 영역을 안쪽으로 깎는 픽셀 수")]
    public int borderErode = 4;

    [Header("새김 색 (x = 원래 색 배율, y = 잉크 배율)")]
    public Vector2 engraveFill = new Vector2(0.62f, 0.22f);
    public Vector2 engraveShadow = new Vector2(0.40f, 0.15f);
    public Vector2 engraveWear = new Vector2(0.85f, 0.05f);
    [Tooltip("갓 찍힌 젖은 잉크의 문양 기본색 (스탬프 선택창 연출)")]
    public Vector2 engraveWetFill = new Vector2(0.45f, 0.50f);
    [Tooltip("닳은 픽셀 비율")]
    [Range(0f, 0.5f)] public float wearRatio = 0.07f;
    [Tooltip("문양 아래·오른쪽 밝은 턱: 원래 색 × 배율 + 더하기")]
    public float lipScale = 1.10f;
    public float lipAdd = 12f;

    [Header("도장 찍기 연출 (스탬프 선택창)")]
    [Tooltip("모든 연출 시간에 곱하는 값. 1이면 기본(약 1.5초), 클수록 천천히")]
    [Min(0.2f)] public float durationScale = 1f;
    [Tooltip("내려찍는 도장 그림 (정면 모습, 찍는 면이 아래)")]
    public Sprite sealTool;
    [Tooltip("도장 크기. 주사위 아이콘을 1로 본 비율. 찍는 면 폭이 새긴 문양과 비슷해지는 값")]
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
    [Tooltip("갓 찍힌 젖은 잉크가 마른 새김으로 바뀌는 시간(초)")]
    public float inkDryDuration = 0.6f;
    [Tooltip("찍는 순간 도장이 눌리는 세로 배율")]
    [Range(0.6f, 1f)] public float sealSquash = 0.85f;
    [Tooltip("찍는 순간 주사위가 눌리는 세로 배율")]
    [Range(0.6f, 1f)] public float squash = 0.92f;
    [Tooltip("찍는 순간 선택창이 흔들리는 거리(px)")]
    public float shake = 6f;

    [Header("잉크 색 (새김 색·찍을 때 튀는 잉크 방울)")]
    public Color heartInk = new Color32(199, 2, 5, 255);
    public Color cloverInk = new Color32(15, 179, 66, 255);
    public Color diamondInk = new Color32(17, 66, 143, 255);
    public Color spadeInk = new Color32(55, 53, 51, 255);

    static StampVisualProfile profile;
    static bool loaded;

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

    public Sprite GetEngrave(StampType type)
    {
        switch (type)
        {
            case StampType.Heart: return engraveHeart;
            case StampType.Clover: return engraveClover;
            case StampType.Diamond: return engraveDiamond;
            case StampType.Spade: return engraveSpade;
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
