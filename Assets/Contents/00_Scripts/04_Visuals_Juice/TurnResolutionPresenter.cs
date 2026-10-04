using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

[System.Serializable]
public sealed class ScorePacing
{
    [Min(0f)] public float beforeHandDelay = 0.25f;
    [Min(0f)] public float afterHandDelay = 0.15f;
    [Header("1~3번째 / 4~6번째 / 7번째 이후")]
    [Min(0.01f)] public float bonusDuration = 0.28f;
    [Min(0f)] public float bonusPause = 0.06f;
    [Min(0.01f)] public float middleDuration = 0.22f;
    [Min(0f)] public float middlePause = 0.04f;
    [Min(0.01f)] public float fastDuration = 0.16f;
    [Min(0f)] public float fastPause = 0.02f;
    [Min(0f)] public float phaseTransition = 0.15f;
    [Header("최종 피해량")]
    [Min(0f)] public float afterScoreDelay = 0.2f;
    [Min(0.01f)] public float finalDamageDuration = 0.3f;
    [Min(0f)] public float finalDamageHold = 0.3f;
    [Min(1f)] public float finalFontScale = 1.4f;
    [Min(1f)] public float finalWidthScale = 1.3f;
    [Min(1f)] public float finalPunchScale = 1.28f;
    [SerializeField, HideInInspector] private bool impactDefaultsUpdated;

    public void UpgradeImpactDefaults()
    {
        if (impactDefaultsUpdated) return;
        // 이전 기본값만 교체합니다. 사용자가 다르게 조절한 값은 유지합니다.
        if (Mathf.Approximately(finalDamageHold, 0.4f)) finalDamageHold = 0.3f;
        if (Mathf.Approximately(finalPunchScale, 1.15f)) finalPunchScale = 1.28f;
        impactDefaultsUpdated = true;
    }

    // completed는 칩/배수 전체에서 실제 표시한 항목 수입니다. 첫 배수도 순서는 초기화하지 않습니다.
    public void GetBonusTiming(int completed, bool firstMultiplier, out float duration, out float pause)
    {
        duration = completed < 3 ? bonusDuration : completed < 6 ? middleDuration : fastDuration;
        pause = completed < 3 ? bonusPause : completed < 6 ? middlePause : fastPause;
        if (firstMultiplier) duration = bonusDuration;
        duration = Mathf.Max(0.01f, duration);
        pause = Mathf.Max(0f, pause);
    }

    public static float GetGap(int completed, bool previousChips, bool currentChips, float pendingPause, float transition)
    {
        if (completed == 0) return 0f;
        return Mathf.Max(0f, previousChips && !currentChips ? transition : pendingPause);
    }
}

[System.Serializable]
public sealed class TurnResolutionTiming
{
    // 새 묶음으로 분리하여 씬에 저장된 이전 0.4/0.2초 값이 새 기본값을 덮지 않게 합니다.
    public ScorePacing pacing = new ScorePacing();
}

// 정산의 표시 순서와 시간만 담당합니다. 피해/골드/회복 등 실제 게임 상태는 변경하지 않습니다.
// 코루틴은 DiceManager에서 실행하므로 기존 턴 진행과 선택창 대기를 유지합니다.
public sealed class TurnResolutionPresenter
{
    private readonly int[] scoreHandValues = new int[5];
    private readonly int[] scoreHandGroups = new int[5];
    private readonly int[] scoreHandOrder = new int[5];

    public IEnumerator PlayOpening(DiceManager manager, List<Dice> keptDice, HandRank rank, float handMult, TurnResolutionTiming timing)
    {
        timing.pacing = timing.pacing ?? new ScorePacing();
        timing.pacing.UpgradeImpactDefaults();
        // 매 정산마다 주사위별 누적값을 초기화합니다. 표시 오브젝트는 재사용합니다.
        foreach (var die in keptDice)
            if (die != null) die.GetComponent<DiceBonusLabel>()?.ResetTotals();
        // 달성한 족보의 이펙트 재생
        foreach (var d in keptDice)
        {
            if (d == null || d.myData == null) continue;

            d.ShowFloatingText(d.currentValue);
        }

        // 숫자 표시 시작 후 설정된 간격을 두고 족보 연출 시작
        yield return new WaitForSeconds(timing.pacing.beforeHandDelay);

        // 달성한 족보의 이펙트 재생
        yield return HandResolutionFeedback.Get(manager).Play(keptDice, rank, manager.ui);

        // 족보 연출이 끝난 뒤 설정된 시간만큼 대기
        yield return new WaitForSeconds(timing.pacing.afterHandDelay);

        // 분리 전과 동일하게 족보 배수가 2 이상이면 슬로모션
        if (handMult >= 2.0f)
        {
            SlowMotion.Instance?.PlaySlowMotion(0.2f, 0.2f);
        }

    }

