using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class LootSelectionPanel : MonoBehaviour
{
    //다른 스크립트에서 전리품 창이 열려있는지 확인할 수 있는 변수
    public static bool IsPanelOpen { get; private set; } = false;
    // 전리품을 실제로 받은 직후(튜토리얼 안내용). 인벤토리가 가득 차 못 받으면 부르지 않는다.
    public static event System.Action LootChosen;

    public GameObject panelRoot;
    public LootChoiceSlot[] choiceSlots;

    [Header("1번 칸 랜덤 전리품 풀 (체력, 골드, 티켓팩)")]
    public List<BaseItemDataSO> firstSlotPool;

    [Header("전리품 풀 세팅")]
    public List<SnackItemSO> snackPool; 
    [Header("3번 칸 랜덤 주사위 풀")]
    public List<DiceItemSO> dicePool;

    private DiceManager diceManager;

    private void Awake()
    {
        IsPanelOpen = false; // 시작할 때 초기화
    }

    public void OpenSelection(DiceManager manager)
    {
        diceManager = manager;

        if (snackPool == null) snackPool = new List<SnackItemSO>();
        SnackItemSO.RegisterResourceSnacks(snackPool);

        // 2번 칸은 누락된 에셋을 제외한 스낵(페퍼민트 포함), 3번 칸은 주사위를 랜덤으로 뽑는다.
        List<SnackItemSO> shuffledSnacks = snackPool.FindAll(snack => snack != null);
        List<DiceItemSO> shuffledDice = dicePool != null ? dicePool.FindAll(dice => dice != null) : new List<DiceItemSO>();

        // 각 랜덤 칸의 후보가 준비되었는지 확인
        if (shuffledSnacks.Count < 1 || shuffledDice.Count < 1 || firstSlotPool == null || firstSlotPool.Count < 1)
        {
            Debug.LogWarning("전리품 풀에 아이템이 부족하거나 1번 칸 아이템이 누락되었습니다!");
            ClosePanelAndProceed();
            return;
        }

        // 1번 칸 아이템 1개 랜덤 뽑기
        BaseItemDataSO selectedFirstSlotItem = firstSlotPool[Random.Range(0, firstSlotPool.Count)];

        ShuffleList(shuffledSnacks);
        ShuffleList(shuffledDice);

        // 슬롯 세팅 (1번: 성장 아이템, 2번: 랜덤 스낵, 3번: 랜덤 주사위)
        choiceSlots[0].Setup(selectedFirstSlotItem, this);
        choiceSlots[1].Setup(shuffledSnacks[0], this);
        choiceSlots[2].Setup(shuffledDice[0], this);

        panelRoot.SetActive(true);
        panelRoot.transform.SetAsLastSibling();
        IsPanelOpen = true;
    }

    public void OnLootSelected(BaseItemDataSO selectedLoot)
    {
        if (selectedLoot is SnackItemSO snack)
        {
            bool added = InventoryManager.Instance.AddItem(snack);
            if (!added)
            {
                Debug.Log("스낵 인벤토리가 꽉 차서 받을 수 없습니다!");
                if (ToastPopupController.Instance != null)
                    ToastPopupController.Instance.ShowToast(LocalizationManager.GetSys("SYS_INVENTORY_FULL", "인벤토리가 가득 찼습니다."));
                return; // 꽉 차서 안 들어가면 리턴하여 단계가 넘어가지 않도록 방지
            }
        }
        else
        {
            // DiceItemSO 또는 MaxHPItemSO일 경우 인벤토리에 들어가지 않고 즉시 효과 발동
            selectedLoot.ApplyItemEffect(diceManager);
        }
        Sfx.Play(SfxId.Shop_Purchase);
        LootChosen?.Invoke();
        ClosePanelAndProceed();
    }

    private void ClosePanelAndProceed()
    {
        // 패널 닫기
        IsPanelOpen = false;
        panelRoot.SetActive(false);

        // DiceManager를 통해 잠깐 기다린 뒤 다음 단계로 넘어간다(티켓 선택 창이 열려 있으면 닫힐 때까지 기다림).
        diceManager.StartCoroutine(WaitAndOpenShopRoutine());
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
    // 0.2초 뒤 지도 흐름으로 넘기는 코루틴(지도 표의 전투 후 상점 설정이 켜져 있으면 "상점 갈래?" 선택이 뜬다)
    private IEnumerator WaitAndOpenShopRoutine()
    {
        yield return new WaitForSeconds(0.2f);

        // 티켓 팩을 골라서 티켓 선택 창이 열려있다면, 유저가 고를 때까지 무한 대기!
        if (diceManager.shopManager != null && diceManager.shopManager.ticketSelectionPanel != null)
        {
            while (diceManager.shopManager.ticketSelectionPanel.activeSelf)
            {
                yield return null; // 티켓 선택 창이 닫힐 때까지 대기
            }
        }

        diceManager.OnMapLootFinished();
    }

}
