using UnityEngine;

public enum ItemCategory { Shaker, Coating, Figure, Snack }
public enum ItemGrade { None, Normal, Rare, Epic, Legendary }
public enum ShakerClass { None, Balance, Attack, Technic, Custom }


public abstract class BaseItemDataSO : ScriptableObject
{
    [Header("--- 공통 기본 정보 ---")]
    public string Item_ID;
    public string itemName;
    public int price;
    public Sprite icon;
    [TextArea(3, 5)] public string description;

    public abstract void ApplyItemEffect(DiceManager diceManager);
}