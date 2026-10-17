using System;
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

    [SerializeField] private GameObject detailFigureTemplate;

    [SerializeField] private TextMeshProUGUI listTitle;

    // 오른쪽 페이지 안쪽 폭. 칸 크기는 이 폭으로 계산한다.
    [SerializeField] private float figureAreaWidth = 604f;

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

        if (listTitle != null)
        {
            listTitle.text = LocalizationManager.GetUi("UI_TAB_SYNERGY", "시너지");
        }
    }

    private bool SortComponents()
    {
        switch (orderType)
        {
            case OrderType.Name:
                // 코드 포인트 순이라 RE:VIVE 같은 라틴 이름이 한글보다 앞에 온다.
                components.Sort((x, y) => string.Compare(
                    x.Item2.SynergyName,
                    y.Item2.SynergyName,
                    StringComparison.Ordinal));
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
            .OrderBy(s => s.SynergyName, StringComparer.Ordinal);

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
            Transform progress = row.Find("Progress");
            if (progress != null)
            {
                progress.GetComponent<TextMeshProUGUI>().text = OwnedCountLabel(synergy);
            }

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
        if (detailIcon != null) detailIcon.sprite = synergy.icon;
        LayoutHeader();
        if (detailFigureRoot == null) return;

        ClearChildren(detailFigureRoot);
        if (detailFigureTemplate != null)
        {
            var figures = new List<FigureItemSO>();
            foreach (FigureItemSO figure in synergy.RequiredFigures)
            {
                if (figure != null) figures.Add(figure);
            }

            float cell = LayoutFigureGrid(figures.Count);
            foreach (FigureItemSO figure in figures)
            {
                CreateBookSlot(figure, cell);
            }

            return;
        }

        if (figureFrameObj == null) return;
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

    // 아이콘과 이름을 한 덩어리로 가운데 둔다. 아이콘이 없으면 이름만 가운데.
    private void LayoutHeader()
    {
        bool showIcon = detailIcon != null && detailIcon.sprite != null;
        if (detailIcon != null)
        {
            detailIcon.gameObject.SetActive(showIcon);
            if (showIcon) FitInteger(detailIcon, 80f);
        }

        if (detailName == null) return;
        detailName.fontStyle = FontStyles.Bold;
        detailName.textWrappingMode = TextWrappingModes.NoWrap;
        float iconWidth = showIcon ? detailIcon.rectTransform.sizeDelta.x : 0f;
        float spacing = showIcon ? 16f : 0f;
        float maxWidth = Mathf.Max(80f, figureAreaWidth - iconWidth - spacing);
        detailName.enableAutoSizing = false;
        detailName.fontSize = 48f;
        detailName.ForceMeshUpdate();
        float preferred = detailName.preferredWidth;
        if (preferred > 1f && preferred <= maxWidth)
        {
            detailName.rectTransform.sizeDelta = new Vector2(preferred, 64f);
            detailName.overflowMode = TextOverflowModes.Overflow;
            return;
        }

        detailName.rectTransform.sizeDelta = new Vector2(maxWidth, 64f);
        detailName.enableAutoSizing = true;
        detailName.fontSizeMin = 28f;
        detailName.fontSizeMax = 48f;
        detailName.overflowMode = TextOverflowModes.Ellipsis;
    }

    // 기본 96. 한 줄에 안 들어가면 줄이고, 72보다 작아지면 두 줄로 나눈다.
    private float LayoutFigureGrid(int count)
    {
        const float spacing = 12f;
        const float cellMax = 96f;
        const float cellMin = 72f;
        float width = figureAreaWidth > 1f ? figureAreaWidth : 604f;
        int columns = Mathf.Max(1, count);
        float cell = FitCell(width, columns, spacing, cellMax);
        if (count > 1 && cell < cellMin)
        {
            columns = Mathf.CeilToInt(count / 2f);
            cell = FitCell(width, columns, spacing, cellMax);
        }

        GridLayoutGroup grid = detailFigureRoot.GetComponent<GridLayoutGroup>();
        if (grid != null)
        {
            grid.cellSize = new Vector2(cell, cell);
            grid.spacing = new Vector2(spacing, spacing);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = columns;
        }

        return cell;
    }

    private static float FitCell(float width, int columns, float spacing, float cellMax)
    {
        float fit = (width - spacing * (columns - 1)) / columns;
        return Mathf.Min(cellMax, fit);
    }

    private void CreateBookSlot(FigureItemSO figure, float cell)
    {
        Transform slot = Instantiate(detailFigureTemplate, detailFigureRoot).transform;
        slot.gameObject.SetActive(true);
        Image icon = slot.Find("Icon").GetComponent<Image>();
        Image silhouette = slot.Find("Silhouette").GetComponent<Image>();
        Image plate = slot.Find("Plate").GetComponent<Image>();
        icon.sprite = figure.icon;
        icon.color = Color.white;
        icon.preserveAspect = true;
        FitInteger(icon, cell * 0.75f);
        silhouette.sprite = figure.icon;
        silhouette.color = new Color(0f, 0f, 0f, 0.9019608f);
        silhouette.preserveAspect = true;
        silhouette.rectTransform.sizeDelta = icon.rectTransform.sizeDelta;
        // 보유는 저장 목록 + 이번 판 인벤토리. 없으면 스프라이트 형태의 어두운 실루엣.
        silhouette.gameObject.SetActive(!ownedFigures.Contains(figure));
        FitSlotFrame(plate, cell);
    }

    // 테두리 픽셀이 칸 크기에 비례하게 남도록 9-slice 배율을 맞춘다.
    private static void FitSlotFrame(Image plate, float cell)
    {
        if (plate == null || plate.sprite == null || plate.sprite.border.x <= 0f || cell <= 0f) return;
        float ppu = plate.pixelsPerUnit;
        float borderUnits = cell * (plate.sprite.border.x / plate.sprite.rect.width);
        if (ppu <= 0.001f || borderUnits <= 0.001f) return;
        plate.pixelsPerUnitMultiplier = plate.sprite.border.x / (ppu * borderUnits);
    }

    // 80px을 넘지 않는 가장 큰 정수 배.
    private static void FitInteger(Image image, float maxSize)
    {
        if (image == null || image.sprite == null) return;
        float longest = Mathf.Max(image.sprite.rect.width, image.sprite.rect.height);
        if (longest <= 0f) return;
        int scale = 1;
        while (longest * (scale + 1) <= maxSize) scale++;
        image.rectTransform.sizeDelta = image.sprite.rect.size * scale;
    }

    private static void ClearChildren(Transform root)
    {
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            GameObject child = root.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
        }
    }

    private string OwnedCountLabel(SynergyData synergy)
    {
        int total = 0;
        int owned = 0;
        foreach (FigureItemSO figure in synergy.RequiredFigures)
        {
            if (figure == null) continue;
            total++;
            if (ownedFigures.Contains(figure)) owned++;
        }

        return owned + " / " + total;
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
