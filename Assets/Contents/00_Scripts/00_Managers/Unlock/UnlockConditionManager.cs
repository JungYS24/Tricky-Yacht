using System;
using UnityEngine;

public class UnlockConditionManager : MonoBehaviour
{
    public static UnlockConditionManager Instance { get; private set; }

    private static readonly UnlockFlag rangeMask = GetMask();

    private static UnlockFlag unlockedFlag = 0;

    [SerializeField] private UnlockFlag initialFlag = 0;

    /// <summary>
    /// 여러 조건 플래그를 하나의 플래그 값으로 합쳐줍니다.
    /// </summary>
    /// <param name="flags"></param>
    /// <returns></returns>
    public static UnlockFlag CombineFlags(params UnlockFlag[] flags)
    {
        var result = UnlockFlag.None;
        foreach (var flag in flags)
        {
            result |= flag;
        }
        return result;
    }

    /// <summary>
    /// 조건이 해금되었는지 확인합니다.
    /// </summary>
    /// <param name="flag"></param>
    /// <returns></returns>
    public static bool IsUnlocked(UnlockFlag flag)
    {
        return flag == UnlockFlag.None || (unlockedFlag & flag) == flag;
    }

    /// <summary>
    /// 조건 플래그 값을 변경합니다.
    /// </summary>
    /// <param name="flag"></param>
    /// <param name="isUnlocked"></param>
    public static void SetUnlockCondition(UnlockFlag flag, bool isUnlocked)
    {
        if ((flag & ~rangeMask) != 0)
        {
            Debug.LogWarning("정의되지 않은 조건: " + Convert.ToString((int)flag, 2));
            flag &= rangeMask;
        }

        if (flag == UnlockFlag.None)
        {
            Debug.LogWarning($"{UnlockFlag.None}은 조건이 될 수 없습니다.");
            return;
        }

        unlockedFlag = isUnlocked ?
            unlockedFlag | flag :
            unlockedFlag & ~flag;
    }

    private static UnlockFlag GetMask()
    {
        var flag = UnlockFlag.None;
        foreach (UnlockFlag f in Enum.GetValues(typeof(UnlockFlag)))
        {
            flag |= f;
        }
        return flag;
    }

    private void Awake()
    {
#if UNITY_EDITOR
        unlockedFlag = initialFlag;
#else
        unlockedFlag = (UnlockFlag)PlayerPrefs.GetInt("unlock", 0);
#endif
    }
}