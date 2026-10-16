using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public static class CombatFlowController
{
    // 메인 턴 결산 흐름 
    // coinDice: 면 효과가 Coin이라 골드를 준 주사위들(코인 날아가기 연출용). 없으면 골드 숫자를 바로 갱신한다.
    // 가시 디버프 피해는 여기가 아니라 결산 시작 때 ProcessDebuffThorn에서 먼저 적용한다.
    public static IEnumerator ProcessTurnResolution(DiceManager dm, int finalDamage, int darkDamage, int finalHeal, int expectedGold, int flameDamage, string handName, int selfDamage = 0, List<Dice> coinDice = null)
    {
        //플레이어 회복
        if (finalHeal > 0)
        {
            int hpBefore = dm.currentPlayerHP;
            dm.playerStatus.Heal(finalHeal);
            if (dm.currentPlayerHP > hpBefore && FigureEffectManager.Instance != null && FigureEffectManager.Instance.GetHealMultiplier() > 1f) FigureEffectManager.Instance.NotifyPassiveApplied(FigureEffectType.IncreaseHealMultiplier);
            dm.ui?.UpdateShieldUI(dm.playerStatus.currentShield);
        }

        //골드 획득 처리
        if (expectedGold > 0 && dm.shopManager != null)
        {
            GrantResolutionGold(dm, expectedGold, coinDice);
        }

        //화염 스택 누적
        if (flameDamage > 0)
        {
            dm.accumulatedFlameDamage += flameDamage;
            dm.ui?.UpdateFlameStackUI(dm.accumulatedFlameDamage);
        }

        // 반복 공격 기능은 사용하지 않음
        dm.extraAttackCount = 0;

        // 피규어 효과로 이미 처치된 경우에는 공격하지 않음
        if (dm.enemy.IsDead || dm.enemy.CurrentHP <= 0)
        {
            // 적을 처치한 판이므로 가시 피해는 들어가되 체력은 1 아래로 내려가지 않는다.
            ApplyThornDamage(dm, selfDamage, true);
            dm.OnEnemyKilled();
            yield break;
        }

        // 다크 피해까지 합쳐 일반 공격 한 번으로 처리
        int combinedDamage = finalDamage + darkDamage;

        yield return dm.StartCoroutine(
            ProcessPlayerAttack(dm, combinedDamage, darkDamage > 0, finalDamage > 0));

        // 반사 피해 등으로 플레이어가 사망한 경우
        if (dm.currentPlayerHP <= 0)
        {
            yield return dm.StartCoroutine(HandlePlayerDeath(dm));
            yield break;
        }

        // 가시 면 피해: 플레이어 공격이 끝난 뒤에 적용한다.
        // 이번 공격으로 적이 죽었다면 체력은 1 아래로 내려가지 않아 적을 잡은 판에 가시로 죽지 않는다.
        bool enemyDefeatedByAttack = dm.enemy.IsDead || dm.enemy.CurrentHP <= 0;
        if (ApplyThornDamage(dm, selfDamage, enemyDefeatedByAttack))
        {
            // 적이 살아 있는데 체력이 0이 되면 기존 사망 처리(부활 피규어 포함)를 타고 끝낸다. 적 반격은 하지 않는다.
            yield return dm.StartCoroutine(HandlePlayerDeath(dm));
            yield break;
        }

        // 적 사망 여부 확정 판정 (포획 이벤트 포함)
        if (dm.enemy.IsDead || dm.enemy.CurrentHP <= 0)
        {
            dm.OnEnemyKilled();
            yield break;
        }

        //적 반격 루틴 (이때 누적된 화상 데미지 데이터를 넘겨줌)
        yield return dm.StartCoroutine(ProcessEnemyTurnRoutine(dm, handName, dm.accumulatedFlameDamage));
    }


    // 결산 골드 지급. 실제 골드는 한 번에 바로 올리고(지급액·세이브·상점 판단은 즉시 맞음),
    // 코인 면 주사위가 있으면 화면 골드 숫자만 코인이 도착할 때 올라가게 미룬다. 연출은 기다리지 않는다.
    // 골드 코팅·금성 위성 등 다른 경로의 골드는 지금처럼 바로 카운터에 반영한다.
    private static void GrantResolutionGold(DiceManager dm, int expectedGold, List<Dice> coinDice)
    {
        ShopManager shop = dm.shopManager;

        int coinBase = 0;
        if (coinDice != null)
        {
            for (int i = 0; i < coinDice.Count; i++)
            {
                if (coinDice[i] != null) coinBase += Mathf.Max(0, coinDice[i].currentValue) * TurnCalculator.GoldPerPip;
            }
        }

        if (coinBase <= 0 || !CoinFlyFeedback.CanPlay(shop))
        {
            shop.GrantGold(expectedGold);
            return;
        }

        int granted = shop.GrantGold(expectedGold, true);

        // 피규어 배율이 섞인 실제 지급액을 코인 몫으로 나눈다(합계는 granted를 넘지 않는다).
        float coinRatio = Mathf.Clamp01((float)coinBase / Mathf.Max(1, expectedGold));
        int remainingShare = Mathf.Min(granted, Mathf.RoundToInt(granted * coinRatio));

        int playedCount = 0;
        int lastIndex = -1;
        for (int i = 0; i < coinDice.Count; i++)
        {
            if (coinDice[i] != null && coinDice[i].currentValue > 0) lastIndex = i;
        }

        for (int i = 0; i < coinDice.Count; i++)
        {
            Dice die = coinDice[i];
            if (die == null || die.currentValue <= 0) continue;

            int dieBase = die.currentValue * TurnCalculator.GoldPerPip;
            int share = i == lastIndex
                ? remainingShare
                : Mathf.Min(remainingShare, Mathf.RoundToInt(granted * coinRatio * dieBase / coinBase));
            remainingShare -= share;

            // 주사위마다 0.1초씩 늦게 출발한다. 코인 수는 눈 값(최대 8개).
            CoinFlyFeedback.Play(die.transform.position, Mathf.Min(die.currentValue, 8), share, playedCount * 0.1f);
            playedCount++;
        }

        // 코인으로 오르는 몫을 뺀 나머지(코팅·위성 골드)는 바로 카운터에 반영한다.
        CoinFlyFeedback.RefreshCounter(shop);
    }

    // 가시 디버프: 킵한 주사위 눈이 뜬 직후, 회복·점수 계산보다 먼저 체력에 적용한다. 여정 가시의 처치 시 생존 보정과 분리한다.
    // 파편이 체력 표시에 닿는 순간 피해가 들어가고, 체력이 0이 되면 사망 처리(부활 피규어 포함)까지 마친 뒤 onPlayerDown을 부른다.
    public static IEnumerator ProcessDebuffThorn(DiceManager dm, List<Dice> spineDice, int damage, System.Action onPlayerDown)
    {
        if (damage <= 0) yield break;
        Transform heart = dm.ui != null && dm.ui.heartText != null ? dm.ui.heartText.transform : null;
        yield return DebuffFeedback.PlayThornBurst(spineDice, heart);
        if (ApplyThornDamage(dm, damage, false))
        {
            onPlayerDown?.Invoke();
            yield return dm.StartCoroutine(HandlePlayerDeath(dm));
            yield break;
        }
        // 체력 감소를 읽을 시간을 두고 족보 연출로 넘어갑니다.
        yield return new WaitForSeconds(DebuffFeedback.Scaled(0.3f));
    }

    // 가시 피해를 플레이어 체력에 직접 적용한다(보호막은 거치지 않음). 피격 연출·피격음·저체력 피규어 검사까지 함께 처리한다.
    // enemyDefeated가 true면 체력은 1 아래로 내려가지 않는다. 체력이 0이 되어 사망 처리가 필요하면 true를 돌려준다.
    private static bool ApplyThornDamage(DiceManager dm, int selfDamage, bool enemyDefeated)
    {
        if (selfDamage <= 0) return false;

        int hpBefore = dm.currentPlayerHP;
        int newHp = Mathf.Max(0, hpBefore - selfDamage);
        if (enemyDefeated) newHp = Mathf.Min(hpBefore, Mathf.Max(1, newHp));
        dm.currentPlayerHP = newHp;
        // 피격 연출은 체력 숫자를 고치지 않으므로, 피해가 들어가는 순간 체력 표시도 바로 갱신한다.
        dm.ui?.UpdateShieldUI(dm.playerStatus.currentShield);

        int hpDamage = Mathf.Max(0, hpBefore - newHp);
        HurtVignetteController.Get(dm).PlayShieldOrHealthHit(dm.ui, 0, hpDamage, dm.playerStatus.currentShield);
        if (hpDamage > 0) dm.PlayPlayerHurtSound();

        if (newHp <= 0) return true;
        FigureEffectManager.Instance?.EvaluateLowHPTriggers(dm, dm.shopManager);
        return false;
    }

    //플레이어 공격 연출
    private static IEnumerator ProcessPlayerAttack(
    DiceManager dm,
    int damage, bool includesDark, bool hasNormalDamage)
    {
        if (dm.enemy == null ||
            dm.enemy.IsDead ||
            dm.enemy.CurrentHP <= 0)
        {
            yield break;
        }

        bool isFirstNormalAttack =
            !dm.stageContext.firstNormalAttackDone;

        // 첫 공격이 0 피해여도 이후 공격을 첫 공격으로 취급하지 않음
        dm.stageContext.firstNormalAttackDone = true;

        if (damage <= 0)
            yield break;

        dm.enemy.TakeDamage(
            damage,
            dm.OnEnemyKilled,
            isFirstNormalAttack,
            hitKind: hasNormalDamage ? EnemyHitKind.Normal : EnemyHitKind.Dark,
            includesDark: includesDark);
        if (dm.ui != null && dm.ui.handInfoText != null)
            dm.ui.handInfoText.GetComponent<FinalDamageFeedback>()?.PlayAttackPulse();

        yield return new WaitForSeconds(0.15f);
    }

    private static IEnumerator HandlePlayerDeath(DiceManager dm)
    {
        bool isRevived = FigureEffectManager.Instance != null && FigureEffectManager.Instance.EvaluateDeathTriggers(dm, dm.shopManager);

        if (isRevived)
        {
            Debug.Log("<color=cyan>부활 성공! 턴을 강제 종료하고 다음 라운드로 넘어갑니다.</color>");
            dm.InvokeStartNewRound(0.5f);
        }
        else
        {
            if (GameSaveManager.Instance != null) GameSaveManager.Instance.DeleteSave();
            string gameOverText = LocalizationManager.Instance != null ? LocalizationManager.Instance.GetLocalizedString(LocalizationManager.UiTable, "UI_GAME_OVER") : "게임 오버";
            dm.ui?.ShowResult("#FF0000", gameOverText);
            dm.StartCoroutine(dm.ShowGameOverPanelDelayed());
        }
        yield break;
    }

    public static IEnumerator ProcessEnemyTurnRoutine(DiceManager dm, string handName, int flameDamage)
    {
        yield return new WaitForSeconds(0.3f);
        dm.UpdateMainUI(handName);

        if (!dm.enemy.IsDead)
        {
            // 일반 공격 후 화염 데미지가 0.3초후에 터짐
            yield return new WaitForSeconds(0.3f);

            if (flameDamage > 0)
            {
                yield return dm.enemy.PlayFlameDamage(flameDamage, dm.OnEnemyKilled);
                dm.UpdateMainUI(LocalizationManager.GetUi(
                    "UI_FLAME_DAMAGE",
                    "화염 데미지! <color=#FF4500>-{0}</color>",
                    flameDamage));

                if (dm.enemy.IsDead) yield break;
                yield return new WaitForSeconds(0.8f);
            }
            else
            {
                yield return new WaitForSeconds(0.55f);
            }

            dm.enemy.DecreaseTurn();

            if (dm.enemy.CurrentAttackTurn <= 0)
            {
                dm.enemy.PlayAttackAnim();
                yield return new WaitForSeconds(dm.enemy.AttackContactDelay);

                int finalEnemyAtk = dm.isNextEnemyAttackFixedToOne ? 1 : dm.enemy.AttackPower;
                dm.isNextEnemyAttackFixedToOne = false;

                int reduction = FigureEffectManager.Instance != null ? FigureEffectManager.Instance.GetTotalDamageReduction(finalEnemyAtk, dm, true) : 0;
                finalEnemyAtk -= reduction;
                if (finalEnemyAtk < 0) finalEnemyAtk = 0;

                int hpBeforeAttack = dm.currentPlayerHP;
                int shieldBeforeAttack = dm.playerStatus.currentShield;
                dm.playerStatus.TakeDamage(finalEnemyAtk);
                int actualHPDamage = Mathf.Max(0, hpBeforeAttack - dm.currentPlayerHP);
                dm.ui?.UpdateShieldUI(dm.playerStatus.currentShield);
                int blockedDamage = Mathf.Max(0, shieldBeforeAttack - dm.playerStatus.currentShield);
                HurtVignetteController.Get(dm).PlayShieldOrHealthHit(dm.ui, blockedDamage, actualHPDamage, dm.playerStatus.currentShield);
                if (actualHPDamage > 0) dm.PlayPlayerHurtSound();
                // 피격 후 회복 피규어 등이 실행되기 전에 체력 조건 검사
                FigureEffectManager.Instance?.EvaluateLowHPTriggers(dm, dm.shopManager);

                if (FigureEffectManager.Instance != null)
                {
                    FigureEffectManager.Instance.EvaluateDamagedTriggers(dm, dm.shopManager);
                }
                if (dm.enemy.IsDead) yield break;

                dm.enemy.ResetTurn();

                if (dm.currentPlayerHP <= 0)
                {
                    bool isRevived = FigureEffectManager.Instance != null && FigureEffectManager.Instance.EvaluateDeathTriggers(dm, dm.shopManager);

                    if (isRevived)
                    {
                        Debug.Log("<color=cyan>부활 성공! 즉시 플레이어 턴으로 넘어갑니다.</color>");
                    }
                    else
                    {
                        if (GameSaveManager.Instance != null) GameSaveManager.Instance.DeleteSave();

                        string gameOverText = LocalizationManager.Instance != null
                            ? LocalizationManager.Instance.GetLocalizedString(LocalizationManager.UiTable, "UI_GAME_OVER")
                            : "게임 오버";
                        dm.ui?.ShowResult("#FF0000", gameOverText);
                        dm.StartCoroutine(dm.ShowGameOverPanelDelayed());

                        yield break;
                    }
                }
            }

            if (GameSaveManager.Instance != null)
            {
                GameSaveManager.Instance.SaveGame(dm, InventoryManager.Instance, dm.shopManager);
            }

            dm.InvokeStartNewRound(0.5f);
        }
    }
}
