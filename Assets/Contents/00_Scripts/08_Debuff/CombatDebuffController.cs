using System.Collections.Generic;
using UnityEngine;

// 전투 디버프 주사위: 노드별로 넣기, 전투가 끝나면 빼기, 외형 찾기, 뽑자마자 발동하는 효과(도둑·족쇄)와 그 연출 대기열.
// DiceManager가 하나 만들어 들고 있습니다. 바깥 스크립트는 예전처럼 DiceManager의 같은 이름으로 부릅니다.
// 시간차 연출(코루틴)은 컴포넌트만 돌릴 수 있어서 DiceManager에 남아 있고, 여기 함수를 부릅니다.
public class CombatDebuffController
{
    readonly DiceManager dice;

    public CombatDebuffController(DiceManager dice)
    {
        this.dice = dice;
    }

    // 옮겨 온 코드가 그대로 읽히도록 자주 쓰는 DiceManager 값을 같은 이름으로 둡니다.
    DeckManager deckManager => dice.deckManager;
    ShopManager shopManager => dice.shopManager;
    DiceBoardController board => dice.Board;

    internal readonly List<DiceData1> currentCombatDebuffs = new List<DiceData1>();

    internal void AddNodeDebuffs()
    {
        MapNode node = dice.stageProgression.FindNode(dice.stageProgression.currentNodeId);
        if (node == null) return;
        switch (node.type)
        {
            // 엘리트(도둑·족쇄, 이번 전투만)와 차원문(가시·저주, 영구)은 바이옴 단계별 개수만큼 디버프 주사위를 넣는다.
            case MapNodeType.Elite:
            {
                int count = BalanceConfig.Current.RollDebuffCount(dice.CurrentBiomeTier);
                for (int i = 0; i < count; i++)
                    AddCombatDebuff(UnityEngine.Random.Range(0, 2) == 0 ? DebuffDiceType.Thief : DebuffDiceType.Shackle);
                break;
            }
            case MapNodeType.Portal:
            {
                BalanceConfig cfg = BalanceConfig.Current;
                int count = cfg.RollDebuffCount(dice.CurrentBiomeTier);
                for (int i = 0; i < count; i++)
                    AddCombatDebuff(UnityEngine.Random.Range(0, 2) == 0 ? DebuffDiceType.Spine : DebuffDiceType.Curse, !cfg.portalDebuffPermanent);
                break;
            }
            case MapNodeType.Boss:
                AddCombatDebuff(DebuffDiceType.Fake);
                break;
        }
    }

    private void AddCombatDebuff(DebuffDiceType type, bool fightOnly = false)
    {
        DiceItemSO source = FindDebuffSource(type);
        DiceData1 die = deckManager.AddDebuffDice(type, source);
        if (source == null) ApplyDebuffAppearance(die);
        currentCombatDebuffs.Add(die);
        if (fightOnly) fightOnlyDebuffs.Add(die);
    }

    // 이번 전투에만 넣은 영구형 디버프(차원문의 가시·저주). 전투가 끝나면 덱에서 뺀다. 세이브에는 남지 않는다.
    readonly List<DiceData1> fightOnlyDebuffs = new List<DiceData1>();

    internal void RemoveFightOnlyDebuffs()
    {
        if (fightOnlyDebuffs.Count == 0) return;
        System.Predicate<DiceData1> match = die => die != null && fightOnlyDebuffs.Contains(die);
        board?.RemoveDiceMatching(match);
        deckManager.masterDeck.RemoveAll(match);
        deckManager.drawPile.RemoveAll(match);
        deckManager.discardPile.RemoveAll(match);
        currentCombatDebuffs.RemoveAll(match);
        fightOnlyDebuffs.Clear();
    }

    // 이번 전투에만 넣은 영구형 디버프인지(스테이지 클리어 때 사라지는 연출 대상 고르기용)
    internal bool IsFightOnly(DiceData1 die) => fightOnlyDebuffs.Contains(die);

    internal DiceItemSO FindDebuffSource(DebuffDiceType type)
    {
        var database = GameSaveManager.Instance != null ? GameSaveManager.Instance.masterItemDatabase : null;
        return database != null ? database.Find(item => item is DiceItemSO diceItem && diceItem.debuffType == type) as DiceItemSO : null;
    }

