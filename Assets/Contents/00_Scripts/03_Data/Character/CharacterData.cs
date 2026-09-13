using UnityEngine;

public class CharacterData : ScriptableObject
{
    [SerializeField] private string characterName;

    [SerializeField] private DiceItemSO dedicateDice;

    [SerializeField] private UnlockConditionManager.UnlockFlag unlockCondition;

    public string CharacterName => characterName;

    public DiceItemSO DedicateDiceData => dedicateDice;

    public bool IsUnlocked => UnlockConditionManager.IsUnlocked(unlockCondition);
}