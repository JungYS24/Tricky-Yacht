using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class DiceManager : MonoBehaviour
{
    [Header("프리팹 설정")]
    public GameObject dicePrefab;

    [Header("슬롯 부모 오브젝트 설정")]
    // 이제 낱개가 아니라 부모(Group)만 연결하면 됩니다.
    public Transform keepSlotParent;
    public Transform rollSlotParent;

    // 코드가 내부적으로 사용할 배열들입니다.
    private Transform[] keepSlots;
    private Transform[] rollSlots;

    [Header("참조 설정")]
    public UIManager ui;
    public ShopManager shopManager;
    public Slider enemyHPSlider;

    private List<Dice> activeDiceList = new List<Dice>();
    private Dice[] keepSlotOccupants;

    [Header("게임 데이터 (전투)")]
    public int enemyMaxHP = 40;
    public int currentEnemyHP;
    public int currentStage = 1;
    public int maxPlays = 3;
    private int currentPlayNum;
    public int maxRerolls = 2;
    private int currentRerolls;

    void Awake()
    {
        if (ui == null) ui = FindObjectOfType<UIManager>();

        // 부모 오브젝트로부터 자식 슬롯들을 자동으로 찾아옵니다.
        InitializeSlots();

        keepSlotOccupants = new Dice[keepSlots.Length];
        Dice.OnDiceStateChanged += HandleDiceChanged;

        if (ui.goShopButton != null) ui.goShopButton.onClick.AddListener(GoToShop);
        if (ui.nextStageButton != null) ui.nextStageButton.onClick.AddListener(SkipShopAndNextStage);
    }

    // 자식 슬롯들을 배열에 자동으로 채워넣는 함수입니다.
    void InitializeSlots()
    {
        // KeepSlot 찾기
        if (keepSlotParent != null)
        {
            keepSlots = new Transform[keepSlotParent.childCount];
            for (int i = 0; i < keepSlotParent.childCount; i++)
            {
                keepSlots[i] = keepSlotParent.GetChild(i);
            }
        }

        // RollSlot 찾기
        if (rollSlotParent != null)
        {
            rollSlots = new Transform[rollSlotParent.childCount];
            for (int i = 0; i < rollSlotParent.childCount; i++)
            {
                rollSlots[i] = rollSlotParent.GetChild(i);
            }
        }
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

        ui.UpdateGameUI(currentStage, currentEnemyHP, enemyMaxHP, currentPlayNum, maxPlays, maxRerolls - currentRerolls, handName, totalBoardSum, multiplier);
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

        foreach (var d in activeDiceList)
        {
            if (!d.isKept)
            {
                int rolledValue = Random.Range(1, 7);
                d.PlayRollEffect(rolledValue);
            }
        }

        currentRerolls++;
        StartCoroutine(HandleDiceChangedDelayed());
    }

    private IEnumerator HandleDiceChangedDelayed()
{
    yield return new WaitForSeconds(0.5f);
    HandleDiceChanged();
}
    public void OnFinishButtonClick()
    {
        int baseSum = activeDiceList.Where(d => d.isKept).Sum(d => d.currentValue);
        List<int> keptValues = activeDiceList.Where(d => d.isKept).Select(d => d.currentValue).ToList();
        CalculateHandData(keptValues, out float multiplier, out string handName);

        int damage = Mathf.FloorToInt(baseSum * multiplier);
        currentEnemyHP -= damage;
        if (currentEnemyHP < 0) currentEnemyHP = 0;

        StopAllCoroutines();
        StartCoroutine(ShrinkHPBarRoutine(handName));
    }

    private IEnumerator ShrinkHPBarRoutine(string handName)
    {
        float duration = 0.3f;
        float elapsed = 0f;

        float startValue = enemyHPSlider != null ? enemyHPSlider.value : 0;
        float targetValue = currentEnemyHP;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            if (enemyHPSlider != null)
                enemyHPSlider.value = Mathf.Lerp(startValue, targetValue, elapsed / duration);
            yield return null;
        }

        if (enemyHPSlider != null) enemyHPSlider.value = targetValue;

        ui.UpdateGameUI(currentStage, currentEnemyHP, enemyMaxHP, currentPlayNum, maxPlays, maxRerolls - currentRerolls, handName, 0, 0f);

        if (currentEnemyHP <= 0)
        {
            ui.ShowResult("#00FF00", "스테이지 클리어!");
            Invoke("PromptShopChoice", 1.5f);
        }
        else if (currentPlayNum >= maxPlays)
        {
            ui.ShowResult("#FF0000", "게임 오버");
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
        if (shopManager != null) shopManager.OpenShop();
    }

    public void SkipShopAndNextStage() { ui.HideShopChoice(); NextStage(); }

    public void NextStage()
    {
        currentStage++;
        enemyMaxHP += 30;
        StartNewStage();
    }

    void RestartGame() { currentStage = 1; enemyMaxHP = 40; StartNewStage(); }

    void StartNewStage()
    {
        currentEnemyHP = enemyMaxHP;
        currentPlayNum = 1;

        if (enemyHPSlider != null)
        {
            enemyHPSlider.maxValue = enemyMaxHP;
            enemyHPSlider.value = currentEnemyHP;
        }

        StartNewRound();
    }

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