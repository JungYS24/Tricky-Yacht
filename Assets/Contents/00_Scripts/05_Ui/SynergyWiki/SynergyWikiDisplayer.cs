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

    private readonly List<(Transform, SynergyData)> components = new();

    private HashSet<FigureItemSO> ownedFigures = new();

    private OrderType orderType;
    
    public void OrderReverse(bool reverse)
    {
        Order((int)orderType);
        int cnt = components.Count;
        if (reverse)
        {
            for (int i = 0; i < cnt; i++)
            {
                components[^(i + 1)].Item1.SetSiblingIndex(i);
            }
        }
        else
        {
            for (int i = 0; i < cnt; i++)
            {
                components[i].Item1.SetSiblingIndex(i);
            }
        }
    }

    public void Order(int orderTypeInt)
    {
        if (orderType == (OrderType)orderTypeInt)
        {
            return;
        }

        var pre = orderType;
        switch (orderType = (OrderType)orderTypeInt)
        {
            case OrderType.Name:
                components.Sort((x, y) => x.Item2.SynergyName.CompareTo(y.Item2.SynergyName));
                break;

            case OrderType.OwnedFigurePercentile:
                components.Sort(
                    (x, y) => (y.Item2.RequiredFigures
                            .Count(
                                f => ownedFigures.Contains(f)
                            ) / (float)y.Item2.RequiredFigures.Count)
                        .CompareTo(
                            x.Item2.RequiredFigures
                                .Count(
                                    f => ownedFigures.Contains(f)
                                ) / (float)x.Item2.RequiredFigures.Count
                        ));
                break;

            case OrderType.FigureCount:
                components.Sort(
                    (x, y) => x.Item2.RequiredFigures.Count
                    .CompareTo(y.Item2.RequiredFigures.Count));
                break;

            default:
                Debug.LogWarning($"'{nameof(orderType)}'에 잘못된 값이 입력되었습니다: {orderType}");
                orderType = pre;
                return;
        }

        int cnt = components.Count;
        for (int i = 0; i < cnt; i++)
        {
            components[i].Item1.SetSiblingIndex(i);
        }
    }

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

        ownedFigures = data.ownedFigureIDs
            .Select(id => GameSaveManager.Instance.FindFigureByID(id))
            .ToHashSet();
        var synergies = SynergyManager.Instance.GetAllSynergies()
            .OrderBy(s => s.SynergyName);

        foreach (var synergy in synergies)
        {
            var component = Instantiate(componentObj, viewport).transform;
            component
               .Find("Description/DescriptionBackground/SynergyIcon")
               .GetComponent<Image>()
               .sprite = synergy.icon;
            component
                .Find("Description/DescriptionBackground/Name")
                .GetComponent<TextMeshProUGUI>()
                .text = synergy.SynergyName;
            component
                .Find("Description/DescriptionBackground/Description")
                .GetComponent<TextMeshProUGUI>()
                .text = synergy.SynergyDescription;

            var figureArea = component.GetChild(0);
            components.Add((component, synergy));
            foreach (var figure in synergy.RequiredFigures)
            {
                var figureFrame = Instantiate(figureFrameObj, figureArea).transform;
                figureFrame.GetComponentInChildren<Image>().sprite = figure.icon;
                figureFrame.GetComponentInChildren<TextMeshProUGUI>().text = figure.itemName;
                figureFrame.Find("Silhouette").gameObject.SetActive(!ownedFigures.Contains(figure));
            }
        }
    }

    private enum OrderType
    {
        Name,
        OwnedFigurePercentile,
        FigureCount,
    }
}
