using System;
using System.Collections.Generic;
using UnityEngine;

// 전리품 표의 키(Reward_ID, SnackType, 주사위 키, Coating_ID, SatelliteType, StampType)를 실제 에셋으로 바꾸는 목록.
// Resources.Load 경로를 새로 만들지 않고 이 SO 하나에 모은다.
[CreateAssetMenu(fileName = "RewardAssetCatalog", menuName = "TrickyYacht/Data/Reward Asset Catalog")]
public class RewardAssetCatalog : ScriptableObject
{
    [Serializable]
    public class IconEntry
    {
        public string rewardId;
        public Sprite icon;
    }

    [Serializable]
    public class DiceEntry
    {
        [Tooltip("Dice_Add의 Param_Str (Coin, Heart, Flame, Odd, Even, High)")]
        public string key;
        public DiceItemSO dice;
    }

    [Serializable]
    public class CoatingEntry
    {
        [Tooltip("Coating_Apply의 Param_Str (2002 Prism, 2003 Gold, 2004 Dark, 2005 Ice)")]
        public string coatingId;
        public CoatingItemSO coating;
    }

    [Header("보상 전용 아이콘 (Reward_ID → 13_Reward/{Reward_ID}.png)")]
    public List<IconEntry> rewardIcons = new List<IconEntry>();

    [Header("대상 에셋")]
    public List<SnackItemSO> snacks = new List<SnackItemSO>();
    public List<DiceEntry> dice = new List<DiceEntry>();
    public List<CoatingEntry> coatings = new List<CoatingEntry>();
    public List<SatelliteItemSO> satellites = new List<SatelliteItemSO>();
    public List<StampItemSO> stamps = new List<StampItemSO>();

    [Header("효과 종류 기본 아이콘")]
    public MaxHPItemSO maxHpItem;
    public CoinItemSO coinItem;
    [Tooltip("티켓 봉투. 아이콘이 리뉴얼한 Random_Ticket 이라 Rwd_Ticket_R이 이 아이콘을 쓴다.")]
    public TicketPackSO ticketPack;

    public Sprite RewardIcon(string rewardId)
    {
        if (string.IsNullOrEmpty(rewardId)) return null;
        foreach (var e in rewardIcons)
            if (e != null && e.icon != null && e.rewardId == rewardId) return e.icon;
        return null;
    }

    public SnackItemSO Snack(SnackType type)
    {
        foreach (var s in snacks)
            if (s != null && s.snackType == type) return s;
        return null;
    }

    public SnackItemSO Snack(string key)
    {
        return TryParseSnack(key, out SnackType type) ? Snack(type) : null;
    }

    public static bool TryParseSnack(string key, out SnackType type)
    {
        type = SnackType.Cherry;
        if (string.IsNullOrEmpty(key)) return false;
        return Enum.TryParse(key.Trim(), true, out type) && Enum.IsDefined(typeof(SnackType), type);
    }

    public DiceItemSO Dice(string key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        foreach (var e in dice)
            if (e != null && e.dice != null && string.Equals(e.key, key.Trim(), StringComparison.OrdinalIgnoreCase)) return e.dice;
        return null;
    }

    public CoatingItemSO Coating(string coatingId)
    {
        if (string.IsNullOrEmpty(coatingId)) return null;
        foreach (var e in coatings)
            if (e != null && e.coating != null && e.coatingId == coatingId.Trim()) return e.coating;
        return null;
    }

    public SatelliteItemSO Satellite(string key)
    {
        if (string.IsNullOrEmpty(key) || !Enum.TryParse(key.Trim(), true, out SatelliteType type)) return null;
        foreach (var s in satellites)
            if (s != null && s.satelliteType == type) return s;
        return null;
    }

    public StampItemSO Stamp(StampType type)
    {
        foreach (var s in stamps)
            if (s != null && s.StampKind == type) return s;
        return null;
    }
}
