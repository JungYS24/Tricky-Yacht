using UnityEngine;
using UnityEngine.UI;

// 오펠 노드. 위성 4종 중 하나를 고르면 기존 위성 장착 창을 엽니다.
public class OpelPanel : MonoBehaviour
{
    static readonly SatelliteType[] Satellites =
    {
        SatelliteType.Jupiter,
        SatelliteType.Mars,
        SatelliteType.Mercury,
        SatelliteType.Venus
    };

    static readonly string[] FallbackNames = { "목성", "화성", "수성", "금성" };

    DiceManager diceManager;
    bool built;

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public void Open(DiceManager manager)
    {
        diceManager = manager;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        EnsureUi();
    }

    void EnsureUi()
    {
        if (built) return;
        built = true;

        var root = GetComponent<RectTransform>();
        if (root == null) root = gameObject.AddComponent<RectTransform>();
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;

        var dim = gameObject.GetComponent<Image>();
        if (dim == null) dim = gameObject.AddComponent<Image>();
        dim.color = new Color(0.04f, 0.06f, 0.1f, 0.82f);

        var boxGo = new GameObject("Box", typeof(RectTransform), typeof(Image));
        boxGo.transform.SetParent(transform, false);
        var box = boxGo.GetComponent<RectTransform>();
        box.sizeDelta = new Vector2(720f, 520f);
        boxGo.GetComponent<Image>().color = new Color(0.12f, 0.14f, 0.2f, 1f);

        var titleGo = new GameObject("Title", typeof(RectTransform), typeof(Text));
        titleGo.transform.SetParent(box, false);
        var titleRect = titleGo.GetComponent<RectTransform>();
        titleRect.sizeDelta = new Vector2(600f, 60f);
        titleRect.anchoredPosition = new Vector2(0f, 190f);
        var title = titleGo.GetComponent<Text>();
        title.font = MapPanel.BuiltinFont();
        title.fontSize = 36;
        title.alignment = TextAnchor.MiddleCenter;
        title.color = Color.white;
        MapTableData table = diceManager != null ? diceManager.GetMapTable() : null;
        title.text = table != null ? table.GetNodeName(MapNodeType.Opel) : "오펠";

        for (int i = 0; i < Satellites.Length; i++)
        {
            SatelliteType type = Satellites[i];
            float y = 80f - i * 80f;
            CreateButton(box, SatelliteLabel(type, FallbackNames[i]), new Vector2(0f, y), () => OnPick(type));
        }
    }

    void OnPick(SatelliteType type)
    {
        if (diceManager == null || diceManager.shopManager == null)
        {
            diceManager?.ReturnToMap();
            return;
        }

        SatelliteSelectionPanel panel = diceManager.shopManager.satelliteSelectionPanel;
        if (panel == null)
        {
            diceManager.ReturnToMap();
            return;
        }

        panel.onClosed = () =>
        {
            if (diceManager != null) diceManager.ReturnToMap();
        };
        diceManager.shopManager.ShowSatelliteSelection(type);
    }

    static string SatelliteLabel(SatelliteType type, string fallback)
    {
        string key = "SAT_" + type.ToString().ToUpperInvariant() + "_NAME";
        if (LocalizationManager.TryGetLocalized(LocalizationManager.SatelliteTable, key, out string localized))
            return localized;
        return fallback;
    }

    static void CreateButton(Transform parent, string label, Vector2 position, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(460f, 64f);
        rect.anchoredPosition = position;
        go.GetComponent<Image>().color = new Color(0.2f, 0.28f, 0.42f, 1f);
        go.GetComponent<Button>().onClick.AddListener(onClick);

        var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textGo.transform.SetParent(go.transform, false);
        var textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        var text = textGo.GetComponent<Text>();
        text.font = MapPanel.BuiltinFont();
        text.fontSize = 26;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.text = label;
    }
}
