using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[System.Serializable]
public class DeckManager
{
    public List<DiceData1> masterDeck = new List<DiceData1>();
    public List<DiceData1> drawPile = new List<DiceData1>();
    public List<DiceData1> discardPile = new List<DiceData1>();

    public void InitializeMasterDeck()
    {
        masterDeck.Clear();
        for (int i = 0; i < 12; i++) masterDeck.Add(new DiceData1()); // 주사위 12개
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

    public List<DiceData1> GetRandomDiceForCoating(int count)
    {
        return masterDeck.OrderBy(x => Random.value).Take(count).ToList();
    }

    // --- 가짜 주사위 (보스 기믹) 관리 ---
    public void ApplyFakeDice(Sprite fakeShell, Sprite fakeFace, ref DiceData1 originalBossDice, ref int fakeDiceIndex)
    {
        if (masterDeck.Count == 0) return;

        fakeDiceIndex = Random.Range(0, masterDeck.Count);
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