using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance { get; private set; }

    [Header("참조")]
    public DiceManager diceManager;

    [Header("슬롯 배열")]
    public InventorySlot[] figureSlots;
    public InventorySlot[] snackSlots;

    [Header("판매 팝업 UI")]
    public GameObject sellPopupRoot;      
    public GameObject sellPopupPanel;     // 실제 그래픽이 있는 팝업창 (마우스 따라다닐 부분)
    public Button sellButton;             // 판매 확인 버튼
    public Button backgroundCloseButton;  // 팝업 뒤에 깔린 투명한 전체화면 닫기 버튼
    public TextMeshProUGUI sellPriceText;

    private InventorySlot targetSellSlot;

    private void Awake()
    {
        Instance = this;

        foreach (var slot in figureSlots) slot.Initialize(this);
        foreach (var slot in snackSlots) slot.Initialize(this);

        if (sellButton != null) sellButton.onClick.AddListener(SellTargetItem);

        // 취소 버튼 대신 투명한 배경을 누르면 팝업이 닫히도록 연결
        if (backgroundCloseButton != null) backgroundCloseButton.onClick.AddListener(HideSellPopup);

        HideSellPopup();
    }

    public bool AddItem(BaseItemDataSO item)
    {
        if (item is FigureItemSO) return PlaceIntoEmptySlot(item, figureSlots);
        else if (item is SnackItemSO) return PlaceIntoEmptySlot(item, snackSlots);
        return false;
    }

    private bool PlaceIntoEmptySlot(BaseItemDataSO item, InventorySlot[] slots)
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i].isEmpty)
            {
                slots[i].SetItem(item);
                return true;
            }
        }
        return false;
    }

    public void ShowSellPopup(InventorySlot slot)
    {
        targetSellSlot = slot;

        int sellPrice = Mathf.FloorToInt(slot.currentItem.price * 0.5f);
        if (sellPriceText != null) sellPriceText.text = $"판매: {sellPrice} G";

        if (sellPopupRoot != null)
        {
            // 전체 팝업 루트를 켭니다 (투명 배경 활성화)
            sellPopupRoot.SetActive(true);

            // 실제 내용물이 있는 작은 팝업창만 마우스(슬롯) 위치 근처로 이동시킵니다
            if (sellPopupPanel != null)
            {

                sellPopupPanel.transform.position = slot.transform.position;

                sellPopupPanel.transform.localPosition += new Vector3(0f, 100f, 0f);

                Vector3 localPos = sellPopupPanel.transform.localPosition;
                localPos.z = 0f;
                sellPopupPanel.transform.localPosition = localPos;


            }
        }
    }

    public void HideSellPopup()
    {
        if (sellPopupRoot != null) sellPopupRoot.SetActive(false);
        targetSellSlot = null;
    }

    private void SellTargetItem()
    {
        if (targetSellSlot == null || targetSellSlot.isEmpty) return;

        int sellPrice = Mathf.FloorToInt(targetSellSlot.currentItem.price * 0.5f);

        if (diceManager != null && diceManager.shopManager != null)
        {
            diceManager.shopManager.currentGold += sellPrice;
            diceManager.ui?.UpdateGoldUI(diceManager.shopManager.currentGold);
        }

        Debug.Log($"피규어 [{targetSellSlot.currentItem.itemName}] 판매 완료! +{sellPrice} G");

        targetSellSlot.ClearSlot();
        HideSellPopup();
    }
}