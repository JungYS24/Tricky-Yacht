using UnityEngine;
using UnityEngine.UI;

// 오펠 노드. 위성 4종 중 하나를 고르면 기존 위성 장착 창을 엽니다.
public class OpelPanel : MonoBehaviour
{
    public Text titleText;
    public Button jupiterButton;
    public Button marsButton;
    public Button mercuryButton;
    public Button venusButton;

    DiceManager diceManager;

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public void Open(DiceManager manager)
    {
        diceManager = manager;
        MapUiHierarchy.EnsureOpel(this);
        Wire();
        RefreshTexts();
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
    }

    void Wire()
    {
        Bind(jupiterButton, SatelliteType.Jupiter);
        Bind(marsButton, SatelliteType.Mars);
        Bind(mercuryButton, SatelliteType.Mercury);
        Bind(venusButton, SatelliteType.Venus);
    }

    void Bind(Button button, SatelliteType type)
    {
        if (button == null) return;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => OnPick(type));
    }

    void RefreshTexts()
    {
        MapTableData table = diceManager != null ? diceManager.GetMapTable() : null;
        if (titleText != null)
            titleText.text = table != null ? table.GetNodeName(MapNodeType.Opel) : "오펠";
        SetButtonLabel(jupiterButton, SatelliteType.Jupiter, "목성");
        SetButtonLabel(marsButton, SatelliteType.Mars, "화성");
        SetButtonLabel(mercuryButton, SatelliteType.Mercury, "수성");
        SetButtonLabel(venusButton, SatelliteType.Venus, "금성");
    }

    static void SetButtonLabel(Button button, SatelliteType type, string fallback)
    {
        if (button == null) return;
        Text label = button.GetComponentInChildren<Text>();
        if (label != null) label.text = SatelliteLabel(type, fallback);
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
}
