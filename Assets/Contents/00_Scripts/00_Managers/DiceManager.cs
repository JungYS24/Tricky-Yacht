using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class DiceManager : MonoBehaviour
{
    [Header("프리팹 및 슬롯 설정")]
    public GameObject dicePrefab;
    public Transform[] keepSlots;
    public Transform[] rollSlots;

    [Header("다른 스크립트 참조")]
    private UIManager ui;
    private List<Dice> activeDiceList = new List<Dice>();
    private Dice[] keepSlotOccupants;

    [Header("게임 진행 데이터")]
    public int targetScore = 100;
    public int currentStage = 1;

    [Header("룰 설정")]
    public int maxPlays = 3;
    private int currentPlayNum;
    private int accumulatedScore;

    public int maxRerolls = 2;
    private int currentRerolls;

    void Awake()
    {
        ui = FindObjectOfType<UIManager>();
        keepSlotOccupants = new Dice[keepSlots.Length];
        Dice.OnDiceStateChanged += HandleDiceChanged;
    }

    void Start() => StartNewStage();

    void HandleDiceChanged()
    {
        int keptCount = 0;
        bool hasDiceToRoll = false;

        // 슬롯 이동 및 상태 체크
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

        // [핵심 업그레이드] 킵 여부와 상관없이 전체 5개의 값을 가져와 합계와 족보를 구함.
        List<int> allValues = activeDiceList.Where(d => d != null).Select(d => d.currentValue).ToList();

        float multiplier = 1.0f;
        string handName = "에러";
        int totalBoardSum = 0;

        if (allValues.Count == keepSlots.Length)
        {
            totalBoardSum = allValues.Sum(); // 화면에 있는 주사위 5개의 눈금을 모두 더함
            CalculateHandData(allValues, out multiplier, out handName);
        }

        // UI에 전체 합계(totalBoardSum) 전달
        ui.UpdateGameUI(currentStage, accumulatedScore, targetScore, currentPlayNum, maxPlays, maxRerolls - currentRerolls, handName, totalBoardSum, multiplier);

        // 버튼 제어 (끝내기는 여전히 5개를 다 킵해야 활성화됨)
        bool canRoll = (currentRerolls < maxRerolls) && hasDiceToRoll;
        ui.SetRollButtonInteractable(canRoll);
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
        bool hasDiceToRoll = false;
        foreach (var d in activeDiceList) if (!d.isKept) { hasDiceToRoll = true; break; }
        if (!hasDiceToRoll) return;

        foreach (var d in activeDiceList) if (!d.isKept) d.SetValue(UnityEngine.Random.Range(1, 7));
        currentRerolls++;
        HandleDiceChanged();
    }

    public void OnFinishButtonClick()
    {
        int baseSum = 0;
        List<int> keptValues = new List<int>();
        foreach (var d in activeDiceList)
        {
            if (d != null && d.isKept)
            {
                baseSum += d.currentValue;
                keptValues.Add(d.currentValue);
            }
        }

        float multiplier; string handName;
        CalculateHandData(keptValues, out multiplier, out handName);
        int finalScoreForTurn = Mathf.FloorToInt(baseSum * multiplier);

        accumulatedScore += finalScoreForTurn;

        ui.SetRollButtonInteractable(false);
        ui.SetFinishButtonInteractable(false);

        if (accumulatedScore >= targetScore)
        {
            ui.ShowResult("스테이지 클리어!", "#00FF00", $"최종 누적 점수: {accumulatedScore}\n다음 스테이지로 넘어갑니다.");
            Invoke("NextStage", 1.5f);
        }
        else if (currentPlayNum >= maxPlays)
        {
            ui.ShowResult("스테이지 실패...", "#FF0000", $"최종 누적 점수: {accumulatedScore} / {targetScore}\n다시 도전하세요.");
            Invoke("RestartGame", 1.5f);
        }
        else
        {
            currentPlayNum++;
            ui.ShowResult("제출 완료!", "#FFFFFF", $"{handName} 적용!\n<color=#FFD700>+{finalScoreForTurn}점 획득</color>");
            Invoke("StartNewRound", 1.0f);
        }
    }

    void NextStage()
    {
        currentStage++;
        targetScore += 80;
        StartNewStage();
    }

    void RestartGame()
    {
        currentStage = 1;
        targetScore = 100;
        StartNewStage();
    }

    void StartNewStage()
    {
        accumulatedScore = 0;
        currentPlayNum = 1;
        StartNewRound();
    }

    void StartNewRound()
    {
        ui.HideResult();
        currentRerolls = 0;
        SpawnDice();
        HandleDiceChanged();
    }

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
        activeDiceList.Clear(); System.Array.Clear(keepSlotOccupants, 0, keepSlotOccupants.Length);
        for (int i = 0; i < rollSlots.Length; i++)
        {
            GameObject go = Instantiate(dicePrefab, rollSlots[i].position, Quaternion.identity);
            Dice d = go.GetComponent<Dice>(); d.rollPos = rollSlots[i].position;
            d.SetValue(UnityEngine.Random.Range(1, 7)); activeDiceList.Add(d);
        }
    }
}