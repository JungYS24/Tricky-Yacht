public enum HandRank
{
    HighCard,
    OnePair,
    TwoPair,
    Triple,
    Straight,
    FullHouse,
    FourOfAKind,
    Yacht
}

public static class HandRankUtil
{
    public static string GetLocKey(HandRank rank)
    {
        switch (rank)
        {
            case HandRank.OnePair: return "GP_HAND_ONE_PAIR";
            case HandRank.TwoPair: return "GP_HAND_TWO_PAIR";
            case HandRank.Triple: return "GP_HAND_THREE_OF_A_KIND";
            case HandRank.Straight: return "GP_HAND_STRAIGHT";
            case HandRank.FullHouse: return "GP_HAND_FULL_HOUSE";
            case HandRank.FourOfAKind: return "GP_HAND_FOUR_OF_A_KIND";
            case HandRank.Yacht: return "GP_HAND_YACHT";
            default: return "GP_HAND_HIGH_CARD";
        }
    }

    public static string GetFallbackName(HandRank rank)
    {
        switch (rank)
        {
            case HandRank.OnePair: return "원 페어";
            case HandRank.TwoPair: return "투 페어";
            case HandRank.Triple: return "트리플";
            case HandRank.Straight: return "스트레이트";
            case HandRank.FullHouse: return "풀하우스";
            case HandRank.FourOfAKind: return "포카드";
            case HandRank.Yacht: return "야추";
            default: return "하이 카드";
        }
    }

    public static bool MatchesTrigger(HandRank rank, FigureTriggerType trigger)
    {
        switch (trigger)
        {
            case FigureTriggerType.OnePair: return rank == HandRank.OnePair;
            case FigureTriggerType.TwoPair: return rank == HandRank.TwoPair;
            case FigureTriggerType.Triple: return rank == HandRank.Triple;
            case FigureTriggerType.Straight: return rank == HandRank.Straight;
            case FigureTriggerType.FullHouse: return rank == HandRank.FullHouse;
            case FigureTriggerType.FourOfAKind: return rank == HandRank.FourOfAKind;
            case FigureTriggerType.Yacht: return rank == HandRank.Yacht;
            default: return false;
        }
    }

    public static void AddHandTriggers(HandRank rank, System.Collections.Generic.List<FigureTriggerType> activeTriggers)
    {
        switch (rank)
        {
            case HandRank.OnePair: activeTriggers.Add(FigureTriggerType.OnePair); break;
            case HandRank.TwoPair: activeTriggers.Add(FigureTriggerType.TwoPair); break;
            case HandRank.Triple: activeTriggers.Add(FigureTriggerType.Triple); break;
            case HandRank.Straight: activeTriggers.Add(FigureTriggerType.Straight); break;
            case HandRank.FullHouse: activeTriggers.Add(FigureTriggerType.FullHouse); break;
            case HandRank.FourOfAKind: activeTriggers.Add(FigureTriggerType.FourOfAKind); break;
            case HandRank.Yacht: activeTriggers.Add(FigureTriggerType.Yacht); break;
        }
    }
}
