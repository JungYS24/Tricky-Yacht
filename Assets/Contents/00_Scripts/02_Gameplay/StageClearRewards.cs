using UnityEngine;

// 몬스터를 잡은 뒤 주는 보상: 처치 골드, 보스 처치 회복, 클리어 피규어 효과, 피규어 획득(포획·자연 드롭).
// 클리어 진행 순서(디버프 정리, 방랑자 체력, 게임 클리어, 조우자·전리품)는 DiceManager.ProcessStageClear가 정하고,
// 보상 단계에서 Give를 한 번 부른다. 기억할 값이 없어서 정적 클래스로 둔다.
public static class StageClearRewards
{
    // 보상 순서가 중요하다: 골드 → 보스 처치 회복 → 클리어 피규어 효과(골드·체력을 바꿀 수 있음) → 피규어 획득(무작위 1회).
    // peppermintSuccess: 페퍼민트 포획 성공 여부(DiceManager가 처치 순간에 정한 값).
    public static void Give(DiceManager dice, bool peppermintSuccess)
    {
        int baseClearReward = GetClearRewardGold(dice);
        baseClearReward = Mathf.FloorToInt(baseClearReward * dice.combatWinGoldMultiplier); // 투탕카멘 2배 적용
        dice.combatWinGoldMultiplier = 1.0f; // 초기화

        if (dice.shopManager != null)
        {
            //스테이지 클리어 기본 골드 카운팅 연출 실행: GrantGold에서 지급과 UI 갱신을 함께 처리
            dice.shopManager.GrantGold(baseClearReward);
        }

        // 바이옴 보스 처치 회복. 튜토리얼 모드는 예전처럼 회복하지 않는다.
        int clearHealPercent = BalanceConfig.Current.biomeClearHealPercent;
        if (!TutorialMode.Active && dice.enemy.Grade == MonsterGrade.Boss && clearHealPercent > 0)
        {
            dice.playerStatus.Heal(Mathf.FloorToInt(dice.playerMaxHP * (clearHealPercent / 100f)));
            dice.UpdateMainUI("");
        }

        //스테이지 클리어 시 패시브(Passive) 피규어 효과 일괄 발동
        if (FigureEffectManager.Instance != null)
        {
            FigureEffectManager.Instance.EvaluateStageClearTriggers(dice, dice.shopManager);
        }

        if (peppermintSuccess)
        {
            if (dice.enemy.dropFigureData != null)
            {
                InventoryManager.Instance.AddItem(dice.enemy.dropFigureData);
            }
        }
        else if (RollNaturalFigureDrop(dice))
        {
            InventoryManager.Instance.AddItem(dice.enemy.dropFigureData);
            ToastPopupController.Instance?.ShowToast(LocalizationManager.GetSys("SYS_FIGURE_DROPPED", "피규어 획득: {0}", LocalizationManager.GetItemDisplayName(dice.enemy.dropFigureData)));
        }
    }

    // 처치 골드: 몬스터 dropGold x 배율. 보스는 바이옴 클리어 보상을 더하고, 엘리트·보스는 등급 배율을 한 번 더 곱한다.
    // 튜토리얼 모드는 예전 고정값을 유지한다.
    static int GetClearRewardGold(DiceManager dice)
    {
        if (TutorialMode.Active) return 500;
        BalanceConfig cfg = BalanceConfig.Current;
        int gold = Mathf.FloorToInt(dice.enemy.DropGold * cfg.monsterGoldMultiplier);
        if (dice.enemy.Grade == MonsterGrade.Boss) gold += cfg.biomeClearBonusGold;
        return Mathf.FloorToInt(gold * cfg.GoldMultiplierFor(dice.enemy.Grade) * cfg.TierGold(dice.CurrentBiomeTier));
    }

    // 페퍼민트 없이 처치했을 때 등급별 확률로 드롭 피규어를 준다. 이미 가진 피규어는 주지 않는다.
    static bool RollNaturalFigureDrop(DiceManager dice)
    {
        if (TutorialMode.Active || dice.enemy == null || dice.enemy.dropFigureData == null) return false;
        InventoryManager inventory = InventoryManager.Instance;
        if (inventory == null || inventory.ownedFigures.Contains(dice.enemy.dropFigureData)) return false;
        float chance = BalanceConfig.Current.DropRateFor(dice.enemy.Grade) + dice.snackBonusFigureDropRate;
        return UnityEngine.Random.value < chance;
    }
}
