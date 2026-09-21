using TMPro;
using UnityEngine;

public class CharacterApply : MonoBehaviour
{
    [SerializeField] private GameObject applyButton;

    [SerializeField] private GameObject finalCheckPanel;

    [SerializeField] private TextMeshProUGUI nameText;

    private CharacterData data;

    private void Start()
    {
        applyButton.SetActive(false);
        finalCheckPanel.SetActive(false);
    }

    public void SetCharacter(CharacterData character)
    {
        data = character;
        nameText.text = data.CharacterName;
        applyButton.SetActive(true);
    }

    public void OnApply()
    {
        finalCheckPanel.SetActive(true);
    }

    public void OnFinalApply(bool apply)
    {
        if (apply)
        {
            CharacterStatus status = new(data.CharacterName, data.InitialHP, data.DedicateDiceData, data.DedicateFigureData);
            Debug.Log("시작");
        }
        else
        {
            finalCheckPanel.SetActive(false);
        }
    }
}
