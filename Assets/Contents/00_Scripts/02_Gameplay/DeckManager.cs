using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[System.Serializable]
public class DeckManager
{
    public List<DiceData1> masterDeck = new List<DiceData1>();
    public List<DiceData1> drawPile = new List<DiceData1>();
    public List<DiceData1> discardPile = new List<DiceData1>();

    // 시작 덱: 기본 주사위 11개 + 여정 주사위 1개 = 12개. 에셋을 못 찾으면 코드 기본 면 구성으로 만든다.
    public void InitializeMasterDeck(DiceItemSO journeySource = null)
    {
        masterDeck.Clear();
        masterDeck.Add(JourneyDice.Create(journeySource));
        for (int i = 0; i < 11; i++) masterDeck.Add(new DiceData1());
    }

    // 덱에 여정 주사위가 없으면(구버전 저장 등) 기본 주사위 1개를 여정 주사위로 바꾼다. 바꿨으면 true.
    // 기본 주사위가 하나도 없는 예외 상황에서만 덱 끝에 추가한다.
    public bool EnsureJourneyDice(DiceItemSO journeySource)
    {
        if (JourneyDice.Find(masterDeck) != null) return false;

        int target = -1;
        // 강화가 없는 기본 주사위를 먼저 바꿔 플레이어가 키운 주사위를 잃지 않게 한다.
        for (int i = 0; i < masterDeck.Count && target < 0; i++)
        {
            DiceData1 d = masterDeck[i];
            if (d != null && d.diceName == "기본 주사위" && !d.isCoated && (d.activeSatellites == null || d.activeSatellites.Count == 0)) target = i;
        }
        for (int i = 0; i < masterDeck.Count && target < 0; i++)
        {
            if (masterDeck[i] != null && masterDeck[i].diceName == "기본 주사위") target = i;
        }

        DiceData1 journey = JourneyDice.Create(journeySource);
        if (target < 0)
        {
            masterDeck.Add(journey);
            return true;
        }

        // 바꾸는 주사위에 붙어 있던 코팅·위성은 그대로 옮긴다.
        DiceData1 old = masterDeck[target];
        journey.isCoated = old.isCoated;
        journey.type = old.type;
        journey.multiplier = old.multiplier;
        journey.diceColor = old.diceColor;
        if (old.activeSatellites != null) journey.activeSatellites = new List<SatelliteType>(old.activeSatellites);

        masterDeck[target] = journey;
        for (int i = 0; i < drawPile.Count; i++) if (ReferenceEquals(drawPile[i], old)) drawPile[i] = journey;
        for (int i = 0; i < discardPile.Count; i++) if (ReferenceEquals(discardPile[i], old)) discardPile[i] = journey;
        return true;
    }

    // 파괴·제거·변환할 수 없는 주사위인지 여부. 여정 주사위가 항상 1개 유지되도록 모든 삭제 경로가 이 값을 본다.
    public static bool IsProtected(DiceData1 die) => die != null && die.isJourneyDice;

    // 파괴·제거 대상으로 보여 줄 수 있는 주사위 수
    public int RemovableCount()
    {
        int count = 0;
        for (int i = 0; i < masterDeck.Count; i++) if (!IsProtected(masterDeck[i])) count++;
        return count;
    }

    public void PrepareDeckForNewStage()
    {
        drawPile = new List<DiceData1>(masterDeck);
        discardPile.Clear();
        ShufflePile(drawPile);
    }

    public void ShufflePile(List<DiceData1> pile)
    {
        for (int i = 0; i < pile.Count; i++)
        {
            int rnd = Random.Range(i, pile.Count);
            var temp = pile[i];
            pile[i] = pile[rnd];
            pile[rnd] = temp;
        }
    }

    // 드로우하기 전 덱이 부족하면 묘지를 섞어 채우는 로직
    public void CheckAndRefillDrawPile(int minimumCount)
    {
        if (drawPile.Count < minimumCount && discardPile.Count > 0)
        {
            drawPile.AddRange(discardPile);
            discardPile.Clear();
            ShufflePile(drawPile);
        }
    }

