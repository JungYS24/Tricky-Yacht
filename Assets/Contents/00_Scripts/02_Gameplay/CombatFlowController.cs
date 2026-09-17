using UnityEngine;
using System.Collections;

public static class CombatFlowController
{
    // 적의 반격, 화상 데미지, 플레이어의 피격 및 사망(부활) 처리를 전담
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
                CameraShake.Instance.Shake(0.1f, 0.1f); // 가벼운 흔들림 연출
                dm.enemy.TakeDamage(flameDamage, dm.OnEnemyKilled); // 화염 데미지 적용
                dm.UpdateMainUI($"화염 데미지! <color=#FF4500>-{flameDamage}</color>");

                // 화염 데미지로 몬스터가 타죽었다면 적의 공격 캔슬
                if (dm.enemy.IsDead) yield break;

                // 화염 폭발 후 적이 반격하기 전 템포 조절 (0.8초 대기)
                yield return new WaitForSeconds(0.8f);
            }
            else
            {
                // 화염 주사위가 없을 때는 0.4초만 대기 후 바로 반격
                yield return new WaitForSeconds(0.55f);
            }

            dm.enemy.DecreaseTurn();

            if (dm.enemy.CurrentAttackTurn <= 0)
            {
                dm.enemy.PlayAttackAnim();
                yield return new WaitForSeconds(0.2f);

                // 플레이어 체력 감소 및 화면 흔들림
                int finalEnemyAtk = dm.isNextEnemyAttackFixedToOne ? 1 : dm.enemy.AttackPower;
                dm.isNextEnemyAttackFixedToOne = false; // 적용 후 스위치 끄기

                // [방어 로직 적용] FigureEffectManager에 뎀감이 총 얼마인지 물어보고 빼줍니다.
                int reduction = FigureEffectManager.Instance != null ? FigureEffectManager.Instance.GetTotalDamageReduction(finalEnemyAtk, dm) : 0;
                finalEnemyAtk -= reduction;
                if (finalEnemyAtk < 0) finalEnemyAtk = 0; // 뎀감이 너무 높아도 체력이 차진 않도록 방어

                //보호막이 있다면, 내 기본 체력보다 보호막이 먼저 깎임
                if (dm.currentShield > 0)
                {
                    if (dm.currentShield >= finalEnemyAtk)
                    {
                        dm.currentShield -= finalEnemyAtk;
                        finalEnemyAtk = 0; // 보호막이 다 막아줌
                    }
                    else
                    {
                        finalEnemyAtk -= dm.currentShield;
                        dm.currentShield = 0; // 보호막 파괴됨
                    }
                    dm.ui?.UpdateShieldUI(dm.currentShield); // 깎인 보호막 UI 즉시 갱신
                }

                // 보호막을 뚫고 들어온 최종 데미지만 체력에서 깎음
                dm.currentPlayerHP -= finalEnemyAtk;
                CameraShake.Instance.Shake(0.15f, 0.1f);

                // 비네트 피격 연출 실행
                if (HurtVignetteController.Instance != null) HurtVignetteController.Instance.TriggerHurtEffect();

                // 플레이어 피격 효과음 재생 (DiceManager의 안전한 래퍼 함수 호출)
                dm.PlayPlayerHurtSound();

                //피격 완료 시 발동하는 피규어(광대의 눈물 등) 처리
                if (FigureEffectManager.Instance != null)
                {
                    FigureEffectManager.Instance.EvaluateDamagedTriggers(dm, dm.shopManager);
                }

                dm.enemy.ResetTurn(); // 공격을 했으므로 턴 카운트를 다시 원래대로(2) 되돌림

                if (dm.currentPlayerHP <= 0)
                {
                    //사망 시 부활 피규어가 있는지 효과 매니저에 물어봄
                    bool isRevived = FigureEffectManager.Instance != null && FigureEffectManager.Instance.EvaluateDeathTriggers(dm, dm.shopManager);

                    if (isRevived)
                    {
                        Debug.Log("<color=cyan>부활 성공! 즉시 플레이어 턴으로 넘어갑니다.</color>");
                    }
                    else
                    {
                        //게임 오버 처리
                        if (GameSaveManager.Instance != null) GameSaveManager.Instance.DeleteSave();

                        string gameOverText = LocalizationManager.Instance != null
                            ? LocalizationManager.Instance.GetLocalizedString(LocalizationManager.UiTable, "UI_GAME_OVER")
                            : "게임 오버";
                        dm.ui?.ShowResult("#FF0000", gameOverText);

                        dm.InvokeRestartGame(1.5f);
                        dm.StartCoroutine(dm.ShowGameOverPanelDelayed());

                        //플레이어가 죽었다면 아래 세이브 및 다음 라운드 코드가 실행되지 않도록 강제 종료
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