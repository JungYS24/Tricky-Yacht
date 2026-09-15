using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SynergyManager : MonoBehaviour
{
    public static SynergyManager Instance { get; private set; }

    [SerializeField] private List<SynergyData> synergies;

    private readonly Dictionary<string, FigureItemSO[]> synergyToFigures = new();

    private readonly Dictionary<string, List<SynergyData>> figureToSynergies = new();

    public IReadOnlyList<FigureItemSO> GetRequiredFigures(string id)
    {
        return synergyToFigures[id];
    }

    public IReadOnlyList<SynergyData> GetSynergyList(string id)
    {
        return figureToSynergies[id];
    }

    private void Awake()
    {
        if (Instance == null || !Instance.isActiveAndEnabled)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }

        Initailize();
    }

    private void Initailize()
    {
        foreach (var synergy in synergies)
        {
            var required = synergy.RequiredFigures.ToArray();
            synergyToFigures[synergy.SynergyName] = required;
            foreach (var figure in required)
            {
                var id = figure.Item_ID;
                if (figureToSynergies.ContainsKey(id))
                {
                    figureToSynergies[id].Add(synergy);
                }
                else
                {
                    figureToSynergies[id] = new() { synergy };
                }
            }
        }
    }
}