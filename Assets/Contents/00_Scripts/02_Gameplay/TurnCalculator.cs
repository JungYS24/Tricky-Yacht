using UnityEngine;
using System.Collections.Generic;
using System.Linq;

// 결산 결과를 한 번에 담아 전달할 구조체
public struct TurnCalcResult
{
    public int baseSum;
    public int expectedGold;
    public int expectedHeal;
    public int flameDamageThisTurn;

    public int iceBonusChips;
    public float prismMultTotal;

    public int satelliteBonusChips;
    public float satelliteBonusMult;
    public float iceBonusMult;
}

public static class TurnCalculator
{
    public const int GoldPerPip = 10;

    public static HandRank CalculateHand(List<int> values, DiceManager dm, out float multiplier)
    {
        multiplier = dm.multHighCard;
        HandRank rank = HandRank.HighCard;

        Dictionary<int, int> countDict = new Dictionary<int, int>();
        foreach (int v in values)
        {
            if (countDict.ContainsKey(v)) countDict[v]++;
            else countDict[v] = 1;
        }
        List<int> counts = countDict.Values.ToList();

        List<int> sortedValues = new List<int>(values); sortedValues.Sort();

        if (counts.Any(c => c == 5))
        {
            multiplier = dm.multYacht;
            return HandRank.Yacht;
        }

        bool isStraight = true;
        if (sortedValues.Contains(0))
        {
            isStraight = false;
        }
        else
        {
            for (int i = 0; i < sortedValues.Count - 1; i++)
            {
                if (sortedValues[i] + 1 != sortedValues[i + 1])
                {
                    isStraight = false;
                    break;
                }
            }
        }

        if (isStraight)
        {
            multiplier = dm.multStraight;
            return HandRank.Straight;
        }

        if (counts.Any(c => c == 4)) { multiplier = dm.multFourOfAKind; return HandRank.FourOfAKind; }
        if (counts.Any(c => c == 3) && counts.Any(c => c == 2)) { multiplier = dm.multFullHouse; return HandRank.FullHouse; }
        if (counts.Any(c => c == 3)) { multiplier = dm.multTriple; return HandRank.Triple; }
        if (counts.Count(c => c == 2) == 2) { multiplier = dm.multTwoPair; return HandRank.TwoPair; }
        if (counts.Any(c => c == 2)) { multiplier = dm.multOnePair; return HandRank.OnePair; }

        return rank;
    }

    // 결산 및 UI 갱신 시 주사위 개별 효과들을 한 번에 합산해주는 순수 연산 함수
    public static TurnCalcResult CalculateDiceEffects(List<Dice> targetDice, int currentEnemyHP, float healMultiplier)
    {
        TurnCalcResult res = new TurnCalcResult();
        int currentSimulatedHP = currentEnemyHP;

        foreach (var d in targetDice)
        {
            int extraIceChips = FigureEffectManager.Instance != null? FigureEffectManager.Instance.GetIceChipsBonus(): 0;
            float extraIceMult = FigureEffectManager.Instance != null? FigureEffectManager.Instance.GetIceMultiplierBonus(): 0f;
            res.baseSum += d.currentValue;

            switch (d.myData.specialEffect)
            {
                case SpecialDieEffect.Coin:
                    res.expectedGold += d.currentValue * GoldPerPip;
                    break;
                case SpecialDieEffect.Heart:
                    res.expectedHeal += Mathf.FloorToInt(d.currentValue * healMultiplier);
                    break;
                case SpecialDieEffect.Flame:
                    res.flameDamageThisTurn += (d.currentValue * 2);
                    break;
            }

            if (d.myData.isCoated)
            {
                switch (d.myData.type)
                {
                    case DiceType.Prism:
                        res.prismMultTotal += (d.myData.multiplier - 1.0f);
                        break;
                    case DiceType.Gold:
                        res.expectedGold += d.currentValue * GoldPerPip;
                        break;
                    case DiceType.Ice:
                        res.iceBonusChips += 10 + extraIceChips;
                        res.iceBonusMult += extraIceMult;
                        break;
                }
            }

            // 위성 효과 연산 
            if (d.myData.activeSatellites != null && d.myData.activeSatellites.Count > 0)
            {
                foreach (var sat in d.myData.activeSatellites)
                {
                    switch (sat)
                    {
                        case SatelliteType.Mercury: res.satelliteBonusChips += 15; break;
                        case SatelliteType.Venus: res.expectedGold += 30; break;
                        case SatelliteType.Mars: res.satelliteBonusMult += 1.1f; break;
                        case SatelliteType.Jupiter:
                            res.expectedHeal += Mathf.FloorToInt(2 * healMultiplier);
                            break;
                    }
                }
            }
        }
        return res;
    }
}