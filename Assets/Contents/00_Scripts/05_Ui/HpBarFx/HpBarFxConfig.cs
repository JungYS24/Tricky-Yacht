using UnityEngine;
using DG.Tweening;

// 몬스터 HP바 연출 수치. 필드 기본값 = 명세 값.
[CreateAssetMenu(fileName = "HpBarFxConfig", menuName = "HPBar/HpBarFx Config")]
public class HpBarFxConfig : ScriptableObject
{
    [Header("색")]
    public Color fillColor = Rgb(0x5C, 0xC8, 0x5A);
    public Color emptyColor = Rgb(0x7A, 0x1E, 0x1E);
    public Color ghostColor = Rgb(0xF6, 0xEE, 0xDC);
    public Color flashColor = Rgb(0xFF, 0xFF, 0xFF);
    public Color glowColor = Rgb(0xFF, 0x30, 0x30);
    public Color healGhostColor = Rgb(0xC8, 0xFF, 0xD0);
    public Color lowFillColor = Rgb(0xE8, 0xA2, 0x3A);
    public Color deathFrameColor = Rgb(0x6B, 0x6B, 0x6B);
    public Color textColor = Rgb(0xFF, 0xFF, 0xFF);
    public Color textOutlineColor = Rgb(0x1A, 0x0E, 0x0A);
    public float textOutlineWidth = 0.2f;
    public float textHeightRatio = 0.7f;
    [Tooltip("안쪽 구멍 패딩(px). x=왼쪽, y=아래, z=오른쪽, w=위")]
    public Vector4 innerPadding = new Vector4(10f, 10f, 10f, 10f);
    public float glowOutset = 4f;

    [Header("피격 (피해 < 20%)")]
    public float hitFillDuration = 0.12f;
    public Ease hitFillEase = Ease.OutQuad;
    public float hitFlashPeakAlpha = 0.7f;
    public float hitFlashInDuration = 0.06f;
    public float hitFlashOutDuration = 0.12f;
    public Ease hitFlashEase = Ease.Linear;
    public float hitShakeStrength = 6f;
    public float hitShakeDuration = 0.25f;
    public int shakeVibrato = 20;
    public float textCountDuration = 0.3f;
    public Ease textCountEase = Ease.OutQuad;
    public float textPunch = 0.15f;
    public float textPunchDuration = 0.2f;
    public float ghostDelay = 0.35f;
    public float ghostDuration = 0.4f;
    public Ease ghostEase = Ease.InOutQuad;
    public float sparkDuration = 0.3f;

    [Header("큰 피격 (피해 >= 최대 HP 20%)")]
    public float bigHitRatio = 0.2f;
    public float bigShakeStrength = 14f;
    public float bigShakeDuration = 0.35f;
    public float bigFlashPeakAlpha = 1f;
    public float bigGhostDelay = 0.5f;
    public float bigGhostDuration = 0.6f;
    public float bigSparkDuration = 0.4f;
    public int bigSparkCountMultiplier = 2;

    [Header("회복")]
    public float healGhostDuration = 0.1f;
    public Ease healGhostEase = Ease.OutQuad;
    public float healFillDelay = 0.15f;
    public float healFillDuration = 0.4f;
    public Ease healFillEase = Ease.OutCubic;
    public float healTextDelay = 0.15f;
    public float healTextDuration = 0.4f;
    public Ease healTextEase = Ease.OutQuad;
    public float healGhostRestoreDelay = 0.55f;

    [Header("저체력 (<= 25%)")]
    [Range(0f, 1f)] public float lowHpThreshold = 0.25f;
    public float lowFillDuration = 0.2f;
    public float glowMinAlpha = 0.3f;
    public float glowMaxAlpha = 0.9f;
    public float glowDuration = 0.6f;
    public float lowExitDuration = 0.2f;

    [Header("사망")]
    public float deathGhostDelay = 0.3f;
    public float deathGhostDuration = 0.6f;
    public Ease deathGhostEase = Ease.InQuad;
    public float deathFadeDelay = 0.9f;
    public float deathFadeDuration = 0.3f;
    public float deathFadeAlpha = 0.4f;
    public Ease deathFadeEase = Ease.OutQuad;
    public float deathFrameDelay = 0.9f;
    public float deathFrameDuration = 0.3f;
    public Ease deathFrameEase = Ease.OutQuad;

    [Header("임시 파티클 (프리팹이 비어 있으면 UI Image 풀)")]
    public GameObject hitSparkPrefab;
    public GameObject healParticlePrefab;
    public int sparkCount = 8;
    public int healParticleCount = 8;
    public float sparkDistanceMin = 16f;
    public float sparkDistanceMax = 42f;
    public float sparkSizeMin = 6f;
    public float sparkSizeMax = 12f;
    public Color sparkStartColor = Rgb(0xFF, 0xFF, 0xFF);
    public Color sparkEndColor = Rgb(0xE0, 0x48, 0x48);
    public float healDurationMin = 0.5f;
    public float healDurationMax = 0.9f;
    public float healRiseMin = 25f;
    public float healRiseMax = 55f;
    public Color healParticleColor = Rgb(0xFF, 0xFF, 0xFF);

    public static Color Rgb(byte r, byte g, byte b)
    {
        return new Color(r / 255f, g / 255f, b / 255f, 1f);
    }
}