    public IEnumerator PlayScore(UIManager ui, List<Dice> keptDice, HandRank handRank, string handName, float handMult, TurnCalcResult calcResult, List<DiceScoreBonus> diceScoreBonuses, TurnScoreBonuses bonuses, int chipsBeforeFigures, float multBeforeFigures, TurnScoreResult total, TurnResolutionTiming timing, SnackUseController snacks = null)
    {
        // 연출 전용 값: 실제 피해 계산에는 사용하지 않음
        float shownChips = calcResult.baseSum;
        float shownMult = 1f;
        var pace = timing.pacing;
        int playedCount = 0;
        bool previousWasChips = true;
        bool multiplierStarted = false;
        float pendingPause = 0f;

        // 칩과 배수의 보너스를 항목별로 표시
        IEnumerator AnimateBonus(bool isChips, float amount, string label, Dice source = null, bool reactHand = false, SnackUseEntry snack = null)
        {
            if (Mathf.Approximately(amount, 0f))
            {
                if (snack != null) yield return snacks.CompleteVisual(snack);
                yield break;
            }
            if (ui == null)
            {
                if (snack != null) yield return snacks.CompleteVisual(snack);
                yield break;
            }

            bool firstMultiplier = !isChips && !multiplierStarted;
            // 다음 실제 항목 앞에서 대기하므로 마지막 항목 뒤에는 bonusPause가 남지 않습니다.
            float gap = ScorePacing.GetGap(playedCount, previousWasChips, isChips, pendingPause, pace.phaseTransition);
            if (gap > 0f) yield return new WaitForSecondsRealtime(gap);
            pace.GetBonusTiming(playedCount, firstMultiplier, out float duration, out float pause);
            playedCount++;
            previousWasChips = isChips;
            if (!isChips) multiplierStarted = true;
            pendingPause = pause;

            if (snack != null) snacks.BeginDisappear(snack);

            // 숫자 상승과 같은 순간에 원인이 된 주사위를 반응
            if (reactHand)
            {
                if (keptDice.Count == 5)
                {
                    for (int i = 0; i < keptDice.Count; i++) scoreHandValues[i] = keptDice[i].currentValue;

                    HandResolutionFeedback.GetEmphasisGroups(handRank, scoreHandValues, scoreHandGroups, scoreHandOrder);

                    for (int i = 0; i < keptDice.Count; i++)
                    {
                        if (scoreHandGroups[i] >= 0) keptDice[i].PlayScoreFeedback(duration);
                    }
                }
            }
            else if (source != null)
            {
                source.PlayScoreFeedback(duration);
                DiceBonusLabel.Get(source).Add(isChips, amount, duration, ui.handInfoText != null ? ui.handInfoText.font : null);
            }


            var valueText = isChips ? ui.chipsSumText : ui.multSumText;
            var logText = isChips ? ui.chipsLogText : ui.multLogText;

            float start = isChips ? shownChips : shownMult;
            float target = start + amount;

            if (logText != null)
            {
                string amountText = isChips ? amount.ToString("+0;-0;0") : amount.ToString("+0.0;-0.0;0.0");

                logText.text = string.IsNullOrEmpty(label) ? "" : $"{label} ({amountText})";
            }

            // 계산된 최종 칩과 배수를 UI에 띄우고 통통 튀는 펀치 스케일 적용
            // 각 보너스가 반영될 때마다 숫자와 연출을 갱신
            if (valueText != null)
            {
                valueText.transform.DOKill(true);
                valueText.transform.DOPunchScale(Vector3.one * 0.2f, duration, 4, 0.5f).SetUpdate(true);

                var counter = UiCountUpText.On(valueText, isChips ? UiCountUpText.FormatKind.Chips : UiCountUpText.FormatKind.Mult);
                if (counter != null)
                    yield return counter.Play(target, duration).WaitForCompletion();
                else
                    yield return DOVirtual.Float(start, target, duration, value =>
                    {
                        valueText.text = isChips ? UIManager.FormatChipsValue(Mathf.FloorToInt(value)) : UIManager.FormatMultValue(value);
                    }).SetEase(Ease.OutQuad).SetUpdate(true).WaitForCompletion();
            }

            if (valueText == null) yield return new WaitForSecondsRealtime(duration);

            if (isChips)
                shownChips = target;
            else
                shownMult = target;

            if (snack != null) yield return snacks.CompleteVisual(snack);

            if (logText != null)
                logText.text = "";
        }

        IEnumerator AnimateDiceBonuses(bool isChips)
        {
            foreach (var bonus in diceScoreBonuses)
            {
                if (bonus.IsChips != isChips) continue;

                yield return AnimateBonus(isChips, bonus.amount, "", bonus.source);
            }
        }


        IEnumerator AnimateSnackBonuses(bool isChips, float aggregate)
        {
            float tracked = 0f;
            if (snacks != null)
            {
                // 사용 순서를 유지하고, 같은 종류를 여러 개 먹었어도 하나씩 표시
                for (int i = 0; i < snacks.Entries.Count; i++)
                {
                    var entry = snacks.Entries[i];
                    if (!snacks.IsScoreEntry(entry, isChips)) continue;
                    float amount = isChips ? entry.chips : entry.multiplier;
                    tracked += amount;
                    yield return AnimateBonus(isChips, amount, "", null, false, entry);
                }
            }
            // 이전 저장 파일이나 피규어가 미리 추가한 값은 남은 합계로 표시하여 누락/중복을 방지
            yield return AnimateBonus(isChips, aggregate - tracked, LocalizationManager.GetUi("UI_BONUS_SNACK", "스낵"));
        }


        if (ui != null)
        {
            if (ui.chipsLogText != null) ui.chipsLogText.text = "";
            if (ui.multLogText != null) ui.multLogText.text = "";
            if (ui.finalDamageText != null) ui.finalDamageText.text = "";

            if (ui.chipsSumText != null)
                UiCountUpText.On(ui.chipsSumText, UiCountUpText.FormatKind.Chips)?.SetInstant(Mathf.FloorToInt(shownChips));

            if (ui.multSumText != null)
                UiCountUpText.On(ui.multSumText, UiCountUpText.FormatKind.Mult)?.SetInstant(1f);

            // 족보 완성 연출 이후: 모든 칩 추가 → 모든 배수 추가
            // 주사위별 칩 보너스: 아이스·수성
            yield return AnimateDiceBonuses(true);
            // 기존 스낵·피규어·전투 누적 칩 처리 유지
            yield return AnimateSnackBonuses(true, chipsBeforeFigures);
            yield return AnimateBonus(true, bonuses.snackChips - chipsBeforeFigures, LocalizationManager.GetUi("UI_BONUS_FIGURE", "피규어"));
            yield return AnimateBonus(true, bonuses.stageChips, LocalizationManager.GetUi("UI_BONUS_STAGE", "전투 누적"));
            // 모든 칩 처리가 끝난 뒤 족보 배수부터 표시
            yield return AnimateBonus(false, handMult - 1f, handName, null, true);
            // 이후 배수 보너스 표시
            yield return AnimateBonus(false, bonuses.permanentMult, LocalizationManager.GetUi("UI_BONUS_PERMANENT", "영구 보너스"));
            yield return AnimateBonus(false, bonuses.snackMult - multBeforeFigures, LocalizationManager.GetUi("UI_BONUS_FIGURE", "피규어"));
            // 주사위별 배수 보너스: 프리즘·아이스 추가 배수·화성
            yield return AnimateDiceBonuses(false);
            // 체리는 족보와 주사위 배수 이후 각 스낵이 사라지는 순간에 표시
            yield return AnimateSnackBonuses(false, multBeforeFigures);
            yield return AnimateBonus(false, bonuses.stageMult, LocalizationManager.GetUi("UI_BONUS_STAGE", "전투 누적"));

            // 표시의 최종값을 실제 계산 결과에 맞춤
            if (ui.chipsSumText != null)
                UiCountUpText.On(ui.chipsSumText, UiCountUpText.FormatKind.Chips)?.SetInstant(total.chips);

            if (ui.multSumText != null)
                UiCountUpText.On(ui.multSumText, UiCountUpText.FormatKind.Mult)?.SetInstant(total.multiplier);

            // 배수 표시를 유지한 뒤 최종 피해 표시로 진행
            yield return new WaitForSecondsRealtime(timing.pacing.afterScoreDelay);
        }

    }

    public IEnumerator PlayFinalDamage(UIManager ui, int displayedDamage, TurnResolutionTiming timing)
    {
        if (ui == null || ui.handInfoText == null)
            yield break;

        ui.ApplyFinalDamageStyle();
        var handText = ui.handInfoText;
        var emphasis = FinalDamageFeedback.Get(handText);
        emphasis.Prepare(timing.pacing.finalFontScale, timing.pacing.finalWidthScale);
        var damageCounter = UiCountUpText.On(handText, UiCountUpText.FormatKind.Integer);
        if (damageCounter != null)
        {
            damageCounter.SetInstant(0f);
            yield return damageCounter.Play(displayedDamage, timing.pacing.finalDamageDuration).WaitForCompletion();
        }
        else
        {
            handText.text = displayedDamage.ToString();
            yield return new WaitForSecondsRealtime(timing.pacing.finalDamageDuration);
        }
        // 도달 순간 강조는 유지 시간 안에 재생하며, 완료된 피해량을 읽은 뒤 실제 공격합니다.
        emphasis.Pulse(timing.pacing.finalPunchScale, timing.pacing.finalDamageHold);
        yield return new WaitForSecondsRealtime(timing.pacing.finalDamageHold);
    }
}
