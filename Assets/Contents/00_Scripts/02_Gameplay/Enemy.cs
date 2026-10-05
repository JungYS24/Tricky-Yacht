using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic; // List를 사용하기 위해 추가
using TMPro;
using DG.Tweening;

public class Enemy : MonoBehaviour
{
    [Header("참조 설정")]
    public SpriteRenderer monsterImage;
    public Slider enemyHPSlider;
    public Animator monsterAnimator;
    public GameObject deathParticlePrefab;

    //데미지 텍스트
    [Header("데미지 텍스트 설정")]
    public GameObject damageTextPrefab;
    public Canvas uiCanvas; // 데미지 텍스트가 생성될 부모 캔버스

    //몬스터 공격력을 표시할 텍스트 UI 연결용 변수
    [Header("공격력 텍스트 설정")]
    public TextMeshProUGUI attackPowerText;

    [Header("몬스터 턴 UI")]
    public TextMeshProUGUI turnText;

    [Header("피격 효과")]
    public Color hitColor = Color.red;
    public float hitEffectDuration = 0.18f;

    [Header("사망 연출")]
    public float dissolveDuration = 0.8f;
    public float punchScale = 1.12f;
    public float punchDuration = 0.08f;
    public float edgeGlowPower = 7f;
    public float normalGlowPower = 4f;
    public Color edgeColorPink = new Color(1f, 0.3f, 0.85f, 1f);
    public Color edgeColorMint = new Color(0.4f, 1f, 0.85f, 1f);

    [Header("쉐이더 프로퍼티")]
    public string dissolveProperty = "_DissolveAmount";
    public string edgeColorAProperty = "_EdgeColorA";
    public string edgeColorBProperty = "_EdgeColorB";
    public string edgeGlowPowerProperty = "_EdgeGlowPower";


    //현재 몬스터의 능력
    public BossAbilityType CurrentBossAbility { get; private set; }

    // 세이브를 위해 몬스터가 자기 이름을 기억하게 함
    public string CurrentMonsterName { get; private set; }
    public int CurrentMonsterIndex { get { return currentMonsterIndex; } }

    // 몬스터 출현 순서 리스트
    // 몇번 째 몬스터인지 체크
    private int currentMonsterIndex = 0;

    // 에디터 창에서는 숨기지만 DiceManager가 읽어갈 수 있도록 HideInInspector 처리
    [HideInInspector] public FigureItemSO dropFigureData;
    [HideInInspector] public float baseDropRate = 0.5f;


    public int MaxHP { get; private set; }
    public int CurrentHP { get; private set; }
    public int AttackPower { get; private set; }
    public bool IsDead { get; private set; } = false;

    //몬스터 턴 카운트 변수
    public int MaxAttackTurn { get; private set; }
    public int CurrentAttackTurn { get; private set; }

    private Material monsterRuntimeMat;
    private Vector3 originalScale;
    private Vector3 originalPosition;
    public bool useExternalDeathSequence = false;
    private Coroutine hitEffectCoroutine;
    private Coroutine hpCoroutine;
    private bool deathPresentationStarted;
    private bool deathAnimatorPaused;
    private bool deathAnimatorWasEnabled;

    //최초 크기는 무조건 Awake에서 딱 한 번만 저장!
    void Awake()
    {
        originalScale = transform.localScale;
        originalPosition = transform.position;

        // 기존 체력바 자동 할당 코드
        if (enemyHPSlider == null)
        {
            GameObject sliderObj = GameObject.Find("EnemyHPSlider");
            if (sliderObj != null)
            {
                enemyHPSlider = sliderObj.GetComponent<Slider>();
            }
        }

        //공격력 텍스트 자동 할당 코드
        if (attackPowerText == null)
        {
            GameObject attackTextObj = GameObject.Find("AttackPowerText");
            if (attackTextObj != null)
            {
                attackPowerText = attackTextObj.GetComponent<TextMeshProUGUI>();
            }
        }

        if (uiCanvas == null)
        {
            GameObject canvasObj = GameObject.Find("Canvas");
            if (canvasObj != null)
            {
                uiCanvas = canvasObj.GetComponent<Canvas>();
            }
        }

        if (turnText == null)
        {
            GameObject turnTextObj = GameObject.Find("EnemyTurnText");
            if (turnTextObj != null)
            {
                turnText = turnTextObj.GetComponent<TextMeshProUGUI>();
            }
        }
    }

