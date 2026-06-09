using UnityEngine;
using System.Collections.Generic;

public class LobbyRandomVisualizer : MonoBehaviour
{
    [Header("Background World Reference")]
    public SpriteRenderer lobbyBackgroundSpriteRenderer;

    [Header("Monster World References")]
    public SpriteRenderer lobbyMonsterSpriteRenderer;
    public Animator lobbyMonsterAnimator;

    [Header("Biome Data Source")]
    public List<BiomeDataSO> biomeDataList;

    void Start()
    {
        VisualizeRandomBiome();
    }

    public void VisualizeRandomBiome()
    {
        if (biomeDataList == null || biomeDataList.Count == 0) return;
        if (lobbyBackgroundSpriteRenderer == null || lobbyMonsterAnimator == null || lobbyMonsterSpriteRenderer == null) return;

        // 1. 15종 바이옴 중 하나 랜덤 선택
        int randomBiomeIndex = Random.Range(0, biomeDataList.Count);
        BiomeDataSO selectedBiome = biomeDataList[randomBiomeIndex];

        // 2. 메인 로비 배경 스프라이트 교체
        if (selectedBiome.backgroundImage != null)
        {
            lobbyBackgroundSpriteRenderer.gameObject.SetActive(true);
            lobbyBackgroundSpriteRenderer.sprite = selectedBiome.backgroundImage;
        }

        // 3. 해당 바이옴의 일반 몬스터 목록 중 하나 랜덤 선택
        if (selectedBiome.biomeMonsters != null && selectedBiome.biomeMonsters.Count > 0)
        {
            int randomMonsterIndex = Random.Range(0, selectedBiome.biomeMonsters.Count);
            MonsterDataSO selectedMonster = selectedBiome.biomeMonsters[randomMonsterIndex];

            if (selectedMonster != null)
            {
                // 4. 몬스터 스프라이트 및 애니메이터 컨트롤러 스왑
                if (selectedMonster.animatorController != null)
                {
                    lobbyMonsterSpriteRenderer.gameObject.SetActive(true);

                    // 초기 기본 스프라이트 갱신 (애니메이션 시작 전 굳어있는 현상 방지)
                    if (selectedMonster.monsterSprite != null)
                    {
                        lobbyMonsterSpriteRenderer.sprite = selectedMonster.monsterSprite;
                    }

                    lobbyMonsterAnimator.runtimeAnimatorController = selectedMonster.animatorController;

                    // Idle 애니메이션 강제 재생
                    lobbyMonsterAnimator.Play("Idle", 0, 0f);
                }
                else
                {
                    lobbyMonsterSpriteRenderer.gameObject.SetActive(false);
                }
            }
        }
        else
        {
            lobbyMonsterSpriteRenderer.gameObject.SetActive(false);
        }
    }
}