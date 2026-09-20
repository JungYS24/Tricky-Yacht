using UnityEngine;

public enum SnackType { Cherry, Pancake, LimeJuice, Steak, Garnish, Peppermint }

[CreateAssetMenu(fileName = "NewSnack", menuName = "Shop/Items/Snack")]
public class SnackItemSO : BaseItemDataSO
{
    [Header("--- 스낵 전용 스펙 ---")]
    public SnackType snackType;

    public override void ApplyItemEffect(DiceManager diceManager)
    {
        if (diceManager == null) return;

        float snackMultiplier = FigureEffectManager.Instance != null? FigureEffectManager.Instance.GetSnackEffectMultiplier(): 1f;

        switch (snackType)
        {
            case SnackType.Cherry:
                // [체리] 최종 데미지 배수 +0.2 (합연산)
                diceManager.snackBonusMult += 0.2f * snackMultiplier;
                break;

            case SnackType.Pancake:
                // [팬케이크] 기본 칩 수 +30
                diceManager.snackBonusChips +=
                    Mathf.FloorToInt(30f * snackMultiplier);
                break;

            case SnackType.LimeJuice:
                // [라임 주스] 리롤 기회 +1
                diceManager.snackBonusRerolls +=
                    Mathf.FloorToInt(1f * snackMultiplier);
                break;

            case SnackType.Steak:
                {
                    // [스테이크] 체력 10 회복
                    // 스낵 배율과 도도새 모자의 회복 배율을 각각 한 번 적용
                    float healMultiplier = FigureEffectManager.Instance != null
                        ? FigureEffectManager.Instance.GetHealMultiplier()
                        : 1f;

                    int healAmount = Mathf.FloorToInt(
                        10f * snackMultiplier * healMultiplier);

                    diceManager.playerStatus.Heal(healAmount);
                    break;
                }

            case SnackType.Garnish:
                // [가니쉬] 피규어 포획 확률 증가
                diceManager.snackBonusFigureDropRate +=
                    0.15f * snackMultiplier;
                break;

            case SnackType.Peppermint:
                // [페퍼민트] 포획 활성화만 수행. 스낵 배율 적용 제외
                diceManager.isPeppermintActive = true;
                break;
        }

        Debug.Log($"스낵 [{itemName}] 사용! 효과가 적용되었습니다.");

        // 아이템 사용 후 화면에 바뀐 데미지/횟수/확률 등을 즉시 반영
        diceManager.ForceUpdateUI();
    }
}