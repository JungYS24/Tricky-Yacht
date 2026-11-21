using UnityEngine;

public static class CaptureResolver
{
    // 페퍼민트 포획 확률. 예전 계산식(enemy.baseDropRate + snackBonusFigureDropRate)은 꺼 두고 100%로 고정되어 있다.
    public const float PeppermintCaptureChance = 1.0f;

    // 몬스터 포획 가능 여부와 확률을 계산하여 최종 성공 여부를 반환
    public static bool CheckCaptureSuccess(Enemy enemy, float snackBonusFigureDropRate, bool isPeppermintActive)
    {
        if (!isPeppermintActive || enemy == null)
            return false;

        float dropChance = PeppermintCaptureChance;//enemy.baseDropRate + snackBonusFigureDropRate;

        // 중복 획득 방지 조건
        bool canCapture = CanCapture(enemy);

        if (canCapture && Random.value <= dropChance)
        {
            return true;
        }

        return false;
    }

    // 이 몬스터가 주는 피규어가 있고 아직 가지고 있지 않을 때만 포획할 수 있다.
    public static bool CanCapture(Enemy enemy)
    {
        return enemy != null && enemy.dropFigureData != null &&
               InventoryManager.Instance != null &&
               !InventoryManager.Instance.ownedFigures.Contains(enemy.dropFigureData);
    }

    public static bool IsFigureOwned(Enemy enemy)
    {
        return enemy != null && enemy.dropFigureData != null &&
               InventoryManager.Instance != null &&
               InventoryManager.Instance.ownedFigures.Contains(enemy.dropFigureData);
    }

    // 점수판 포획 확률 칸: 이미 가진 피규어면 "보유 중", 페퍼민트를 눌렀으면 포획 확률, 아니면 0%.
    public static void RefreshDisplay(DiceManager dm)
    {
        if (dm == null || dm.ui == null) return;
        bool owned = IsFigureOwned(dm.enemy);
        bool peppermint = dm.isPeppermintActive || (InventoryManager.Instance != null && InventoryManager.Instance.SnackUses.HasPendingPeppermint);
        float chance = !owned && peppermint && CanCapture(dm.enemy) ? PeppermintCaptureChance : 0f;
        dm.ui.UpdateCaptureRateUI(owned, chance);
    }
}
