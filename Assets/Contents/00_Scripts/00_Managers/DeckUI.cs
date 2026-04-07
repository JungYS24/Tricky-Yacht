using UnityEngine;

public class DeckUI : MonoBehaviour
{
    public DiceManager diceManager;

    [Header("UI 텍스트 참조")]
    public TMPro.TextMeshProUGUI totalText;
    public TMPro.TextMeshProUGUI normalText;
    public TMPro.TextMeshProUGUI prismText;
    public TMPro.TextMeshProUGUI goldText;
    public TMPro.TextMeshProUGUI blackText;

    public GameObject deckPanel; 

    public void OnClickDeckButton()
    {
        if (deckPanel.activeSelf)
        {
            CloseDeckPanel();
            return;
        }

        UpdateDeckStatus();
        deckPanel.SetActive(true);
    }

  
    private void UpdateDeckStatus()
    {
        var status = diceManager.GetCurrentDeckStatus();

        totalText.text = "남아 있는 주사위 : " + status.totalCount.ToString();
        normalText.text = "x" + status.normalCount;
        prismText.text = "x" + status.prismCount;
        goldText.text = "x" + status.goldCount;
        blackText.text = "x" + status.blackCount;
    }

    public void CloseDeckPanel()
    {
        deckPanel.SetActive(false);
    }
}