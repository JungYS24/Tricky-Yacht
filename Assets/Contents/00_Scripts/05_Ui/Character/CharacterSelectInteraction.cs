using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

public class CharacterSelectInteraction : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private CharacterData characterData;

    [SerializeField] private TextMeshProUGUI nameText;
    
    [SerializeField] private UnityEvent<CharacterData> OnClickDisplay;

    public void OnPointerClick(PointerEventData eventData)
    {
        OnClickDisplay.Invoke(characterData);
    }

    public void Awake()
    {
        if (characterData.IsUnlocked)
        {
            nameText.text = characterData.CharacterName;

            OnClickDisplay.AddListener(data =>
            {
                Debug.Log(data.name);
                Debug.Log(data.CharacterName);
                Debug.Log(data.InitialHP);
                Debug.Log(data.IsUnlocked);

                Debug.Log(data.DedicateDiceData != null ? data.DedicateDiceData.name : "None");
                Debug.Log(data.DedicateFigureData != null ? data.DedicateFigureData.name : "None");
            });
        }
        else
        {
            nameText.text = "???";
            enabled = false;
        }
    }
}
