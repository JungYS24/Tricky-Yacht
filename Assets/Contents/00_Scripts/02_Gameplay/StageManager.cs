using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class StageManager
{
    public int currentStage = 1;
    public BiomeDataSO currentBiome;

    // 지도 진행. currentMap은 시드로 다시 만들므로 세이브하지 않습니다.
    public int runSeed;
    public int biomeIndex;
    [System.NonSerialized] public MapData currentMap;
    public int currentNodeId = -1;
    public bool currentNodeCleared;
    public List<int> visitedNodeIds = new List<int>();

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

    public void BeginRun(int seed)
    {
        runSeed = seed;
        biomeIndex = 0;
        currentNodeId = -1;
        currentNodeCleared = false;
        if (visitedNodeIds == null) visitedNodeIds = new List<int>();
        visitedNodeIds.Clear();
        currentMap = null;
    }

    public void BeginBiome(MapTableData table, BiomeType biome)
    {
        currentNodeId = -1;
        currentNodeCleared = false;
        if (visitedNodeIds == null) visitedNodeIds = new List<int>();
        visitedNodeIds.Clear();
        currentMap = biome == BiomeType.Void || table == null
            ? null
            : MapGenerator.Generate(runSeed, biomeIndex, biome, table);
    }

    public MapNode FindNode(int nodeId)
    {
        if (currentMap == null || currentMap.nodes == null) return null;
        if (nodeId < 0 || nodeId >= currentMap.nodes.Count) return null;
        MapNode node = currentMap.nodes[nodeId];
        return node != null && node.id == nodeId ? node : null;
    }

    // 층 위치로 기존 10스테이지 곡선을 맞춥니다. 보스는 10의 배수라서 보스 판정이 그대로 동작합니다.
    public void ApplyNodeDifficulty(MapNode node, MapConfig config)
    {
        if (node == null || config == null) return;
        if (node.type == MapNodeType.Boss)
        {
            currentStage = biomeIndex * 10 + 10;
            return;
        }

        int span = Mathf.Max(1, config.MaxPathLength - 1);
        currentStage = biomeIndex * 10 + 1 + (node.row * config.MinEnemyCount / span);
    }
}