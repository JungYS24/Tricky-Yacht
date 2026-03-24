using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class DiceManager : MonoBehaviour
{
    [Header("프리팹 및 슬롯 설정")]
    public GameObject dicePrefab;
    public Transform[] keepSlots;
    public Transform[] rollSlots;

    [Header("참조 설정")]
    public UIManager ui;
    public ShopManager shopManager; // ✨ 인스펙터에서 ShopManager 오브젝트를 연결하세요.

    private List<Dice> activeDiceList = new List<Dice>();
    private Dice[] keepSlotOccupants;

    [Header("게임 데이터")]
    public int targetScore = 100;
    public int currentStage = 1;
    public int maxPlays = 3;
    private int currentPlayNum;
    private int accumulatedScore;
    public int maxRerolls = 2;
    private int currentRerolls;

    void Awake()
    {
        if (ui == null) ui = FindObjectOfType<UIManager>();
        keepSlotOccupants = new Dice[keepSlots.Length];
        Dice.OnDiceStateChanged += HandleDiceChanged;

        // UI 버튼에 함수 연결
        if (ui.goShopButton != null) ui.goShopButton.onClick.AddListener(GoToShop);
        if (ui.nextStageButton != null) ui.nextStageButton.onClick.AddListener(SkipShopAndNextStage);
    }

    void Start() => StartNewStage();

    void HandleDiceChanged()
    {
        int keptCount = 0;
        bool hasDiceToRoll = false;

        foreach (var d in activeDiceList)
        {
            if (d == null) continue;
            if (d.isKept)
            {
                if (d.currentKeepIndex == -1) AssignToKeepSlot(d);
                keptCount++;
            }
            else
            {
                if (d.currentKeepIndex != -1) ReleaseFromKeepSlot(d);
                hasDiceToRoll = true;
            }
        }

        List<int> allValues = activeDiceList.Where(d => d != null).Select(d => d.currentValue).ToList();
        float multiplier = 1.0f;
        string handName = "없음";
        int totalBoardSum = 0;

        if (allValues.Count == 5)
        {
            totalBoardSum = allValues.Sum();
            CalculateHandData(allValues, out multiplier, out handName);
        }

        ui.UpdateGameUI(currentStage, accumulatedScore, targetScore, currentPlayNum, maxPlays, maxRerolls - currentRerolls, handName, totalBoardSum, multiplier);
        ui.SetRollButtonInteractable((currentRerolls < maxRerolls) && hasDiceToRoll);
        ui.SetFinishButtonInteractable(keptCount == keepSlots.Length);
    }

    void CalculateHandData(List<int> values, out float multiplier, out string handName)
    {
        multiplier = 1.0f; handName = "탑 (High Card)";
        int[] counts = new int[7]; foreach (int v in values) counts[v]++;
        List<int> sortedValues = new List<int>(values); sortedValues.Sort();

        if (counts.Any(c => c == 5)) { multiplier = 2.5f; handName = "파이브 카드"; return; }
        bool isStraight = true;
        for (int i = 0; i < sortedValues.Count - 1; i++)
            if (sortedValues[i] + 1 != sortedValues[i + 1]) { isStraight = false; break; }
        if (isStraight) { multiplier = 2.0f; handName = "스트레이트"; return; }
        if (counts.Any(c => c == 4)) { multiplier = 1.8f; handName = "포카드"; return; }
        if (counts.Any(c => c == 3) && counts.Any(c => c == 2)) { multiplier = 1.7f; handName = "풀하우스"; return; }
        if (counts.Any(c => c == 3)) { multiplier = 1.5f; handName = "트리플"; return; }
        if (counts.Count(c => c == 2) == 2) { multiplier = 1.4f; handName = "투 페어"; return; }
        if (counts.Any(c => c == 2)) { multiplier = 1.2f; handName = "원 페어"; return; }
    }

    public void OnRollButtonClick()
    {
        if (currentRerolls >= maxRerolls) return;
        foreach (var d in activeDiceList) if (!d.isKept) d.SetValue(Random.Range(1, 7));
        currentRerolls++;
        HandleDiceChanged();
    }

    public void OnFinishButtonClick()
    {
        int baseSum = activeDiceList.Where(d => d.isKept).Sum(d => d.currentValue);
        List<int> keptValues = activeDiceList.Where(d => d.isKept).Select(d => d.currentValue).ToList();
        CalculateHandData(keptValues, out float multiplier, out string handName);
        accumulatedScore += Mathf.FloorToInt(baseSum * multiplier);

        if (accumulatedScore >= targetScore)
        {
            ui.ShowResult("스테이지 클리어!", "#00FF00", $"최종 점수: {accumulatedScore}\n목표를 달성했습니다!");
            Invoke("PromptShopChoice", 1.5f);
        }
        else if (currentPlayNum >= maxPlays)
        {
            ui.ShowResult("스테이지 실패...", "#FF0000", $"최종 점수: {accumulatedScore}\n게임 오버");
            Invoke("RestartGame", 1.5f);
        }
        else
        {
            currentPlayNum++;
            Invoke("StartNewRound", 0.5f);
        }
    }

    void PromptShopChoice() { ui.HideResult(); ui.ShowShopChoice(); }

    public void GoToShop()
    {
        ui.HideShopChoice();
        if (shopManager != null) shopManager.OpenShop(); // ✨ 상점 UI 활성화
    }

    public void SkipShopAndNextStage() { ui.HideShopChoice(); NextStage(); }

    public void NextStage()
    {
        currentStage++;
        targetScore += 30;
        StartNewStage();
    }

    void RestartGame() { currentStage = 1; targetScore = 100; StartNewStage(); }
    void StartNewStage() { accumulatedScore = 0; currentPlayNum = 1; StartNewRound(); }
    void StartNewRound() { ui.HideResult(); currentRerolls = 0; SpawnDice(); HandleDiceChanged(); }

    void AssignToKeepSlot(Dice d)
    {
        for (int i = 0; i < keepSlotOccupants.Length; i++)
        {
            if (keepSlotOccupants[i] == null)
            {
                keepSlotOccupants[i] = d; d.currentKeepIndex = i;
                d.MoveToTarget(keepSlots[i].position); break;
            }
        }
    }

    void ReleaseFromKeepSlot(Dice d)
    {
        if (d.currentKeepIndex != -1)
        {
            keepSlotOccupants[d.currentKeepIndex] = null; d.currentKeepIndex = -1;
            d.MoveToTarget(d.rollPos);
        }
    }

    void SpawnDice()
    {
        foreach (var d in activeDiceList) if (d != null) Destroy(d.gameObject);
        activeDiceList.Clear();
        System.Array.Clear(keepSlotOccupants, 0, keepSlotOccupants.Length);
        for (int i = 0; i < rollSlots.Length; i++)
        {
            GameObject go = Instantiate(dicePrefab, rollSlots[i].position, Quaternion.identity);
            Dice d = go.GetComponent<Dice>(); d.rollPos = rollSlots[i].position;
            d.SetValue(Random.Range(1, 7)); activeDiceList.Add(d);
        }
    }
}