using System.Collections;
using UnityEngine;

public class MonsterDissolve : MonoBehaviour
{
    [Header("Renderer")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("Dissolve")]
    [SerializeField] private float dissolveDuration = 0.8f;
    [SerializeField] private string dissolveProperty = "_DissolveAmount";

    [Header("Flash")]
    [SerializeField] private Color flashColorA = new Color(1f, 0.3f, 0.85f, 1f);   // 핑크
    [SerializeField] private Color flashColorB = new Color(0.4f, 1f, 0.85f, 1f);   // 민트
    [SerializeField] private float flashDuration = 0.08f;
    [SerializeField] private string edgeColorAProperty = "_EdgeColorA";
    [SerializeField] private string edgeColorBProperty = "_EdgeColorB";
    [SerializeField] private string edgeGlowPowerProperty = "_EdgeGlowPower";
    [SerializeField] private float flashGlowPower = 7f;
    [SerializeField] private float normalGlowPower = 4f;

    [Header("Punch Scale")]
    [SerializeField] private float punchScale = 1.12f;
    [SerializeField] private float punchDuration = 0.08f;

    [Header("FX")]
    [SerializeField] private GameObject deathParticlePrefab;

    [Header("Camera Shake")]
    [SerializeField] private float shakeDuration = 0.12f;
    [SerializeField] private float shakeMagnitude = 0.12f;

    [Header("Fake Haptic / Hit Stop")]
    [SerializeField] private float hitStopDuration = 0.03f;

    private Material runtimeMat;
    private Vector3 originalScale;
    private bool isDead;

    private void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        originalScale = transform.localScale;

        if (spriteRenderer != null && spriteRenderer.material != null)
        {
            runtimeMat = Instantiate(spriteRenderer.material);
            spriteRenderer.material = runtimeMat;
            runtimeMat.SetFloat(dissolveProperty, 0f);
            runtimeMat.SetFloat(edgeGlowPowerProperty, normalGlowPower);
            runtimeMat.SetColor(edgeColorAProperty, flashColorA);
            runtimeMat.SetColor(edgeColorBProperty, flashColorB);
        }
    }

    public void KillMonster()
    {
        if (isDead) return;
        isDead = true;
        StartCoroutine(DeathRoutine());
    }

    private IEnumerator DeathRoutine()
    {
        // 1) 핑크/민트 번쩍
        if (runtimeMat != null)
        {
            runtimeMat.SetColor(edgeColorAProperty, flashColorA);
            runtimeMat.SetColor(edgeColorBProperty, flashColorB);
            runtimeMat.SetFloat(edgeGlowPowerProperty, flashGlowPower);
        }

        // 2) 파티클 생성
        if (deathParticlePrefab != null)
            Instantiate(deathParticlePrefab, transform.position, Quaternion.identity);

        // 3) 화면 흔들림
        if (CameraShake2D.Instance != null)
            CameraShake2D.Instance.Shake(shakeDuration, shakeMagnitude);

        // 4) 살짝 튀는 느낌
        yield return StartCoroutine(PunchScaleRoutine());

        // 5) 짧은 히트스탑
        yield return StartCoroutine(HitStopRoutine(hitStopDuration));

        // 6) 글로우 원래값으로
        if (runtimeMat != null)
            runtimeMat.SetFloat(edgeGlowPowerProperty, normalGlowPower);

        // 7) 디졸브 시작
        float elapsed = 0f;

        while (elapsed < dissolveDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / dissolveDuration);

            if (runtimeMat != null)
                runtimeMat.SetFloat(dissolveProperty, t);

            yield return null;
        }

        if (runtimeMat != null)
            runtimeMat.SetFloat(dissolveProperty, 1f);

        Destroy(gameObject);
    }

    private IEnumerator PunchScaleRoutine()
    {
        float elapsed = 0f;
        Vector3 targetScale = originalScale * punchScale;

        while (elapsed < punchDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / punchDuration;
            transform.localScale = Vector3.Lerp(originalScale, targetScale, t);
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < punchDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / punchDuration;
            transform.localScale = Vector3.Lerp(targetScale, originalScale, t);
            yield return null;
        }

        transform.localScale = originalScale;
    }

    private IEnumerator HitStopRoutine(float duration)
    {
        float prevTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;
            yield return null;
        }

        Time.timeScale = prevTimeScale;
    }

#if UNITY_EDITOR
    private void Update()
    {
        // 테스트용
        if (Input.GetKeyDown(KeyCode.K))
            KillMonster();
    }
#endif
}