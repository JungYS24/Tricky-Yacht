using UnityEngine;

public class HandVFXManager : MonoBehaviour
{
    [Header("재생 위치")]
    public Transform effectSpawnPoint;

    [Header("족보별 VFX 프리팹")]
    public GameObject onePairVFX;
    public GameObject twoPairVFX;
    public GameObject tripleVFX;
    public GameObject straightVFX;
    public GameObject fiveCardVFX;

    public void PlayHandVFX(string handName)
    {
        GameObject targetPrefab = null;

        switch (handName)
        {
            case "원 페어":
                targetPrefab = onePairVFX;
                break;

            case "투 페어":
                targetPrefab = twoPairVFX;
                break;

            case "트리플":
                targetPrefab = tripleVFX;
                break;

            case "스트레이트":
                targetPrefab = straightVFX;
                break;

            case "파이브 카드":
            case "Yacht":
            case "요트":
                targetPrefab = fiveCardVFX;
                break;
        }

        if (targetPrefab == null)
            return;

        Vector3 spawnPos = effectSpawnPoint != null ? effectSpawnPoint.position : transform.position;
        GameObject vfx = Instantiate(targetPrefab, spawnPos, Quaternion.identity);

        ParticleSystem ps = vfx.GetComponent<ParticleSystem>();
        if (ps != null)
        {
            Destroy(vfx, ps.main.duration + ps.main.startLifetime.constantMax + 0.2f);
        }
        else
        {
            Destroy(vfx, 2f);
        }
    }
}