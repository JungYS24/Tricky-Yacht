using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class DiceEntity
{
    public int Dice_ID;
    public string Dice_Name;
    public string Rarity;
    public string Grade_Color;
    public int Base_Faces;
    public int Default_Coating_ID;
    public float Weight_Modifier;
    public string Sprite_Key;
    public string Description;
}

[System.Serializable]
public class DiceDataWrapper
{
    public List<DiceEntity> Dice_Base;
}

public class DiceDatabase : ScriptableObject
{
    public List<DiceEntity> diceList = new List<DiceEntity>();
}