using UnityEngine;
using System.Collections.Generic;

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
    HealHP,        
    AddGold,       
    GetSnack,      
    DamageEnemy,   
    AddMultiplier,  
    AddChips,     
    AddReroll
}

// 보상 노드 데이터 구조
[System.Serializable]
public struct FigureEffectNode
{
    public FigureEffectType effectType;
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