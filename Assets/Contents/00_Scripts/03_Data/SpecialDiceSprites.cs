using UnityEngine;

// 세이브 데이터에 스프라이트를 넣지 않고, 표시할 때 specialEffect(또는 구세이브 면 구성)로 찾는다.
public static class SpecialDiceSprites
{
    public const string ResourceName = "SpecialDiceSpriteTable";

    static SpecialDiceSpriteTable cached;

    static SpecialDiceSpriteTable Table
    {
        get
        {
            if (cached == null)
                cached = Resources.Load<SpecialDiceSpriteTable>(ResourceName);
            return cached;
        }
    }

    public static bool TryGetSpecialFaceSprite(DiceData1 data, int faceIndexOrValue, out Sprite sprite)
    {
        if (data == null)
        {
            sprite = null;
            return false;
        }
        return TryGetSpecialFaceSprite(data.specialEffect, data.faceValues, data.customFaceSprites, faceIndexOrValue, out sprite);
    }

    public static bool TryGetSpecialFaceSprite(DiceItemSO so, int faceIndexOrValue, out Sprite sprite)
    {
        if (so == null)
        {
            sprite = null;
            return false;
        }
        return TryGetSpecialFaceSprite(so.specialEffect, so.customFaces, so.customFaceSprites, faceIndexOrValue, out sprite);
    }

    public static bool TryGetSpecialFaceSprite(
        SpecialDieEffect specialEffect,
        int[] faceValues,
        Sprite[] customFaceSprites,
        int faceIndexOrValue,
        out Sprite sprite)
    {
        sprite = null;

        // 1. 커스텀 면 스프라이트가 해당 칸에 있으면 그걸 쓴다(여정·88·가짜).
        if (TryGetCustomSprite(customFaceSprites, faceIndexOrValue, out sprite))
            return true;

        // 2. 홀수·짝수 전용 테이블
        SpecialDieEffect kind = ResolveOddEven(specialEffect, faceValues);
        Sprite[] table = GetTable(kind);
        if (table == null || table.Length < 6)
            return false;

        int index = Mathf.Clamp(faceIndexOrValue, 0, 5);
        sprite = table[index];
        return sprite != null;
    }

    public static bool IsOddEven(DiceData1 data)
    {
        return data != null && ResolveOddEven(data.specialEffect, data.faceValues) != SpecialDieEffect.None;
    }

    public static bool IsOddEven(DiceItemSO so)
    {
        return so != null && ResolveOddEven(so.specialEffect, so.customFaces) != SpecialDieEffect.None;
    }

    // 상점 SO에 specialEffect가 없던 구세이브도 1·3·5 / 2·4·6 구성이면 전용 이미지를 쓴다.
    public static SpecialDieEffect ResolveOddEven(SpecialDieEffect specialEffect, int[] faceValues)
    {
        if (specialEffect == SpecialDieEffect.Odd || specialEffect == SpecialDieEffect.Even)
            return specialEffect;
        if (MatchesUniqueFaces(faceValues, 1, 3, 5))
            return SpecialDieEffect.Odd;
        if (MatchesUniqueFaces(faceValues, 2, 4, 6))
            return SpecialDieEffect.Even;
        return SpecialDieEffect.None;
    }

    static Sprite[] GetTable(SpecialDieEffect kind)
    {
        SpecialDiceSpriteTable table = Table;
        if (table == null) return null;
        if (kind == SpecialDieEffect.Odd) return table.oddFaces;
        if (kind == SpecialDieEffect.Even) return table.evenFaces;
        return null;
    }

    static bool TryGetCustomSprite(Sprite[] customFaceSprites, int faceIndexOrValue, out Sprite sprite)
    {
        sprite = null;
        if (customFaceSprites == null || customFaceSprites.Length == 0)
            return false;

        int index = customFaceSprites.Length == 1
            ? 0
            : Mathf.Clamp(faceIndexOrValue, 0, customFaceSprites.Length - 1);
        sprite = customFaceSprites[index];
        return sprite != null;
    }

    static bool MatchesUniqueFaces(int[] faces, int a, int b, int c)
    {
        if (faces == null || faces.Length == 0) return false;
        bool hasA = false, hasB = false, hasC = false;
        for (int i = 0; i < faces.Length; i++)
        {
            int value = faces[i];
            if (value == a) hasA = true;
            else if (value == b) hasB = true;
            else if (value == c) hasC = true;
            else return false;
        }
        return hasA && hasB && hasC;
    }
}
