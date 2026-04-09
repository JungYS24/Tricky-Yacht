using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class Enemy : MonoBehaviour
{
    [Header("참조 설정")]
    public SpriteRenderer monsterImage;
    public Slider enemyHPSlider;
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

    public int MaxHP { get; private set; }
    public int CurrentHP { get; private set; }
    public bool IsDead { get; private set; } = false;

    private Material monsterRuntimeMat;
    private Vector3 originalScale;
    private Coroutine hitEffectCoroutine;
    private Coroutine hpCoroutine; // HP바 코루틴 추적용 변수 추가

    // [수정 1] 최초 크기는 무조건 Awake에서 딱 한 번만 저장!
    void Awake()
    {
        originalScale = transform.localScale;
    }

    public void Initialize(int maxHP)
    {
        MaxHP = maxHP;
        CurrentHP = maxHP;
        IsDead = false;

        // [수정 2] 스테이지가 시작될 때마다 아까 저장해둔 원래 크기로 원상복구
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
            StartCoroutine(MonsterDeathRoutine(onDeathCallback));
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

        // 펀치 연출
        float elapsed = 0f;
        Vector3 bigScale = originalScale * punchScale;
        while (elapsed < punchDuration)
        {
            elapsed += Time.deltaTime;
            transform.localScale = Vector3.Lerp(originalScale, bigScale, elapsed / punchDuration);
            yield return null;
        }

        if (deathParticlePrefab != null)
            Instantiate(deathParticlePrefab, monsterImage.bounds.center, Quaternion.identity);

        // 추가 : 파티클이 바로 재생되도록 보장
        if (deathParticlePrefab != null)
        {
            GameObject particle = Instantiate(deathParticlePrefab, monsterImage.bounds.center, Quaternion.identity);
            ParticleSystem ps = particle.GetComponent<ParticleSystem>();
            if (ps != null) ps.Play();
        }

        // 디졸브 연출
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
        onFinished?.Invoke(); // 사망 후 로직 실행 (스테이지 클리어 등)
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