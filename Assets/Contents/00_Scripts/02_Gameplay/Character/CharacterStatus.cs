using UnityEngine;

public class CharacterStatus
{
    public string CharacterName { get; private set; }

    public int HP { get; private set; }

    public DiceItemSO Dice { get; private set; }

    public FigureItemSO Figure { get; private set; }

    public CharacterStatus(string name, int initialHP, DiceItemSO dice, FigureItemSO figure)
    {
        CharacterName = name;
        HP            = initialHP;
        Dice          = dice;
        Figure        = figure;
    }
}
