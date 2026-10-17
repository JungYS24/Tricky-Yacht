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
}
