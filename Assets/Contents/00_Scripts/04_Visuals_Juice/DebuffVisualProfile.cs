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
    [Tooltip("픽셀 이미지라 64의 배수(128, 192, 256)여야 깨지지 않음")]
    public float portalSize = 256f;
    [Tooltip("차원문에서 나와 판 위에 떨어지는 주사위 크기")]
    public float portalDieSize = 96f;
    [Tooltip("주사위 이미지는 부드럽게 확대되므로 아무 크기나 괜찮음")]
    public float eliteIconSize = 115f;
    public float bossIconSize = 154f;

    [Header("차원문 (아치와 소용돌이를 겹쳐 씀. 둘 중 하나라도 없으면 통짜 이미지)")]
    public Sprite portal;
    public Sprite portalArch;
    public Sprite portalVortex;
    [Tooltip("안쪽이 비어 있는 돌 문틀. 소용돌이와 주사위 위에 겹쳐 문 안에서 나오는 느낌을 냄")]
    public Sprite portalFrame;
    [Tooltip("차원문과 착지한 주사위 밑에 까는 그림자")]
    public Sprite groundShadow;

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
