using UnityEngine;

[CreateAssetMenu(fileName = "CharacterData", menuName = "Playable/Character")]
public class CharacterData : ScriptableObject
{
    [Header("캐릭터 데이터")]
    [SerializeField] private string characterName;

    [SerializeField] private int initialHP;

    [SerializeField] private Sprite characterSprite;

    [SerializeField] private UnlockFlag unlockCondition;

    [Header("전용 데이터")]
    [SerializeField] private DiceItemSO dedicateDice;

    [SerializeField] private FigureItemSO dedicateFigure;

    public string CharacterName => characterName;

    public int InitialHP => initialHP;

    public Sprite CharacterSprite => characterSprite;

    public bool IsUnlocked => UnlockConditionManager.IsUnlocked(unlockCondition);

    public DiceItemSO DedicateDiceData => dedicateDice;

    public FigureItemSO DedicateFigureData => dedicateFigure;
}