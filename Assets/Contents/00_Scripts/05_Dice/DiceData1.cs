using UnityEngine;

// [주요 변경] 주사위의 유형을 정의하는 enum을 만듭니다.
public enum DiceType
{
    Normal, // 기본 주사위
    Prism,  // 프리즘 코팅
    Gold,   // 골드 코팅
    Black   // 검정 코팅
}

[System.Serializable]
public class DiceData1
{
    public bool isCoated = false;
    public float multiplier = 1f;
    public Color diceColor = Color.white;

    // [주요 변경] 이 주사위의 현재 유형을 저장합니다.
    public DiceType type = DiceType.Normal;

    // 기본 생성자 (기본 주사위 생성용)
    public DiceData1()
    {
        isCoated = false;
        multiplier = 1f;
        diceColor = Color.white;
        type = DiceType.Normal; // 기본은 Normal
    }
}