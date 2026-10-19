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

    public void PlayHandVFX(HandRank handRank)
    {
        GameObject targetPrefab = null;

        switch (handRank)
        {
            case HandRank.OnePair:
                targetPrefab = onePairVFX;
                break;

            case HandRank.TwoPair:
                targetPrefab = twoPairVFX;
                break;

            case HandRank.Triple:
                targetPrefab = tripleVFX;
                break;

            case HandRank.Straight:
                targetPrefab = straightVFX;
                break;

            case HandRank.Yacht:
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