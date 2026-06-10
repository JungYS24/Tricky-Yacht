using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class BiomeChoiceSlot : MonoBehaviour
{
    public Image backgroundImage;
    public TextMeshProUGUI biomeNameText;
    public Button selectButton;

    private BiomeType targetBiomeType;
    private DiceManager diceManager;

    public void Setup(BiomeDataSO data, DiceManager manager)
    {
        targetBiomeType = data.biomeType;
        diceManager = manager;

        if (backgroundImage != null) backgroundImage.sprite = data.choiceBackgroundImaage;
        if (biomeNameText != null) biomeNameText.text = data.biomeName;

        selectButton.onClick.RemoveAllListeners();
        selectButton.onClick.AddListener(OnSelected);
    }

    private void OnSelected()
    {
        diceManager.ApplySelectedBiome(targetBiomeType);
    }
}

