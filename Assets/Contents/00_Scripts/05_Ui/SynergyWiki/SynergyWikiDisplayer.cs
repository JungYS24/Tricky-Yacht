using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SynergyWikiDisplayer : MonoBehaviour
{
    [SerializeField] private Transform Viewport;

    [SerializeField] private GameObject componentObj;

    [SerializeField] private GameObject figureFrameObj;

    private static readonly List<SynergyData> synergyCache = new(38);

    private void Start()
    {
        var synergies = SynergyManager.Instance.GetAllSynergies();
        foreach (var synergy in synergies)
        {
            var component = Instantiate(componentObj, Viewport).transform;
            //component
            //    .Find("Description/DescriptionBackground/SynergyIcon")
            //    .GetComponent<Image>()
            //    .sprite = synergy.SynergyIcon;
            component
                .Find("Description/DescriptionBackground/Name")
                .GetComponent<TextMeshProUGUI>()
                .text = synergy.SynergyName;
            component
                .Find("Description/DescriptionBackground/Description")
                .GetComponent<TextMeshProUGUI>()
                .text = synergy.SynergyDescription;

            var figureArea = component.GetChild(0);
            foreach (var figure in synergy.RequiredFigures)
            {
                var figureFrame = Instantiate(figureFrameObj, figureArea).transform;
                figureFrame.GetComponentInChildren<Image>().sprite = figure.icon;
                figureFrame.GetComponentInChildren<TextMeshProUGUI>().text = figure.itemName;
            }
        }
    }
}
