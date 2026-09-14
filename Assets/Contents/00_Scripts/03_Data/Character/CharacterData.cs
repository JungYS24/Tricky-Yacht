using UnityEngine;

[CreateAssetMenu(fileName = "CharacterData", menuName = "Playable/Character")]
public class CharacterData : ScriptableObject
{
    [SerializeField] private string characterName;

    [SerializeField] private int initialHP;

    [SerializeField] private DiceItemSO dedicateDice;

    [SerializeField] private UnlockConditionManager.UnlockFlag unlockCondition;

    public string CharacterName => characterName;

    public int InitialHP => initialHP;

    public DiceItemSO DedicateDiceData => dedicateDice;

    public bool IsUnlocked => UnlockConditionManager.IsUnlocked(unlockCondition);
}