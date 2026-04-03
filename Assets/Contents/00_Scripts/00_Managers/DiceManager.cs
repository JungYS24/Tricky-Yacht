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

    [Header("몬스터 참조")]
    public GameObject monsterObject;
    public SpriteRenderer monsterImage;
    public GameObject deathParticlePrefab;

    [Header("몬스터 피격 효과 설정")]
    public Color hitColor = Color.red;
    public float hitEffectDuration = 0.18f;
    private Coroutine hitEffectCoroutine;

    [Header("몬스터 사망 연출")]
    public float dissolveDuration = 0.8f;
    public float punchScale = 1.12f;
    public float punchDuration = 0.08f;
    public float edgeGlowPower = 7f;
    public float normalGlowPower = 4f;
    public Color edgeColorPink = new Color(1f, 0.3f, 0.85f, 1f);
    public Color edgeColorMint = new Color(0.4f, 1f, 0.85f, 1f);

    [Header("쉐이더 프로퍼티 이름")]
    public string dissolveProperty = "_DissolveAmount";
    public string edgeColorAProperty = "_EdgeColorA";
    public string edgeColorBProperty = "_EdgeColorB";
    public string edgeGlowPowerProperty = "_EdgeGlowPower";

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

    private Material monsterRuntimeMat;
    private Vector3 monsterOriginalScale;
    private bool monsterDead = false;
    private Coroutine monsterDeathCoroutine;

    void Awake()
    {
        if (ui == null)
            ui = FindFirstObjectByType<UIManager>();

        InitializeSlots();
        keepSlotOccupants = new Dice[keepSlots.Length];
        Dice.OnDiceStateChanged += HandleDiceChanged;

        if (ui != null)
        {
            ui.goShopButton?.onClick.AddListener(GoToShop);
            ui.nextStageButton?.onClick.AddListener(SkipShopAndNextStage);
        }

        SetupMonsterMaterial();
    }

    void Start()
    {
        StartNewStage();
    }

    void OnDestroy()
    {
        Dice.OnDiceStateChanged -= HandleDiceChanged;
    }

    void SetupMonsterMaterial()
    {
        if (monsterObject == null)
            return;

        if (monsterImage == null)
            monsterImage = monsterObject.GetComponent<SpriteRenderer>();

        if (monsterImage == null)
            return;

        monsterOriginalScale = monsterObject.transform.localScale;

        if (monsterImage.material != null)
        {
            monsterRuntimeMat = Instantiate(monsterImage.material);
            monsterImage.material = monsterRuntimeMat;

            monsterRuntimeMat.SetFloat(dissolveProperty, 0f);
            monsterRuntimeMat.SetFloat(edgeGlowPowerProperty, normalGlowPower);
            monsterRuntimeMat.SetColor(edgeColorAProperty, edgeColorPink);
            monsterRuntimeMat.SetColor(edgeColorBProperty, edgeColorMint);
        }
    }

    public void OnFinishButtonClick()
    {
        if (ShopManager.IsShopOpen) return;
        if (monsterDead) return;

        var keptDice = activeDiceList.Where(d => d != null && d.isKept).ToList();
        int baseSum = keptDice.Sum(d => d.currentValue);

        CalculateHandData(
            keptDice.Select(d => d.currentValue).ToList(),
            out float multiplier,
            out string handName
        );

        int damage = Mathf.FloorToInt(baseSum * multiplier);

        // 내부 HP 감소
        currentEnemyHP = Mathf.Max(0, currentEnemyHP - damage);

        // 피격 빨개짐
        if (damage > 0 && monsterImage != null)
        {
            if (hitEffectCoroutine != null)
                StopCoroutine(hitEffectCoroutine);

            hitEffectCoroutine = StartCoroutine(HitEffectRoutine());
        }

        // HP 0이면 죽는 연출 시작
        if (currentEnemyHP <= 0 && !monsterDead)
        {
            if (monsterDeathCoroutine != null)
                StopCoroutine(monsterDeathCoroutine);

            monsterDeathCoroutine = StartCoroutine(MonsterDeathRoutine());
        }

        StartCoroutine(ShrinkHPBarRoutine(handName));
    }

    private IEnumerator HitEffectRoutine()
    {
        float elapsed = 0f;
        monsterImage.color = hitColor;

        float hpPercent = enemyMaxHP > 0 ? (float)currentEnemyHP / enemyMaxHP : 0f;
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

    private IEnumerator MonsterDeathRoutine()
    {
        monsterDead = true;

        if (monsterObject == null || monsterImage == null)
            yield break;

        Vector3 startScale = monsterOriginalScale;
        Vector3 bigScale = monsterOriginalScale * punchScale;

        // 1. 경계선 글로우 강화
        if (monsterRuntimeMat != null)
        {
            monsterRuntimeMat.SetColor(edgeColorAProperty, edgeColorPink);
            monsterRuntimeMat.SetColor(edgeColorBProperty, edgeColorMint);
            monsterRuntimeMat.SetFloat(edgeGlowPowerProperty, edgeGlowPower);
        }

        // 2. 잠깐 커짐
        float elapsed = 0f;
        while (elapsed < punchDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / punchDuration);
            monsterObject.transform.localScale = Vector3.Lerp(startScale, bigScale, t);
            yield return null;
        }

        // 3. 디졸브 + 작아짐
        elapsed = 0f;
        while (elapsed < dissolveDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / dissolveDuration);

            if (monsterRuntimeMat != null)
                monsterRuntimeMat.SetFloat(dissolveProperty, t);

            monsterObject.transform.localScale = Vector3.Lerp(bigScale, Vector3.zero, t);

            yield return null;
        }

        // 4. 마지막 파티클
        if (deathParticlePrefab != null)
            Instantiate(deathParticlePrefab, monsterObject.transform.position, Quaternion.identity);

        if (monsterRuntimeMat != null)
        {
            monsterRuntimeMat.SetFloat(dissolveProperty, 1f);
            monsterRuntimeMat.SetFloat(edgeGlowPowerProperty, normalGlowPower);
        }

        // 5. 숨김
        monsterObject.SetActive(false);
        monsterDeathCoroutine = null;
    }

    private IEnumerator ShrinkHPBarRoutine(string handName)
    {
        float duration = 0.3f;
        float elapsed = 0f;
        float startValue = enemyHPSlider != null ? enemyHPSlider.value : 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            if (enemyHPSlider != null)
                enemyHPSlider.value = Mathf.Lerp(startValue, currentEnemyHP, elapsed / duration);

            yield return null;
        }

        if (enemyHPSlider != null)
            enemyHPSlider.value = currentEnemyHP;

        if (ui != null)
        {
            ui.UpdateGameUI(
                currentStage,
                currentEnemyHP,
                enemyMaxHP,
                currentPlayNum,
                maxPlays,
                maxRerolls - currentRerolls,
                handName,
                0,
                0f
            );
        }

        if (currentEnemyHP <= 0)
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
        if (keepSlotParent != null)
            keepSlots = keepSlotParent.Cast<Transform>().ToArray();

        if (rollSlotParent != null)
            rollSlots = rollSlotParent.Cast<Transform>().ToArray();
    }

    void CalculateHandData(List<int> values, out float multiplier, out string handName)
    {
        multiplier = 1.0f;
        handName = "탑 (High Card)";

        int[] counts = new int[7];
        foreach (int v in values)
            counts[v]++;

        List<int> sorted = new List<int>(values);
        sorted.Sort();

        if (counts.Contains(5))
        {
            multiplier = 2.5f;
            handName = "파이브 카드";
            return;
        }

        bool isStraight = true;
        for (int i = 0; i < sorted.Count - 1; i++)
        {
            if (sorted[i] + 1 != sorted[i + 1])
            {
                isStraight = false;
                break;
            }
        }

        if (isStraight)
        {
            multiplier = 2.0f;
            handName = "스트레이트";
            return;
        }

        if (counts.Contains(4))
        {
            multiplier = 1.8f;
            handName = "포카드";
            return;
        }

        if (counts.Contains(3) && counts.Contains(2))
        {
            multiplier = 1.7f;
            handName = "풀하우스";
            return;
        }

        if (counts.Contains(3))
        {
            multiplier = 1.5f;
            handName = "트리플";
            return;
        }

        if (counts.Count(c => c == 2) == 2)
        {
            multiplier = 1.4f;
            handName = "투 페어";
            return;
        }

        if (counts.Contains(2))
        {
            multiplier = 1.2f;
            handName = "원 페어";
            return;
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

    private IEnumerator HandleDiceChangedDelayed()
    {
        yield return new WaitForSeconds(0.5f);
        HandleDiceChanged();
    }

    void PromptShopChoice()
    {
        ui?.HideResult();
        ui?.ShowShopChoice();
    }

    public void GoToShop()
    {
        ui?.HideShopChoice();
        shopManager?.OpenShop();
    }

    public void SkipShopAndNextStage()
    {
        ui?.HideShopChoice();
        NextStage();
    }

    public void NextStage()
    {
        currentStage++;
        enemyMaxHP += 30;
        StartNewStage();
    }

    void RestartGame()
    {
        currentStage = 1;
        enemyMaxHP = 40;
        StartNewStage();
    }

    void StartNewStage()
    {
        currentEnemyHP = enemyMaxHP;
        currentPlayNum = 1;
        currentRerolls = 0;
        monsterDead = false;

        if (monsterObject != null)
        {
            monsterObject.SetActive(true);
            monsterObject.transform.localScale = monsterOriginalScale;
        }

        if (monsterRuntimeMat != null)
        {
            monsterRuntimeMat.SetFloat(dissolveProperty, 0f);
            monsterRuntimeMat.SetFloat(edgeGlowPowerProperty, normalGlowPower);
            monsterRuntimeMat.SetColor(edgeColorAProperty, edgeColorPink);
            monsterRuntimeMat.SetColor(edgeColorBProperty, edgeColorMint);
        }

        if (monsterImage != null)
            monsterImage.color = Color.white;

        if (enemyHPSlider != null)
        {
            enemyHPSlider.maxValue = enemyMaxHP;
            enemyHPSlider.value = currentEnemyHP;
        }

        StartNewRound();
    }

    void StartNewRound()
    {
        ui?.HideResult();
        currentRerolls = 0;
        SpawnDice();
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
                if (d.currentKeepIndex == -1)
                    AssignToKeepSlot(d);

                keptCount++;
            }
            else
            {
                if (d.currentKeepIndex != -1)
                    ReleaseFromKeepSlot(d);

                hasDiceToRoll = true;
            }
        }

        var allValues = activeDiceList
            .Where(d => d != null)
            .Select(d => d.currentValue)
            .ToList();

        float multiplier = 1.0f;
        string handName = "없음";
        int totalBoardSum = 0;

        if (allValues.Count == 5)
        {
            totalBoardSum = allValues.Sum();
            CalculateHandData(allValues, out multiplier, out handName);
        }

        ui?.UpdateGameUI(
            currentStage,
            currentEnemyHP,
            enemyMaxHP,
            currentPlayNum,
            maxPlays,
            maxRerolls - currentRerolls,
            handName,
            totalBoardSum,
            multiplier
        );

        ui?.SetRollButtonInteractable((currentRerolls < maxRerolls) && hasDiceToRoll);
        ui?.SetFinishButtonInteractable(keptCount == keepSlots.Length);
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

    void SpawnDice()
    {
        foreach (var d in activeDiceList)
        {
            if (d != null)
                Destroy(d.gameObject);
        }

        activeDiceList.Clear();
        Array.Clear(keepSlotOccupants, 0, keepSlotOccupants.Length);

        for (int i = 0; i < rollSlots.Length; i++)
        {
            GameObject go = Instantiate(dicePrefab, rollSlots[i].position, Quaternion.identity);
            Dice d = go.GetComponent<Dice>();
            d.rollPos = rollSlots[i].position;
            d.SetValue(UnityEngine.Random.Range(1, 7));
            activeDiceList.Add(d);
        }
    }
}