    // 기존 아이템 DB의 눈별 에셋에서 외형을 모은다. 디버프 종류와 눈 값은 덱 생성 규칙을 유지한다.
    internal void ApplyDebuffAppearance(DiceData1 die)
    {
        DiceItemSO sourceAsset = FindDebuffSource(die.debuffType);
        if (sourceAsset != null)
        {
            die.customDiceShell = sourceAsset.customDiceShell;
            die.customFaceSprites = sourceAsset.customFaceSprites != null ? (Sprite[])sourceAsset.customFaceSprites.Clone() : null;
            return;
        }
        if (die.debuffType == DebuffDiceType.Fake)
        {
            die.customDiceShell = dice.fakeDiceShell;
            die.customFaceSprites = new Sprite[] { dice.fakeDiceFace, dice.fakeDiceFace, dice.fakeDiceFace, dice.fakeDiceFace, dice.fakeDiceFace, dice.fakeDiceFace };
            return;
        }
        string assetName;
        switch (die.debuffType)
        {
            case DebuffDiceType.Thief: assetName = "Thief Dice"; break;
            case DebuffDiceType.Shackle: assetName = "Captive Dice"; break;
            case DebuffDiceType.Spine: assetName = "Spine_Dice_"; break;
            case DebuffDiceType.Curse: assetName = "Cursed Dice_"; break;
            default: return;
        }
        bool numbered = die.debuffType == DebuffDiceType.Spine || die.debuffType == DebuffDiceType.Curse;
        var sprites = new Sprite[6];
        for (int i = 0; i < 6; i++)
        {
            string sourceName = assetName + (numbered ? (i + 1).ToString() : "");
            DiceItemSO source = GameSaveManager.Instance != null
                ? GameSaveManager.Instance.masterItemDatabase.Find(item => item is DiceItemSO && item.name == sourceName) as DiceItemSO
                : null;
            if (source == null) continue;
            if (i == 0) die.customDiceShell = source.customDiceShell;
            sprites[i] = source.customFaceSprites != null && source.customFaceSprites.Length > 0
                ? source.customFaceSprites[0] : null;
        }
        die.customFaceSprites = sprites;
    }

    public void RemoveCombatDebuffs(bool temporaryOnly = false, bool currentEnemyOnly = false)
    {
        System.Predicate<DiceData1> match = die => die != null && die.IsDebuff
            && (!temporaryOnly || die.IsTemporaryDebuff)
            && (!currentEnemyOnly || currentCombatDebuffs.Contains(die));
        board?.RemoveDiceMatching(match);
        if (currentEnemyOnly)
        {
            deckManager.masterDeck.RemoveAll(match);
            deckManager.drawPile.RemoveAll(match);
            deckManager.discardPile.RemoveAll(match);
        }
        else if (temporaryOnly) deckManager.RemoveTemporaryDebuffDice();
        else deckManager.RemoveAllDebuffDice();
        currentCombatDebuffs.RemoveAll(match);
    }

    // 뽑는 즉시 효과를 적용하고, 화면 연출은 주사위가 보드에 놓인 뒤 FlushDrawTriggers에서 보여 준다.
    private readonly List<DebuffFeedback.DrawTrigger> pendingDrawTriggers = new List<DebuffFeedback.DrawTrigger>();

    internal void ApplyDrawDebuff(DiceData1 die)
    {
        if (die == null || !die.TriggersDebuffOnDraw) return;
        if (die.debuffType == DebuffDiceType.Thief && shopManager != null)
        {
            int stolen = Mathf.Min(100, Mathf.Max(0, shopManager.currentGold));
            shopManager.currentGold -= stolen;
            // 화면 골드 숫자는 코인이 빠져나가는 연출에서 내린다.
            pendingDrawTriggers.Add(new DebuffFeedback.DrawTrigger { data = die, stolenGold = stolen });
        }
        else if (die.debuffType == DebuffDiceType.Shackle)
        {
            dice.turnContext.debuffRerollPenalty++;
            pendingDrawTriggers.Add(new DebuffFeedback.DrawTrigger { data = die });
        }
    }

    internal void FlushDrawTriggers()
    {
        if (pendingDrawTriggers.Count == 0) return;
        var triggers = new List<DebuffFeedback.DrawTrigger>(pendingDrawTriggers);
        pendingDrawTriggers.Clear();
        DebuffFeedback.PlayDrawTriggers(triggers, dice.activeDiceList, dice.ui, shopManager);
    }

    // 연출을 보여 주지 못하고 넘어가는 경우(새 전투·재시작) 화면 골드만 실제 값에 맞춘다.
    internal void DiscardDrawTriggers()
    {
        if (pendingDrawTriggers.Count == 0) return;
        pendingDrawTriggers.Clear();
        shopManager?.SyncGoldDisplay();
    }

    // 재시작: 대기 중인 연출만 버린다(골드는 재시작이 초기값으로 다시 맞춘다).
    internal void DropDrawTriggers() => pendingDrawTriggers.Clear();
}
