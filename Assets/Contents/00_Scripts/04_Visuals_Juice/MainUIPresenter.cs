using UnityEngine;
using System.Collections.Generic;

// 씬 연결이 필요 없는 메인 UI 갱신 담당자입니다. 버퍼는 DiceManager당 한 번 생성해 재사용합니다.
public sealed class MainUIPresenter
{
    //오브젝트 풀링 및 UI 갱신용 재사용 버퍼
    private readonly List<Dice> uiDiceBuffer = new List<Dice>(5);
    private readonly List<int> uiValuesBuffer = new List<int>(5);
    private readonly HashSet<FigureItemSO> previewFigures = new HashSet<FigureItemSO>();
    private readonly List<Sprite> previewFigureSprites = new List<Sprite>();
    private readonly int[] previewDiceCounts = new int[7];

    public void Refresh(DiceManager dm)
    {
        CollectKept(dm);
        HandRank rank = dm.currentHandRank;
        if (!dm.isCalculating)
        {
            rank = uiValuesBuffer.Count > 0 ? TurnCalculator.CalculateHand(uiValuesBuffer, dm, out _) : HandRank.HighCard;
            dm.currentHandRank = rank;
            dm.currentHandName = uiValuesBuffer.Count > 0 ? LocalizationManager.GetHandDisplayName(rank) : "";
            RefreshScorePreview(dm);
        }

        RefreshFigures(dm, rank);
        string bName = dm.currentBiome != null ? LocalizationManager.GetBiomeDisplayName(dm.currentBiome.biomeType) : "Stage";
        string stageDisplayName = $"{bName} {dm.currentStage}";
        int remainingRerolls = (dm.maxRerolls + dm.snackBonusRerolls + dm.figureBonusRerolls) - dm.currentRerolls;
        int remainingFinishes = dm.isCalculating ? 0 : 1;
        // 끝내기를 누른 후(결산 중): 시퀀스 코루틴이 각 텍스트를 개별 제어하므로 건드리지 않음
        dm.ui?.UpdateGameUI(stageDisplayName, dm.enemy != null ? dm.enemy.CurrentHP : 0, dm.enemy != null ? dm.enemy.MaxHP : 0, dm.currentPlayerHP, dm.playerMaxHP, remainingRerolls, "", "", previewFigureSprites, remainingFinishes);
    }

    private void CollectKept(DiceManager dm)
    {
        //매 틱마다 List를 새로 만들지 않고, 고정된 버퍼를 비우고 다시 채워 메모리 낭비 차단
        uiDiceBuffer.Clear();
        uiValuesBuffer.Clear();
        foreach (var d in dm.activeDiceList)
        {
            if (d != null && d.gameObject.activeInHierarchy && d.isKept)
            {
                uiDiceBuffer.Add(d);
                uiValuesBuffer.Add(d.currentValue);
            }
        }

    }

    private void RefreshScorePreview(DiceManager dm)
    {
        if (dm.ui == null) return;
        // 순수 연산기를 통한 통합 연산 호출
        float healMultUI = FigureEffectManager.Instance != null ? FigureEffectManager.Instance.GetHealMultiplier() : 1f;
        int simEnemyHP = dm.enemy != null ? dm.enemy.CurrentHP : 0;
        TurnCalcResult calcResult = TurnCalculator.CalculateDiceEffects(uiDiceBuffer, simEnemyHP, healMultUI);
        // 다크 데미지는 피규어 이후 계산을 시뮬레이션하기 위해 따로 빼서 수동 계산
        int darkDamageTotal = TurnCalculator.CalculateDarkDamage(uiDiceBuffer, simEnemyHP);
        // 기존 미리보기 정책 유지. 실제 정산 보너스의 적용 순서/계산은 변경하지 않습니다.
        int chips = calcResult.baseSum + calcResult.iceBonusChips + calcResult.satelliteBonusChips + dm.stageBonusChips;
        float mult = 1f + dm.stageBonusMult + dm.permanentFigureMultiplier;
        dm.ui.ShowTurnPreview(dm.currentHandName, chips, mult, calcResult.iceBonusChips, calcResult.satelliteBonusChips, darkDamageTotal);
    }

    private void RefreshFigures(DiceManager dm, HandRank rank)
    {
        // 피규어 발동 실시간 시뮬레이션
        previewFigures.Clear();
        previewFigureSprites.Clear();
        List<Sprite> activeFigureSprites = previewFigureSprites; //피규어 아이콘 담을 리스트

        if (uiValuesBuffer.Count == 5 && InventoryManager.Instance != null) // 5개가 모였을 때만 피규어 발동 검사
        {
            System.Array.Clear(previewDiceCounts, 0, previewDiceCounts.Length);
            int[] diceCounts = previewDiceCounts;
            foreach (int v in uiValuesBuffer)
            {
                if (v >= 0 && v <= 6)
                {
                    diceCounts[v]++;
                }
            }

            foreach (var figure in InventoryManager.Instance.ownedFigures)
            {
                if (figure == null || figure.figureNodes == null) continue;
                bool isTriggered = false;

                for (int nodeIndex = 0; nodeIndex < figure.figureNodes.Count; nodeIndex++)
                {
                    var node = figure.figureNodes[nodeIndex];
                    if (node == null || node.effects == null || node.effects.Count == 0) continue;
                    if (node.requiredKills > 0 && (!dm.figureKillCounts.TryGetValue(figure.itemName, out int kills) || kills < node.requiredKills)) continue;
                    if (node.oncePerStage && dm.stageContext.usedFigureNodes.Contains($"{figure.itemName}:{nodeIndex}")) continue;
                    bool nodeTriggered = false;
                    switch (node.triggerType)
                    {
                        case FigureTriggerType.ThreeOf1: if (diceCounts[1] >= 3) nodeTriggered = true; break;
                        case FigureTriggerType.ThreeOf2: if (diceCounts[2] >= 3) nodeTriggered = true; break;
                        case FigureTriggerType.ThreeOf3: if (diceCounts[3] >= 3) nodeTriggered = true; break;
                        case FigureTriggerType.ThreeOf4: if (diceCounts[4] >= 3) nodeTriggered = true; break;
                        case FigureTriggerType.ThreeOf5: if (diceCounts[5] >= 3) nodeTriggered = true; break;
                        case FigureTriggerType.ThreeOf6: if (diceCounts[6] >= 3) nodeTriggered = true; break;
                        case FigureTriggerType.OnePair:
                        case FigureTriggerType.TwoPair:
                        case FigureTriggerType.Triple:
                        case FigureTriggerType.Straight:
                        case FigureTriggerType.FullHouse:
                        case FigureTriggerType.FourOfAKind:
                        case FigureTriggerType.Yacht:
                            if (HandRankUtil.MatchesTrigger(rank, node.triggerType)) nodeTriggered = true;
                            break;
                    }

                    if (nodeTriggered)
                    {
                        isTriggered = true;
                        break; // 표시 여부만 필요하므로 첫 일치 노드에서 종료
                    }
                }

                if (isTriggered)
                {
                    if (previewFigures.Add(figure))
                    {
                        activeFigureSprites.Add(figure.icon); //발동된 피규어의 아이콘 저장
                    }
                }
            }
        }

        FigureFeedback.ApplyPreview(previewFigures);

    }
}
