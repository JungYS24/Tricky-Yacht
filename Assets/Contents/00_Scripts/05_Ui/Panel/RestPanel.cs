using UnityEngine;
using UnityEngine.UI;

// 휴식 노드. 체력 회복 또는 주사위 하나 파괴 중 하나를 고릅니다.
public class RestPanel : MonoBehaviour
{
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
        dim.color = new Color(0.04f, 0.05f, 0.08f, 0.82f);

        var box = CreateBox(transform, new Vector2(640f, 420f));
        CreateLabel(box, "휴식", 36, new Vector2(0f, 140f), 520f);
        MapTableData table = diceManager != null ? diceManager.GetMapTable() : null;
        string desc = table != null ? table.GetNodeRule(MapNodeType.Rest)?.Desc_KR : "체력을 회복하거나 주사위를 파괴합니다.";
        CreateLabel(box, desc ?? "", 22, new Vector2(0f, 70f), 520f);

        int percent = table != null ? table.Config.RestHealPercent : 30;
        CreateButton(box, $"체력 회복 ({percent}%)", new Vector2(0f, -20f), OnHeal);
        CreateButton(box, "주사위 파괴", new Vector2(0f, -110f), OnDestroyDice);
    }

    void OnHeal()
    {
        if (diceManager == null) return;
        MapTableData table = diceManager.GetMapTable();
        int percent = table != null ? table.Config.RestHealPercent : 30;
        int amount = Mathf.FloorToInt(diceManager.playerMaxHP * (percent / 100f));
        diceManager.playerStatus.Heal(amount);
        diceManager.ui?.UpdateShieldUI(diceManager.currentShield);
        diceManager.UpdateMainUI("");
        diceManager.ReturnToMap();
    }

    void OnDestroyDice()
    {
        if (diceManager == null || diceManager.shopManager == null) return;
        DiceDestructionPanel panel = diceManager.shopManager.diceDestructionPanel;
        if (panel == null)
        {
            diceManager.ReturnToMap();
            return;
        }

        panel.onClosed = () =>
        {
            if (diceManager != null) diceManager.ReturnToMap();
        };
        diceManager.shopManager.ShowDiceDestructionSelection();
        if (panel.panelRoot == null || !panel.panelRoot.activeSelf)
        {
            panel.onClosed = null;
            diceManager.ReturnToMap();
        }
    }

    static RectTransform CreateBox(Transform parent, Vector2 size)
    {
        var go = new GameObject("Box", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = size;
        go.GetComponent<Image>().color = new Color(0.12f, 0.14f, 0.18f, 1f);
        return rect;
    }

    static void CreateLabel(Transform parent, string message, int size, Vector2 position, float width)
    {
        var go = new GameObject("Label", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(width, 80f);
        rect.anchoredPosition = position;
        var text = go.GetComponent<Text>();
        text.font = MapPanel.BuiltinFont();
        text.fontSize = size;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.text = message;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
    }

    static void CreateButton(Transform parent, string label, Vector2 position, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(420f, 64f);
        rect.anchoredPosition = position;
        go.GetComponent<Image>().color = new Color(0.22f, 0.28f, 0.36f, 1f);
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
