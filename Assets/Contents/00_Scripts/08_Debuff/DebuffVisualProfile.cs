using UnityEngine;

// 디버프 주사위 등장·발동 연출에 쓰는 이미지와 색. 기본 에셋은 Resources에서 한 번 읽습니다.
// 이미지가 비어 있으면 해당 연출만 건너뛰고 게임 진행은 그대로입니다.
[CreateAssetMenu(fileName = "DebuffVisualProfile", menuName = "TrickYacht/Visuals/Debuff Profile")]
public sealed class DebuffVisualProfile : ScriptableObject
{
    [Header("연출 속도")]
    [Tooltip("모든 디버프 연출 시간에 곱하는 값. 1이면 기본 속도, 클수록 천천히")]
    [Min(0.2f)] public float durationScale = 1.5f;
    [Tooltip("전투 시작 연출 중 화면을 누르면 남은 연출을 이 배속으로 재생")]
    [Min(1f)] public float skipSpeed = 4f;

    [Header("등장 위치와 크기 (1920×1080 기준)")]
    [Tooltip("주사위가 굴러가는 줄(굴림 칸) 높이에서 위아래로 옮기는 거리. 양수면 위로")]
    public float rowOffset = 0f;
    [Tooltip("균열 이미지 1픽셀을 화면 몇 픽셀로 그릴지. 픽셀이 깨지지 않게 정수(2 권장)")]
    [Min(1)] public int riftPixelScale = 2;
    [Tooltip("균열에서 나와 판 위에 떨어지는 주사위 크기")]
    public float portalDieSize = 96f;
    [Tooltip("주사위 이미지는 부드럽게 확대되므로 아무 크기나 괜찮음")]
    public float eliteIconSize = 115f;
    public float bossIconSize = 154f;

    [Header("차원문 주사위 움직임")]
    [Tooltip("균열 가운데가 주사위 굴림 줄보다 얼마나 위에 열리는지")]
    public float riftLift = 80f;
    [Tooltip("균열 가운데에서 판 위 착지 지점까지의 거리. 줄이면 착지 지점이 위로 올라옴")]
    public float portalDrop = 112f;
    [Tooltip("균열에서 튕겨 나올 때 위로 솟구치는 높이")]
    public float launchHeight = 48f;
    [Tooltip("균열을 나올 때 커지는 배율. 착지해 보여 준 뒤 덱으로 가면서 작아짐")]
    public float emphasisScale = 1.3f;
    [Tooltip("착지한 디버프 주사위를 보여 주는 시간(기본 속도 기준 초)")]
    public float confirmHold = 0.35f;

    [Header("켜기/끄기 (어색하면 하나씩 꺼 보며 비교)")]
    [Tooltip("등장 연출 동안 화면을 살짝 어둡게 해서 소환에 시선을 모음")]
    public bool dimScreen = true;
    [Range(0f, 0.9f)] public float dimAlpha = 0.35f;
    [Tooltip("등장 연출 동안 몬스터 애니메이션을 멈춤")]
    public bool pauseMonster = true;
    [Tooltip("착지·변신 순간 화면을 짧게 흔듦")]
    public bool cameraShake = true;
    [Tooltip("균열에서 튕겨 나오는 순간 주사위가 하얗게 한 번 번쩍임")]
    public bool exitFlash = true;
    [Tooltip("디버프 주사위 테두리에 자주색·붉은색 빛 (사각 테두리처럼 보여 기본은 꺼 둠)")]
    public bool edgeGlow = false;
    public Color edgeGlowColor = new Color(0.85f, 0.2f, 0.6f);
    [Tooltip("디버프 주사위가 덱에 들어갈 때 덱 버튼이 흔들리며 보랏빛으로 물듦")]
    public bool deckReaction = true;
    public Color deckGlowColor = new Color(0.72f, 0.45f, 1f);

    [Header("차원문 (판 위에 열리는 균열)")]
    [Tooltip("판 위에 누운 타원형 차원 균열")]
    public Sprite portalRift;
    [Tooltip("균열 주변 판에 번지는 보랏빛")]
    public Sprite riftGlow;
    [Tooltip("착지한 주사위 밑에 까는 그림자")]
    public Sprite groundShadow;

    [Header("덱으로 스며드는 안개")]
    [Tooltip("주사위가 녹아내린 뒤 덱으로 날아가는 안개의 밝은 핵 색")]
    public Color wispCoreColor = new Color(0.8f, 0.5f, 1f);
    [Tooltip("안개 꼬리 색")]
    public Color wispTrailColor = new Color(0.24f, 0.1f, 0.34f);

