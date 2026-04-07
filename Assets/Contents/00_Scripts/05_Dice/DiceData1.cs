using UnityEngine;

public enum DiceType
{
    Normal, 
    Prism,  
    Gold,   
    Black   
}

[System.Serializable]
public class DiceData1
{
    public bool isCoated = false;
    public float multiplier = 1f;
    public Color diceColor = Color.white;


    public DiceType type = DiceType.Normal;

    public DiceData1()
    {
        isCoated = false;
        multiplier = 1f;
        diceColor = Color.white;
        type = DiceType.Normal; 
    }
}