using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class SatelliteSelectionPanel : MonoBehaviour
{
    public static bool IsPanelOpen { get; private set; } = false;

    public GameObject panelRoot;
    public Transform slotParent;
    public GameObject diceSlotPrefab;

    private SatelliteType pendingSatelliteType;
    private List<GameObject> activeSlots = new List<GameObject>();
    private DiceManager diceManager;

    public void OpenSelection(DiceManager dm, SatelliteType type)
    {
        IsPanelOpen = true;
        diceManager = dm;
        pendingSatelliteType = type;

        // 전체 덱을 가져온 뒤 조건에 맞는 주사위만 필터링합니다.
        List<DiceData1> validOptions = new List<DiceData1>();

        foreach (var dice in dm.masterDeck)
        {
            //위성이 4개 미만이어야 함
            //이미 같은 종류의 위성이 없어야 함 (중복 방지)
            if (dice.activeSatellites.Count < 4 && !dice.activeSatellites.Contains(type))
            {
                validOptions.Add(dice);
            }
        }

        if (validOptions.Count == 0)
        {
            //필터링된 주사위가 없다면 돈을 돌려주거나 토스트 메시지 출력
            Debug.Log("위성을 달 수 있는 주사위가 덱에 없습니다! (전부 4개이거나 이미 동일 위성 장착중)");
            if (ToastPopupController.Instance != null) ToastPopupController.Instance.ShowToast("장착 가능한 주사위가 없습니다.");
            ClosePanel();
            return;
        }

        // 유효한 옵션 중 랜덤 5개 추출
        ShuffleList(validOptions);
        int maxShowCount = Mathf.Min(5, validOptions.Count);
        List<DiceData1> finalOptions = validOptions.GetRange(0, maxShowCount);

        ClearSlots();
        panelRoot.SetActive(true);

        foreach (var dice in finalOptions)
        {
            GameObject slotGo = Instantiate(diceSlotPrefab, slotParent);
            activeSlots.Add(slotGo);

            Button btn = slotGo.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.AddListener(() => OnDiceSelected(dice));
            }

            DeckSlot deckSlot = slotGo.GetComponent<DeckSlot>();
            if (deckSlot != null)
            {
                deckSlot.SetDice(dice, false);
            }
        }
    }

    private void OnDiceSelected(DiceData1 selectedDice)
    {
        selectedDice.activeSatellites.Add(pendingSatelliteType);
        Debug.Log($"{selectedDice.diceName}에 {pendingSatelliteType} 위성 장착! (현재 위성 갯수: {selectedDice.activeSatellites.Count})");

        ClosePanel();

        // 아이템 장착 직후 데미지 표기 갱신
        diceManager.ForceUpdateUI();
    }

    public void ClosePanel()
    {
        IsPanelOpen = false;
        panelRoot.SetActive(false);
        ClearSlots();
    }

    private void ClearSlots()
    {
        foreach (var slot in activeSlots) Destroy(slot);
        activeSlots.Clear();
    }

    private void ShuffleList<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int rnd = Random.Range(i, list.Count);
            T temp = list[i];
            list[i] = list[rnd];
            list[rnd] = temp;
        }
    }
}