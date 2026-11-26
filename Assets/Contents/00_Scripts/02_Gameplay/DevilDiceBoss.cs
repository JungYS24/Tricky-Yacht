using System.Collections.Generic;
using UnityEngine;

// 최종보스 악마주사위 기믹. 처치형 2페이즈: 1페이즈 체력이 0이 되면 보상 대신 각성해 2페이즈(타락한 악마주사위) 체력을 새로 채운다.
//  1페이즈 악마주사위
//   - 장난의 저주: 매 턴 시작, 보드의 주사위 1개를 그 턴만 저주·가시 주사위(무작위)로 바꾼다. 덱으로 돌아가면 원래 주사위다.
//   - 간식 시간: 매 턴 끝, 플레이어에게 추가 피해(보호막·피해 감소 적용, 피격으로 치지 않음).
//  2페이즈 타락한 악마주사위
//   - 타락의 낙인: 매 턴 시작, 덱에 가시·저주 디버프 주사위를 넣는다(이번 전투 한정).
//   - 영혼 포식: 결산이 끝난 뒤 킵한 주사위 하나를 덱에서 영구히 지운다(주사위 파괴로 취급).
//   - 심연의 숨결: 매 턴 끝, 추가 피해 = 기본값 + 덱 안 디버프 주사위 수 × 배율(피격으로 치지 않음).
//   - 악마의 식탁: 원페어·투페어 족보는 피해가 들어가지 않는다.
//   - 악마의 눈: 공격할 때마다 자기 눈을 굴려 다음 공격의 턴 수와 공격력을 바꾼다.
// 턴 끝 순서: 결산 → 영혼 포식 → 화상 → 보스 공격 → 간식 시간/심연의 숨결 → (2페이즈) 다음 턴 낙인
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

    // 2페이즈 공격 패턴(악마의 눈): 눈 1~2 빠른 약공, 3~4 보통, 5~6 느린 강공
    static readonly int[] PatternTurns = { 1, 1, 2, 2, 3, 3 };
    static readonly float[] PatternAttack = { 0.6f, 0.6f, 1f, 1f, 1.8f, 1.8f };

    public static bool IsActive(Enemy enemy)
    {
        return enemy != null && !enemy.IsDead && enemy.CurrentBossAbility == BossAbilityType.DevilDice && !TutorialMode.Active;
    }

    // 1페이즈 악마주사위, 2페이즈 타락한 악마주사위(각성한 뒤).
    public static int Phase(Enemy enemy)
    {
        return enemy != null && enemy.DevilAwakened ? 2 : 1;
    }

    // 1페이즈 체력이 0이 되었을 때 처치 대신 각성으로 넘어가는지. 쓰러진 상태에서 부르므로 IsActive를 쓰지 않는다.
    public static bool ShouldAwaken(Enemy enemy)
    {
        return enemy != null && enemy.CurrentBossAbility == BossAbilityType.DevilDice && !enemy.DevilAwakened && !TutorialMode.Active;
    }

    // 매 턴 끝 추가 피해(보호막·피해 감소 적용 전). 1페이즈 간식 시간, 2페이즈 심연의 숨결(덱 안 디버프 주사위 수에 비례).
    public static int TurnEndDamage(Enemy enemy, List<DiceData1> deck)
    {
        if (!IsActive(enemy)) return 0;
        BalanceConfig cfg = BalanceConfig.Current;
        if (Phase(enemy) < 2) return Mathf.Max(0, cfg.devilSnackDamage);
        return Mathf.Max(0, cfg.devilBreathDamage + CountDebuffDice(deck) * cfg.devilBreathPerDebuff);
    }

    public static int CountDebuffDice(List<DiceData1> deck)
    {
        int count = 0;
        if (deck == null) return count;
        foreach (DiceData1 die in deck)
            if (die != null && die.IsDebuff) count++;
        return count;
    }

    // 영혼 포식 대상: 킵한 주사위 중 여정·디버프 주사위를 뺀 하나를 무작위로 고른다.
    // 덱(여정·디버프 주사위 제외)이 5개 미만이면 더 먹지 않는다(진행 불가 방지). 대상이 없으면 null.
    public static Dice PickDevourTarget(Enemy enemy, List<Dice> kept, List<DiceData1> deck)
    {
        if (!IsActive(enemy) || Phase(enemy) < 2 || kept == null || deck == null) return null;
        int remaining = 0;
        foreach (DiceData1 die in deck)
            if (die != null && !die.IsDebuff && !DeckManager.IsProtected(die)) remaining++;
        if (remaining < 5) return null;

        var candidates = new List<Dice>();
        foreach (Dice dice in kept)
        {
            DiceData1 data = dice != null ? dice.myData : null;
            if (data == null || data.IsDebuff || DeckManager.IsProtected(data) || !deck.Contains(data)) continue;
            candidates.Add(dice);
        }
        return candidates.Count > 0 ? candidates[Random.Range(0, candidates.Count)] : null;
    }

    // 주사위를 덱·뽑을 더미·버린 더미에서 영구히 지운다(주사위 파괴와 같은 경로).
    // 영혼 포식으로 삼킨 주사위로 기록해 두었다가, 보스를 처치할 때 부서지는 몸에서 튀어나오는 연출에 쓴다(덱으로 돌아오지는 않는다).
    public static void RemoveFromDeck(DeckManager deckManager, DiceData1 die)
    {
        if (deckManager == null || die == null) return;
        deckManager.masterDeck.Remove(die);
        deckManager.drawPile.RemoveAll(d => ReferenceEquals(d, die));
        deckManager.discardPile.RemoveAll(d => ReferenceEquals(d, die));
        devoured.Add(die);
    }

    // 이번 전투에서 영혼 포식으로 삼킨 주사위(처치 연출 전용 기록). 새 몬스터를 세팅할 때 비운다.
    static readonly List<DiceData1> devoured = new List<DiceData1>();
    public static IReadOnlyList<DiceData1> Devoured => devoured;
    public static void ClearDevoured() => devoured.Clear();

    // 장난의 저주로 그 턴만 바꿔 놓은 주사위(바뀐 데이터 → 원래 데이터). 바뀐 데이터는 덱에 넣지 않는다.
    static readonly Dictionary<DiceData1, DiceData1> swappedDice = new Dictionary<DiceData1, DiceData1>();

    // 보드 주사위가 덱(뽑을·버린 더미, 저장)으로 돌아갈 때 쓸 데이터. 장난의 저주로 바뀐 주사위면 원래 주사위를 돌려준다.
    public static DiceData1 OriginalOf(DiceData1 die)
    {
        return die != null && swappedDice.TryGetValue(die, out DiceData1 original) ? original : die;
    }

    public static bool IsSwapped(DiceData1 die) => die != null && swappedDice.ContainsKey(die);

    // 장난의 저주 대상: 보드에 놓인 주사위 중 디버프·여정 주사위가 아닌 하나와, 바꿀 종류(저주·가시 무작위)를 고른다. 없으면 null.
    public static Dice PickCurseTarget(IReadOnlyList<Dice> activeDice, out DebuffDiceType type)
    {
        type = Random.Range(0, 2) == 0 ? DebuffDiceType.Curse : DebuffDiceType.Spine;
        var candidates = new List<Dice>();
        foreach (Dice dice in activeDice)
        {
            DiceData1 data = dice != null ? dice.myData : null;
            if (data == null || data.IsDebuff || IsSwapped(data) || DeckManager.IsProtected(data)) continue;
            candidates.Add(dice);
        }
        return candidates.Count > 0 ? candidates[Random.Range(0, candidates.Count)] : null;
    }

    // 장난의 저주 시전: 보스의 시전(IdleB) 애니메이션과 시전 소리. 연출(DebuffFeedback.PlayDevilCurse)을 시작할 때 부른다.
    public static void PlayCurseCast(Enemy boss)
    {
        // 시전 소리는 애니메이션 상태와 관계없이 항상 낸다.
        Sfx.Play(SfxId.Devil_Curse_Cast);
        if (boss != null) { boss.PlayDevilCast(); boss.PlaySfxDelayed(SfxId.Devil_Curse_Flicker, 0.35f); }
        else Sfx.Play(SfxId.Devil_Curse_Flicker);
    }

    // 장난의 저주 적용: target을 그 턴만 type 디버프 주사위로 바꾼다. 눈 값은 그대로 두고(다시 굴려 바꿀 수 있다),
    // 원래 주사위는 덱으로 돌아갈 때 되돌린다(OriginalOf). createDebuff는 덱에 넣지 않는 디버프 주사위 데이터를 만든다.
    public static bool ApplyCurse(Dice target, DebuffDiceType type, System.Func<DebuffDiceType, DiceData1> createDebuff)
    {
        if (target == null || createDebuff == null || target.myData == null || target.myData.IsDebuff || IsSwapped(target.myData)) return false;
        DiceData1 swapped = createDebuff(type);
        if (swapped == null) return false;
        swappedDice[swapped] = target.myData;
        int value = target.currentValue;
        int face = swapped.FindFaceIndex(value);
        if (face < 0)
        {
            // 원래 눈이 1~6 밖이면(0·특수 눈) 가장 가까운 눈으로 맞춘다.
            face = Mathf.Clamp(value, 1, 6) - 1;
            value = swapped.faceValues[face];
        }
        target.SetData(swapped, value, face);
        ToastPopupController.Instance?.ShowToast(LocalizationManager.GetSys("SYS_DEVIL_CURSE", "악마주사위가 주사위 하나에 저주를 걸었습니다!"));
        return true;
    }

    // 새 턴(보드 주사위가 덱으로 돌아간 뒤)·전투 종료 때 지난 저주를 모두 푼다.
    public static void ClearCurses(List<DiceData1> deck)
    {
        swappedDice.Clear();
        if (deck == null) return;
        foreach (DiceData1 die in deck)
            if (die != null) die.devilCursed = false;
    }

    public static bool BlocksHand(Enemy enemy, HandRank rank)
    {
        return IsActive(enemy) && Phase(enemy) >= 2 && (rank == HandRank.OnePair || rank == HandRank.TwoPair);
    }

    // 2페이즈 공격 패턴을 굴린다. 반환값은 나온 눈(1~6). eyeRollSfxDelay만큼 늦게 눈 굴림 소리를 낸다.
    public static int RollPattern(Enemy enemy, float eyeRollSfxDelay = 0f)
    {
        int face = Random.Range(1, 7);
        enemy.SetAttackPattern(PatternTurns[face - 1], PatternAttack[face - 1]);
        enemy.PlaySfxDelayed(SfxId.Devil_Eye_Roll, eyeRollSfxDelay);
        ToastPopupController.Instance?.ShowToast(LocalizationManager.GetSys("SYS_DEVIL_PATTERN",
            "악마의 눈 {0}: 공격력 {1}, {2}턴 뒤 공격", face, enemy.AttackPower, enemy.MaxAttackTurn));
        return face;
    }
}
