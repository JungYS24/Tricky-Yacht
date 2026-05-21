using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 전리품 선택 버튼 하나하나에 붙을 스크립트
public class LootChoiceSlot : MonoBehaviour
{
    [Header("UI 연결 (슬롯 내부)")]
    public Image lootIcon;
    public TextMeshProUGUI lootNameText;
    public TextMeshProUGUI lootDescText;


    private BaseItemDataSO currentLootData;
    private LootSelectionPanel parentPanel;

    private void Awake()
    {

        Button selfButton = GetComponent<Button>();
        if (selfButton != null)
        {
            selfButton.onClick.RemoveAllListeners();
            selfButton.onClick.AddListener(OnClicked);
        }
    }

    public void Setup(BaseItemDataSO data, LootSelectionPanel panel)
    {
        currentLootData = data;
        parentPanel = panel;

        // 아이템 정보 채우기
        lootIcon.sprite = data.icon;
        lootNameText.text = data.itemName;
        //툴팁 대신 슬롯 내부 텍스트에 설명을 채웁니다.
        if (lootDescText != null)
        {
            lootDescText.text = data.description;
        }
    }

    private void OnClicked()
    {
        // 부모 패널에게 내가 선택되었다고 알림
        if (parentPanel != null && currentLootData != null)
        {
            parentPanel.OnLootSelected(currentLootData);
        }
    }

}