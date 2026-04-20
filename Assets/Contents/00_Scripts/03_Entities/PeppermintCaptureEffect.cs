using System.Collections;
using UnityEngine;

public class PeppermintCaptureEffect : MonoBehaviour
{
    [Header("타이밍")]
    public float appearTime = 0.15f;
    public float suctionTime = 0.55f;
    public float endTime = 0.15f;

    [Header("크기")]
    public Vector3 peppermintScale = Vector3.one * 2.0f;

    [Header("이펙트")]
    public GameObject burstPrefab;

    public IEnumerator PlayCapture(Transform targetEnemy, Vector3 captureCenter, GameObject peppermintPrefab)
    {
        if (targetEnemy == null || peppermintPrefab == null)
            yield break;

        GameObject peppermint = Instantiate(peppermintPrefab, captureCenter, Quaternion.identity);
        peppermint.transform.localScale = Vector3.zero;

        Vector3 targetStartPos = targetEnemy.position;
        Vector3 targetStartScale = targetEnemy.localScale;

        Enemy enemyComponent = targetEnemy.GetComponent<Enemy>();
        SpriteRenderer enemySR = null;

        if (enemyComponent != null)
        {
            enemySR = enemyComponent.monsterImage;
        }

        Color enemyColor = Color.white;

        if (enemySR != null)
            enemyColor = enemySR.color;

        float t = 0f;

        // 1) 중앙에 페퍼민트 등장
        while (t < appearTime)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / appearTime);
            peppermint.transform.localScale = Vector3.Lerp(Vector3.zero, peppermintScale, p);
            yield return null;
        }

        // 2) 몬스터 빨려들어가기
        t = 0f;
        while (t < suctionTime)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / suctionTime);
            float curve = p * p; // 점점 빨라짐

            targetEnemy.position = Vector3.Lerp(targetStartPos, captureCenter, curve);
            targetEnemy.localScale = Vector3.Lerp(targetStartScale, Vector3.zero, curve);

            if (enemySR != null)
            {
                Color c = enemyColor;
                c.a = Mathf.Lerp(1f, 0f, curve);
                enemySR.color = c;
            }

            yield return null;
        }

        // 3) 마지막 팡
        if (burstPrefab != null)
            Instantiate(burstPrefab, captureCenter, Quaternion.identity);

        yield return new WaitForSeconds(endTime);

        Destroy(peppermint);
    }
}