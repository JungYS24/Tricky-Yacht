using UnityEngine;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance { get; private set; }

    [Header("참조")]
    public DiceManager diceManager;

    [Header("슬롯 배열")]
    public InventorySlot[] figureSlots; 
    public InventorySlot[] snackSlots;  

    private void Awake()
    {
        Instance = this;

        foreach (var slot in figureSlots) slot.Initialize(this);
        foreach (var slot in snackSlots) slot.Initialize(this);
    }

    public bool AddItem(BaseItemDataSO item)
    {
        if (item is FigureItemSO)
        {
            return PlaceIntoEmptySlot(item, figureSlots);
        }
        else if (item is SnackItemSO)
        {
            return PlaceIntoEmptySlot(item, snackSlots);
        }

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
}