    public void ResetMonsterIndex()
    {
        currentMonsterIndex = 0;
    }

    public void Initialize(int currentStage, BiomeDataSO currentBiome)
    {
        StopAttackPresentation();
        ResetDeathPresentation();
        GetComponent<MonsterHitFeedback>()?.Stop();
        //만약의 사태를 대비한 기본 체력 (리스트가 비어있을 때 등)
        int finalMaxHP = 40;
        int finalAttack = 10;
        MonsterDataSO nextMonsterData = null;

        // 튜토리얼 중이라면 매니저에서 튜토리얼용 데이터를 가져옵니다 ---
        if (TutorialManager.Instance != null && TutorialManager.Instance.isTutorialActive)
        {
            nextMonsterData = TutorialManager.Instance.GetTutorialMonster(currentStage);
        }

        else if (currentBiome != null)
        {
            //보스 여기서 바꾸면 됨
            if (currentStage % 10 == 0 && currentBiome.bossMonster != null)
            {
                nextMonsterData = currentBiome.bossMonster;
            }
            //그 외의 일반 스테이지는 기존처럼 리스트에서 랜덤 출현
            else if (currentBiome.biomeMonsters != null && currentBiome.biomeMonsters.Count > 0)
            {
                int randomIndex = UnityEngine.Random.Range(0, currentBiome.biomeMonsters.Count);
                nextMonsterData = currentBiome.biomeMonsters[randomIndex];
            }
        }

        // 선택된 몬스터(보스 혹은 일반)의 데이터를 덮어씌웁니다.
        if (nextMonsterData != null)
        {
            //이름 기억하기
            CurrentMonsterName = nextMonsterData.monsterName;
            //초기화할 때 몬스터의 능력 기억
            CurrentBossAbility = nextMonsterData.bossAbility;

            if (monsterAnimator != null && nextMonsterData.animatorController != null)
            {
                monsterAnimator.runtimeAnimatorController = nextMonsterData.animatorController;
            }
            else
            {
                if (monsterAnimator != null) monsterAnimator.runtimeAnimatorController = null;
                if (monsterImage != null) monsterImage.sprite = nextMonsterData.monsterSprite;
            }

            dropFigureData = nextMonsterData.dropFigureData;
            baseDropRate = nextMonsterData.dropRate;

            if (dropFigureData != null && CollectionDataManager.Instance != null)
                CollectionDataManager.Instance.EncounterFigure(dropFigureData);

            finalMaxHP = nextMonsterData.maxHp;
            finalAttack = nextMonsterData.baseAtk;

            currentMonsterIndex++;
        }
        //스테이지당 몬스터 배수 설정
        float hpMultiplier = Mathf.Pow(1.05f, currentStage - 1);
        finalMaxHP = Mathf.RoundToInt(finalMaxHP * hpMultiplier);

        //(눈먼 점술가 패널티 적용)
        if (DiceManager.Instance != null && DiceManager.Instance.isNextEnemyHPBoosted)
        {
            finalMaxHP *= 2; // 적 체력을 2배(100% 증가)로 만듬
            DiceManager.Instance.isNextEnemyHPBoosted = false; // 적용했으니 스위치를 다시 끔
        }

        //2스테이지당 몬스터 공격력 1 증가 (2스테이지=+1, 4스테이지=+2 ...)
        finalAttack += (currentStage / 2);

        // 여기서 최종적으로 체력을 확정(덮어씌워지는 문제 해결)
        MaxHP = finalMaxHP;
        CurrentHP = finalMaxHP;
        AttackPower = finalAttack;
        IsDead = false;
        useExternalDeathSequence = false;

        MaxAttackTurn = 2;
        CurrentAttackTurn = MaxAttackTurn;
        UpdateTurnUI();

        //몬스터가 등장할 때(초기화될 때) 공격력 텍스트를 업데이트
        if (attackPowerText != null)
        {
            attackPowerText.text = $"{AttackPower}";
        }

        //시각적 초기화 (크기, 색상, 디졸브 등)
        transform.position = originalPosition;
        transform.localScale = originalScale;

        if (monsterImage != null)
        {
            gameObject.SetActive(true);
            monsterImage.color = Color.white;

            // 머티리얼 인스턴스화 (원본 보호)
            if (monsterRuntimeMat == null)
            {
                monsterRuntimeMat = Instantiate(monsterImage.material);
                monsterImage.material = monsterRuntimeMat;
            }

            monsterRuntimeMat.SetFloat(dissolveProperty, 0f);
            monsterRuntimeMat.SetFloat(edgeGlowPowerProperty, normalGlowPower);
            monsterRuntimeMat.SetColor(edgeColorAProperty, edgeColorPink);
            monsterRuntimeMat.SetColor(edgeColorBProperty, edgeColorMint);
        }

        UpdateHPBar(true);
    }


