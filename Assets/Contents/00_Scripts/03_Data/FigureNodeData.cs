using UnityEngine;
using System.Collections.Generic;

public enum FigureCategory { None = 0, HandEffect = 1, DiceFaceEffect = 2, HPDefense = 3, OneTime = 4, ShopPurchase = 5, RerollEffect = 6, CombatEnd = 7, Special = 8 }

public enum FigureTriggerType
{
    None,
    OnePair, TwoPair, Triple, Straight, FullHouse, FourOfAKind, Yacht,
    ThreeOf1, ThreeOf2, ThreeOf3, ThreeOf4, ThreeOf5, ThreeOf6,
    OnDamaged, OnShopEntered, OnItemPurchased, OnDiceReroll, OnCombatEnd, OnSnackUsed, OnAcquired, Always
}

// 1. 순수한 행동(Action)만 남긴 효과 타입
//16종 원인(Trigger)
public enum FigureTriggerType
{
    None,
    ThreeOf1, ThreeOf2, ThreeOf3, ThreeOf4, ThreeOf5, ThreeOf6, // T-01 ~ T-06, 주사위 3개 숫자가 같을 때
    OnePair, TwoPair, Triple, Straight, FullHouse, FourOfAKind, Yacht, // T-07 ~ T-13 족보 처리
    OnSnackUsed, // T-14 스낵 먹었을 때
    OnHPLost,    // T-15 hp 차감시
    Passive      // T-16
}

//12종 보상(Effect)
public enum FigureEffectType
{
    None, HealHP, AddGold, AddMultiplier, AddChips, DamageEnemy, AddReroll, ReduceDamageTaken, GetSnack, DestroySelf,
    MultiplyCombatEndGold, IncreaseMaxHP, AddExtraAttack, NullifyEnemySkill, FixEnemyAttackToOne, DestroyDebuffDice,
    OpenTicketSelection, OpenCoatingSelection, OpenSatelliteSelection
}

// 2.값 계산 방식 분리
public enum EffectCalcType
{
    Flat,            // 고정 수치 (기본값)
    MissingHP,       // 잃은 체력 비례 (%)
    EnemyHP,         // 적 남은 체력 비례 (%)
    CurrentChips,    // 이번 턴에 결산된 칩수 비례 (예: 1이면 100%, 0.5면 50%)
    OwnedFigures     // 내 보유 피규어 개수 비례 (아룡의 알 같은 케이스)
}

// 보상 노드 데이터 구조
[System.Serializable]
public struct FigureEffectNode
{
    public FigureEffectType effectType;
    public EffectCalcType calcType;     // [추가됨] 계산 방식
    public float effectValue;           // 고정값이거나 비율(%)
    public float probability;           // 발동 확률 (0이면 100% 발동)
    public BaseItemDataSO optionalItem;
}

    public float effectValue; // 에디터에서 조절할 수치 필드
    public BaseItemDataSO optionalItem; // 특정 스낵 지급 등 아이템 연동용
}

//원인 노드 데이터 구조 (1개의 원인에 복수의 보상 연결)
[System.Serializable]
public class FigureNode
{
    public FigureTriggerType triggerType;
    public List<FigureEffectNode> effects = new List<FigureEffectNode>();
}