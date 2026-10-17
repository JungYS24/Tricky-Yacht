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

    [Header("정렬 UI 라벨 (비어 있으면 건너뜀)")]
    [SerializeField] private TMP_Dropdown orderDropdown;

    [SerializeField] private TextMeshProUGUI reverseLabel;

    [Header("왼쪽 목록 (비어 있으면 예전 펼침 줄)")]
    [SerializeField] private GameObject rowTemplate;

    [SerializeField] private Sprite rowNormalSprite;

    [SerializeField] private Sprite rowSelectedSprite;

    [SerializeField] private ScrollRect listScroll;

    [Header("오른쪽 상세")]
    [SerializeField] private TextMeshProUGUI detailName;

    [SerializeField] private TextMeshProUGUI detailDescription;

    [SerializeField] private Image detailIcon;

    [SerializeField] private Transform detailFigureRoot;

    // 드롭다운 옵션 순서는 OrderType 순서와 같다.
    private static readonly (string key, string fallback)[] OrderLabelKeys =
    {
        ("UI_SYNERGY_SORT_NAME", "이름"),
        ("UI_SYNERGY_SORT_OWNED", "보유율"),
        ("UI_SYNERGY_SORT_COUNT", "피규어 수"),
    };

    private readonly List<(Transform, SynergyData)> components = new();

    private HashSet<FigureItemSO> ownedFigures = new();

    private OrderType orderType;

    private bool isReversed;

    private bool hasLoaded;

    private SynergyData pendingFocus;

    private SynergyData selectedSynergy;

    // 새로고침 뒤에 이 시너지 줄을 선택한다. 이름은 겹칠 수 있어 참조로 찾는다.
    public void ArmFocus(SynergyData synergy)
    {
        pendingFocus = synergy;
    }

    public bool IsOrderDropdownExpanded => orderDropdown != null && orderDropdown.IsExpanded;

    public void CloseOrderDropdown()
    {
        if (orderDropdown != null)
        {
            orderDropdown.Hide();
        }
    }
    
    public void OrderReverse(bool reverse)
    {
        isReversed = reverse;
        Order((int)orderType);
        ApplySiblingOrder(reverse);
    }

    public void Order(int orderTypeInt)
    {
        if (orderType == (OrderType)orderTypeInt)
        {
            return;
        }

        var pre = orderType;
        orderType = (OrderType)orderTypeInt;
        if (!SortComponents())
        {
            Debug.LogWarning($"'{nameof(orderType)}'에 잘못된 값이 입력되었습니다: {orderType}");
            orderType = pre;
            return;
        }

        ApplySiblingOrder(false);
    }

    /// <summary>
    /// 생성한 항목을 지우고 최신 보유 피규어 기준으로 다시 만든 뒤 현재 정렬 상태를 다시 적용한다.
    /// </summary>
    public void Refresh()
    {
        hasLoaded = true;
        ApplyLocalizedLabels();
        ClearWiki();
        LoadWiki();
        SortComponents();
        ApplySiblingOrder(isReversed);

        if (noSynergyMessageObj != null)
        {
            noSynergyMessageObj.SetActive(components.Count == 0);
        }

        ApplyFocus();
    }

    private void ApplyLocalizedLabels()
    {
        if (orderDropdown != null)
        {
            int count = Mathf.Min(orderDropdown.options.Count, OrderLabelKeys.Length);
            for (int i = 0; i < count; i++)
            {
                orderDropdown.options[i].text = LocalizationManager.GetUi(OrderLabelKeys[i].key, OrderLabelKeys[i].fallback);
            }
            orderDropdown.RefreshShownValue();
        }

        if (reverseLabel != null)
        {
            reverseLabel.text = LocalizationManager.GetUi("UI_SYNERGY_SORT_REVERSE", "역순");
        }
    }

    private bool SortComponents()
    {
        switch (orderType)
        {
            case OrderType.Name:
                components.Sort((x, y) => x.Item2.SynergyName.CompareTo(y.Item2.SynergyName));
                return true;

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
                return true;

            case OrderType.FigureCount:
                components.Sort(
                    (x, y) => x.Item2.RequiredFigures.Count
                    .CompareTo(y.Item2.RequiredFigures.Count));
                return true;

            default:
                return false;
        }
    }

    private void ApplySiblingOrder(bool reverse)
    {
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

    private void Start()
    {
        // 페이지를 열면서 Refresh()가 먼저 불렸으면 다시 만들지 않는다.
        if (!hasLoaded)
        {
            Refresh();
        }
    }

    private void ClearWiki()
    {
        foreach (var (component, _) in components)
        {
            if (component == null)
            {
                continue;
            }

            // Destroy는 프레임 끝에 처리되므로 레이아웃에서 바로 빠지도록 먼저 끈다.
            component.gameObject.SetActive(false);
            Destroy(component.gameObject);
        }

        components.Clear();
    }

    private void LoadWiki()
    {
        if (InventoryManager.Instance == null ||
            SynergyManager.Instance == null)
        {
            Debug.Log("아직 데이터가 로드되지 않았습니다.");
            return;
        }

        ownedFigures = CollectOwnedFigures();
        var synergies = SynergyManager.Instance.GetAllSynergies()
            .OrderBy(s => s.SynergyName);

        if (rowTemplate != null)
        {
            LoadRows(synergies);
            return;
        }

        foreach (var synergy in synergies)
        {
            var component = Instantiate(componentObj, viewport).transform;
            var synergyIcon = component
               .Find("Description/DescriptionBackground/SynergyIcon")
               .GetComponent<Image>();
            synergyIcon.sprite = synergy.icon;
            // 아이콘이 아직 없는 시너지는 흰 사각형 대신 비워 둔다.
            synergyIcon.enabled = synergy.icon != null;
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
                figureFrame.GetComponentInChildren<TextMeshProUGUI>().text = LocalizationManager.GetItemDisplayName(figure);
                figureFrame.Find("Silhouette").gameObject.SetActive(!ownedFigures.Contains(figure));
            }
        }
    }

    private void LoadRows(IOrderedEnumerable<SynergyData> synergies)
    {
        foreach (SynergyData synergy in synergies)
        {
            Transform row = Instantiate(rowTemplate, viewport).transform;
            row.gameObject.SetActive(true);
            Image background = row.GetComponent<Image>();
            if (background != null && rowNormalSprite != null)
            {
                background.sprite = rowNormalSprite;
                background.color = Color.white;
            }

            Image icon = row.Find("Icon").GetComponent<Image>();
            icon.sprite = synergy.icon;
            icon.enabled = synergy.icon != null;
            row.Find("Name").GetComponent<TextMeshProUGUI>().text = synergy.SynergyName;

            SynergyData captured = synergy;
            row.GetComponent<Button>().onClick.AddListener(() => Select(captured, true));
            components.Add((row, synergy));
        }
    }

    private void ApplyFocus()
    {
        if (rowTemplate == null || components.Count == 0) return;

        SynergyData target = pendingFocus;
        pendingFocus = null;
        if (target == null || components.All(item => item.Item2 != target))
            target = selectedSynergy != null && components.Any(item => item.Item2 == selectedSynergy)
                ? selectedSynergy
                : components[0].Item2;
        Select(target, true);
    }

    private void Select(SynergyData target, bool scroll)
    {
        Transform row = null;
        foreach (var (component, data) in components)
        {
            bool chosen = data == target;
            Image background = component.GetComponent<Image>();
            if (background != null && rowNormalSprite != null)
                background.sprite = chosen && rowSelectedSprite != null ? rowSelectedSprite : rowNormalSprite;
            if (chosen) row = component;
        }

        selectedSynergy = target;
        if (scroll && row is RectTransform rect)
            ScrollTo(rect);
        ShowDetail(target);
    }

    private void ShowDetail(SynergyData synergy)
    {
        if (synergy == null) return;
        if (detailName != null) detailName.text = synergy.SynergyName;
        if (detailDescription != null) detailDescription.text = synergy.SynergyDescription;
        if (detailIcon != null)
        {
            detailIcon.sprite = synergy.icon;
            detailIcon.enabled = synergy.icon != null;
        }
        if (detailFigureRoot == null || figureFrameObj == null) return;

        for (int i = detailFigureRoot.childCount - 1; i >= 0; i--)
        {
            GameObject child = detailFigureRoot.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
        }

        foreach (FigureItemSO figure in synergy.RequiredFigures)
        {
            if (figure == null) continue;
            Transform frame = Instantiate(figureFrameObj, detailFigureRoot).transform;
            frame.GetComponentInChildren<Image>().sprite = figure.icon;
            frame.GetComponentInChildren<TextMeshProUGUI>().text = LocalizationManager.GetItemDisplayName(figure);
            Transform silhouette = frame.Find("Silhouette");
            if (silhouette != null) silhouette.gameObject.SetActive(!ownedFigures.Contains(figure));
        }
    }

    private void ScrollTo(RectTransform row)
    {
        if (listScroll == null || listScroll.content == null || listScroll.viewport == null) return;
        Canvas.ForceUpdateCanvases();
        float hidden = listScroll.content.rect.height - listScroll.viewport.rect.height;
        if (hidden <= 0f) return;
        float fromTop = Mathf.Max(0f, -row.anchoredPosition.y - listScroll.viewport.rect.height * 0.5f);
        listScroll.verticalNormalizedPosition = 1f - Mathf.Clamp01(fromTop / hidden);
    }

    // 저장 데이터의 보유 목록에 현재 런의 인벤토리를 더한다. 저장 전에 얻은 피규어도 바로 반영된다.
    private static HashSet<FigureItemSO> CollectOwnedFigures()
    {
        var result = new HashSet<FigureItemSO>();

        var saveManager = GameSaveManager.Instance;
        var data = saveManager != null ? saveManager.LoadSaveData() : null;
        if (data != null && data.ownedFigureIDs != null)
        {
            foreach (var id in data.ownedFigureIDs)
            {
                var figure = saveManager.FindFigureByID(id);
                if (figure != null)
                {
                    result.Add(figure);
                }
            }
        }

        if (InventoryManager.Instance != null && InventoryManager.Instance.ownedFigures != null)
        {
            foreach (var figure in InventoryManager.Instance.ownedFigures)
            {
                if (figure != null)
                {
                    result.Add(figure);
                }
            }
        }

        return result;
    }

    private enum OrderType
    {
        Name,
        OwnedFigurePercentile,
        FigureCount,
    }
}
