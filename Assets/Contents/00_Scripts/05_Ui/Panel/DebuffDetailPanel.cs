using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 디버프 주사위 설명 패널. 인벤토리의 피규어 설명 패널을 실행 중에 복제해 틀·배경·크기·배치를 그대로 씁니다.
// 맨 위 "영구 디버프 / 이번 전투 한정 디버프" 표시로 구분하고, 이번 전투 한정이면 주사위 그림 칸 오른쪽 아래에 모래시계를 붙입니다.
// 씬에 따로 배치하지 않습니다.
public sealed class DebuffDetailPanel : MonoBehaviour
{
    public struct Entry
    {
        public DebuffDiceType type;
        public int count;
        public Sprite sprite;
        public string name;
        public string description;
        public bool temporary;
    }

    public static bool IsPanelOpen { get; private set; }
    static DebuffDetailPanel instance;

    Image dieImage;
    TextMeshProUGUI nameText;
    TextMeshProUGUI descText;
    Button leftButton;
    Button rightButton;
    Image hourglass;
    RectTransform stage;
    RectTransform stageShadow;
    TextMeshProUGUI kindLabel;
    List<Entry> entries = new List<Entry>();
    int index;

    public static void Open(List<Entry> list, int selected)
    {
        if (list == null || list.Count == 0) return;
        if (instance == null) instance = Build();
        if (instance == null) return;
        instance.entries = list;
        instance.index = Mathf.Clamp(selected, 0, list.Count - 1);
        instance.Show();
    }

    public static void Close()
    {
        if (instance != null) instance.Hide();
    }

    void OnDestroy()
    {
        if (instance != this) return;
        instance = null;
        IsPanelOpen = false;
    }

    static DebuffDetailPanel Build()
    {
        FigureDetailPanel template = InventoryManager.Instance != null ? InventoryManager.Instance.figureDetailPanel : null;
        if (template == null || template.panelRoot == null)
        {
            Debug.LogWarning("[Debuff] 피규어 설명 패널을 찾지 못해 디버프 설명 패널을 만들 수 없습니다.");
            return null;
        }
        GameObject source = template.panelRoot;
        // 복제본의 피규어 패널 스크립트가 깨어나 기존 패널 상태를 건드리지 않도록 꺼진 임시 부모 아래에서 복제하고 바로 제거합니다.
        var holder = new GameObject("Debuff Panel Holder");
        holder.SetActive(false);
        GameObject clone = Instantiate(source, holder.transform);
        clone.name = "DebuffDetailPanel";
        foreach (var figurePanel in clone.GetComponentsInChildren<FigureDetailPanel>(true)) DestroyImmediate(figurePanel);

        var panel = clone.AddComponent<DebuffDetailPanel>();
        panel.Bind(template, source.transform, clone.transform);
        clone.transform.SetParent(source.transform.parent, false);
        clone.transform.SetSiblingIndex(source.transform.GetSiblingIndex() + 1);
        clone.SetActive(false);
        Destroy(holder);
        return panel;
    }

