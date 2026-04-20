using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic; // List를 사용하기 위해 추가

public class Enemy : MonoBehaviour
{
    [Header("참조 설정")]
    public SpriteRenderer monsterImage;
    public Slider enemyHPSlider;
    public Animator monsterAnimator;
    public GameObject deathParticlePrefab;

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

    // 몬스터 데이터베이스 관리
    [Header("몬스터 출현 순서 리스트")]
    private int currentMonsterIndex = 0; // 몇번 째 몬스터인지 체크

    // 에디터 창에서는 숨기지만 DiceManager가 읽어갈 수 있도록 HideInInspector 처리
    [HideInInspector] public FigureItemSO dropFigureData;
    [HideInInspector] public float baseDropRate = 0.5f;


    public int MaxHP { get; private set; }
    public int CurrentHP { get; private set; }
    public bool IsDead { get; private set; } = false;

    private Material monsterRuntimeMat;
    private Vector3 originalScale;
    private Vector3 originalPosition;
    public bool useExternalDeathSequence = false;
    private Coroutine hitEffectCoroutine;
    private Coroutine hpCoroutine;

    //최초 크기는 무조건 Awake에서 딱 한 번만 저장!
    void Awake()
    {
        originalScale = transform.localScale;
        originalPosition = transform.position;

        if (enemyHPSlider == null)
        {
            GameObject sliderObj = GameObject.Find("EnemyHPSlider");
            if (sliderObj != null)
            {
                enemyHPSlider = sliderObj.GetComponent<Slider>();
            }
            else
            {
                Debug.LogWarning("씬에 'EnemyHPSlider'라는 이름의 오브젝트가 없습니다!");
            }
        }
    }

    public void ResetMonsterIndex()
    {
        currentMonsterIndex = 0;
    }

    public void Initialize(int currentStage, List<MonsterDataSO> currentBiomeMonsters)
    {
        // 1. 만약의 사태를 대비한 기본 체력 (리스트가 비어있을 때 등)
        int finalMaxHP = 40;

        if (currentBiomeMonsters != null && currentBiomeMonsters.Count > 0)
        {
            // 리스트에서 랜덤 몬스터 데이터를 가져옴
            int randomIndex = UnityEngine.Random.Range(0, currentBiomeMonsters.Count);
            MonsterDataSO nextMonsterData = currentBiomeMonsters[randomIndex];

            // 이미지와 전리품 정보 덮어쓰기
            if (monsterAnimator != null && nextMonsterData.animatorController != null)
            {
                monsterAnimator.runtimeAnimatorController = nextMonsterData.animatorController;
            }
            // 전용 애니메이션이 없고 그냥 멈춰있는 이미지라면 애니메이터를 끄고 이미지만 교체
            else
            {
                if (monsterAnimator != null) monsterAnimator.runtimeAnimatorController = null;
                if (monsterImage != null) monsterImage.sprite = nextMonsterData.monsterSprite;
            }
            dropFigureData = nextMonsterData.dropFigureData;
            baseDropRate = nextMonsterData.dropRate;
            Debug.Log($"[{currentStage} 스테이지] 등장 몬스터: {nextMonsterData.monsterName}");

            // 애니메이션 커브로 체력 배율 계산
            float curveMultiplier = nextMonsterData.hpScalingCurve.Evaluate(currentStage);
            //if (curveMultiplier <= 0f)
            //{
            //    curveMultiplier = 1f;
            //}

            // 최종 체력 계산해서 finalMaxHP에 저장
            finalMaxHP = Mathf.FloorToInt(nextMonsterData.baseHP * curveMultiplier);

            // 다음 스테이지를 위해 인덱스 1 증가
            currentMonsterIndex++;
        }

        // 여기서 최종적으로 체력을 확정(덮어씌워지는 문제 해결)
        MaxHP = finalMaxHP;
        CurrentHP = finalMaxHP;
        IsDead = false;
        useExternalDeathSequence = false;

        // 3. 시각적 초기화 (크기, 색상, 디졸브 등)
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

    public void TakeDamage(int damage, System.Action onDeathCallback)
    {
        if (IsDead) return;

        CurrentHP = Mathf.Max(0, CurrentHP - damage);
        // [수정 3] 데미지를 입었으니 HP바 깎는 코루틴 실행!
        if (hpCoroutine != null) StopCoroutine(hpCoroutine);
        hpCoroutine = StartCoroutine(ShrinkHPBarRoutine());

        if (damage > 0)
        {
            if (hitEffectCoroutine != null) StopCoroutine(hitEffectCoroutine);
            hitEffectCoroutine = StartCoroutine(HitEffectRoutine());
        }

        if (CurrentHP <= 0)
        {
            IsDead = true;

            if (useExternalDeathSequence)
            {
                onDeathCallback?.Invoke();
            }
            else
            {
                StartCoroutine(MonsterDeathRoutine(onDeathCallback));
            }
        }
    }

    private void UpdateHPBar(bool immediate)
    {
        if (enemyHPSlider == null) return;
        enemyHPSlider.maxValue = MaxHP;
        if (immediate) enemyHPSlider.value = CurrentHP;
    }

    private IEnumerator HitEffectRoutine()
    {
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
}