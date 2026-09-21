using UnityEngine;
using System.Collections.Generic;

public enum FigureCategory { None = 0, HandEffect = 1, DiceFaceEffect = 2, HPDefense = 3, OneTime = 4, ShopPurchase = 5, RerollEffect = 6, CombatEnd = 7, Special = 8 }

public enum FigureTriggerType
{
    None,
    OnePair, TwoPair, Triple, Straight, FullHouse, FourOfAKind, Yacht,
    ThreeOf1, ThreeOf2, ThreeOf3, ThreeOf4, ThreeOf5, ThreeOf6,
    OnDamaged, OnShopEntered, OnItemPurchased, OnDiceReroll, OnCombatEnd, OnSnackUsed,  Always,
    OnAcquired, OnCombatStart, OnRoundStart, OnDeath, OnLowHP, OnEnemyDamaged,OnFirstNormalAttack, OnDiceDestroyed
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
    AddShield,

    // --- [특수 기믹 (주로 1번 족보 카테고리 전용)] ---
    MultiplyCombatEndGold, // [투탕카멘] 이번 전투 승리 시 얻는 기본 골드 보상 N배 뻥튀기
    IncreaseMaxHP,         // [얼음 수정] 최대 체력 상한치 자체를 영구적으로 증가시킴
    AddExtraAttack,        // [풍신의 북] 결산 데미지로 적을 때린 후, 똑같은 데미지로 N번 더 때림
    NullifyEnemySkill,     // [조련사의 모자] 적 보스의 특수 능력(가짜 주사위 등)을 이번 턴에 무력화
    FixEnemyAttackToOne,   // [검은 지느러미] 적이 다음 턴에 때릴 공격력 수치를 1로 고정시킴
    DestroyDebuffDice,     // [황금 발톱] 덱 안에 들어있는 방해용 가짜 주사위들을 전부 찾아서 파괴함
    AddFlameDamage, // [지옥견 송곳니] 적에게 화상(Flame) 데미지 스택 추가

    //3번
    ReduceDamageTakenLowHP, // [안전모] 체력이 30% 이하일 때만 피해 감소
    IncreaseHealMultiplier,  // [도도새 모자] 받는 모든 체력 회복량 증가 (%)

    //[상점/구매 관련]
    DiscountCoating,       // 코팅 구매 비용 할인 (%)
    DiscountSatellite,     // 위성 구매 비용 할인 (%)
    DiscountShopReroll,    // 상점 리롤 비용 할인 (%)
    MakeRandomShopItemFree, // 상점 진입 시 무작위 아이템 1개 가격을 0으로 만듦

    // --- [UI 선택창 호출 (코루틴 대기 발생)] ---
    OpenTicketSelection,   // 무작위 티켓 3장 중 1장 선택하는 팝업창 띄우기
    OpenCoatingSelection,  // 주사위 코팅(속성 부여) 타겟 선택 팝업창 띄우기
    OpenSatelliteSelection, // 주사위 위성(행성 효과) 타겟 선택 팝업창 띄우기

    AddCombatMultiplier, // 이번 전투 동안 족보 배수 누적 (예: 광대의 눈물)
    AddCombatChips,     // 이번 전투 동안 기본 칩수 누적

    PreserveSnackChance, // 스낵 사용 시 소모하지 않을 확률(%)
    MultiplySnackEffects, // 페퍼민트 외 스낵 효과 배율
    IncreaseGoldGainPercent, // 모든 골드 획득량 증가(%)
    ReduceEnemyMaxHP, // 적 최대 체력 감소
    AddIceMultiplier, // 아이스 코팅 주사위의 추가 배수
    AddIceChips,       // 아이스 코팅 주사위의 추가 칩

    DamageEnemyOrPlayer,   // 50%로 적 피해, 나머지 50%로 플레이어 피해
    AddPermanentMultiplier // 현재 회차 동안 유지되는 배수 추가
}

// 2.값 계산 방식 분리
public enum EffectCalcType
{
    Flat,            // 고정 수치 (기본값)
    MissingHP,       // 잃은 체력 비례 (%)
    EnemyHP,         // 적 남은 체력 비례 (%)
    CurrentChips,    // 이번 턴에 결산된 칩수 비례 (예: 1이면 100%, 0.5면 50%)
    OwnedFigures,    // 내 보유 피규어 개수 비례 (아룡의 알 같은 케이스)
    PlayerMaxHP,   //내 현재 최대 체력 비례 (예: 바나나 왕관 10%)
    IncomingDamage,   //적이 때리려던 기본 데미지 비례 (퍼센트 뎀감용)
    DeckDiceCount,    //현재 덱(masterDeck)에 있는 주사위 총 개수 비례 (조개껍질용)
    SnackCount,       //인벤토리에 남아있는 스낵 개수 비례 (맹그로브 버섯용)
    CurrentGold,     // 현재 소지한 골드 비례 (황금 해골용)
    EnemyMaxHP,       // 몬스터의 최대 체력 비례 (소용돌이 트로피용)
    ActualDamage // 실제로 감소한 적 체력 비례(%)

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
    // DamageEnemyOrPlayer에서 플레이어가 받을 피해량
    public float secondaryEffectValue;
}




//원인 노드 데이터 구조 (1개의 원인에 복수의 보상 연결)
[System.Serializable]
public class FigureNode
{
    public FigureTriggerType triggerType;
    public List<FigureEffectNode> effects = new List<FigureEffectNode>();
    // 활성화하면 같은 스테이지에서 이 노드는 한 번만 실행
    public bool oncePerStage = false;
    // 이 피규어 획득 이후 필요한 적 처치 수. 0이면 제한 없음
    public int requiredKills = 0;

    // OnLowHP에서 사용. 예: 15이면 최대 체력의 15% 이하
    [Range(0f, 100f)]
    public float healthThresholdPercent = 15f;
}