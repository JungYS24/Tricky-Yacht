using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class MonsterController : MonoBehaviour
{
    [Header("몬스터 참조")]
    public GameObject monsterObject;
    public SpriteRenderer monsterImage;
    public GameObject deathParticlePrefab;
    public Slider enemyHPSlider; 

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

    public int maxHP { get; private set; }
    public int currentHP { get; private set; }
    public bool isDead { get; private set; }

    private Material monsterRuntimeMat;
    private Vector3 monsterOriginalScale;
    private Coroutine monsterDeathCoroutine;

    void Awake()
    {
        SetupMonsterMaterial();
    }

    void SetupMonsterMaterial()
    {
        if (monsterObject == null) return;
        if (monsterImage == null) monsterImage = monsterObject.GetComponent<SpriteRenderer>();
        if (monsterImage == null) return;

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

    public void InitializeMonster(int newMaxHP)
    {
        maxHP = newMaxHP;
        currentHP = maxHP;
        isDead = false;

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

        if (monsterImage != null) monsterImage.color = Color.white;

        if (enemyHPSlider != null)
        {
            enemyHPSlider.maxValue = maxHP;
            enemyHPSlider.value = currentHP;
        }
    }

    public void TakeDamage(int damage)
    {
        if (isDead) return;

        currentHP = Mathf.Max(0, currentHP - damage);

        // 피격 효과
        if (damage > 0 && monsterImage != null)
        {
            if (hitEffectCoroutine != null) StopCoroutine(hitEffectCoroutine);
            hitEffectCoroutine = StartCoroutine(HitEffectRoutine());
        }

        StartCoroutine(ShrinkHPBarRoutine());
        if (currentHP <= 0 && !isDead)
        {
            if (monsterDeathCoroutine != null) StopCoroutine(monsterDeathCoroutine);
            monsterDeathCoroutine = StartCoroutine(MonsterDeathRoutine());
        }
    }

    private IEnumerator HitEffectRoutine()
    {
        float elapsed = 0f;
        monsterImage.color = hitColor;

        float hpPercent = maxHP > 0 ? (float)currentHP / maxHP : 0f;
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
        isDead = true;

        if (monsterObject == null || monsterImage == null) yield break;

        Vector3 startScale = monsterOriginalScale;
        Vector3 bigScale = monsterOriginalScale * punchScale;

        if (monsterRuntimeMat != null)
        {
            monsterRuntimeMat.SetColor(edgeColorAProperty, edgeColorPink);
            monsterRuntimeMat.SetColor(edgeColorBProperty, edgeColorMint);
            monsterRuntimeMat.SetFloat(edgeGlowPowerProperty, edgeGlowPower);
        }

        float elapsed = 0f;
        while (elapsed < punchDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / punchDuration);
            monsterObject.transform.localScale = Vector3.Lerp(startScale, bigScale, t);
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < dissolveDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / dissolveDuration);

            if (monsterRuntimeMat != null) monsterRuntimeMat.SetFloat(dissolveProperty, t);
            monsterObject.transform.localScale = Vector3.Lerp(bigScale, Vector3.zero, t);
            yield return null;
        }

        if (deathParticlePrefab != null) Instantiate(deathParticlePrefab, monsterObject.transform.position, Quaternion.identity);

        if (monsterRuntimeMat != null)
        {
            monsterRuntimeMat.SetFloat(dissolveProperty, 1f);
            monsterRuntimeMat.SetFloat(edgeGlowPowerProperty, normalGlowPower);
        }

        monsterObject.SetActive(false);
        monsterDeathCoroutine = null;
    }

    private IEnumerator ShrinkHPBarRoutine()
    {
        float duration = 0.3f, elapsed = 0f;
        float startValue = enemyHPSlider != null ? enemyHPSlider.value : 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            if (enemyHPSlider != null) enemyHPSlider.value = Mathf.Lerp(startValue, currentHP, elapsed / duration);
            yield return null;
        }

        if (enemyHPSlider != null) enemyHPSlider.value = currentHP;
    }
}