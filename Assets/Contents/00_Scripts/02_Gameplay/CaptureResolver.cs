using UnityEngine;

public static class CaptureResolver
{
    // 몬스터 포획 가능 여부와 확률을 계산하여 최종 성공 여부를 반환
    public static bool CheckCaptureSuccess(Enemy enemy, float snackBonusFigureDropRate, bool isPeppermintActive)
    {
        if (!isPeppermintActive || enemy == null)
            return false;

        float dropChance = enemy.baseDropRate + snackBonusFigureDropRate;

        // 중복 획득 방지 조건
        bool canCapture = enemy.dropFigureData != null &&
                          InventoryManager.Instance != null &&
                          !InventoryManager.Instance.ownedFigures.Contains(enemy.dropFigureData);

        if (canCapture && Random.value <= dropChance)
        {
            return true;
        }

        return false;
    }
}