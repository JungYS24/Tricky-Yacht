using UnityEngine;

public enum DiceType
{
    Normal,
    Prism,
    Gold,
    Dark,
    Ice
}

[System.Serializable]
public class DiceData1
{
    // 1. 변수 선언부에 이름을 추가하고 기본값을 넣어줍니다.
    public string diceName = "기본 주사위";

    public bool isCoated = false;
    public float multiplier = 1f;
    public Color diceColor = Color.white;
    public DiceType type = DiceType.Normal;

    //public int minRoll = 1;
    //public int maxRoll = 6;
    public int[] faceValues = new int[6] { 1, 2, 3, 4, 5, 6 };
    public DiceData1()
    {
        diceName = "기본 주사위";
        isCoated = false;
        multiplier = 1f;
        diceColor = Color.white;
        type = DiceType.Normal;

        // 6개 면의 기본값을 1~6으로 설정
        faceValues = new int[6] { 1, 2, 3, 4, 5, 6 };
    }

    // 특수 주사위 생성을 위한 생성자 (선택 사항)
    public DiceData1(string name, int[] faces)
    {
        this.diceName = name;
        this.faceValues = faces;
    }
}