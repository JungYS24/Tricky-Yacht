using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class BaseEntityData
{
    public int ID;
    public string Name_Key;
}

[Serializable]
public class DiceDataWrapper
{
    public List<DiceEntity> Dice_Base;
}

[Serializable]
public class DiceData : BaseEntityData
{
    public string Rarity_Key;
    public string Body_Sprite_Key;
    public string[] Face_Keys;
    public int Default_Coating_ID;
    public float Glitch_Intensity;
}

[Serializable]
public class CoatingData : BaseEntityData
{
    public string Effect_Type;
    public float Effect_Value;
}

[Serializable]
public class MonsterData : BaseEntityData
{
    public int HP;
    public int ATK;
}

[Serializable]
public class GameDataWrapper<T>
{
    public List<T> data;
}

[Serializable]
public class DiceEntity : BaseEntityData
{
    public string Rarity;
    public string Grade_Color;
    public int Base_Faces;
    public int Default_Coating_ID;
    public float Weight_Modifier;
    public string Sprite_Key;
    public string Description;
}

public class DiceDatabase : ScriptableObject
{
    public List<DiceEntity> diceList = new List<DiceEntity>();
}