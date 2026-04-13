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

        switch (snackType)
        {
            case SnackType.Cherry:
                // [체리] 최종 데미지 배수 +0.2 (합연산)
                diceManager.snackBonusMult += 0.2f;
                break;
            case SnackType.Pancake:
                // [팬케이크] 기본 칩 수 +30
                diceManager.snackBonusChips += 30;
                break;
            case SnackType.LimeJuice:
                // [라임 주스] 리롤 기회 +1
                diceManager.snackBonusRerolls++;
                break;

            case SnackType.Steak:
                // [스테이크] 엔딩(Finish) 기회 +1
                diceManager.maxPlays++;
                break;
            case SnackType.Garnish:
                // [가니쉬] 이번 라운드 몬스터 박제(피규어) 확률 15% 증가 (원하시는 수치로 조절 가능)
                diceManager.snackBonusFigureDropRate += 0.15f;
                break;
            case SnackType.Peppermint:
                // [페퍼민트] 즉시 몬스터 박제 시도 (도박)
                diceManager.TryPeppermintCapture();
                break;

        }

        Debug.Log($"스낵 [{itemName}] 사용! 효과가 적용되었습니다.");

        // 아이템 사용 후 화면에 바뀐 데미지/횟수를 즉시 반영
        diceManager.ForceUpdateUI();
    }
}