using UnityEngine;
using System.Collections.Generic;

// 결산 결과를 한 번에 담아 전달할 구조체
public struct TurnCalcResult
{
    public int baseSum;
    public int expectedGold;
    public int expectedHeal;
    public int flameDamageThisTurn;
    public int selfDamage; // 가시 면 등으로 플레이어가 받는 피해
    public float curseMultiplier;

    public int iceBonusChips;
    public float prismMultTotal;

    public int satelliteBonusChips;
    public float satelliteBonusMult;
    public float iceBonusMult;

    public int stampBonusChips; // 스페이드: 족보에 쓰인 주사위의 칩
    public int stampDamage;     // 스페이드: 족보에 안 쓰인 주사위의 눈만큼, 배수 없이 최종 피해에 더함
    public int stampGold;       // 다이아: expectedGold와 따로 모아 결산에서 더함
    public int stampShield;     // 하트: 결산 때 얻는 보호막

    public int oddBonusChips;     // 홀수 주사위: 결산 포함 1개당 칩
    public float evenBonusMult;   // 짝수 주사위: 결산 포함 1개당 배수
}

public enum DiceScoreBonusKind
{
    IceChips, MercuryChips, PrismMult, IceMult, MarsMult, SpadeChips, OddChips, EvenMult
}

public struct DiceScoreBonus
{
    public Dice source;
    public DiceScoreBonusKind kind;
    public float amount;
    public bool IsChips => kind == DiceScoreBonusKind.IceChips || kind == DiceScoreBonusKind.MercuryChips || kind == DiceScoreBonusKind.SpadeChips || kind == DiceScoreBonusKind.OddChips;

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

    // 스탬프 수치
    public const int SpadeChips = 20;
    public const int HeartShield = 4;
    public const int DiamondGoldPerPip = 5;
    public const int CloverKeepChancePercent = 30;

    public const int OddChipsPerDie = 5;
    public const float EvenMultPerDie = 0.2f;

    // 하트 면 회복량. 전투 결산과 지도 굴림이 같은 계산을 쓴다.
    public static int CalculateHeartHeal(int pips, float healMultiplier)
    {
        return Mathf.FloorToInt(pips * healMultiplier);
    }

    // 결산 때 코인이 날아가는 골드: 코인 면(눈×10) + 다이아 스탬프(눈×5). 코팅·위성 골드는 코인 없이 바로 오른다.
    public static int GetCoinFlyGold(Dice d)
    {
        if (d == null || d.myData == null || d.currentValue <= 0) return 0;
        int gold = 0;
        if (d.myData.GetFaceEffect(d.ResolveFaceIndex()) == SpecialDieEffect.Coin) gold += d.currentValue * GoldPerPip;
        if (d.myData.stampType == StampType.Diamond) gold += d.currentValue * DiamondGoldPerPip;
        return gold;
    }

