using UnityEngine;
using System.Collections.Generic;

public class LootSelectionPanel : MonoBehaviour
{
    //다른 스크립트에서 전리품 창이 열려있는지 확인할 수 있는 변수
    public static bool IsPanelOpen { get; private set; } = false;

    public GameObject panelRoot;
    public LootChoiceSlot[] choiceSlots;

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

        if (snackPool.Count < 2 || dicePool.Count < 1)
        {
            Debug.LogWarning("전리품 풀에 스낵이 2개 미만이거나 주사위가 없습니다! 에디터 세팅을 확인하세요.");
            ClosePanelAndProceed();
            return;
        }
        // 스낵 2개, 주사위 1개 뽑기
        List<SnackItemSO> shuffledSnacks = new List<SnackItemSO>(snackPool);
        ShuffleList(shuffledSnacks);

        List<DiceItemSO> shuffledDice = new List<DiceItemSO>(dicePool);
        ShuffleList(shuffledDice);

        choiceSlots[0].Setup(shuffledSnacks[0], this);
        choiceSlots[1].Setup(shuffledSnacks[1], this);
        choiceSlots[2].Setup(shuffledDice[0], this);
        //패널이 열렸다고 상태 변경
        IsPanelOpen = true;
        panelRoot.SetActive(true);
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
        else if (selectedLoot is DiceItemSO dice)
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
        //패널이 닫혔다고 상태 변경
        IsPanelOpen = false;
        panelRoot.SetActive(false);

        diceManager.PromptShopChoice();
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