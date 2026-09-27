using UnityEngine;
using System.Collections.Generic;

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

    public int stampBonusChips;
    public int stampDamage;
    public int stampGold;
}

public enum DiceScoreBonusKind
{
    IceChips, MercuryChips, PrismMult, IceMult, MarsMult
}

public struct DiceScoreBonus
{
    public Dice source;
    public DiceScoreBonusKind kind;
    public float amount;
    public bool IsChips => kind == DiceScoreBonusKind.IceChips || kind == DiceScoreBonusKind.MercuryChips;

    public DiceScoreBonus(Dice source, DiceScoreBonusKind kind, float amount)
    {
        this.source = source;
        this.kind = kind;
        this.amount = amount;
    }
}

public struct TurnScoreBonuses
{
    public int stageChips;
    public int snackChips;
    public float stageMult;
    public float snackMult;
    public float permanentMult;
}

public struct TurnScoreResult
{
    public int chips;
    public float multiplier;
    public int damage;
}

public static class TurnCalculator
{
    public const int GoldPerPip = 10;

    public static TurnScoreResult CalculateScore(TurnCalcResult dice, float handMult, TurnScoreBonuses bonuses)
    {
        // 최종 칩 = 주사위 기본합 + 얼음/위성 + 피규어/스테이지 보너스 + 스낵 보너스
        int chips = dice.baseSum + dice.iceBonusChips + dice.satelliteBonusChips + bonuses.stageChips + bonuses.snackChips;
        // 최종 배수 = 족보 배수 + 피규어/스테이지 배수 + 스낵 배수 + 프리즘/위성 배수
        float multiplier = handMult + bonuses.stageMult + bonuses.snackMult + dice.prismMultTotal + dice.satelliteBonusMult + dice.iceBonusMult + bonuses.permanentMult;
        return new TurnScoreResult { chips = chips, multiplier = multiplier, damage = Mathf.FloorToInt(chips * multiplier) };
    }

    // 미리보기와 실제 정산에서 동일한 순차 HP 감소 계산을 사용합니다.
    public static int CalculateDarkDamage(List<Dice> dice, int enemyHP)
    {
        int total = 0;
        foreach (var d in dice)
        {
            if (d.myData.isCoated && d.myData.type == DiceType.Dark)
            {
                int drop = Mathf.FloorToInt(enemyHP * 0.1f);
                total += drop;
                enemyHP -= drop;
            }
        }
        return total;
    }

    public static HandRank CalculateHand(List<int> values, DiceManager dm, out float multiplier)
    {
        multiplier = dm.multHighCard;
        HandRank rank = HandRank.HighCard;

        if (values == null || values.Count == 0)
        {
            multiplier = dm.multHighCard;
            return HandRank.HighCard;
        }

        // 최대 5개인 주사위는 작은 중첩 루프가 Dictionary/List 생성보다 간단하고 할당이 없습니다.
        int pairs = 0;
        bool triple = false, four = false, yacht = false, hasZero = false;
        int uniqueCount = 0, minimum = int.MaxValue, maximum = int.MinValue;
        for (int i = 0; i < values.Count; i++)
        {
            int value = values[i];
            bool alreadyCounted = false;
            for (int j = 0; j < i; j++)
            {
                if (values[j] == value) { alreadyCounted = true; break; }
            }
            if (alreadyCounted) continue;

            uniqueCount++;
            if (value == 0) hasZero = true;
            if (value < minimum) minimum = value;
            if (value > maximum) maximum = value;
            int count = 1;
            for (int j = i + 1; j < values.Count; j++) if (values[j] == value) count++;
            if (count == 2) pairs++;
            else if (count == 3) triple = true;
            else if (count == 4) four = true;
            else if (count == 5) yacht = true;
        }

        // 기존 판정 우선순위와 0 눈금의 스트레이트 제외 규칙을 유지합니다.
        if (yacht) { multiplier = dm.multYacht; return HandRank.Yacht; }
        if (values.Count == 5 && uniqueCount == 5 && !hasZero && (long)maximum - minimum == 4)
        { multiplier = dm.multStraight; return HandRank.Straight; }
        if (four) { multiplier = dm.multFourOfAKind; return HandRank.FourOfAKind; }
        if (triple && pairs > 0) { multiplier = dm.multFullHouse; return HandRank.FullHouse; }
        if (triple) { multiplier = dm.multTriple; return HandRank.Triple; }
        if (pairs == 2) { multiplier = dm.multTwoPair; return HandRank.TwoPair; }
        if (pairs > 0) { multiplier = dm.multOnePair; return HandRank.OnePair; }

        return rank;
    }

    // 결산 및 UI 갱신 시 주사위 개별 효과들을 한 번에 합산해주는 순수 연산 함수
    public static TurnCalcResult CalculateDiceEffects(List<Dice> targetDice, int currentEnemyHP, float healMultiplier, List<DiceScoreBonus> bonusSteps = null)
    {
        bonusSteps?.Clear();
        TurnCalcResult res = new TurnCalcResult();
        int extraIceChips = FigureEffectManager.Instance != null ? FigureEffectManager.Instance.GetIceChipsBonus() : 0;
        float extraIceMult = FigureEffectManager.Instance != null ? FigureEffectManager.Instance.GetIceMultiplierBonus() : 0f;

        foreach (var d in targetDice)
        {
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
                        bonusSteps?.Add(new DiceScoreBonus(d, DiceScoreBonusKind.PrismMult, d.myData.multiplier - 1.0f));
                        break;
                    case DiceType.Gold:
                        res.expectedGold += d.currentValue * GoldPerPip;
                        break;
                    case DiceType.Ice:
                        res.iceBonusChips += 10 + extraIceChips;
                        res.iceBonusMult += extraIceMult;
                        bonusSteps?.Add(new DiceScoreBonus(d, DiceScoreBonusKind.IceChips, 10 + extraIceChips));
                        if (!Mathf.Approximately(extraIceMult, 0f)) bonusSteps?.Add(new DiceScoreBonus(d, DiceScoreBonusKind.IceMult, extraIceMult));
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
                        case SatelliteType.Mercury:res.satelliteBonusChips += 15;
                            bonusSteps?.Add(new DiceScoreBonus(d, DiceScoreBonusKind.MercuryChips, 15));
                            break;
                        case SatelliteType.Venus: res.expectedGold += 30; break;
                        case SatelliteType.Mars:res.satelliteBonusMult += 1.1f;
                            bonusSteps?.Add(new DiceScoreBonus(d, DiceScoreBonusKind.MarsMult, 1.1f));
                            break;
                        case SatelliteType.Jupiter:
                            res.expectedHeal += Mathf.FloorToInt(2 * healMultiplier);
                            break;
                    }
                }
            }

            // 스탬프 효과
            switch (d.myData.stampType)
            {
                case StampType.Spade:
                    res.stampBonusChips += 20;
                    res.stampDamage += d.currentValue;
                    break;

                case StampType.Heart:
                    res.stampDamage -= 4;
                    break;
                
                case StampType.Diamond:
                    res.stampGold += d.currentValue * 5;
                    break;

                default:
                    break;
            }
        }
        return res;
    }
}