    [Header("왼쪽 위 디버프 표시와 설명 패널 (크기·간격·높이는 피규어 칸에서 그대로 가져옴)")]
    [Tooltip("흰색 둥근 빛. 코드에서 보라색을 입혀 숨 쉬듯 밝아졌다 어두워짐")]
    public Sprite roundGlow;
    [Tooltip("이번 전투 한정 디버프 표시")]
    public Sprite hourglass;
    [Tooltip("피규어 칸을 찾지 못했을 때만 쓰는 아이콘 크기")]
    public float trackerIconSize = 70f;
    [Tooltip("피규어 칸 크기에 곱하는 배율. 1이면 피규어와 같은 크기")]
    [Range(0.5f, 1.2f)] public float trackerScale = 1f;
    [Tooltip("설명 패널 그림 칸 안에서 디버프 주사위가 차지하는 비율")]
    [Range(0.3f, 1f)] public float panelDieScale = 0.65f;
    [Tooltip("전투 화면: 나무 패널 안쪽 칸들의 오른쪽 끝에서 띄우는 거리(x). y는 피규어 칸을 못 찾았을 때 화면 위에서 내리는 거리")]
    public Vector2 trackerCombatOffset = new Vector2(90f, -24f);
    [Tooltip("지도 화면: 체력 칸 왼쪽 아래 모서리에서 띄우는 거리")]
    public Vector2 trackerMapOffset = new Vector2(6f, -16f);
    public Color trackerGlowColor = new Color(0.7f, 0.4f, 1f);

    [Header("테이블 위 디버프 주사위 보랏빛 후광 (주사위 뒤에서 은은하게 번짐)")]
    [Tooltip("굴리는 디버프 주사위 뒤에 보랏빛을 깔지")]
    public bool boardGlow = true;
    [Tooltip("가장자리가 부드럽게 사라지는 흰 둥근 네모 빛. 색은 아래 색으로 입힘")]
    public Sprite haloSprite;
    [Tooltip("후광 전용 머티리얼(M_DebuffHalo). 바닥을 보라로 덮는 정도(Tint)와 더하는 밝기(Glow)는 머티리얼에서 조절")]
    public Material haloMaterial;
    [Tooltip("주사위 크기 대비 후광 크기")]
    [Range(1.2f, 2.5f)] public float boardGlowSize = 1.8f;
    [Tooltip("숨 쉬듯 바뀌는 세기 범위(최소, 최대). 1이면 머티리얼 값 그대로")]
    public Vector2 boardGlowStrength = new Vector2(0.7f, 1f);
    [Tooltip("한 번 밝아졌다 어두워지는 데 걸리는 시간(초)")]
    [Min(0.5f)] public float boardGlowPeriod = 2.4f;
    public Color boardGlowColor = new Color(0.62f, 0.22f, 1f);

    [Header("조우: 제물이 된 소녀 (바친 주사위가 녹아 소녀에게 빨려 들어감)")]
    [Tooltip("소녀 앞에 떠오르는 주사위 크기")]
    public float sacrificeDieSize = 100f;
    [Tooltip("안개가 빨려 들어가는 지점. 조우 그림 안에서의 비율(왼쪽 아래 0,0 ~ 오른쪽 위 1,1)")]
    public Vector2 sacrificeHeart = new Vector2(0.4f, 0.55f);
    [Tooltip("주사위가 떠올라 멈추는 높이. 조우 그림 아래 끝에서부터의 비율")]
    [Range(0f, 1f)] public float sacrificeHoverHeight = 0.22f;
    [Tooltip("녹은 주사위의 안개가 소녀에게 날아가는 시간(기본 속도 기준 초). 클수록 천천히")]
    [Min(0.1f)] public float sacrificeTravelTime = 0.7f;
    [Tooltip("안개가 스며들 때 소녀 모양을 따라 뒤에서 번지는 오라 색")]
    public Color sacrificeTint = new Color(0.72f, 0.45f, 1f);
    [Tooltip("오라가 가장 진할 때의 불투명도(안쪽 겹 기준, 바깥 겹은 더 연함)")]
    [Range(0f, 1f)] public float sacrificeAuraAlpha = 0.8f;
    [Tooltip("오라 가장 바깥 겹이 소녀 그림보다 커지는 배율. 클수록 오라가 넓게 퍼짐")]
    [Range(1f, 1.2f)] public float sacrificeAuraScale = 1.08f;

    [Header("발동 연출 이미지")]
    public Sprite chainLink;
    public Sprite smokePuff;
    public Sprite thornShard;

    [Header("연기 색 (흰 연기 이미지에 곱해짐)")]
    public Color thiefSmoke = new Color(0.29f, 0.26f, 0.32f);
    public Color curseSmoke = new Color(0.24f, 0.9f, 0.16f);
    public Color vanishSmoke = new Color(0.61f, 0.61f, 0.64f);
    public Color portalSmoke = new Color(0.63f, 0.42f, 0.88f);

    [Header("숫자 표시 색")]
    public Color lossColor = new Color(1f, 0.32f, 0.27f);
    public Color curseColor = new Color(0.45f, 1f, 0.35f);

