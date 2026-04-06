using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class DiceManager2 : MonoBehaviour
{   
    [Header("프리팹 및 슬롯 설정")]
    public GameObject dicePrefab;
    public Transform keepSlotParent;
    public Transform rollSlotParent;
    private Transform[] keepSlots;
    private Transform[] rollSlots;

    [Header("오브젝트 풀링 (최적화)")]
    private List<Dice> dicePool = new List<Dice>();

    [Header("참조 설정")]
    public UIManager ui;
    public ShopManager shopManager;

    // [핵심] 분리한 몬스터 컨트롤러를 연결합니다.
    public MonsterController monsterController;

    [Header("게임 데이터")]
    public int enemyMaxHP = 40;
    public int currentStage = 1;
    public int maxPlays = 3;
    private int currentPlayNum;
    public int maxRerolls = 2;
    public int currentRerolls;

    private List<Dice> activeDiceList = new List<Dice>();
    private Dice[] keepSlotOccupants;

    void Awake()
    {
        if (ui == null) ui = FindFirstObjectByType<UIManager>();
        InitializeSlots();
        keepSlotOccupants = new Dice[keepSlots.Length];
        Dice.OnDiceStateChanged += HandleDiceChanged;

        if (ui != null)
        {
            ui.goShopButton?.onClick.AddListener(GoToShop);
            ui.nextStageButton?.onClick.AddListener(SkipShopAndNextStage);
        }

        // 최적화: 주사위 오브젝트 풀 생성
        InitializeDicePool();
    }

    void Start() => StartNewStage();

    void OnDestroy()
    {
        Dice.OnDiceStateChanged -= HandleDiceChanged;
    }

    public void OnFinishButtonClick()
    {
        if (ShopManager.IsShopOpen) return;
        if (monsterController != null && monsterController.isDead) return;

        var keptDice = activeDiceList.Where(d => d != null && d.isKept).ToList();
        int baseSum = keptDice.Sum(d => d.currentValue);
        CalculateHandData(keptDice.Select(d => d.currentValue).ToList(), out float multiplier, out string handName);

        int damage = Mathf.FloorToInt(baseSum * multiplier);

        // 몬스터에게 "너 이만큼 데미지 입어!" 라고 넘겨주기만 합니다.
        if (monsterController != null)
        {
            monsterController.TakeDamage(damage);
        }

        // HP 바 연출을 기다린 후 라운드 결과 처리
        StartCoroutine(CheckRoundResultRoutine(handName));
    }

    private IEnumerator CheckRoundResultRoutine(string handName)
    {
        // 몬스터 체력바가 줄어드는 시간(0.3초)만큼 대기
        yield return new WaitForSeconds(0.3f);

        int currentHP = monsterController != null ? monsterController.currentHP : 0;

        ui?.UpdateGameUI(currentStage, currentHP, enemyMaxHP, currentPlayNum, maxPlays, maxRerolls - currentRerolls, handName, 0, 0f);

        if (currentHP <= 0)
        {
            ui?.ShowResult("#00FF00", "스테이지 클리어!");
            Invoke(nameof(PromptShopChoice), 1.5f);
        }
        else if (currentPlayNum >= maxPlays)
        {
            ui?.ShowResult("#FF0000", "게임 오버");
            Invoke(nameof(RestartGame), 1.5f);
        }
        else
        {
            currentPlayNum++;
            Invoke(nameof(StartNewRound), 0.5f);
        }
    }

    void InitializeSlots()
    {
        if (keepSlotParent != null) keepSlots = keepSlotParent.Cast<Transform>().ToArray();
        if (rollSlotParent != null) rollSlots = rollSlotParent.Cast<Transform>().ToArray();
    }

    void CalculateHandData(List<int> values, out float multiplier, out string handName)
    {
        multiplier = 1.0f; handName = "탑 (High Card)";
        int[] counts = new int[7]; foreach (int v in values) counts[v]++;
        List<int> sorted = new List<int>(values); sorted.Sort();
        if (counts.Contains(5)) { multiplier = 2.5f; handName = "파이브 카드"; return; }
        bool isStraight = true;
        for (int i = 0; i < sorted.Count - 1; i++) if (sorted[i] + 1 != sorted[i + 1]) { isStraight = false; break; }
        if (isStraight) { multiplier = 2.0f; handName = "스트레이트"; return; }
        if (counts.Contains(4)) { multiplier = 1.8f; handName = "포카드"; return; }
        if (counts.Contains(3) && counts.Contains(2)) { multiplier = 1.7f; handName = "풀하우스"; return; }
        if (counts.Contains(3)) { multiplier = 1.5f; handName = "트리플"; return; }
        if (counts.Count(c => c == 2) == 2) { multiplier = 1.4f; handName = "투 페어"; return; }
        if (counts.Contains(2)) { multiplier = 1.2f; handName = "원 페어"; return; }
    }

    public void OnRollButtonClick()
    {
        if (currentRerolls >= maxRerolls || ShopManager.IsShopOpen) return;
        foreach (var d in activeDiceList.Where(d => d != null && !d.isKept)) d.PlayRollEffect(UnityEngine.Random.Range(1, 7));
        currentRerolls++;
        StartCoroutine(HandleDiceChangedDelayed());
    }

    private IEnumerator HandleDiceChangedDelayed() { yield return new WaitForSeconds(0.5f); HandleDiceChanged(); }
    void PromptShopChoice() { ui?.HideResult(); ui?.ShowShopChoice(); }
    public void GoToShop() { ui?.HideShopChoice(); shopManager?.OpenShop(); }
    public void SkipShopAndNextStage() { ui?.HideShopChoice(); NextStage(); }
    public void NextStage() { currentStage++; enemyMaxHP += 30; StartNewStage(); }
    void RestartGame() { currentStage = 1; enemyMaxHP = 40; StartNewStage(); }

    void StartNewStage()
    {
        currentPlayNum = 1;

        // 새 스테이지 시작 시, 몬스터에게 체력을 넘겨주며 초기화를 지시합니다.
        if (monsterController != null)
        {
            monsterController.InitializeMonster(enemyMaxHP);
        }

        // 주사위 숨김 처리
        foreach (var d in dicePool) if (d != null) d.gameObject.SetActive(false);

        StartNewRound();
    }

    void StartNewRound() { ui?.HideResult(); currentRerolls = 0; SpawnDice(); HandleDiceChanged(); }

    void HandleDiceChanged()
    {
        int keptCount = 0; bool hasDiceToRoll = false;
        foreach (var d in activeDiceList.Where(d => d != null))
        {
            if (d.isKept) { if (d.currentKeepIndex == -1) AssignToKeepSlot(d); keptCount++; }
            else { if (d.currentKeepIndex != -1) ReleaseFromKeepSlot(d); hasDiceToRoll = true; }
        }
        var allValues = activeDiceList.Where(d => d != null).Select(d => d.currentValue).ToList();
        float multiplier = 1.0f; string handName = "없음"; int totalBoardSum = 0;
        if (allValues.Count == 5) { totalBoardSum = allValues.Sum(); CalculateHandData(allValues, out multiplier, out handName); }

        int currentHP = monsterController != null ? monsterController.currentHP : 0;

        ui?.UpdateGameUI(currentStage, currentHP, enemyMaxHP, currentPlayNum, maxPlays, maxRerolls - currentRerolls, handName, totalBoardSum, multiplier);
        ui?.SetRollButtonInteractable((currentRerolls < maxRerolls) && hasDiceToRoll);
        ui?.SetFinishButtonInteractable(keptCount == keepSlots.Length);
    }

    void AssignToKeepSlot(Dice d)
    {
        int index = Array.IndexOf(keepSlotOccupants, null);
        if (index != -1) { keepSlotOccupants[index] = d; d.currentKeepIndex = index; d.MoveToTarget(keepSlots[index].position); }
    }

    void ReleaseFromKeepSlot(Dice d)
    {
        if (d.currentKeepIndex != -1) { keepSlotOccupants[d.currentKeepIndex] = null; d.currentKeepIndex = -1; d.MoveToTarget(d.rollPos); }
    }

    void InitializeDicePool()
    {
        for (int i = 0; i < rollSlots.Length; i++)
        {
            GameObject go = Instantiate(dicePrefab, transform);
            go.SetActive(false);
            Dice d = go.GetComponent<Dice>();
            dicePool.Add(d);
        }
    }

    void SpawnDice()
    {
        activeDiceList.Clear();
        Array.Clear(keepSlotOccupants, 0, keepSlotOccupants.Length);

        for (int i = 0; i < rollSlots.Length; i++)
        {
            Dice d = dicePool[i];
            d.StopAllCoroutines();
            d.isKept = false;
            d.currentKeepIndex = -1;
            d.rollPos = rollSlots[i].position;

            d.transform.position = rollSlots[i].position;
            d.transform.rotation = Quaternion.identity;
            SpriteRenderer sr = d.GetComponent<SpriteRenderer>();
            if (sr != null) sr.color = Color.white;

            d.gameObject.SetActive(true);
            d.SetValue(UnityEngine.Random.Range(1, 7));

            activeDiceList.Add(d);
        }
    }
}