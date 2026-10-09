using UnityEngine;
using UnityEngine.UI;

// 휴식 노드. 체력 회복 또는 주사위 하나 파괴 중 하나를 고릅니다.
public class RestPanel : MonoBehaviour
{
    public Text titleText;
    public Text descText;
    public Button healButton;
    public Button destroyButton;

    DiceManager diceManager;

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public void Open(DiceManager manager)
    {
        diceManager = manager;
        MapUiHierarchy.EnsureRest(this);
        Wire();
        RefreshTexts();
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
    }

    void Wire()
    {
        if (healButton != null)
        {
            healButton.onClick.RemoveListener(OnHeal);
            healButton.onClick.AddListener(OnHeal);
        }
        if (destroyButton != null)
        {
            destroyButton.onClick.RemoveListener(OnDestroyDice);
            destroyButton.onClick.AddListener(OnDestroyDice);
        }
    }

    void RefreshTexts()
    {
        MapTableData table = diceManager != null ? diceManager.GetMapTable() : null;
        if (titleText != null)
            titleText.text = table != null ? table.GetNodeName(MapNodeType.Rest) : "휴식";
        if (descText != null)
        {
            MapNodeTypeRow rule = table != null ? table.GetNodeRule(MapNodeType.Rest) : null;
            descText.text = rule != null && !string.IsNullOrEmpty(rule.Desc_KR)
                ? rule.Desc_KR
                : "체력을 회복하거나 주사위를 파괴합니다.";
        }
        if (healButton != null)
        {
            int percent = table != null ? table.Config.RestHealPercent : 30;
            Text label = healButton.GetComponentInChildren<Text>();
            if (label != null) label.text = $"체력 회복 ({percent}%)";
        }
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
}
