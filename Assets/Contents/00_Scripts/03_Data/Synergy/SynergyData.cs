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
                InventoryManager.Instance.AddItem(GameSaveManager.Instance.FindItemByName("페퍼민트"));
                break;

            case SynergyEffectType.BurnDamageReductionUp:
                ///
                /// 
                /// 
                /// 
                break;

            case SynergyEffectType.VictoryGoldChanceUp:
                diceManager.combatWinGoldMultiplier += effectValue;
                break;

            case SynergyEffectType.BurnPowerUp:
                diceManager.accumulatedFlameDamage += (int)effectValue;
                break;

            case SynergyEffectType.StartGoldUp:
                diceManager.shopManager.currentGold += (int)effectValue;
                break;

            case SynergyEffectType.IncomingDamageDown:
                diceManager.playerStatus.currentShield += (int)effectValue;
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
                var dice = diceManager.masterDeck[Random.Range(0, diceManager.masterDeck.Count)];
                dice.isCoated = true;
                dice.type = DiceType.Dark;
                dice.diceColor = new Color(0x2B, 0x2A, 0x1A);
                break;

            case SynergyEffectType.StartWithSnackGarnish:
                InventoryManager.Instance.AddItem(GameSaveManager.Instance.FindItemByName("가니쉬"));
                break;

            case SynergyEffectType.BaseChipsUp:
                diceManager.snackBonusChips += (int)effectValue;
                break;

            case SynergyEffectType.ShopPriceDiscountUp:
                foreach (var slot in diceManager.shopManager.shopSlots)
                {
                    slot.priceMultiplier *= effectValue / 100;
                }
                break;

            case SynergyEffectType.StartWithSnackSteak:
                InventoryManager.Instance.AddItem(GameSaveManager.Instance.FindItemByName("스테이크"));
                break;

            case SynergyEffectType.StartWithSnackCherry:
                InventoryManager.Instance.AddItem(GameSaveManager.Instance.FindItemByName("체리"));
                break;

            default:
                break;
        }
    }
}