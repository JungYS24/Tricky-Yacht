using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SynergySO", menuName = "Synergy/SynergyObject")]
public class SynergyData : ScriptableObject
{
    [SerializeField] private string synergyName;

    [SerializeField, TextArea] private string synergyDescription;

    [SerializeField] private FigureItemSO[] requiredFigures;

    [SerializeField] private FigureTriggerType triggerType;

    [SerializeField] private SynergyEffectContext skillEffect;

    public string SynergyName => synergyName;

    public string SynergyDescription => synergyDescription;

    public IReadOnlyList<FigureItemSO> RequiredFigures => requiredFigures;

    public FigureTriggerType TriggerType => triggerType;

    public SynergyEffectContext Skilleffect => skillEffect;
}