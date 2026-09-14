using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SynergySO", menuName = "Synergy/SynergyObject")]
public class SynergyData : ScriptableObject
{
    [SerializeField] private string synergyName;

    [SerializeField, TextArea] private string synergyDescription;

    [SerializeField] private FigureItemSO[] requiredFigures;

    public string SynergyName => synergyName;

    public string SynergyDescription => synergyDescription;

    public IReadOnlyList<FigureItemSO> RequiredFigures => requiredFigures;
}