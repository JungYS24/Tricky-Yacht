using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SynergySO", menuName = "Synergy/SynergyObject")]
public class SynergyData : BaseItemDataSO
{
    [SerializeField] private FigureItemSO[] requiredFigures;

    [SerializeField] private SynergyEffectContext skillEffect;

    public string SynergyName
    {
        get => itemName;
        set => itemName = value;
    }

    public string SynergyDescription
    {
        get => description;
        set => description = value;
    }

    public IReadOnlyList<FigureItemSO> RequiredFigures => requiredFigures;

    public SynergyEffectContext Skilleffect => skillEffect;

    public override void ApplyItemEffect(DiceManager diceManager)
    {
        
    }
}