using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class DiceManager : MonoBehaviour
{

    [Header("덱 시스템 ")]
    public List<DiceData1> masterDeck = new List<DiceData1>(); // 스테이지를 넘나드는 영구 20개 덱
    public List<DiceData1> drawPile = new List<DiceData1>();  // 이번 스테이지 뽑기 통
    private List<DiceData1> discardPile = new List<DiceData1>(); // 이번 스테이지 버린 통

    [Header("프리팹 및 슬롯 설정")]
    public GameObject dicePrefab;
    public Transform keepSlotParent;
    public Transform rollSlotParent;
    private Transform[] keepSlots;
    private Transform[] rollSlots;

    [Header("참조 설정")]
    public UIManager ui;
    public ShopManager shopManager;
    public Enemy enemy;

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
    }

    void Start()
    {
        InitializeMasterDeck();
        StartNewStage();
    }

    void OnDestroy() => Dice.OnDiceStateChanged -= HandleDiceChanged;

    void InitializeMasterDeck()
    {
        masterDeck.Clear();
        for (int i = 0; i < 20; i++)
        {
            masterDeck.Add(new DiceData1());
        }
    }

    public void ApplyRandomCoating(DiceType coatingType, float mult, Color color)
    {
        var nonCoatedDice = masterDeck.Where(d => !d.isCoated).ToList();
        if (nonCoatedDice.Count > 0)
        {
            DiceData1 selected = nonCoatedDice[UnityEngine.Random.Range(0, nonCoatedDice.Count)];

            selected.isCoated = true;
            selected.multiplier = mult;
            selected.diceColor = color;
            selected.type = coatingType;

            Debug.Log($"마스터 덱 주사위 업그레이드! 유형: {coatingType}, 색상: {color}");
        }
    }

    void StartNewStage()
    {
        currentPlayNum = 1;
        currentRerolls = 0;
        enemy.Initialize(enemyMaxHP);

        drawPile = new List<DiceData1>(masterDeck);
        discardPile.Clear();
        ShufflePile(drawPile);

        StartNewRound();
    }

    void ShufflePile(List<DiceData1> pile)
    {
        for (int i = 0; i < pile.Count; i++)
        {
            int rnd = UnityEngine.Random.Range(i, pile.Count);
            var temp = pile[i];
            pile[i] = pile[rnd];
            pile[rnd] = temp;
        }
    }

    void StartNewRound()
    {
        ui?.HideResult();
        currentRerolls = 0;
        SpawnDice();
        HandleDiceChanged();
    }

    void SpawnDice()
    {
        foreach (var d in activeDiceList) if (d != null) Destroy(d.gameObject);
        activeDiceList.Clear();
        Array.Clear(keepSlotOccupants, 0, keepSlotOccupants.Length);

        for (int i = 0; i < rollSlots.Length; i++)
        {
            if (drawPile.Count == 0)
            {
                drawPile = new List<DiceData1>(discardPile);
                discardPile.Clear();
                ShufflePile(drawPile);
                if (drawPile.Count == 0) break;
            }

            DiceData1 drawnData = drawPile[0];
            drawPile.RemoveAt(0);
            discardPile.Add(drawnData);

            GameObject go = Instantiate(dicePrefab, rollSlots[i].position, Quaternion.identity);
            Dice d = go.GetComponent<Dice>();
            d.rollPos = rollSlots[i].position;

            d.SetData(drawnData, UnityEngine.Random.Range(1, 7));
            activeDiceList.Add(d);
        }
    }

    public void OnRollButtonClick()
    {
        if (currentRerolls >= maxRerolls || ShopManager.IsShopOpen) return;

        foreach (var d in activeDiceList.Where(d => d != null && !d.isKept))
            d.PlayRollEffect(UnityEngine.Random.Range(1, 7));

        currentRerolls++;
        StartCoroutine(HandleDiceChangedDelayed());
    }

    public void OnFinishButtonClick()
    {
        if (ShopManager.IsShopOpen || enemy.IsDead) return;

        var keptDice = activeDiceList.Where(d => d != null && d.isKept).ToList();
        int baseSum = keptDice.Sum(d => d.currentValue);

        CalculateHandData(keptDice.Select(d => d.currentValue).ToList(), out float comboMultiplier, out string handName);

        float finalMultiplier = comboMultiplier;

        int goldEarned = 0;
        int currentSimulatedHP = enemy.CurrentHP;
        int darkDamageTotal = 0;

        foreach (var d in keptDice)
        {
            if (d.myData.isCoated)
            {
                switch (d.myData.type)
                {
                    case DiceType.Prism:
                        // [프리즘] 합연산 적용: 기존 배율에 (아이템 배율 - 1.0)만큼 더하기
                        // 예: 1.2배면 0.2만 더해짐
                        finalMultiplier += (d.myData.multiplier - 1.0f);
                        break;

                    case DiceType.Gold:
                        // [골드] 출현 눈금 수만큼 코인 획득 누적
                        goldEarned += d.currentValue;
                        break;

                    case DiceType.Dark:
                        // [다크] 공격 전 적 현재 체력 10% 감소 (순차 적용)
                        int drop = Mathf.FloorToInt(currentSimulatedHP * 0.1f);
                        darkDamageTotal += drop;
                        currentSimulatedHP -= drop;
                        break;
                }
            }
        }

        // 1. 다크 주사위 선 데미지 적용
        if (darkDamageTotal > 0)
        {
            // 죽음 콜백 없이 데미지만 먼저 입힙니다.
            enemy.TakeDamage(darkDamageTotal, null);
            Debug.Log($"[다크 효과] 공격 전 적 체력 {darkDamageTotal} 감소!");
        }

        // 2. 골드 주사위 재화 획득 적용
        if (goldEarned > 0 && shopManager != null)
        {
            shopManager.currentGold += goldEarned;
            ui?.UpdateGoldUI(shopManager.currentGold);
            Debug.Log($"[골드 효과] 눈금 합산하여 {goldEarned} 코인 획득!");
        }

        // ---최종 메인 데미지 적용 ---
        int damage = Mathf.FloorToInt(baseSum * finalMultiplier);

        enemy.TakeDamage(damage, () => {
            ui?.ShowResult("#00FF00", "스테이지 클리어!");
            Invoke(nameof(PromptShopChoice), 1.5f);
        });

        StartCoroutine(ProcessTurnResult(handName));
    }

    private IEnumerator ProcessTurnResult(string handName)
    {
        yield return new WaitForSeconds(0.4f);
        UpdateMainUI(handName);

        if (!enemy.IsDead)
        {
            if (currentPlayNum >= maxPlays)
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
    }

    void InitializeSlots()
    {
        if (keepSlotParent != null) keepSlots = keepSlotParent.Cast<Transform>().ToArray();
        if (rollSlotParent != null) rollSlots = rollSlotParent.Cast<Transform>().ToArray();
    }

    private IEnumerator HandleDiceChangedDelayed()
    {
        yield return new WaitForSeconds(0.5f);
        HandleDiceChanged();
    }

    void HandleDiceChanged()
    {
        int keptCount = 0;
        bool hasDiceToRoll = false;

        foreach (var d in activeDiceList.Where(d => d != null))
        {
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

        UpdateMainUI("없음");

        ui?.SetRollButtonInteractable((currentRerolls < maxRerolls) && hasDiceToRoll);
        ui?.SetFinishButtonInteractable(keptCount == keepSlots.Length);
    }

    void UpdateMainUI(string handName)
    {
        var targetDice = activeDiceList.Where(d => d != null).ToList();
        var allValues = targetDice.Select(d => d.currentValue).ToList();

        int baseSum = 0;
        float baseMult = 1.0f;

        if (allValues.Count == 5)
        {
            baseSum = allValues.Sum();
            CalculateHandData(allValues, out baseMult, out handName);
        }
        else if (allValues.Count > 0)
        {
            baseSum = allValues.Sum();
            handName = "계산 중...";
        }

        float finalMult = baseMult;
        int darkDamageTotal = 0;
        int currentSimulatedHP = (enemy != null) ? enemy.CurrentHP : 0;

        foreach (var d in targetDice)
        {
            if (d.myData.isCoated)
            {
                if (d.myData.type == DiceType.Prism)
                {
                    finalMult += (d.myData.multiplier - 1.0f);
                }
                else if (d.myData.type == DiceType.Dark)
                {
                    int drop = Mathf.FloorToInt(currentSimulatedHP * 0.1f);
                    darkDamageTotal += drop;
                    currentSimulatedHP -= drop;
                }
            }
        }

        int expectedDiceDamage = Mathf.FloorToInt(baseSum * finalMult);
        int finalTotalDamage = expectedDiceDamage + darkDamageTotal;

        string formulaString = $"{baseSum} x {finalMult:F1}배";
        if (darkDamageTotal > 0)
        {
            formulaString += $" + {darkDamageTotal}(다크)";
        }

        // 족보, 계산식, 최종 데미지를 \n(줄바꿈)으로 묶고 색상 태그를 입힙니다.
        string finalCombinedText = $"<color=#FFD700>{handName}</color>\n{formulaString}\n<color=#FF5555>= {finalTotalDamage} 대미지 예정</color>";

        int curHP = (enemy != null) ? enemy.CurrentHP : 0;
        int maxHP = (enemy != null) ? enemy.MaxHP : 100;

        // UI 매니저로 합쳐진 텍스트 하나만 넘겨줍니다.
        ui?.UpdateGameUI(currentStage, curHP, maxHP, currentPlayNum, maxPlays,
                         maxRerolls - currentRerolls, finalCombinedText);
    }



    void AssignToKeepSlot(Dice d)
    {
        int index = Array.IndexOf(keepSlotOccupants, null);
        if (index != -1)
        {
            keepSlotOccupants[index] = d;
            d.currentKeepIndex = index;
            d.MoveToTarget(keepSlots[index].position);
        }
    }

    void ReleaseFromKeepSlot(Dice d)
    {
        if (d.currentKeepIndex != -1)
        {
            keepSlotOccupants[d.currentKeepIndex] = null;
            d.currentKeepIndex = -1;
            d.MoveToTarget(d.rollPos);
        }
    }

    void PromptShopChoice() { ui?.HideResult(); ui?.ShowShopChoice(); }
    public void GoToShop() { ui?.HideShopChoice(); shopManager?.OpenShop(); }
    public void SkipShopAndNextStage() { ui?.HideShopChoice(); NextStage(); }
    public void NextStage() { currentStage++; enemyMaxHP += 30; StartNewStage(); }
    void RestartGame() { currentStage = 1; enemyMaxHP = 40; StartNewStage(); }

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
}