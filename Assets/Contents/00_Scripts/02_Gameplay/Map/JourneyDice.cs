using System.Collections.Generic;
using UnityEngine;

// 여정 주사위 규칙을 한곳에 모은 파일: 기본 면 구성, 만들기·찾기, 굴려서 결과 계산, 면 그림, 피해·회복·골드 적용.
// 지도 진행 상태(굴릴 수 있는 때, 이동 칸 수 대기, 저장)는 MapFlowController가 맡고, 효과 적용만 여기를 부른다.
// 바깥 스크립트(MapPanel, TutorialOverlay 등)는 예전처럼 DiceManager의 같은 이름 함수로 부른다.

// 지도에서 여정 주사위를 굴린 결과가 어떤 종류인지
public enum JourneyOutcome
{
    Move,   // 면 효과 없음: 눈 값만큼 이동
    Damage, // 가시: 체력 감소 후 다시 굴림
    Heal,   // 하트: 체력 회복 후 다시 굴림
    Gold    // 코인: 골드 획득 후 다시 굴림
}

// 굴림 결과 한 건. 굴리기 자체는 부작용이 없고, 적용은 DiceManager.ApplyJourneyResult가 한다.
public struct JourneyRollResult
{
    public int faceIndex;      // 나온 면 번호(0~5)
    public int pips;           // 나온 눈 값
    public SpecialDieEffect effect;
    public JourneyOutcome outcome;
    public int amount;         // 이동 칸 수 / 피해량 / 회복량 / 골드량
    public Sprite faceSprite;  // 결과 슬롯에 보여 줄 면 스프라이트(없으면 null)
}

// 여정 주사위의 기본 면 구성과 생성/검색 도우미. 덱에서 못 찾을 때도 같은 구성으로 굴린다.
public static class JourneyDice
{
    // Dice_Journey 에셋의 itemName과 같아야 저장 복원이 이름으로 찾는다.
    public const string DefaultName = "Journey";

    // 전투 눈금. 이동 면은 1·2·3, 해골·하트·골드는 전부 1. 지도 효과량은 MapPipsFromFaceIndex를 쓴다.
    public static readonly int[] DefaultFaceValues = { 1, 2, 3, 1, 1, 1 };
    public static readonly SpecialDieEffect[] DefaultFaceEffects =
    {
        SpecialDieEffect.None, SpecialDieEffect.None, SpecialDieEffect.None,
        SpecialDieEffect.Thorn, SpecialDieEffect.Heart, SpecialDieEffect.Coin
    };

    // 에셋(DiceItemSO)을 못 찾을 때 쓰는 코드 기본형. 스프라이트가 없어 기본 눈금 이미지로 보인다.
    public static DiceData1 CreateDefault()
    {
        DiceData1 die = new DiceData1(DefaultName, (int[])DefaultFaceValues.Clone());
        die.faceEffects = (SpecialDieEffect[])DefaultFaceEffects.Clone();
        die.isJourneyDice = true;
        return die;
    }

    // 에셋이 있으면 에셋 기준으로, 없으면 코드 기본형으로 새 여정 주사위를 만든다.
    public static DiceData1 Create(DiceItemSO source)
    {
        return source != null ? source.CreateDiceData() : CreateDefault();
    }

    // 세이브 매니저의 아이템 목록에서 여정 주사위 플래그가 켜진 에셋을 찾는다. 등록이 안 된 씬이면 null.
    public static DiceItemSO FindSource()
    {
        GameSaveManager save = GameSaveManager.Instance;
        if (save == null || save.masterItemDatabase == null) return null;
        for (int i = 0; i < save.masterItemDatabase.Count; i++)
        {
            if (save.masterItemDatabase[i] is DiceItemSO dice && dice.isJourneyDice)
                return dice;
        }
        return null;
    }

    public static DiceData1 Find(List<DiceData1> deck)
    {
        if (deck == null) return null;
        for (int i = 0; i < deck.Count; i++)
        {
            if (deck[i] != null && deck[i].isJourneyDice) return deck[i];
        }
        return null;
    }

    // 면 번호 0~5 중 하나를 고른다. 눈 값 배열이 비정상이면 기본 구성으로 대신한다.
    public static int RollFaceIndex(DiceData1 die)
    {
        int count = die != null && die.faceValues != null && die.faceValues.Length > 0 ? die.faceValues.Length : DefaultFaceValues.Length;
        return Random.Range(0, count);
    }

    // 지도 이동·피해·회복·골드량은 면 번호(1~6) 기준. 전투 눈금(faceValues)과 분리한다.
    public static int MapPipsFromFaceIndex(int faceIndex) => faceIndex + 1;

    // ===== 덱에서 찾기·굴리기·면 그림·효과 적용 (MapFlowController에서 옮겨 옴) =====

    // 지도 굴림에 쓰는 덱의 여정 주사위. 못 찾으면 null이며, 굴림은 기본 면 구성으로 대신한다.
    public static DiceData1 FindInDeck(DiceManager dice) => JourneyDice.Find(dice.deckManager.masterDeck);

