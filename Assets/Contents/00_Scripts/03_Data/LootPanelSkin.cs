using UnityEngine;

/// <summary>
/// 전리품 카드의 등급별 바탕 그림과 이름 색. 순서는 노멀, 레어, 에픽, 레전드다.
/// </summary>
[CreateAssetMenu(fileName = "LootPanelSkin", menuName = "TrickyYacht/UI/Loot Panel Skin")]
public class LootPanelSkin : ScriptableObject
{
    [Tooltip("노멀, 레어, 에픽, 레전드 순서")]
    public Sprite[] panelSprites = new Sprite[4];

    [Tooltip("노멀, 레어, 에픽, 레전드 순서")]
    public Color[] nameColors =
    {
        new Color32(0xF6, 0xE4, 0xC2, 0xFF),
        new Color32(0xBD, 0xEE, 0xFA, 0xFF),
        new Color32(0xE6, 0xD8, 0xFF, 0xFF),
        new Color32(0xF7, 0xD5, 0x6A, 0xFF),
    };

    public Color descColor = new Color32(0xF6, 0xE4, 0xC2, 0xFF);

    public static int IndexOf(ItemGrade grade)
    {
        switch (grade)
        {
            case ItemGrade.Rare: return 1;
            case ItemGrade.Epic: return 2;
            case ItemGrade.Legendary: return 3;
            default: return 0;
        }
    }

    public Sprite PanelFor(ItemGrade grade)
    {
        int i = IndexOf(grade);
        return panelSprites != null && i < panelSprites.Length ? panelSprites[i] : null;
    }

    public Color NameColorFor(ItemGrade grade)
    {
        int i = IndexOf(grade);
        return nameColors != null && i < nameColors.Length ? nameColors[i] : descColor;
    }
}
