using UnityEngine;

// 폭탄 주사위 연출 설정. Assets/Resources/BombVisualProfile.asset 을 읽는다.
// 흐름: (일반 공격 피격 연출이 끝나길 기다림) → 준비(주사위 뒤 붉은 빛무리 + 1픽셀 떨림)
//       → 왼쪽부터 터짐: 하얀 섬광 + 주사위를 덮는 폭발(그동안 주사위는 숨김) → 그을린 주사위가 툭 내려앉고 작은 연기
//       → 충격파가 옆 주사위에 닿으면 그 주사위가 밀렸다 돌아오며 떨다가 터짐 (점점 빠르게, 흔들림은 점점 세게)
//       → 마지막 직전 모으기 → 마지막 폭탄은 몬스터 큰 폭발 + 연쇄 보너스까지 더한 피해.
// 주사위 쪽 그림은 주사위와 같은 점 크기로 그린다(확대하지 않음). 시간·크기는 이 에셋 인스펙터에서 조절한다.
[CreateAssetMenu(fileName = "BombVisualProfile", menuName = "ScriptableObjects/BombVisualProfile")]
public sealed class BombVisualProfile : ScriptableObject
{
    [Header("주사위 쪽 그림 (02_Sprites/04_Dice/Boom_Dice/FX, 주사위와 같은 점 크기)")]
    public Sprite[] diePopFrames;  // 96px: 주사위를 덮는 폭발
    public Sprite[] ringFrames;    // 128px: 옆 주사위까지 퍼지는 충격파
    public Sprite glowSprite;      // 80px: 터지기 전 주사위 뒤 붉은 빛무리
    public Sprite[] smokeFrames;   // 32px: 터진 주사위에서 피어오르는 작은 연기

    [Header("몬스터 쪽 그림")]
    public Sprite[] blastFrames;   // 192px 큰 폭발: 마지막 폭탄(1개뿐이어도)

    [Header("시간 (초)")]
    [Min(0f)] public float startDelay = 0.2f;        // 일반 공격 피격 연출(약 0.3초)이 끝나길 기다림
    [Min(0f)] public float chargeDuration = 0.15f;   // 준비
    [Min(0.05f)] public float firstInterval = 0.3f;  // 첫 폭탄 → 다음 폭탄 간격
    [Min(0f)] public float intervalShrink = 0.03f;   // 폭탄마다 간격이 이만큼 줄어든다(점점 빨라짐)
    [Min(0.05f)] public float minInterval = 0.18f;   // 간격 최소값
    [Min(0f)] public float critExtra = 0.05f;        // 6 크리티컬 다음은 이만큼 더 쉰다
    [Min(0f)] public float finalWindup = 0.25f;      // 마지막 폭탄 직전 모으기
    [Min(0f)] public float finalHold = 0.45f;        // 마지막 큰 피해 숫자를 보여 주는 시간
    [Min(0.02f)] public float ringDuration = 0.16f;  // 충격파가 다 퍼지는 시간
    [Min(1f)] public float diePopFps = 18f;
    [Min(1f)] public float smokeFps = 10f;
    [Min(1f)] public float blastFps = 16f;

    [Header("주사위 움직임 (주사위 점 단위)")]
    [Min(0)] public int revealFrames = 2;            // 폭발 몇 프레임 뒤에 그을린 주사위가 다시 보이나
    [Min(0)] public int dropPixels = 2;              // 다시 보일 때 위에서 툭 내려앉는 높이
    [Min(0)] public int joltPixels = 2;              // 충격파에 맞은 옆 주사위가 밀리는 거리
    [Min(0)] public int fuseJitterPixels = 1;        // 터지기 직전 덜덜 떨림
    [Min(0)] public int windupJitterPixels = 2;      // 마지막 모으기 떨림
    [Min(1f)] public float jitterHz = 30f;
    [Min(0)] public int smokeRisePixels = 6;
    public Color scorchTint = new Color(0.72f, 0.66f, 0.64f, 1f); // 터진 주사위는 끝날 때까지 살짝 그을린 색

    [Header("빛무리")]
    [Range(0f, 1f)] public float glowChargeAlpha = 0.5f;  // 준비 중
    [Range(0f, 1f)] public float glowFuseAlpha = 1f;      // 충격파에 맞아 터지기 직전
    [Min(0f)] public float glowFlickerHz = 8f;

    [Header("몬스터 쪽")]
    [Min(0.1f)] public float blastScale = 0.9f;      // 몬스터 위 큰 폭발: 몬스터 높이 대비
    [Min(0f)] public float damageTextSpread = 0.22f; // 피해 숫자를 좌우로 엇갈리는 폭: 몬스터 너비 대비

    [Header("화면 흔들림")]
    [Min(0f)] public float tickShakeStep = 0.8f;     // 2번째 폭탄부터 폭탄마다 이만큼(화면 픽셀) 세진다
    [Min(0.01f)] public float tickShakeDuration = 0.12f;
    [Range(1f, 1.35f)] public float impactShake = 1.25f; // 마지막 큰 폭발

    static BombVisualProfile cached;
    public static BombVisualProfile Load()
    {
        if (cached == null) cached = Resources.Load<BombVisualProfile>("BombVisualProfile");
        return cached;
    }
}