    public void TakeDamage(int damage,System.Action onDeathCallback,bool isFirstNormalAttack = false, EnemyHitKind hitKind = EnemyHitKind.Other, bool includesDark = false)
    {
        if (IsDead || damage <= 0) return;
        StopAttackPresentation();
        GetComponent<MonsterHitFeedback>()?.Stop();

        // 적 체력보다 큰 공격도 실제 감소한 체력까지만 피해로 인정
        int actualDamage = Mathf.Min(CurrentHP, damage);
        CurrentHP -= actualDamage;

        // 사망 콜백 전에 지급해야 마지막 공격의 효과도 적용됨
        DiceManager dm = DiceManager.Instance;

        if (dm != null && dm.enemy == this)
        {
            FigureEffectManager.Instance?.EvaluateEnemyDamageTriggers(dm,actualDamage,isFirstNormalAttack);
        }

        // 데미지를 입었으니 HP바 깎는 코루틴 실행!
        if (hpCoroutine != null) StopCoroutine(hpCoroutine);
        hpCoroutine = StartCoroutine(ShrinkHPBarRoutine());

        if (damage > 0)
        {
            if (hitEffectCoroutine != null) StopCoroutine(hitEffectCoroutine);
            if (hitKind != EnemyHitKind.Other)
            {
                // 피해 종류별 오버레이를 사용하고 바탕색은 현재 체력에 맞춥니다.
                if (monsterImage != null) monsterImage.color = Color.Lerp(Color.red, Color.white, MaxHP > 0 ? (float)CurrentHP / MaxHP : 0f);
                hitEffectCoroutine = null;
            }
            else hitEffectCoroutine = StartCoroutine(HitEffectRoutine());
            // 종류에 맞는 연출을 재생합니다. 치명타는 사망 확대와 충돌하지 않게 밀림을 생략합니다.
            if (hitKind != EnemyHitKind.Other)
                {
                float strength = 1f + 0.35f * Mathf.Clamp01((float)damage / Mathf.Max(1, MaxHP));
                MonsterHitFeedback.Get(this).Play(monsterImage, CurrentHP > 0, monsterAnimator, strength, hitKind, includesDark);
                if (hitKind == EnemyHitKind.Normal) CameraShake.Instance?.ShakeImpact(strength);
                else CameraShake.Instance?.ShakeElemental();
            }

            // ===== 데미지 텍스트 생성 호출 =====
            ShowDamageText(damage);

        }

        if (CurrentHP <= 0)
        {
            IsDead = true;

            // 게임 진행 중에는 DiceManager가 포획 여부를 판정한 뒤 연출을 하나만 선택합니다.
            // 정산 중 피규어 피해의 null 콜백은 CombatFlowController의 사망 확인까지 기다립니다.
            if ((dm != null && dm.enemy == this) || useExternalDeathSequence)
            {
                onDeathCallback?.Invoke();
            }
            else if (BeginDeathPresentation())
            {
                // DiceManager 없이 사용하는 테스트 몬스터는 기존 일반 사망 동작을 유지합니다.
                StartCoroutine(MonsterDeathRoutine(onDeathCallback));
            }
        }
    }

