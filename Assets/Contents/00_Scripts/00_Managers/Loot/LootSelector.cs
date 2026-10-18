using UnityEngine;

public class LootSelector : MonoBehaviour
{
    // Normal, Rare, Epic, Legendary 순서대로 배치
    // 스테이지에서 확률은 (스테이지 - 1) / 25 식으로 인덱스를 구함
    private static readonly int[,] probability =
    {
        { 68, 24,  6,  2 },
        { 50, 31, 15,  4 },
        { 34, 34, 24,  8 },
        { 20, 32, 32, 16 },
    };

    /// <summary>
    /// 일반 스테이지 클리어 시 얻을 전리품 등급을 뽑습니다.
    /// </summary>
    /// <param name="stage">현재 스테이지</param>
    /// <returns></returns>
    /// <exception cref="System.ArgumentOutOfRangeException"></exception>
    public static ItemGrade GetLootGrade(int stage)
    {
        int range = (stage - 1) / 25;
        int sum = 0;
        int rnd = Random.Range(0, 100);
        for (int i = 0; i < 4; i++)
        {
            sum += probability[range, i];
            if (rnd < sum)
            {
                return (ItemGrade)i + 1;
            }
        }

        throw new System.ArgumentOutOfRangeException();
    }

    /// <summary>
    /// 보스 처치 후 얻을 전리품 등급을 뽑습니다.
    /// </summary>
    /// <param name="stage">현재 스테이지</param>
    /// <returns></returns>
    public static ItemGrade GetLootGradeAfterBeatBoss(int stage)
    {
        int range = (stage - 1) / 25;
        int epic = probability[range, 2];
        int legendary = probability[range, 3];
        int rnd = Random.Range(0, epic + legendary);
        return rnd < epic ? ItemGrade.Epic : ItemGrade.Legendary;
    }
}
