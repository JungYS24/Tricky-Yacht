using UnityEngine;

[System.Serializable]
public sealed class ScorePacing
{
    [Min(0f)] public float beforeHandDelay = 0.25f;
    [Min(0f)] public float afterHandDelay = 0.15f;
    [Header("1~3번째 / 4~6번째 / 7번째 이후")]
    [Min(0.01f)] public float bonusDuration = 0.28f;
    [Min(0f)] public float bonusPause = 0.06f;
    [Min(0.01f)] public float middleDuration = 0.22f;
    [Min(0f)] public float middlePause = 0.04f;
    [Min(0.01f)] public float fastDuration = 0.16f;
    [Min(0f)] public float fastPause = 0.02f;
    [Min(0f)] public float phaseTransition = 0.15f;
    [Header("최종 피해량")]
    [Min(0f)] public float afterScoreDelay = 0.2f;
    [Min(0.01f)] public float finalDamageDuration = 0.3f;
    [Min(0f)] public float finalDamageHold = 0.4f;
    [Min(1f)] public float finalFontScale = 1.4f;
    [Min(1f)] public float finalWidthScale = 1.3f;
    [Min(1f)] public float finalPunchScale = 1.15f;

    // completed는 칩/배수 전체에서 실제 표시한 항목 수입니다. 첫 배수도 순서는 초기화하지 않습니다.
    public void GetBonusTiming(int completed, bool firstMultiplier, out float duration, out float pause)
    {
        duration = completed < 3 ? bonusDuration : completed < 6 ? middleDuration : fastDuration;
        pause = completed < 3 ? bonusPause : completed < 6 ? middlePause : fastPause;
        if (firstMultiplier) duration = bonusDuration;
        duration = Mathf.Max(0.01f, duration);
        pause = Mathf.Max(0f, pause);
    }

    public static float GetGap(int completed, bool previousChips, bool currentChips, float pendingPause, float transition)
    {
        if (completed == 0) return 0f;
        return Mathf.Max(0f, previousChips && !currentChips ? transition : pendingPause);
    }
}
