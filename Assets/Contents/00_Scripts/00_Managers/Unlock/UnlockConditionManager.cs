using System;
using UnityEngine;

public class UnlockConditionManager : MonoBehaviour
{
    public static UnlockConditionManager Instance { get; private set; }

    private static readonly UnlockFlag rangeMask = GetMask();

    private static UnlockFlag unlockedFlag = 0;

    private static readonly string UNLOCKED_KEY = "unlock";

    [SerializeField] private UnlockFlag initialFlag = 0;
#if UNITY_EDITOR
    [SerializeField] private bool useUserData;
#else
    private bool useUserData = true;
#endif

    private static RuntimeConditions rc;

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
    [Obsolete(nameof(Unlock) + " 함수를 사용하십시오.", false)]
    public static void SetUnlockCondition(UnlockFlag flag, bool isUnlocked)
    {
        Unlock(flag);
    }

    /// <summary>
    /// 조건을 해금합니다.
    /// </summary>
    /// <param name="flag"></param>
    public static void Unlock(UnlockFlag flag)
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

        unlockedFlag |= flag;
        PlayerPrefs.SetInt(UNLOCKED_KEY, (int)unlockedFlag);
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
        unlockedFlag = useUserData ? (UnlockFlag)PlayerPrefs.GetInt(UNLOCKED_KEY, 0) : initialFlag;

        rc = new();
    }

    private class RuntimeConditions
    {
        public int UsedGolds { get; private set; } = 0;
        public int MaxHP { get; private set; } = DiceManager.Instance.playerMaxHP;

        public RuntimeConditions()
        {
            ShopManager.OnChangeGoldAmount += AddUsedGold;
            DiceManager.OnChangeMaxHP += ChangeMaxHP;
        }

        ~RuntimeConditions()
        {
            ShopManager.OnChangeGoldAmount -= AddUsedGold;
            DiceManager.OnChangeMaxHP -= ChangeMaxHP;
        }

        public void AddUsedGold(int amount)
        {
            UsedGolds += amount;
            if (UsedGolds >= 7777)
            {
                Unlock(UnlockFlag.Spend7777GoldInRun);
            }
        }

        public void ChangeMaxHP(int value)
        {
            MaxHP = value;
            if (MaxHP >= 300)
            {
                Unlock(UnlockFlag.MaxHPThreshold300);
            }
        }
    }
}