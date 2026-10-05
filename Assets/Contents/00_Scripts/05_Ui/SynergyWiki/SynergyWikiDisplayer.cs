using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SynergyWikiDisplayer : MonoBehaviour
{
    [SerializeField] private Transform viewport;

    [SerializeField] private GameObject noSynergyMessageObj;

    [SerializeField] private GameObject componentObj;

    [SerializeField] private GameObject figureFrameObj;

    private void Start()
    {
        LoadWiki();
    }

    private void LoadWiki()
    {
        if (InventoryManager.Instance == null ||
            SynergyManager.Instance == null)
        {
            Debug.Log("아직 데이터가 로드되지 않았습니다.");
            return;
        }

        var data = GameSaveManager.Instance.LoadSaveData();

        var ownedFigures = data.ownedFigureIDs
            .Select(id => GameSaveManager.Instance.FindFigureByID(id))
            .ToHashSet();
        var synergies = SynergyManager.Instance.GetAllSynergies()
            .Where(s => s.RequiredFigures.All(f => ownedFigures.Contains(f)));

        if (synergies.Count() == 0)
        {
            noSynergyMessageObj.SetActive(true);
            return;
        }

        foreach (var synergy in synergies)
        {
            var component = Instantiate(componentObj, viewport).transform;
            // component
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