    void Bind(FigureDetailPanel template, Transform sourceRoot, Transform cloneRoot)
    {
        dieImage = Mirror<Image>(template.figureImage, sourceRoot, cloneRoot);
        nameText = Mirror<TextMeshProUGUI>(template.nameText, sourceRoot, cloneRoot);
        descText = Mirror<TextMeshProUGUI>(template.descText, sourceRoot, cloneRoot);
        leftButton = Mirror<Button>(template.leftButton, sourceRoot, cloneRoot);
        rightButton = Mirror<Button>(template.rightButton, sourceRoot, cloneRoot);
        Button closeButton = Mirror<Button>(template.closeButton, sourceRoot, cloneRoot);
        if (leftButton != null) leftButton.onClick.AddListener(() => Step(-1));
        if (rightButton != null) rightButton.onClick.AddListener(() => Step(1));
        if (closeButton != null) closeButton.onClick.AddListener(Hide);

        // 틀·배경·글자색은 피규어 패널 그대로 둡니다.
        if (nameText != null)
        {
            // "가시 주사위 ×2"처럼 길어져도 이름 칸을 넘지 않게 글자 크기를 줄여 맞춥니다.
            nameText.fontSizeMax = nameText.fontSize;
            nameText.fontSizeMin = Mathf.Min(nameText.fontSize, 18f);
            nameText.enableAutoSizing = true;
        }
        if (descText != null) descText.richText = true;

        // 좌우 화살표 버튼 안의 "Button" 기본 글자는 숨깁니다.
        foreach (var button in new[] { leftButton, rightButton })
        {
            if (button == null) continue;
            foreach (var label in button.GetComponentsInChildren<TMP_Text>(true)) label.gameObject.SetActive(false);
        }

        var p = DebuffFeedback.Profile;
        if (dieImage == null) return;
        stage = Mirror<RectTransform>(template.figureStage, sourceRoot, cloneRoot);
        stageShadow = Mirror<RectTransform>(template.figureStageShadow, sourceRoot, cloneRoot);
        if (stage != null)
        {
            BindStage(p);
            return;
        }
        RectTransform dieRect = dieImage.rectTransform;
        Rect area = dieRect.rect; // 피규어 그림이 들어가던 그림 칸

        // 이번 전투 한정이면 그림 칸 오른쪽 아래에 모래시계를 붙입니다. 주사위 그림을 줄여도 칸 모서리에 남도록 형제로 둡니다.
        if (p != null && p.hourglass != null)
        {
            hourglass = new GameObject("Hourglass", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            RectTransform glass = hourglass.rectTransform;
            glass.SetParent(dieRect.parent, false);
            glass.SetSiblingIndex(dieRect.GetSiblingIndex() + 1);
            glass.anchorMin = dieRect.anchorMin;
            glass.anchorMax = dieRect.anchorMax;
            glass.pivot = new Vector2(1f, 0f);
            float height = Mathf.Max(28f, area.height * 0.2f);
            glass.sizeDelta = new Vector2(height * p.hourglass.rect.width / Mathf.Max(1f, p.hourglass.rect.height), height);
            // 그림 칸의 오른쪽 아래 모서리(그림 칸 기준점이 어디든 같은 자리)
            Vector2 bottomRight = dieRect.anchoredPosition + new Vector2(area.xMax, area.yMin);
            glass.anchoredPosition = bottomRight + new Vector2(-4f, 4f);
            hourglass.sprite = p.hourglass;
            hourglass.preserveAspect = true;
            hourglass.raycastTarget = false;
        }

        // 디버프 주사위는 가장자리 여백이 없어 칸을 꽉 채우면 커 보이므로, 그림 칸 가운데에 조금 작게 놓습니다.
        float scale = p != null ? p.panelDieScale : 0.65f;
        Vector2 center = dieRect.anchoredPosition + area.center;
        dieRect.pivot = new Vector2(0.5f, 0.5f);
        dieRect.sizeDelta = area.size * scale;
        dieRect.anchoredPosition = center;
    }

    // 피규어 패널이 나무 무대 구성이면, 구분 표시는 무대 위쪽 안에 크림색 글자로, 모래시계는 무대 오른쪽 아래 모서리에 붙입니다.
    // 복제 시점의 피규어 그림 크기(마지막에 본 피규어)에 기대지 않도록 무대 크기를 기준으로 잡습니다.
    void BindStage(DebuffVisualProfile p)
    {
        if (nameText != null)
        {
            kindLabel = Instantiate(nameText, stage);
            kindLabel.name = "KindLabel";
            RectTransform label = kindLabel.rectTransform;
            label.anchorMin = label.anchorMax = new Vector2(0.5f, 1f);
            label.pivot = new Vector2(0.5f, 1f);
            label.sizeDelta = new Vector2(stage.rect.width - 48f, 36f);
            label.anchoredPosition = new Vector2(0f, -16f);
            kindLabel.enableAutoSizing = true;
            kindLabel.fontSizeMax = 24f;
            kindLabel.fontSizeMin = 18f;
            kindLabel.raycastTarget = false;
        }

        if (p != null && p.hourglass != null)
        {
            hourglass = new GameObject("Hourglass", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            RectTransform glass = hourglass.rectTransform;
            glass.SetParent(stage, false);
            glass.anchorMin = glass.anchorMax = new Vector2(1f, 0f);
            glass.pivot = new Vector2(1f, 0f);
            Vector2 size = p.hourglass.rect.size;
            int scale = Mathf.Max(1, Mathf.RoundToInt(stage.rect.height * 0.12f / Mathf.Max(1f, size.y)));
            glass.sizeDelta = size * scale;
            glass.anchoredPosition = new Vector2(-16f, 16f);
            hourglass.sprite = p.hourglass;
            hourglass.preserveAspect = true;
            hourglass.raycastTarget = false;
        }
    }

    void Show()
    {
        if (entries.Count == 0) return;
        Entry entry = entries[index];
        if (dieImage != null)
        {
            dieImage.sprite = entry.sprite;
            dieImage.color = Color.white;
            dieImage.preserveAspect = true;
            // 무대 구성이면 주사위를 그림 박스보다 조금 작은 최대 정수 배로 키워 그림자 위에 세웁니다.
            if (stage != null)
            {
                var p = DebuffFeedback.Profile;
                float box = FigureDetailPanel.PopupFigureIconBox * (p != null ? p.panelDieScale : 0.65f);
                FigureDetailPanel.FitPopupStage(dieImage, stage, stageShadow, box);
            }
        }
        if (nameText != null) nameText.text = entry.count > 1 ? $"{entry.name} ×{entry.count}" : entry.name;
        string kind = entry.temporary
            ? DebuffTrackerHud.Localized(LocalizationManager.GetUi("UI_DEBUFF_TEMPORARY", "이번 전투 한정 디버프"), "이번 전투 한정 디버프")
            : DebuffTrackerHud.Localized(LocalizationManager.GetUi("UI_DEBUFF_PERMANENT", "영구 디버프"), "영구 디버프");
        if (kindLabel != null) kindLabel.text = kind;
        if (descText != null)
            descText.text = kindLabel != null ? entry.description : $"<color=#D9B8FF>{kind}</color>\n{entry.description}";
        if (hourglass != null) hourglass.gameObject.SetActive(entry.temporary);
        if (leftButton != null) leftButton.interactable = index > 0;
        if (rightButton != null) rightButton.interactable = index < entries.Count - 1;

        IsPanelOpen = true;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
    }

    void Step(int direction)
    {
        int next = Mathf.Clamp(index + direction, 0, entries.Count - 1);
        if (next == index) return;
        index = next;
        Show();
    }

    void Hide()
    {
        IsPanelOpen = false;
        gameObject.SetActive(false);
    }

    // 원본 패널의 참조와 같은 위치에 있는 복제본 오브젝트를 찾습니다.
    static T Mirror<T>(Component original, Transform sourceRoot, Transform cloneRoot) where T : Component
    {
        if (original == null) return null;
        if (original.transform == sourceRoot) return cloneRoot.GetComponent<T>();
        var path = new List<string>();
        for (Transform t = original.transform; t != null && t != sourceRoot; t = t.parent) path.Insert(0, t.name);
        Transform found = cloneRoot.Find(string.Join("/", path));
        return found != null ? found.GetComponent<T>() : null;
    }
}
