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
    None,

    // --- [공용 기본 보상 (만능 부품)] ---
    HealHP,              // 현재 체력 회복 (EffectValue 만큼)
    AddGold,             // 상점 소지금(골드) 즉시 획득
    AddMultiplier,       // 이번 턴 결산 시 데미지 배수(x) 합연산 증가
    AddChips,            // 이번 턴 결산 시 기본 데미지 칩(+) 합연산 증가
    DamageEnemy,         // 몬스터에게 즉시 고정 피해 입힘 (EffectValue 만큼)
    AddReroll,           // 주사위 굴리기(리롤) 남은 횟수 추가
    ReduceDamageTaken,   // 몬스터에게 맞는 피해량 고정 수치 감소 (방어용)
    GetSnack,            // 지정된 스낵(optionalItem)을 인벤토리에 획득
    DestroySelf,         // 이 효과 발동 후 피규어 자신을 영구 파괴 (일회성 아이템용)

    // --- [특수 기믹 (주로 1번 족보 카테고리 전용)] ---
    MultiplyCombatEndGold, // [투탕카멘] 이번 전투 승리 시 얻는 기본 골드 보상 N배 뻥튀기
    IncreaseMaxHP,         // [얼음 수정] 최대 체력 상한치 자체를 영구적으로 증가시킴
    AddExtraAttack,        // [풍신의 북] 결산 데미지로 적을 때린 후, 똑같은 데미지로 N번 더 때림
    NullifyEnemySkill,     // [조련사의 모자] 적 보스의 특수 능력(가짜 주사위 등)을 이번 턴에 무력화
    FixEnemyAttackToOne,   // [검은 지느러미] 적이 다음 턴에 때릴 공격력 수치를 1로 고정시킴
    DestroyDebuffDice,     // [황금 발톱] 덱 안에 들어있는 방해용 가짜 주사위들을 전부 찾아서 파괴함

    // --- [UI 선택창 호출 (코루틴 대기 발생)] ---
    OpenTicketSelection,   // 무작위 티켓 3장 중 1장 선택하는 팝업창 띄우기
    OpenCoatingSelection,  // 주사위 코팅(속성 부여) 타겟 선택 팝업창 띄우기
    OpenSatelliteSelection // 주사위 위성(행성 효과) 타겟 선택 팝업창 띄우기
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
    public EffectCalcType calcType;     // 계산 방식
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