    // 데미지 텍스트 생성 함수
    private void ShowDamageText(int damage)
    {
        if (damageTextPrefab == null || uiCanvas == null) return;

        // 몬스터의 약간 위쪽에 생성되도록 오프셋 추가
        Vector3 spawnPosition = transform.position + new Vector3(0, 1f, 0);

        // 텍스트를 담을 캔버스(uiCanvas)의 자식으로 생성
        GameObject textObj = Instantiate(damageTextPrefab, spawnPosition, Quaternion.identity, uiCanvas.transform);
        DamageText dmgTextScript = textObj.GetComponent<DamageText>();

        if (dmgTextScript != null)
        {
            // 데미지 수치에 따라 크기 배율 결정
            // 예: 기준 데미지를 20으로 잡고, 데미지가 클수록 배율 증가
            float sizeMultiplier = 1f + (damage / 20f) * 0.5f;
            dmgTextScript.Setup(damage, sizeMultiplier);
        }
    }

    [Header("플레이어를 향한 공격 연출")]
    public float attackPrepareDuration = 0.12f;
    public float attackLungeDuration = 0.10f;
    public float attackReturnDuration = 0.18f;
    public float attackScale = 1.12f;
    public float attackTravelRatio = 0.12f;
    public float AttackContactDelay => Mathf.Max(0.01f, attackPrepareDuration) + Mathf.Max(0.01f, attackLungeDuration);

    private Sequence attackTween;
    private SpriteRenderer attackVisual;
    private bool attackWasHidden;
    private bool attackVisualActive;

    public void PlayAttackAnim()
    {
        StopAttackPresentation();
        GetComponent<MonsterHitFeedback>()?.Stop();
        if (monsterImage == null || IsDead) return;

        // 외형만 복제해 움직이므로 몬스터 HP바, 루트, 대기 Animator는 건드리지 않습니다.
        if (attackVisual == null)
        {
            GameObject visual = new GameObject("Attack Visual");
            visual.transform.SetParent(monsterImage.transform, false);
            attackVisual = visual.AddComponent<SpriteRenderer>();
        }
        attackVisual.sprite = monsterImage.sprite;
        attackVisual.sharedMaterial = monsterImage.sharedMaterial;
        attackVisual.color = monsterImage.color;
        attackVisual.flipX = monsterImage.flipX;
        attackVisual.flipY = monsterImage.flipY;
        attackVisual.sortingLayerID = monsterImage.sortingLayerID;
        attackVisual.sortingOrder = monsterImage.sortingOrder;
        attackVisual.gameObject.layer = monsterImage.gameObject.layer;
        attackVisual.gameObject.SetActive(true);
        attackWasHidden = monsterImage.forceRenderingOff;
        attackVisualActive = true;
        monsterImage.forceRenderingOff = true;
        Transform visualTransform = attackVisual.transform;
        visualTransform.localPosition = Vector3.zero;
        visualTransform.localScale = Vector3.one;
        float travel = monsterImage.sprite != null ? monsterImage.sprite.bounds.size.y * attackTravelRatio : 0.1f;
        float prepare = Mathf.Max(0.01f, attackPrepareDuration);
        float lunge = Mathf.Max(0.01f, attackLungeDuration);
        attackTween = DOTween.Sequence();
        attackTween.Append(visualTransform.DOScale(0.96f, prepare).SetEase(Ease.OutSine));
        attackTween.Join(visualTransform.DOLocalMoveY(travel * 0.25f, prepare));
        attackTween.Append(visualTransform.DOScale(attackScale, lunge).SetEase(Ease.InQuad));
        attackTween.Join(visualTransform.DOLocalMoveY(-travel, lunge).SetEase(Ease.InQuad));
        attackTween.Append(visualTransform.DOScale(1f, Mathf.Max(0.01f, attackReturnDuration)).SetEase(Ease.OutSine));
        attackTween.Join(visualTransform.DOLocalMoveY(0f, Mathf.Max(0.01f, attackReturnDuration)).SetEase(Ease.OutSine));
        attackTween.OnKill(RestoreAttackVisual);
    }

