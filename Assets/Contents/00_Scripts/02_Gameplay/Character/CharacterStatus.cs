using UnityEngine;

public class CharacterStatus
{
    // 캐릭터 선택에서 고른 캐릭터. 메인 씬에서 시작 체력을 정할 때 읽는다.
    public static CharacterStatus Selected { get; set; }

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
