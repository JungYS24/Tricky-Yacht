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
    public List<int> passedNodeIds = new List<int>();
    public int pendingJourneyMove;

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
        ClearMapTrail();
        currentMap = null;
    }

    public void BeginBiome(MapTableData table, BiomeType biome)
    {
        currentNodeId = -1;
        currentNodeCleared = false;
        ClearMapTrail();
        currentMap = table == null
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

    // 층 위치를 바이옴당 스테이지 번호 폭(Stages_Per_Biome)에 맞춥니다.
    // 일반 노드는 항상 바이옴 시작+1 ~ 시작+(폭-1) 안에 들어오고, 보스는 폭의 배수라서 보스 판정이 그대로 동작합니다.
    public void ApplyNodeDifficulty(MapNode node, MapConfig config)
    {
        if (node == null || config == null) return;
        int perBiome = Mathf.Max(3, config.StagesPerBiome);
        int baseStage = biomeIndex * perBiome;
        if (node.type == MapNodeType.Boss)
        {
            currentStage = baseStage + perBiome;
            return;
        }

        int span = Mathf.Max(1, config.MaxPathLength - 1);
        int offset = node.row * (perBiome - 2) / span;
        currentStage = baseStage + 1 + Mathf.Clamp(offset, 0, perBiome - 2);
    }

    public void ClearMapTrail()
    {
        pendingJourneyMove = 0;
        if (visitedNodeIds == null) visitedNodeIds = new List<int>();
        visitedNodeIds.Clear();
        if (passedNodeIds == null) passedNodeIds = new List<int>();
        passedNodeIds.Clear();
    }
}