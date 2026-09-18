using UnityEngine;
using System.Collections;

public static class CombatFlowController
{
    // 메인 턴 결산 흐름 
    public static IEnumerator ProcessTurnResolution(DiceManager dm, int finalDamage, int darkDamage, int finalHeal, int expectedGold, int flameDamage, string handName)
    {
        //플레이어 회복
        if (finalHeal > 0)
        {
            dm.playerStatus.Heal(finalHeal);
            dm.ui?.UpdateShieldUI(dm.playerStatus.currentShield);
        }

        //골드 획득 처리
        if (expectedGold > 0 && dm.shopManager != null)
        {
            dm.shopManager.currentGold += expectedGold;
            dm.ui?.UpdateGoldUI(dm.shopManager.currentGold);
        }

        //화염 스택 누적
        if (flameDamage > 0)
        {
            dm.accumulatedFlameDamage += flameDamage;
            dm.ui?.UpdateFlameStackUI(dm.accumulatedFlameDamage);
        }

        // 플레이어 공격 (야추 추가 타격 적용)
        int attackCount = 1 + dm.extraAttackCount;
        dm.extraAttackCount = 0; // 사용 후 바로 스위치 차단

        for (int i = 0; i < attackCount; i++)
        {
            if (dm.enemy.IsDead) break;

            yield return dm.StartCoroutine(ProcessPlayerAttack(dm, finalDamage, darkDamage));

            // 반사 데미지 등으로 플레이어가 죽었는지 매 타격마다 검사
            if (dm.currentPlayerHP <= 0)
            {
                yield return dm.StartCoroutine(HandlePlayerDeath(dm));
                yield break;
            }
        }

        // 적 사망 여부 확정 판정 (포획 이벤트 포함)
        if (dm.enemy.IsDead || dm.enemy.CurrentHP <= 0)
        {
            dm.OnEnemyKilled();
            yield break;
        }

        // 6. 적 반격 루틴 (이때 누적된 화상 데미지 데이터를 넘겨줌)
        yield return dm.StartCoroutine(ProcessEnemyTurnRoutine(dm, handName, dm.accumulatedFlameDamage));
    }


    //플레이어 공격 연출
    private static IEnumerator ProcessPlayerAttack(DiceManager dm, int damage, int darkDamage)
    {
        if (damage > 0)
        {
            dm.enemy.TakeDamage(damage, dm.OnEnemyKilled);
            yield return new WaitForSeconds(0.4f);
        }

        // 일반 데미지를 맞고 살아있을 때만 다크 데미지 적용
        if (darkDamage > 0 && !dm.enemy.IsDead)
        {
            dm.enemy.TakeDamage(darkDamage, dm.OnEnemyKilled);
            yield return new WaitForSeconds(0.3f);
        }
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
        yield return new WaitForSeconds(0.4f);
        dm.UpdateMainUI(handName);

        if (!dm.enemy.IsDead)
        {
            // 일반 공격 후 화염 데미지가 0.3초후에 터짐
            yield return new WaitForSeconds(0.2f);

            if (flameDamage > 0)
            {
                CameraShake.Instance.Shake(0.1f, 0.1f);
                dm.enemy.TakeDamage(flameDamage, dm.OnEnemyKilled);
                dm.UpdateMainUI($"화염 데미지! <color=#FF4500>-{flameDamage}</color>");

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
                yield return new WaitForSeconds(0.2f);

                int finalEnemyAtk = dm.isNextEnemyAttackFixedToOne ? 1 : dm.enemy.AttackPower;
                dm.isNextEnemyAttackFixedToOne = false;

                int reduction = FigureEffectManager.Instance != null ? FigureEffectManager.Instance.GetTotalDamageReduction(finalEnemyAtk, dm) : 0;
                finalEnemyAtk -= reduction;
                if (finalEnemyAtk < 0) finalEnemyAtk = 0;

                dm.playerStatus.TakeDamage(finalEnemyAtk);

                CameraShake.Instance.Shake(0.15f, 0.1f);
                dm.ui?.UpdateShieldUI(dm.playerStatus.currentShield);

                if (HurtVignetteController.Instance != null) HurtVignetteController.Instance.TriggerHurtEffect();
                dm.PlayPlayerHurtSound();

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