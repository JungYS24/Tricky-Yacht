using System.Collections.Generic;

//생명주기 관리용 순수 데이터 클래스
public class StageContext
{
    public float stageBonusMult = 0f;
    public int stageBonusChips = 0;
    public float combatWinGoldMultiplier = 1.0f;
    public int extraAttackCount = 0;
    public bool isEnemySkillNullified = false;
    public bool isNextEnemyAttackFixedToOne = false;
    public int figureBonusFlameDamage = 0;
    public int accumulatedFlameDamage = 0;
    public bool isPeppermintActive = false;
    public float snackBonusFigureDropRate = 0f;
    public bool firstNormalAttackDone = false;
    // 현재 스테이지에서 사용한 1회 제한 노드
    public HashSet<string> usedFigureNodes = new HashSet<string>();

    public void ResetForNewStage()
    {
        stageBonusMult = 0f;
        stageBonusChips = 0;
        combatWinGoldMultiplier = 1.0f;
        extraAttackCount = 0;
        isEnemySkillNullified = false;
        isNextEnemyAttackFixedToOne = false;
        figureBonusFlameDamage = 0;
        accumulatedFlameDamage = 0;
        isPeppermintActive = false;
        snackBonusFigureDropRate = 0f;
        usedFigureNodes.Clear();
        firstNormalAttackDone = false;
    }
}

public class TurnContext
{
    public int debuffRerollPenalty;
    public float snackBonusMult = 0f;
    public int snackBonusChips = 0;
    public int snackBonusRerolls = 0;
    public int figureBonusRerolls = 0;

    public void ResetForNewTurn()
    {
        debuffRerollPenalty = 0;
        snackBonusMult = 0f;
        snackBonusChips = 0;
        snackBonusRerolls = 0;
        // figureBonusRerolls는 결산 버튼 클릭 시 수동 리셋됨
    }
}
