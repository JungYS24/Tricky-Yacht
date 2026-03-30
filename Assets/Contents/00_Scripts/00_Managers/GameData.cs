using System;
using System.Collections.Generic;

[Serializable]
public class DiceData
{
    public int Dice_ID;
    public string Name_Key;
    public string Rarity_Key;
    public string Body_Sprite_Key;
    public string[] Face_Keys;
    public int Default_Coating_ID;
    public float Glitch_Intensity;
}

[Serializable]
public class CoatingData
{
    public int Coating_ID;
    public string Name_Key;
    public string Effect_Type;
    public float Effect_Value;
}

[Serializable]
public class MonsterData
{
    public int Monster_ID;
    public string Name_Key;
    public int HP;
    public int ATK;
}

[Serializable]
public class GameDataWrapper<T>
{
    public List<T> data;
}