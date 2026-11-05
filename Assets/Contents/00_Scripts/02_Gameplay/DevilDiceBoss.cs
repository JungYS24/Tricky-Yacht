using System.Collections.Generic;
using UnityEngine;

// 최종보스 악마주사위 기믹. 페이즈는 보스 체력 비율로 정해지고, 앞 페이즈의 효과는 뒤 페이즈에서도 계속된다.
//  1페이즈: 매 턴 보드의 주사위 1개를 저주 주사위로 바꾼다(그 턴만, 다음 턴에는 다른 주사위로 옮겨 간다).
//  2페이즈(체력 60% 이하): 원페어·투페어 족보는 피해가 들어가지 않는다.
//  3페이즈(체력 25% 이하): 보스가 자기 눈을 굴려 다음 공격의 턴 수와 공격력을 바꾼다.
public static class DevilDiceBoss
{
    // 비차원 보스 노드에서 조우자 대화창으로 보여 주는 등장 대사. 번역은 ENC 표의 ENC_DEVIL_DICE_INTRO_DLG_01~05.
    public const string IntroKeyPrefix = "ENC_DEVIL_DICE_INTRO";
    public static readonly string[] IntroFallback =
    {
        "어라? 굴러들어 온 게…… 설마 '진짜' 플레이어?",
        "푸하하, 대단해, 정말 대단해! 설마 그 앙증맞은 주사위 몇 개 쥐고 여기까지 기어 올라올 줄이야. 박수라도 쳐줘야 하나? 짝, 짝, 짝.",
        "아, 표정 그렇게 굳히지 마. 난 그냥…… 네가 너무 먹음직스러워 보여서 그래. 알고 있니? 여기까지 온갖 고생을 하며 희망을 꽉꽉 채워 온 영혼일수록, 씹었을 때 육즙이 제일 달콤하거든.",
        "스낵 주머니는 다 털렸고, 손끝은 덜덜 떨리고…… 크으, 완벽해. 이보다 더 훌륭한 디저트가 어디 있겠어? 겉은 바삭하고 속은 절망으로 가득 찬 파이 같잖아.",
        "자, 테이블은 이미 차려졌어. 네 그 알량한 족보가 먼저 완성될지, 아니면 내 식판 위에서 바삭하게 부서질지…… 어디 한번 굴려봐. 뼈 하나 안 남기고 아주 맛있게 삼켜줄 테니까.",
    };

    // 3페이즈 공격 패턴: 눈 1~2 빠른 약공, 3~4 보통, 5~6 느린 강공
    static readonly int[] PatternTurns = { 1, 1, 2, 2, 3, 3 };
    static readonly float[] PatternAttack = { 0.6f, 0.6f, 1f, 1f, 1.8f, 1.8f };

    public static bool IsActive(Enemy enemy)
    {
        return enemy != null && !enemy.IsDead && enemy.CurrentBossAbility == BossAbilityType.DevilDice && !TutorialMode.Active;
    }

    public static int Phase(Enemy enemy)
    {
        if (enemy == null || enemy.MaxHP <= 0) return 1;
        BalanceConfig cfg = BalanceConfig.Current;
        float ratio = (float)enemy.CurrentHP / enemy.MaxHP;
        if (ratio <= cfg.devilPhase3HpRatio) return 3;
        if (ratio <= cfg.devilPhase2HpRatio) return 2;
        return 1;
    }

    // 보드에 놓인 주사위 중 아직 디버프가 아닌 주사위 하나에 저주를 건다. 여정 주사위는 건드리지 않는다.
    public static bool CurseOneDie(IReadOnlyList<Dice> activeDice)
    {
        var candidates = new List<Dice>();
        foreach (Dice dice in activeDice)
        {
            DiceData1 data = dice != null ? dice.myData : null;
            if (data == null || data.IsDebuff || data.devilCursed || DeckManager.IsProtected(data)) continue;
            candidates.Add(dice);
        }
        if (candidates.Count == 0) return false;

        Dice target = candidates[Random.Range(0, candidates.Count)];
        target.myData.devilCursed = true;
        target.SetData(target.myData, target.currentValue, target.ResolveFaceIndex());
        ToastPopupController.Instance?.ShowToast(LocalizationManager.GetSys("SYS_DEVIL_CURSE", "악마주사위가 주사위 하나에 저주를 걸었습니다!"));
        return true;
    }

    // 전투가 끝나면 이번 전투에서 건 저주를 모두 푼다.
    public static void ClearCurses(List<DiceData1> deck)
    {
        if (deck == null) return;
        foreach (DiceData1 die in deck)
            if (die != null) die.devilCursed = false;
    }

    public static bool BlocksHand(Enemy enemy, HandRank rank)
    {
        return IsActive(enemy) && Phase(enemy) >= 2 && (rank == HandRank.OnePair || rank == HandRank.TwoPair);
    }

    // 3페이즈 공격 패턴을 굴린다. 반환값은 나온 눈(1~6).
    public static int RollPattern(Enemy enemy)
    {
        int face = Random.Range(1, 7);
        enemy.SetAttackPattern(PatternTurns[face - 1], PatternAttack[face - 1]);
        ToastPopupController.Instance?.ShowToast(LocalizationManager.GetSys("SYS_DEVIL_PATTERN",
            "악마의 눈 {0}: 공격력 {1}, {2}턴 뒤 공격", face, enemy.AttackPower, enemy.MaxAttackTurn));
        return face;
    }
}