    private void RestoreAttackVisual()
    {
        if (attackVisualActive && monsterImage != null) monsterImage.forceRenderingOff = attackWasHidden;
        attackVisualActive = false;
        if (attackVisual != null) attackVisual.gameObject.SetActive(false);
        attackTween = null;
    }

    private void StopAttackPresentation()
    {
        attackTween?.Kill();
        RestoreAttackVisual();
    }

    private void OnDisable()
    {
        StopAttackPresentation();
    }

    private void UpdateHPBar(bool immediate)
    {
        if (enemyHPSlider == null) return;
        enemyHPSlider.maxValue = MaxHP;
        if (immediate) enemyHPSlider.value = CurrentHP;
    }

    private IEnumerator HitEffectRoutine()
    {
        if (monsterImage == null) yield break;
        monsterImage.color = hitColor;
        float hpPercent = MaxHP > 0 ? (float)CurrentHP / MaxHP : 0f;
        Color targetColor = Color.Lerp(Color.red, Color.white, hpPercent);

        float elapsed = 0f;
        while (elapsed < hitEffectDuration)
        {
            elapsed += Time.deltaTime;
            monsterImage.color = Color.Lerp(hitColor, targetColor, elapsed / hitEffectDuration);
            yield return null;
        }
        monsterImage.color = targetColor;
    }

    // 포획/일반 사망이 Transform과 색상을 독점하도록 기존 피격·공격 연출을 종료합니다.
    public bool BeginDeathPresentation()
    {
        if (!IsDead || deathPresentationStarted) return false;
        deathPresentationStarted = true;
        StopAttackPresentation();
        GetComponent<MonsterHitFeedback>()?.Stop();
        StopAllCoroutines();
        hitEffectCoroutine = null;
        hpCoroutine = null;
        UpdateHPBar(true);
        if (monsterAnimator != null)
        {
            deathAnimatorWasEnabled = monsterAnimator.enabled;
            deathAnimatorPaused = true;
            monsterAnimator.enabled = false;
        }
        return true;
    }

    public IEnumerator PlayNormalDeathPresentation()
    {
        // DiceManager의 코루틴에서 실행하므로 몬스터 비활성화 후에도 클리어 처리가 이어집니다.
        yield return MonsterDeathRoutine(null);
    }

    private void ResetDeathPresentation()
    {
        if (deathAnimatorPaused && monsterAnimator != null) monsterAnimator.enabled = deathAnimatorWasEnabled;
        deathAnimatorPaused = false;
        deathPresentationStarted = false;
    }

    private IEnumerator MonsterDeathRoutine(System.Action onFinished)
    {
        if (monsterRuntimeMat != null)
            monsterRuntimeMat.SetFloat(edgeGlowPowerProperty, edgeGlowPower);

        float elapsed = 0f;
        Vector3 bigScale = originalScale * punchScale;
        while (elapsed < punchDuration)
        {
            elapsed += Time.deltaTime;
            transform.localScale = Vector3.Lerp(originalScale, bigScale, elapsed / punchDuration);
            yield return null;
        }

        if (deathParticlePrefab != null)
        {
            GameObject particle = Instantiate(deathParticlePrefab, monsterImage.bounds.center, Quaternion.identity);
            ParticleSystem ps = particle.GetComponent<ParticleSystem>();
            if (ps != null) ps.Play();
        }

        elapsed = 0f;
        while (elapsed < dissolveDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / dissolveDuration;
            if (monsterRuntimeMat != null) monsterRuntimeMat.SetFloat(dissolveProperty, t);
            transform.localScale = Vector3.Lerp(bigScale, Vector3.zero, t);
            yield return null;
        }

        gameObject.SetActive(false);
        onFinished?.Invoke();
    }

