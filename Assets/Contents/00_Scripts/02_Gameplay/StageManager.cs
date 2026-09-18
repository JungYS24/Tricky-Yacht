using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class StageManager
{
    public int currentStage = 1;
    public BiomeDataSO currentBiome;

    // 게임 시작 시 첫 바이옴(숲)을 강제로 세팅하는 로직
    public void InitFirstBiome(List<BiomeDataSO> biomeList)
    {
        currentStage = 1;
        if (biomeList != null && biomeList.Count > 0)
        {
            currentBiome = biomeList.Find(b => b.biomeType == BiomeType.Forest);
            if (currentBiome == null) currentBiome = biomeList[0];
        }
    }

    // 스테이지 배경 및 BGM 변경 적용
    public void ApplyBiomeEnvironment(SpriteRenderer biomeBackgroundImage)
    {
        if (currentBiome != null)
        {
            if (biomeBackgroundImage != null && currentBiome.backgroundImage != null)
            {
                biomeBackgroundImage.sprite = currentBiome.backgroundImage;
            }
            if (BGMManager.Instance != null && currentBiome.biomeBGM != null)
            {
                BGMManager.Instance.ChangeBGM(currentBiome.biomeBGM);
            }
        }
        else
        {
            Debug.LogWarning("현재 설정된 바이옴이 없습니다!");
        }
    }

    // 다음 스테이지로 라운드 증가 및 보스전(바이옴 교체) 타이밍 판정
    // 반환값이 true이면 "보스를 잡았으니 다음 바이옴을 선택해라"는 뜻
    public bool AdvanceToNextStage(bool isTutorial)
    {
        currentStage++;

        // 튜토리얼이 아니고, 끝자리가 1로 떨어질 때 (11, 21, 31...) 바이옴 선택 타이밍
        return !isTutorial && (currentStage - 1) % 10 == 0 && currentStage <= 100;
    }

    // 유저가 바이옴 선택 패널에서 고른 맵을 적용
    public void SetNewBiome(List<BiomeDataSO> biomeList, BiomeType selectedType)
    {
        currentBiome = biomeList.Find(b => b.biomeType == selectedType);
    }
}