using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BiomeSelectionPanel : MonoBehaviour
{
    public GameObject panelRoot;
    public BiomeChoiceSlot[] choiceSlots; // 3개의 버튼 슬롯 연결
    public Button mainMenuButton; //메인 메뉴로 돌아가는 버튼
    public void OpenPanel(DiceManager manager, List<BiomeType> nextBiomes)
    {
        for (int i = 0; i < choiceSlots.Length; i++)
        {
            if (i < nextBiomes.Count)
            {
                // DiceManager의 리스트에서 해당 타입의 SO를 찾아 슬롯에 세팅
                BiomeDataSO biomeData = manager.biomeList.Find(b => b.biomeType == nextBiomes[i]);
                if (biomeData != null)
                {
                    choiceSlots[i].gameObject.SetActive(true);
                    choiceSlots[i].Setup(biomeData, manager);
                }
            }
            else
            {
                choiceSlots[i].gameObject.SetActive(false);
            }
        }

        if (mainMenuButton != null)
        {
            mainMenuButton.gameObject.SetActive(false);
        }

        panelRoot.SetActive(true);
    }

    public void ClosePanel()
    {
        panelRoot.SetActive(false);
    }
}