    private IEnumerator ShrinkHPBarRoutine()
    {
        float duration = 0.3f, elapsed = 0f;
        float startValue = enemyHPSlider.value;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            enemyHPSlider.value = Mathf.Lerp(startValue, CurrentHP, elapsed / duration);
            yield return null;
        }
        enemyHPSlider.value = CurrentHP;
    }

    public void RestoreMonster(MonsterDataSO monsterData, int hp, int maxHp, int attack, int index, int currentTurn, int maxTurn)
    {
        StopAttackPresentation();
        ResetDeathPresentation();
        GetComponent<MonsterHitFeedback>()?.Stop();
        CurrentMonsterName = monsterData.monsterName;
        currentMonsterIndex = index;

        //세이브 파일에서 불러올 때도 능력 기억하기
        CurrentBossAbility = monsterData.bossAbility;

        if (monsterAnimator != null && monsterData.animatorController != null)
            monsterAnimator.runtimeAnimatorController = monsterData.animatorController;
        else
        {
            if (monsterAnimator != null) monsterAnimator.runtimeAnimatorController = null;
            if (monsterImage != null) monsterImage.sprite = monsterData.monsterSprite;
        }

        dropFigureData = monsterData.dropFigureData;
        baseDropRate = monsterData.dropRate;

        if (dropFigureData != null && CollectionDataManager.Instance != null)
            CollectionDataManager.Instance.EncounterFigure(dropFigureData);

        MaxHP = maxHp;
        CurrentHP = hp;
        AttackPower = attack;
        IsDead = false;
        useExternalDeathSequence = false;

        //세이브 파일에 턴이 없으면(옛날 세이브면) 기본 2로 복구
        MaxAttackTurn = maxTurn > 0 ? maxTurn : 2;
        CurrentAttackTurn = currentTurn > 0 ? currentTurn : 2;

        transform.position = originalPosition;
        transform.localScale = originalScale;

        if (monsterImage != null)
        {
            gameObject.SetActive(true);
            monsterImage.color = Color.white;
            if (monsterRuntimeMat == null)
            {
                monsterRuntimeMat = Instantiate(monsterImage.material);
                monsterImage.material = monsterRuntimeMat;
            }
            monsterRuntimeMat.SetFloat(dissolveProperty, 0f);
        }

        if (attackPowerText != null) attackPowerText.text = $"{AttackPower}";
        UpdateHPBar(true);
    }


    // 턴이 변할 때마다 화면의 글씨를 바꿔주는 함수
    private void UpdateTurnUI()
    {
        if (turnText != null)
        {
            turnText.text = CurrentAttackTurn.ToString();
        }
    }

    // 스테이지 시작 시 최대 체력을 낮추는 효과용
    public void ReduceMaxHP(int amount)
    {
        if (IsDead || amount <= 0) return;

        // 최대 체력 감소만으로 적이 죽지는 않도록 최소 1 유지
        MaxHP = Mathf.Max(1, MaxHP - amount);
        CurrentHP = Mathf.Min(CurrentHP, MaxHP);

        // 이전 체력바 애니메이션이 새 값을 덮어쓰지 않도록 중단
        if (hpCoroutine != null)
        {
            StopCoroutine(hpCoroutine);
            hpCoroutine = null;
        }

        UpdateHPBar(true);
    }

    public void DecreaseTurn()
    {
        CurrentAttackTurn--;
        UpdateTurnUI(); // 턴 깎일 때 UI 업데이트
    }

    public void ResetTurn()
    {
        CurrentAttackTurn = MaxAttackTurn;
        UpdateTurnUI(); // 턴 초기화될 때 UI 업데이트
    }

}