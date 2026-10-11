using System;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

// 휴식 노드. 체력 회복 또는 주사위 하나 파괴 중 하나를 고릅니다.
public class RestPanel : MonoBehaviour
{
    public TMP_Text titleText;
    public Button healButton;
    public TMP_Text healNameText;
    public TMP_Text healDescText;
    public Button destroyButton;
    public TMP_Text destroyNameText;
    public TMP_Text destroyDescText;

    DiceManager diceManager;

    void OnEnable()
    {
        LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
    }

    void OnDisable()
    {
        LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
    }

    void OnLocaleChanged(Locale _)
    {
        if (isActiveAndEnabled)
            RefreshTexts();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public void Open(DiceManager manager)
    {
        diceManager = manager;
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
        if (titleText != null)
            titleText.text = LocalizationManager.GetUi("UI_REST_TITLE", "Choose an Option");
        if (healNameText != null)
            healNameText.text = LocalizationManager.GetUi("UI_REST_HEAL_NAME", "Rest");
        if (destroyNameText != null)
            destroyNameText.text = LocalizationManager.GetUi("UI_REST_DESTROY_NAME", "Destroy");
        if (destroyDescText != null)
            destroyDescText.text = LocalizationManager.GetUi("UI_REST_DESTROY_DESC", "Remove a Die");
        if (healDescText != null)
        {
            MapTableData table = diceManager != null ? diceManager.GetMapTable() : null;
            int percent = table != null ? table.Config.RestHealPercent : 30;
            string template = LocalizationManager.GetUi("UI_REST_HEAL_DESC", "Recover HP (+{0}%)");
            try
            {
                healDescText.text = string.Format(template, percent);
            }
            catch (FormatException)
            {
                healDescText.text = template;
            }
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
