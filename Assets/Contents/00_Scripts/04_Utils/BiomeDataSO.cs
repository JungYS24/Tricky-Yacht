using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewBiome", menuName = "Stage/BiomeData")]
public class BiomeDataSO : ScriptableObject
{
    [Header("맵 설정")]
    public string biomeName = "새로운 생물군계";
    public Sprite backgroundImage;

    [Header("이 맵에 등장하는 일반 몬스터 목록")]
    public List<MonsterDataSO> biomeMonsters;

    // 5스테이지마다 등장할 보스 몬스터를 넣는 칸
    [Header("보스 몬스터 (5스테이지마다 등장)")]
    public MonsterDataSO bossMonster;
}