    public static TurnScoreResult CalculateScore(TurnCalcResult dice, float handMult, TurnScoreBonuses bonuses)
    {
        // 최종 칩 = 주사위 기본합 + 얼음/위성/스페이드/홀수 + 피규어/스테이지 보너스 + 스낵 보너스
        int chips = dice.baseSum + dice.iceBonusChips + dice.satelliteBonusChips + dice.stampBonusChips + dice.oddBonusChips + bonuses.stageChips + bonuses.snackChips;
        // 최종 배수 = 족보 배수 + 피규어/스테이지 배수 + 스낵 배수 + 프리즘/위성/짝수 배수
        float multiplier = handMult + bonuses.stageMult + bonuses.snackMult + dice.prismMultTotal + dice.satelliteBonusMult + dice.iceBonusMult + dice.evenBonusMult + bonuses.permanentMult;
        multiplier *= dice.curseMultiplier;
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

        // 각 눈금을 한 번씩 집계합니다. 개수 세기는 강조 그룹 판정과 같은 함수를 씁니다.
        int pairs = 0;
        bool triple = false, four = false, yacht = false, hasZero = false;
        int uniqueCount = 0, minimum = int.MaxValue, maximum = int.MinValue;
        for (int i = 0; i < values.Count; i++)
        {
            int value = values[i];
            // 앞에서 처리한 눈금이면 중복 집계를 건너뜁니다.
            if (HandRankUtil.CountValues(values, value, i) > 0) continue;

            uniqueCount++;
            if (value == 0) hasZero = true;
            if (value < minimum) minimum = value;
            if (value > maximum) maximum = value;
            int count = HandRankUtil.CountValues(values, value, values.Count);
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

    // 가시 디버프 피해: 킵한 가시 주사위의 눈 x 2. 결산을 시작하자마자 회복보다 먼저 적용하며, 처치 시 피해 면제가 없다.
    public static int CalculateSpineDamage(List<Dice> targetDice)
    {
        int damage = 0;
        foreach (var d in targetDice)
        {
            if (d != null && d.myData != null && d.myData.debuffType == DebuffDiceType.Spine)
                damage += Mathf.Max(0, d.currentValue) * 2;
        }
        return damage;
    }

    // 결산 및 UI 갱신 시 주사위 개별 효과들을 한 번에 합산해주는 순수 연산 함수
    // scoredHand: 결산에서 이미 판정한 족보. 없으면 스페이드 스탬프가 있을 때만 직접 판정한다.
    public static TurnCalcResult CalculateDiceEffects(List<Dice> targetDice, int currentEnemyHP, float healMultiplier, List<DiceScoreBonus> bonusSteps = null, HandRank? scoredHand = null)
    {
        bonusSteps?.Clear();
        TurnCalcResult res = new TurnCalcResult { curseMultiplier = 1f };
        int extraIceChips = FigureEffectManager.Instance != null ? FigureEffectManager.Instance.GetIceChipsBonus() : 0;
        float extraIceMult = FigureEffectManager.Instance != null ? FigureEffectManager.Instance.GetIceMultiplierBonus() : 0f;
        List<int> handValues = null;
        HandRank hand = HandRank.HighCard;

        foreach (var d in targetDice)
        {
            res.baseSum += d.currentValue;
            if (d.myData.debuffType == DebuffDiceType.Curse)
                res.curseMultiplier *= Mathf.Clamp01(1f - d.currentValue * 0.05f);

            // 주사위 전체 효과 대신 이번에 나온 면의 효과로 분기한다(면별 효과가 없으면 기존 효과와 같다).
            switch (d.myData.GetFaceEffect(d.ResolveFaceIndex()))
            {
                case SpecialDieEffect.Coin:
                    res.expectedGold += d.currentValue * GoldPerPip;
                    break;
                case SpecialDieEffect.Heart:
                    res.expectedHeal += CalculateHeartHeal(d.currentValue, healMultiplier);
                    break;
                case SpecialDieEffect.Flame:
                    res.flameDamageThisTurn += (d.currentValue * 2);
                    break;
                case SpecialDieEffect.Thorn:
                    res.selfDamage += d.currentValue;
                    break;
                case SpecialDieEffect.Odd:
                    if (d.currentValue <= 0) break;
                    res.oddBonusChips += OddChipsPerDie;
                    bonusSteps?.Add(new DiceScoreBonus(d, DiceScoreBonusKind.OddChips, OddChipsPerDie));
                    break;
                case SpecialDieEffect.Even:
                    if (d.currentValue <= 0) break;
                    res.evenBonusMult += EvenMultPerDie;
                    bonusSteps?.Add(new DiceScoreBonus(d, DiceScoreBonusKind.EvenMult, EvenMultPerDie));
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

            // 스탬프 효과 (주사위마다 하나). 클로버는 리롤할 때 DiceManager에서 처리한다.
            switch (d.myData.stampType)
            {
                case StampType.Spade:
                    // 족보에 쓰였으면 칩 +20, 아니면(하이카드 포함) 눈만큼 추가 피해
                    if (handValues == null)
                    {
                        handValues = new List<int>(targetDice.Count);
                        foreach (var die in targetDice) handValues.Add(die.currentValue);
                        hand = scoredHand ?? CalculateHand(handValues, DiceManager.Instance, out _);
                    }
                    if (HandRankUtil.IsUsedInHand(hand, handValues, targetDice.IndexOf(d)))
                    {
                        res.stampBonusChips += SpadeChips;
                        bonusSteps?.Add(new DiceScoreBonus(d, DiceScoreBonusKind.SpadeChips, SpadeChips));
                    }
                    else
                    {
                        res.stampDamage += Mathf.Max(0, d.currentValue);
                    }
                    break;

                case StampType.Heart:
                    res.stampShield += HeartShield;
                    break;

                case StampType.Diamond:
                    res.stampGold += Mathf.Max(0, d.currentValue) * DiamondGoldPerPip;
                    break;
            }
        }
        return res;
    }
}
