using System.Collections.Generic;
using UnityEngine;

public enum SpecialDieEffect
{
    None,
    Coin,
    Heart,
    Odd,
    Even,
    Flame,
    Thorn
}

// 기존 특수 면 효과(여정 주사위의 Thorn 등)와 별도로 구분한다.
// 저장 호환성을 위해 기존 값을 바꾸지 않고 새 종류는 뒤에 추가한다.
public enum DebuffDiceType
{
    None = 0,
    Fake = 1,    // 가짜: 영구, 눈 0
    Spine = 2,   // 가시: 영구, 킵 결산 시 피해
    Curse = 3,   // 저주: 영구, 킵 결산 시 배수 감소
    Thief = 4,   // 도둑: 해당 전투 한정, 뽑는 즉시 발동
    Shackle = 5  // 족쇄: 해당 전투 한정, 뽑는 즉시 발동
}

public enum SatelliteType
{
    Mercury, 
    Venus,  
    Mars,    
    Jupiter  
}

public enum DiceType
{
    Normal,
    Prism,
    Gold,
    Dark,
    Ice
}

[System.Serializable]
public class DiceData1
{
    //변수 선언부에 이름을 추가하고 기본값을 넣어줍니다.
    public string diceName = "기본 주사위";

    public bool isCoated = false;
    public float multiplier = 1f;
    public Color diceColor = Color.white;
    public DiceType type = DiceType.Normal;

    public SpecialDieEffect specialEffect = SpecialDieEffect.None;

    public DebuffDiceType debuffType = DebuffDiceType.None;

    public bool IsDebuff => debuffType != DebuffDiceType.None;
    public bool IsTemporaryDebuff => debuffType == DebuffDiceType.Thief || debuffType == DebuffDiceType.Shackle;
    // 발동 시점 판별용. 실제 효과 적용은 드로우 처리에서 연결한다.
    public bool TriggersDebuffOnDraw => debuffType == DebuffDiceType.Thief || debuffType == DebuffDiceType.Shackle;

    // 면마다 다른 효과가 필요한 주사위용(길이 6, null 허용). 없으면 specialEffect를 모든 면에 쓴다.
    public SpecialDieEffect[] faceEffects;
    // 덱에 항상 1개 유지되는 여정 주사위 여부. 파괴·교체 대상에서 빠진다.
    public bool isJourneyDice = false;

    public Sprite customDiceShell;
    public Sprite[] customFaceSprites;
    public List<SatelliteType> activeSatellites = new List<SatelliteType>();//위성 리스트

    //public int minRoll = 1;
    //public int maxRoll = 6;
    public int[] faceValues = new int[6] { 1, 2, 3, 4, 5, 6 };

    public DiceData1()
    {
        diceName = "기본 주사위";
        isCoated = false;
        multiplier = 1f;
        diceColor = Color.white;
        type = DiceType.Normal;
        specialEffect = SpecialDieEffect.None;

        // 6개 면의 기본값을 1~6으로 설정
        faceValues = new int[6] { 1, 2, 3, 4, 5, 6 };
    }

    // 특수 주사위 생성을 위한 생성자 (선택 사항)
    public DiceData1(string name, int[] faces)
    {
        this.diceName = name;
        this.faceValues = faces;
    }

    // 해당 면의 효과를 돌려준다. 면별 효과가 없으면 기존 주사위 전체 효과를 그대로 쓴다.
    public SpecialDieEffect GetFaceEffect(int faceIndex)
    {
        if (faceEffects != null && faceIndex >= 0 && faceIndex < faceEffects.Length)
            return faceEffects[faceIndex];
        return specialEffect;
    }

    // 눈 값으로 면 번호를 찾는다. 면 번호를 모르는 경로(복원 등)에서 첫 번째로 같은 눈의 면을 쓴다.
    public int FindFaceIndex(int value)
    {
        if (faceValues == null) return -1;
        for (int i = 0; i < faceValues.Length; i++)
        {
            if (faceValues[i] == value) return i;
        }
        return -1;
    }
}

// 지도에서 여정 주사위를 굴린 결과가 어떤 종류인지
public enum JourneyOutcome
{
    Move,   // 면 효과 없음: 눈 값만큼 이동
    Damage, // 가시: 체력 감소 후 다시 굴림
    Heal,   // 하트: 체력 회복 후 다시 굴림
    Gold    // 코인: 골드 획득 후 다시 굴림
}

// 굴림 결과 한 건. 굴리기 자체는 부작용이 없고, 적용은 DiceManager.ApplyJourneyResult가 한다.
public struct JourneyRollResult
{
    public int faceIndex;      // 나온 면 번호(0~5)
    public int pips;           // 나온 눈 값
    public SpecialDieEffect effect;
    public JourneyOutcome outcome;
    public int amount;         // 이동 칸 수 / 피해량 / 회복량 / 골드량
    public Sprite faceSprite;  // 결과 슬롯에 보여 줄 면 스프라이트(없으면 null)
}

// 여정 주사위의 기본 면 구성과 생성/검색 도우미. 덱에서 못 찾을 때도 같은 구성으로 굴린다.
public static class JourneyDice
{
    // Dice_Journey 에셋의 itemName과 같아야 저장 복원이 이름으로 찾는다.
    public const string DefaultName = "Journey";

    public static readonly int[] DefaultFaceValues = { 1, 2, 3, 4, 5, 6 };
    public static readonly SpecialDieEffect[] DefaultFaceEffects =
    {
        SpecialDieEffect.None, SpecialDieEffect.None, SpecialDieEffect.None,
        SpecialDieEffect.Thorn, SpecialDieEffect.Heart, SpecialDieEffect.Coin
    };

    // 에셋(DiceItemSO)을 못 찾을 때 쓰는 코드 기본형. 스프라이트가 없어 기본 눈금 이미지로 보인다.
    public static DiceData1 CreateDefault()
    {
        DiceData1 die = new DiceData1(DefaultName, (int[])DefaultFaceValues.Clone());
        die.faceEffects = (SpecialDieEffect[])DefaultFaceEffects.Clone();
        die.isJourneyDice = true;
        return die;
    }

    // 에셋이 있으면 에셋 기준으로, 없으면 코드 기본형으로 새 여정 주사위를 만든다.
    public static DiceData1 Create(DiceItemSO source)
    {
        return source != null ? source.CreateDiceData() : CreateDefault();
    }

    // 세이브 매니저의 아이템 목록에서 여정 주사위 플래그가 켜진 에셋을 찾는다. 등록이 안 된 씬이면 null.
    public static DiceItemSO FindSource()
    {
        GameSaveManager save = GameSaveManager.Instance;
        if (save == null || save.masterItemDatabase == null) return null;
        for (int i = 0; i < save.masterItemDatabase.Count; i++)
        {
            if (save.masterItemDatabase[i] is DiceItemSO dice && dice.isJourneyDice)
                return dice;
        }
        return null;
    }

    public static DiceData1 Find(List<DiceData1> deck)
    {
        if (deck == null) return null;
        for (int i = 0; i < deck.Count; i++)
        {
            if (deck[i] != null && deck[i].isJourneyDice) return deck[i];
        }
        return null;
    }

    // 면 번호 0~5 중 하나를 고른다. 눈 값 배열이 비정상이면 기본 구성으로 대신한다.
    public static int RollFaceIndex(DiceData1 die)
    {
        int count = die != null && die.faceValues != null && die.faceValues.Length > 0 ? die.faceValues.Length : DefaultFaceValues.Length;
        return Random.Range(0, count);
    }
}
