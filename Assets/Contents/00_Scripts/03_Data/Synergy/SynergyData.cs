using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SynergySO", menuName = "Synergy/SynergyObject")]
public class SynergyData : BaseItemDataSO
{
    [SerializeField] private FigureItemSO[] requiredFigures;

    [SerializeField] private SynergyEffectType effectType;

    [SerializeField] private float effectValue;

    public string SynergyName
    {
        get => itemName;
        set => itemName = value;
    }

    public string SynergyDescription
    {
        get => description;
        set => description = value;
    }

    public IReadOnlyList<FigureItemSO> RequiredFigures => requiredFigures;

    public SynergyEffectType EffectType => effectType;

    public float EffectValue => effectValue;

    public override void ApplyItemEffect(DiceManager diceManager)
    {
        switch (effectType)
        {
            case SynergyEffectType.YachtMultiplierUp:
                diceManager.multYacht += effectValue;
                break;

            case SynergyEffectType.MaxHpUp:
                diceManager.playerMaxHP += (int)effectValue;
                break;

            case SynergyEffectType.ShopRerollCostDown:
                diceManager.shopManager.rerollCost += (int)effectValue;
                break;

            case SynergyEffectType.FourKindMultiplierUp:
                diceManager.multFourOfAKind += effectValue;
                break;

            case SynergyEffectType.StartWithSnackPeppermint:
                // 스낵
                break;

            case SynergyEffectType.BurnDamageReductionUp:
                // 화염대미지
                break;

            case SynergyEffectType.VictoryGoldChanceUp:
                diceManager.combatWinGoldMultiplier += effectValue;
                break;

            case SynergyEffectType.BurnPowerUp:
                // 화상 부여량
                break;

            case SynergyEffectType.StartGoldUp:
                diceManager.shopManager.currentGold += (int)effectValue;
                break;

            case SynergyEffectType.IncomingDamageDown:
                // 받는 대미지 감소
                break;

            case SynergyEffectType.OnePairMultiplierUp:
                diceManager.multOnePair += effectValue;
                break;

            case SynergyEffectType.StraightMultiplierUp:
                diceManager.multStraight += effectValue;
                break;

            case SynergyEffectType.FullHouseMultiplierUp:
                diceManager.multFullHouse += effectValue;
                break;

            case SynergyEffectType.StartWithDarkCoating:
                // 다크코팅
                break;

            case SynergyEffectType.StartWithSnackGarnish:
                // 가니시
                break;

            case SynergyEffectType.BaseChipsUp:
                // 칩
                break;

            case SynergyEffectType.ShopPriceDiscountUp:
                // 가격 낮추기
                break;

            case SynergyEffectType.StartWithSnackSteak:
                // 스테이크
                break;

            case SynergyEffectType.StartWithSnackCherry:
                // 체리
                break;

            default:
                break;
        }
    }
}