    [Header("저주 구름 (저주가 배수에 곱해질 때. 점수판과 같은 픽셀 크기로 프레임 재생)")]
    [Tooltip("저주 주사위 색으로 칠한 구름 프레임. 비우면 예전 초록 연기를 쓴다")]
    public Sprite[] curseCloudFrames;
    [Tooltip("구름 한 프레임 시간(기본 속도 기준, 초)")]
    [Min(0.02f)] public float curseCloudFrameTime = 0.09f;

    [Header("악마주사위 간식 시간 (체력 바를 무는 이빨. 체력 바와 같은 픽셀 크기로 그린다)")]
    [Tooltip("체력 바 끝을 위에서 무는 윗니(보라 잇몸). 비우면 연출 없이 피해만 들어간다")]
    public Sprite snackJawUpper;
    [Tooltip("체력 바 끝을 아래에서 무는 아랫니")]
    public Sprite snackJawLower;
    [Tooltip("물린 체력 바 끝에 잠깐 남는 한 입 자국(체력 바 바탕색으로 칠한 그림)")]
    public Sprite snackBiteMark;
    [Tooltip("체력을 물었을 때 떨어지는 부스러기(체력 바 빨간색)")]
    public Sprite[] snackCrumbsRed;
    [Tooltip("보호막을 물었을 때 떨어지는 부스러기(보호막 회색)")]
    public Sprite[] snackCrumbsGray;

    [Header("악마주사위 각성 (1페이즈 체력 0 → 타락한 악마주사위)")]
    [Tooltip("죽을 때 눈(DevilDice/Phase1_New): 0 = 마지막 타격에 찡그려 감은 눈(맞는 동작 시트의 8번째 장, 애니메이터 HitDown이 " +
             "멈추는 장), 1~2 = 같은 장에서 눈만 반쯤 풀림 → 편히 감김(Devil_Dice_P1_EyeRelax). " +
             "보스 그림과 같은 칸·기준점(바닥 가운데)으로 잘라 두었다. 비우면 각성 연출 없이 바로 각성한다")]
    public Sprite[] devilCloseFrames;
    [Tooltip("떠오를 때 날개(DevilDice/Phase1_New/Devil_Dice_P1_Wings, 32장, 눈은 모두 편히 감김): 0 = 날개를 위로 편 모습 → " +
             "31 = 날개가 축 늘어진 모습. 떠오르며 31→0으로 서서히 들어 올린다. 몸의 점은 31에서 꺼져 있다가 0으로 갈수록 다시 붉어진다")]
    public Sprite[] devilWingFrames;
    [Tooltip("쓰러진 뒤 날개(DevilDice/Phase1_New/Devil_Dice_P1_WingsDark, 32장): devilWingFrames와 같은 장에 몸의 점만 꺼진 그림. " +
             "눈이 편히 감긴 뒤 7→31로 날개가 축 늘어지고 그대로 누워 있는다. 비우면 devilWingFrames를 쓴다")]
    public Sprite[] devilLimpFrames;
    [Tooltip("각성 애니메이션(DevilDice/Phase1_New/Devil_Dice_P1_Awaken, 26장, 예전 시트와 장 번호가 같다): " +
             "0~7 붉은 눈 뜸·노려봄, 8~15 금이 퍼짐, 16 껍질이 붉은 몸으로(흰 플래시), 17~25 조각이 떨어지며 타락 모습으로 가라앉음. " +
             "어느 장을 얼마나 보여 줄지는 DebuffFeedback.DevilAwaken 맨 위 값")]
    public Sprite[] devilAwakenFrames;
    [Tooltip("타락한 악마주사위 입(DevilDice/Mouth/Devil_Dice_Mouth, 26장 = AI 생성 시트의 10~35번째 장, 눈 감기는 장은 뺐다): " +
             "0~8 입이 열리며 몸이 젖혀짐, 9 가장 크게 벌어짐, 10~13 스르르 닫힘, 14 닫히며 찌그러짐, 15~25 원래 모습으로 돌아옴. " +
             "영혼 포식(먹기)과 타락의 낙인(뱉기)이 같이 쓴다. 비우면 입 없이 예전처럼 처리한다")]
    public Sprite[] devilMouthFrames;
    [Tooltip("타락한 악마주사위(2페이즈) 처치 시트(DevilDice/Death, 36장, 456×256 칸, 바닥 가운데 기준점). " +
             "0~4 놀라 움찔(3~4 눈 질끈), 5~11 눈을 부릅뜨고 떪, 12~17 금이 가며 빛줄기, 18~21 폭발 섬광, 22~35 조각나 흩어짐. " +
             "비우면 예전 파괴 애니메이션(조원 프리팹)을 쓴다")]
    public Sprite[] devilDeathFrames;
    [Tooltip("2페이즈 동안 배경 그림에 곱하는 색")]
    public Color devilBackgroundMultiply = new Color(1f, 0.7f, 0.7f, 1f);
    [Tooltip("2페이즈 동안 배경 바로 위(몬스터 아래)에 까는 붉은 막. 알파가 세기")]
    public Color devilBackgroundOverlay = new Color(0.48f, 0.04f, 0.08f, 0.22f);
}
