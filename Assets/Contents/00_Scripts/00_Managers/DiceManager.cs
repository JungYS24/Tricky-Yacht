using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class DiceManager : MonoBehaviour
{
    [Header("프리팹 및 슬롯 설정")]
    public GameObject dicePrefab;
    public Transform keepSlotParent;
    public Transform rollSlotParent;
    private Transform[] keepSlots;
    private Transform[] rollSlots;

    [Header("참조 설정")]
    public UIManager ui;
    public ShopManager shopManager;
    public Slider enemyHPSlider;

    [Header("몬스터 피격 효과 설정")]
    public Image monsterImage;
    public Color hitColor = Color.red;     
    public float hitEffectDuration = 1.0f;   
    private Coroutine hitEffectCoroutine;    

    [Header("게임 데이터")]
    public int enemyMaxHP = 40;
    public int currentEnemyHP;
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

        ui.goShopButton?.onClick.AddListener(GoToShop);
        ui.nextStageButton?.onClick.AddListener(SkipShopAndNextStage);
    }

    void Start() => StartNewStage();

    public void OnFinishButtonClick()
    {
        if (ShopManager.IsShopOpen) return;

        var keptDice = activeDiceList.Where(d => d.isKept).ToList();
        int baseSum = keptDice.Sum(d => d.currentValue);
        CalculateHandData(keptDice.Select(d => d.currentValue).ToList(), out float multiplier, out string handName);

        int damage = Mathf.FloorToInt(baseSum * multiplier);
        currentEnemyHP = Mathf.Max(0, currentEnemyHP - damage);

        if (damage > 0)
        {
            if (hitEffectCoroutine != null) StopCoroutine(hitEffectCoroutine);
            hitEffectCoroutine = StartCoroutine(HitEffectRoutine());
        }


        StartCoroutine(ShrinkHPBarRoutine(handName));
    }

    private IEnumerator HitEffectRoutine()
    {
        float elapsed = 0f;
        monsterImage.color = hitColor;

        float hpPercent = (float)currentEnemyHP / enemyMaxHP;
        Color targetColor = Color.Lerp(Color.red, Color.white, hpPercent);

        while (elapsed < hitEffectDuration)
        {
            elapsed += Time.deltaTime;
            monsterImage.color = Color.Lerp(hitColor, targetColor, elapsed / hitEffectDuration);
            yield return null;
        }

        monsterImage.color = targetColor;
        hitEffectCoroutine = null;
    }

    private IEnumerator ShrinkHPBarRoutine(string handName)
    {
        float duration = 0.3f, elapsed = 0f;
        float startValue = enemyHPSlider != null ? enemyHPSlider.value : 0;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            if (enemyHPSlider != null)
                enemyHPSlider.value = Mathf.Lerp(startValue, currentEnemyHP, elapsed / duration);
            yield return null;
        }

        if (enemyHPSlider != null) enemyHPSlider.value = currentEnemyHP;

        ui.UpdateGameUI(currentStage, currentEnemyHP, enemyMaxHP, currentPlayNum, maxPlays, maxRerolls - currentRerolls, handName, 0, 0f);

        if (currentEnemyHP <= 0)
        {
            ui.ShowResult("#00FF00", "스테이지 클리어!");
            Invoke(nameof(PromptShopChoice), 1.5f);
        }
        else if (currentPlayNum >= maxPlays)
        {
            ui.ShowResult("#FF0000", "게임 오버");
            Invoke(nameof(RestartGame), 1.5f);
        }
        else
        {
            currentPlayNum++;
            Invoke(nameof(StartNewRound), 0.5f);
        }
    }

    // --- 주사위 게임 필수 로직들 ---
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
        foreach (var d in activeDiceList.Where(d => !d.isKept)) d.PlayRollEffect(UnityEngine.Random.Range(1, 7));
        currentRerolls++;
        StartCoroutine(HandleDiceChangedDelayed());
    }
    private IEnumerator HandleDiceChangedDelayed() { yield return new WaitForSeconds(0.5f); HandleDiceChanged(); }
    void PromptShopChoice() { ui.HideResult(); ui.ShowShopChoice(); }
    public void GoToShop() { ui.HideShopChoice(); shopManager?.OpenShop(); }
    public void SkipShopAndNextStage() { ui.HideShopChoice(); NextStage(); }
    public void NextStage() { currentStage++; enemyMaxHP += 30; StartNewStage(); }
    void RestartGame() { currentStage = 1; enemyMaxHP = 40; StartNewStage(); }
    void StartNewStage()
    {
        currentEnemyHP = enemyMaxHP; currentPlayNum = 1;
        if (enemyHPSlider != null) { enemyHPSlider.maxValue = enemyMaxHP; enemyHPSlider.value = currentEnemyHP; }
        if (monsterImage != null) monsterImage.color = Color.white;
        StartNewRound();
    }
    void StartNewRound() { ui.HideResult(); currentRerolls = 0; SpawnDice(); HandleDiceChanged(); }
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
        ui.UpdateGameUI(currentStage, currentEnemyHP, enemyMaxHP, currentPlayNum, maxPlays, maxRerolls - currentRerolls, handName, totalBoardSum, multiplier);
        ui.SetRollButtonInteractable((currentRerolls < maxRerolls) && hasDiceToRoll);
        ui.SetFinishButtonInteractable(keptCount == keepSlots.Length);
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
    void SpawnDice()
    {
        foreach (var d in activeDiceList) if (d != null) Destroy(d.gameObject);
        activeDiceList.Clear(); Array.Clear(keepSlotOccupants, 0, keepSlotOccupants.Length);
        for (int i = 0; i < rollSlots.Length; i++)
        {
            GameObject go = Instantiate(dicePrefab, rollSlots[i].position, Quaternion.identity);
            Dice d = go.GetComponent<Dice>(); d.rollPos = rollSlots[i].position;
            d.SetValue(UnityEngine.Random.Range(1, 7)); activeDiceList.Add(d);
        }
    }
}