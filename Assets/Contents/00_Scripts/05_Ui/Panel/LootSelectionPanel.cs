using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class LootSelectionPanel : MonoBehaviour
{
    //다른 스크립트에서 전리품 창이 열려있는지 확인할 수 있는 변수
    public static bool IsPanelOpen { get; private set; } = false;

    public GameObject panelRoot;
    public LootChoiceSlot[] choiceSlots;

    [Header("1번 칸 랜덤 전리품 풀 (체력, 골드, 티켓팩)")]
    public List<BaseItemDataSO> firstSlotPool;

    [Header("전리품 풀 세팅")]
    public List<SnackItemSO> snackPool; 
    public List<DiceItemSO> dicePool;

    private DiceManager diceManager;

    private void Awake()
    {
        IsPanelOpen = false; // 시작할 때 초기화
    }

    public void OpenSelection(DiceManager manager)
    {
        diceManager = manager;

        // 에러 방지: 스낵 1개, 주사위 1개, 그리고 고정 체력 아이템이 세팅되었는지 확인
        if (snackPool.Count < 1 || dicePool.Count < 1 || firstSlotPool.Count < 1)
        {
            Debug.LogWarning("전리품 풀에 아이템이 부족하거나 1번 칸 아이템이 누락되었습니다!");
            ClosePanelAndProceed();
            return;
        }

        // 1번 칸 아이템 1개 랜덤 뽑기
        BaseItemDataSO selectedFirstSlotItem = firstSlotPool[Random.Range(0, firstSlotPool.Count)];

        // 스낵 1개 뽑기
        List<SnackItemSO> shuffledSnacks = new List<SnackItemSO>(snackPool);
        ShuffleList(shuffledSnacks);

        // 주사위 1개 뽑기
        List<DiceItemSO> shuffledDice = new List<DiceItemSO>(dicePool);
        ShuffleList(shuffledDice);

        // 슬롯 세팅 (1번: 체력 아이템, 2번: 스낵, 3번: 주사위)
        choiceSlots[0].Setup(selectedFirstSlotItem, this);
        choiceSlots[1].Setup(shuffledSnacks[0], this);
        choiceSlots[2].Setup(shuffledDice[0], this);

        panelRoot.SetActive(true);
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
                return; // 꽉 차서 안 들어가면 리턴하여 단계가 넘어가지 않도록 방지
            }
        }
        else
        {
            // DiceItemSO 또는 MaxHPItemSO일 경우 인벤토리에 들어가지 않고 즉시 효과 발동
            selectedLoot.ApplyItemEffect(diceManager);
        }

        if (selectedLoot is DiceItemSO dice)
        {
            dice.ApplyItemEffect(diceManager);
        }

        //튜토리얼 상태일 때 전리품 선택 완료를 매니저에게 알림
        if (TutorialManager.Instance != null && TutorialManager.Instance.isTutorialActive)
        {
            TutorialManager.Instance.OnLootSelectedComplete();
        }

        ClosePanelAndProceed();
    }

    private void ClosePanelAndProceed()
    {
        // 패널 닫기
        IsPanelOpen = false;
        panelRoot.SetActive(false);

        // 튜토리얼 11번 스텝(첫 번째 전리품)일 때는 0.5초 대기 없이 그냥 넘김
        if (TutorialManager.Instance != null && TutorialManager.Instance.isTutorialActive && TutorialManager.Instance.currentStepIndex == 11)
        {
            // 아무것도 안 함 (TutorialManager에서 12번 대사를 띄우고 다음 버튼을 기다림)
        }
        else
        {
            // 메인 게임이거나 튜토리얼 두 번째 전리품(22번)일 때는 DiceManager를 통해 0.5초 코루틴 실행
            diceManager.StartCoroutine(WaitAndOpenShopRoutine());
        }
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
    // 0.2초 뒤 상점을 여는 코루틴
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

        // 티켓 선택이 끝났거나(창이 닫힘) 애초에 안 열렸다면 드디어 상점 열기
        if (diceManager.shopManager != null)
        {
            diceManager.shopManager.OpenShop();
        }

        // 튜토리얼 두 번째 상점 진입일 경우 자동으로 23번 대사로 넘김
        if (TutorialManager.Instance != null && TutorialManager.Instance.isTutorialActive)
        {
            TutorialManager.Instance.OnAutoShopEntered();
        }
    }

}