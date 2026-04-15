using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewBiome", menuName = "Stage/BiomeData")]
public class BiomeDataSO : ScriptableObject
{
    [Header("맵 설정")]
    public string biomeName = "새로운 생물군계";
    public Sprite backgroundImage; // 바뀔 배경 이미지

    [Header("이 맵에 등장하는 몬스터 목록")]
    public List<MonsterDataSO> biomeMonsters;
}