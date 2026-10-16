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

    // --- 가짜 주사위 (보스 기믹) 관리 ---
    public void ApplyFakeDice(Sprite fakeShell, Sprite fakeFace, ref DiceData1 originalBossDice, ref int fakeDiceIndex, int restoredIndex = -1)
    {
        if (masterDeck.Count == 0 || restoredIndex >= masterDeck.Count) return;

        // 여정 주사위는 가짜 주사위로 바뀌지 않는다. 저장된 위치가 여정 주사위면 다른 주사위를 다시 고른다.
        int pickedIndex = restoredIndex;
        if (pickedIndex < 0 || IsProtected(masterDeck[pickedIndex]))
        {
            var candidates = new List<int>(masterDeck.Count);
            for (int i = 0; i < masterDeck.Count; i++) if (!IsProtected(masterDeck[i])) candidates.Add(i);
            if (candidates.Count == 0) return;
            pickedIndex = candidates[Random.Range(0, candidates.Count)];
        }

        fakeDiceIndex = pickedIndex;
        originalBossDice = masterDeck[fakeDiceIndex];

        DiceData1 fakeDice = new DiceData1("가짜 주사위", new int[] { 0, 0, 0, 0, 0, 0 });
        fakeDice.customDiceShell = fakeShell;
        fakeDice.customFaceSprites = new Sprite[] { fakeFace, fakeFace, fakeFace, fakeFace, fakeFace, fakeFace };

        fakeDice.isCoated = false;
        fakeDice.type = DiceType.Normal;
        fakeDice.specialEffect = SpecialDieEffect.None;
        fakeDice.diceColor = Color.white;
        fakeDice.multiplier = 1.0f;

        masterDeck[fakeDiceIndex] = fakeDice;
        Debug.Log($"<color=red>[보스 기믹]</color> {originalBossDice.diceName}이(가) 가짜 주사위로 변했습니다!");
    }

    public void RestoreFakeDice(ref DiceData1 originalBossDice, ref int fakeDiceIndex)
    {
        if (originalBossDice == null || fakeDiceIndex < 0) return;

        // 인덱스를 신뢰할 수 없는 상황(덱 개수 변경)을 대비한 방어 코드
        if (fakeDiceIndex < masterDeck.Count)
        {
            masterDeck[fakeDiceIndex] = originalBossDice;
        }
        else
        {
            // 인덱스가 밀렸다면, 맨 뒤에라도 안전하게 원본을 다시 넣어줌
            masterDeck.Add(originalBossDice);
        }

        // 복구 완료 후 참조 비우기
        originalBossDice = null;
        fakeDiceIndex = -1;
    }
}