    // 여정 주사위 면 스프라이트. 해골/하트/골드처럼 눈 값이 같아도 면 번호로 고른다.
    public static Sprite FaceSprite(DiceManager dice, DiceData1 die, int faceIndex)
    {
        if (die != null && die.customFaceSprites != null && die.customFaceSprites.Length > 0)
        {
            int index = die.customFaceSprites.Length == 1 ? 0 : Mathf.Clamp(faceIndex, 0, die.customFaceSprites.Length - 1);
            if (die.customFaceSprites[index] != null) return die.customFaceSprites[index];
        }
        Dice prefabDice = dice.DicePrefabDice; // 굴림 연출이 면을 여러 번 바꾸므로 프리팹 컴포넌트는 한 번만 꺼내 둔 것을 쓴다
        if (prefabDice != null && prefabDice.diceFaceSprites != null && prefabDice.diceFaceSprites.Length > 0)
        {
            int pip = die != null && die.faceValues != null && faceIndex >= 0 && faceIndex < die.faceValues.Length
                ? die.faceValues[faceIndex] : JourneyDice.MapPipsFromFaceIndex(faceIndex);
            return prefabDice.diceFaceSprites[Mathf.Clamp(pip - 1, 0, prefabDice.diceFaceSprites.Length - 1)];
        }
        return null;
    }

    // 굴림 연출 중 빠르게 바꿔 보여 줄 면 스프라이트 하나(무작위 면)
    public static Sprite RandomFaceSprite(DiceManager dice)
    {
        DiceData1 die = FindInDeck(dice) ?? JourneyDice.CreateDefault();
        return FaceSprite(dice, die, JourneyDice.RollFaceIndex(die));
    }

    // 여정 주사위의 면 하나를 굴려 결과를 계산한다. 상태는 바꾸지 않는다.
    // 덱에서 여정 주사위를 못 찾아도 기본 면 구성으로 굴려 멈추지 않는다.
    public static JourneyRollResult Roll(DiceManager dice)
    {
        DiceData1 die = FindInDeck(dice);
        if (die == null || die.faceValues == null || die.faceValues.Length == 0)
            die = JourneyDice.CreateDefault();

        int faceIndex = JourneyDice.RollFaceIndex(die);
        // 지도 효과량은 면 번호(1~6). 전투 눈금(faceValues)과 섞지 않는다.
        int mapPips = JourneyDice.MapPipsFromFaceIndex(faceIndex);
        SpecialDieEffect effect = die.GetFaceEffect(faceIndex);
        MapTableData table = dice.GetMapTable();

        var result = new JourneyRollResult
        {
            faceIndex = faceIndex,
            pips = mapPips,
            effect = effect,
            faceSprite = FaceSprite(dice, die, faceIndex)
        };

        // 지도에서는 면 효과만 쓴다. 코팅·위성 효과는 전투 결산에서만 적용한다.
        switch (effect)
        {
            case SpecialDieEffect.Thorn:
                result.outcome = JourneyOutcome.Damage;
                result.amount = Mathf.Max(0, mapPips);
                break;
            case SpecialDieEffect.Heart:
                result.outcome = JourneyOutcome.Heal;
                float healMult = FigureEffectManager.Instance != null ? FigureEffectManager.Instance.GetHealMultiplier() : 1f;
                result.amount = Mathf.Max(0, TurnCalculator.CalculateHeartHeal(mapPips, healMult));
                break;
            case SpecialDieEffect.Coin:
                result.outcome = JourneyOutcome.Gold;
                int goldPerPip = BalanceConfig.Current.journeyGoldPerPip;
                result.amount = Mathf.Max(0, mapPips * goldPerPip);
                break;
            default:
                result.outcome = JourneyOutcome.Move;
                result.amount = Mathf.Max(1, mapPips);
                break;
        }
        return result;
    }

    // 해골·하트·코인 효과를 실제로 적용하고 적용된 양을 돌려준다(이동이면 0).
    // 이동 칸 수 대기와 저장은 MapFlowController.ApplyJourneyResult가 맡는다.
    public static int ApplyEffect(DiceManager dice, JourneyRollResult result, bool deferGoldUi)
    {
        MapTableData table = dice.GetMapTable();
        switch (result.outcome)
        {
            case JourneyOutcome.Damage:
                {
                    // 지도 피해는 최소 체력 아래로 내려가지 않는다. 이미 그보다 낮으면 더 깎지 않는다.
                    int minHp = table != null ? table.Config.JourneyDamageMinHp : 1;
                    int hpBefore = dice.currentPlayerHP;
                    dice.currentPlayerHP = Mathf.Min(dice.currentPlayerHP, Mathf.Max(minHp, dice.currentPlayerHP - result.amount));
                    int hpLost = Mathf.Max(0, hpBefore - dice.currentPlayerHP);
                    dice.UpdateMainUI("");
                    // 몬스터에게 맞았을 때와 같은 화면 붉어짐·피격음(실제로 깎인 체력이 있을 때만)
                    if (hpLost > 0)
                    {
                        HurtVignetteController.Get(dice).PlayShieldOrHealthHit(dice.ui, 0, hpLost, dice.playerStatus.currentShield);
                        dice.PlayPlayerHurtSound();
                    }
                    return hpLost;
                }
            case JourneyOutcome.Gold:
                // 전투와 같은 경로로 지급한다(피규어 골드 증가 효과 포함). 화면 숫자는 deferGoldUi면 코인 연출이 올린다.
                return dice.shopManager != null ? dice.shopManager.GrantGold(result.amount, deferGoldUi) : 0;
            case JourneyOutcome.Heal:
                {
                    int hpBefore = dice.currentPlayerHP;
                    dice.playerStatus.Heal(result.amount);
                    dice.UpdateMainUI("");
                    return Mathf.Max(0, dice.currentPlayerHP - hpBefore);
                }
        }
        return 0;
    }
}
