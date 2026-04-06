using System.Collections;
using UnityEngine;

public class MonsterDissolve : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private GameObject deathParticlePrefab;

    [Header("Dissolve Settings")]
    [SerializeField] private float dissolveDuration = 0.8f;
    [SerializeField] private float hitFlashDuration = 0.06f;
    [SerializeField] private float endScaleMultiplier = 0.85f;

    private Material runtimeMaterial;
    private bool isDying = false;

    private readonly int DissolveAmountID = Shader.PropertyToID("_DissolveAmount");
    private readonly int HitFlashID = Shader.PropertyToID("_HitFlash");

    private void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        runtimeMaterial = Instantiate(spriteRenderer.material);
        spriteRenderer.material = runtimeMaterial;

        runtimeMaterial.SetFloat(DissolveAmountID, 0f);
        runtimeMaterial.SetFloat(HitFlashID, 0f);
    }

    public void PlayHitFlash()
    {
        if (!gameObject.activeInHierarchy || isDying) return;
        StartCoroutine(HitFlashRoutine());
    }

    private IEnumerator HitFlashRoutine()
    {
        runtimeMaterial.SetFloat(HitFlashID, 1f);
        yield return new WaitForSeconds(hitFlashDuration);
        runtimeMaterial.SetFloat(HitFlashID, 0f);
    }

    public void PlayDissolveDeath()
    {
        if (isDying) return;
        isDying = true;
        StartCoroutine(DissolveRoutine());
    }

    private IEnumerator DissolveRoutine()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null) rb.linearVelocity = Vector2.zero;

        Vector3 startScale = transform.localScale;
        Vector3 endScale = startScale * endScaleMultiplier;
        Vector3 spawnPos = transform.position;

        float time = 0f;

        while (time < dissolveDuration)
        {
            time += Time.deltaTime;
            float t = Mathf.Clamp01(time / dissolveDuration);

            runtimeMaterial.SetFloat(DissolveAmountID, t);
            transform.localScale = Vector3.Lerp(startScale, endScale, t);

            yield return null;
        }

        runtimeMaterial.SetFloat(DissolveAmountID, 1f);

        SpawnDeathParticle(spawnPos);

        Destroy(gameObject);
    }

    private void SpawnDeathParticle(Vector3 position)
    {
        if (deathParticlePrefab == null)
        {
            Debug.LogWarning("deathParticlePrefab 이 비어있음");
            return;
        }

        Debug.Log("죽음 파티클 생성!");

        GameObject particleObj = Instantiate(deathParticlePrefab, position, Quaternion.identity);

        ParticleSystem ps = particleObj.GetComponentInChildren<ParticleSystem>();
        if (ps != null)
        {
            ps.gameObject.SetActive(true);
            ps.Clear(true);
            ps.Play(true);
            Destroy(particleObj, 2f);
        }
        else
        {
            Debug.LogWarning("프리팹 안에 ParticleSystem 없음");
            Destroy(particleObj, 2f);
        }
    }
}