    // 주사위 하나를 뽑고 즉시 버린 카드 더미(묘지)로 보내는 역할
    public DiceData1 DrawOneDice()
    {
        if (drawPile.Count == 0)
        {
            if (discardPile.Count > 0)
            {
                drawPile = new List<DiceData1>(discardPile);
                discardPile.Clear();
                ShufflePile(drawPile);
            }
            else return null;
        }

        DiceData1 drawnData = drawPile[0];
        drawPile.RemoveAt(0);
        return drawnData;
    }

    // 코팅·위성 선택용. 여정 주사위도 후보에 들어간다.
    public List<DiceData1> GetRandomDiceForCoating(int count)
    {
        return masterDeck.OrderBy(x => Random.value).Take(count).ToList();
    }

    // 파괴·희생으로 지울 주사위 선택용. 여정 주사위는 후보에서 뺀다.
    public List<DiceData1> GetRandomDiceForDestruction(int count)
    {
        return masterDeck.Where(x => !IsProtected(x)).OrderBy(x => Random.value).Take(count).ToList();
    }

    // 전투 시작 시 PrepareDeckForNewStage 전에 호출한다.
    // 기존 주사위를 교체하지 않고 원본 덱에 추가하며, 드로우 더미는 이후 준비 단계에서 구성한다.
    // SO가 있으면 해당 설정을 사용하고, 없으면 종류별 기본 구성으로 생성한다.
    public DiceData1 AddDebuffDice(DebuffDiceType debuffType, DiceItemSO source = null)
    {
        if (source != null)
        {
            if (debuffType == DebuffDiceType.None || source.debuffType != debuffType)
                throw new System.ArgumentException("디버프 종류와 SO 설정이 일치해야 합니다.", nameof(source));
            DiceData1 fromAsset = source.CreateDiceData();
            masterDeck.Add(fromAsset);
            return fromAsset;
        }
        string name;
        int[] faces;
        switch (debuffType)
        {
            case DebuffDiceType.Fake:
                name = "가짜 주사위";
                faces = new int[] { 0, 0, 0, 0, 0, 0 };
                break;
            case DebuffDiceType.Spine:
                name = "가시 주사위";
                faces = new int[] { 1, 2, 3, 4, 5, 6 };
                break;
            case DebuffDiceType.Curse:
                name = "저주 주사위";
                faces = new int[] { 1, 2, 3, 4, 5, 6 };
                break;
            case DebuffDiceType.Thief:
                name = "도둑 주사위";
                faces = new int[] { 1, 1, 1, 1, 1, 1 };
                break;
            case DebuffDiceType.Shackle:
                name = "족쇄 주사위";
                faces = new int[] { 1, 1, 1, 1, 1, 1 };
                break;
            default:
                throw new System.ArgumentOutOfRangeException(nameof(debuffType), debuffType, "추가할 디버프 종류가 필요합니다.");
        }

        DiceData1 die = new DiceData1(name, faces);
        die.debuffType = debuffType;
        masterDeck.Add(die);
        return die;
    }

    // 전투 종료 시 호출한다. 영구 디버프와 일반 주사위는 유지한다.
    // 보드에 남은 주사위 오브젝트 정리는 호출 측에서 처리해야 한다.
    public int RemoveTemporaryDebuffDice()
    {
        return RemoveDebuffDiceMatching(die => die != null && die.IsTemporaryDebuff);
    }

    // 디버프 제거 피규어 등에서 사용할 전체 디버프 제거 경로.
    public int RemoveAllDebuffDice()
    {
        return RemoveDebuffDiceMatching(die => die != null && die.IsDebuff);
    }

    private int RemoveDebuffDiceMatching(System.Predicate<DiceData1> match)
    {
        int removed = masterDeck.RemoveAll(match);
        drawPile.RemoveAll(match);
        discardPile.RemoveAll(match);
        return removed; // 원본 덱에서 제거한 개수만 반환한다.
    }

}
