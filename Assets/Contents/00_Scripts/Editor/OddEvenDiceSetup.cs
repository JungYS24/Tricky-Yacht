#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 홀수·짝수 전용 스프라이트 테이블을 만들고, 상점 SO에서 JustDice 숫자 스프라이트를 뺀다.
// 재실행해도 값이 다를 때만 쓴다.
public static class OddEvenDiceSetup
{
    const string TablePath = "Assets/Resources/SpecialDiceSpriteTable.asset";
    const string OddSoPath = "Assets/Contents/10_Resources/Data/DiceData/DiceData/odd number.asset";
    const string EvenSoPath = "Assets/Contents/10_Resources/Data/DiceData/DiceData/Even Number.asset";

    static readonly string[] OddSpritePaths =
    {
        "Assets/Contents/02_Sprites/04_Dice/OddDice/Dice_Odd_1.png",
        "Assets/Contents/02_Sprites/04_Dice/OddDice/Dice_Odd_3.png",
        "Assets/Contents/02_Sprites/04_Dice/OddDice/Dice_Odd_5.png"
    };

    static readonly string[] EvenSpritePaths =
    {
        "Assets/Contents/02_Sprites/04_Dice/EvenDice/Dice_Even_2.png",
        "Assets/Contents/02_Sprites/04_Dice/EvenDice/Dice_Even_4.png",
        "Assets/Contents/02_Sprites/04_Dice/EvenDice/Dice_Even_6.png"
    };

    [MenuItem("Tools/Dice/Setup Odd Even Sprites")]
    public static void RunFromMenu()
    {
        RunBatch();
        Debug.Log("[OddEven] 전용 스프라이트 테이블과 상점 SO를 맞췄습니다.");
    }

    public static void RunBatch()
    {
        FixPixelImport(OddSpritePaths);
        FixPixelImport(EvenSpritePaths);

        Sprite odd1 = LoadSprite(OddSpritePaths[0]);
        Sprite odd3 = LoadSprite(OddSpritePaths[1]);
        Sprite odd5 = LoadSprite(OddSpritePaths[2]);
        Sprite even2 = LoadSprite(EvenSpritePaths[0]);
        Sprite even4 = LoadSprite(EvenSpritePaths[1]);
        Sprite even6 = LoadSprite(EvenSpritePaths[2]);
        if (odd1 == null || odd3 == null || odd5 == null || even2 == null || even4 == null || even6 == null)
            throw new InvalidOperationException("[OddEven] 전용 스프라이트를 찾지 못했습니다.");

        SpecialDiceSpriteTable table = AssetDatabase.LoadAssetAtPath<SpecialDiceSpriteTable>(TablePath);
        if (table == null)
        {
            table = ScriptableObject.CreateInstance<SpecialDiceSpriteTable>();
            AssetDatabase.CreateAsset(table, TablePath);
        }

        Sprite[] oddFaces = { odd1, odd1, odd3, odd3, odd5, odd5 };
        Sprite[] evenFaces = { even2, even2, even4, even4, even6, even6 };
        if (!SameSprites(table.oddFaces, oddFaces) || !SameSprites(table.evenFaces, evenFaces))
        {
            table.oddFaces = oddFaces;
            table.evenFaces = evenFaces;
            EditorUtility.SetDirty(table);
        }

        ApplyShopSo(OddSoPath, SpecialDieEffect.Odd, odd1);
        ApplyShopSo(EvenSoPath, SpecialDieEffect.Even, even2);

        LocalizationCsvImporter.ImportAll();
        VerifyCalc();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[OddEven] 배치 적용 완료.");
    }

    static void ApplyShopSo(string path, SpecialDieEffect effect, Sprite icon)
    {
        DiceItemSO so = AssetDatabase.LoadAssetAtPath<DiceItemSO>(path);
        if (so == null)
            throw new InvalidOperationException("[OddEven] 상점 SO를 찾지 못했습니다: " + path);

        bool dirty = false;
        if (so.specialEffect != effect)
        {
            so.specialEffect = effect;
            dirty = true;
        }
        if (so.customDiceShell != null)
        {
            so.customDiceShell = null;
            dirty = true;
        }
        if (so.customFaceSprites != null && so.customFaceSprites.Length > 0)
        {
            so.customFaceSprites = null;
            dirty = true;
        }
        if (so.icon != icon)
        {
            so.icon = icon;
            dirty = true;
        }
        if (dirty)
            EditorUtility.SetDirty(so);
    }

    static void FixPixelImport(string[] paths)
    {
        foreach (string path in paths)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;
            bool dirty = false;
            if (importer.filterMode != FilterMode.Point)
            {
                importer.filterMode = FilterMode.Point;
                dirty = true;
            }
            if (importer.textureCompression != TextureImporterCompression.Uncompressed)
            {
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                dirty = true;
            }
            if (dirty)
                importer.SaveAndReimport();
        }
    }

    static Sprite LoadSprite(string path)
    {
        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        if (assets == null) return null;
        Sprite best = null;
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is Sprite sprite)
            {
                if (best == null || sprite.rect.width * sprite.rect.height > best.rect.width * best.rect.height)
                    best = sprite;
            }
        }
        return best;
    }

    static bool SameSprites(Sprite[] current, Sprite[] expected)
    {
        if (current == null || current.Length != expected.Length) return false;
        for (int i = 0; i < expected.Length; i++)
        {
            if (current[i] != expected[i]) return false;
        }
        return true;
    }

    static void VerifyCalc()
    {
        List<Dice> oddTwo = MakeDice(2, SpecialDieEffect.Odd, 1, new int[] { 1, 1, 3, 3, 5, 5 });
        oddTwo.AddRange(MakeDice(3, SpecialDieEffect.None, 4, new int[] { 1, 2, 3, 4, 5, 6 }));
        TurnCalcResult oddResult = TurnCalculator.CalculateDiceEffects(oddTwo, 0, 1f);
        if (oddResult.oddBonusChips != 10)
            throw new InvalidOperationException("[OddEven] 홀수 2개 칩 +10 검증 실패: " + oddResult.oddBonusChips);

        List<Dice> evenTwo = MakeDice(2, SpecialDieEffect.Even, 2, new int[] { 2, 2, 4, 4, 6, 6 });
        TurnCalcResult evenResult = TurnCalculator.CalculateDiceEffects(evenTwo, 0, 1f);
        if (!Mathf.Approximately(evenResult.evenBonusMult, 0.4f))
            throw new InvalidOperationException("[OddEven] 짝수 2개 배수 +0.4 검증 실패: " + evenResult.evenBonusMult);

        List<Dice> mixed = MakeDice(1, SpecialDieEffect.Odd, 5, new int[] { 1, 1, 3, 3, 5, 5 });
        mixed.AddRange(MakeDice(1, SpecialDieEffect.Even, 6, new int[] { 2, 2, 4, 4, 6, 6 }));
        mixed.AddRange(MakeDice(1, SpecialDieEffect.None, 3, new int[] { 1, 2, 3, 4, 5, 6 }));
        TurnCalcResult mixedResult = TurnCalculator.CalculateDiceEffects(mixed, 0, 1f);
        if (mixedResult.oddBonusChips != 5 || !Mathf.Approximately(mixedResult.evenBonusMult, 0.2f))
            throw new InvalidOperationException("[OddEven] 혼합 검증 실패");

        List<Dice> zero = MakeDice(1, SpecialDieEffect.Odd, 0, new int[] { 1, 1, 3, 3, 5, 5 });
        TurnCalcResult zeroResult = TurnCalculator.CalculateDiceEffects(zero, 0, 1f);
        if (zeroResult.oddBonusChips != 0)
            throw new InvalidOperationException("[OddEven] 눈 0 홀수는 효과가 없어야 합니다.");

        List<Dice> unused = MakeDice(2, SpecialDieEffect.Odd, 1, new int[] { 1, 1, 3, 3, 5, 5 });
        TurnCalcResult unusedResult = TurnCalculator.CalculateDiceEffects(new List<Dice>(), 0, 1f);
        if (unusedResult.oddBonusChips != 0)
            throw new InvalidOperationException("[OddEven] 결산 밖 홀수는 효과가 없어야 합니다.");

        Debug.Log("[OddEven] 계산 검증 통과. 홀수2=+10칩, 짝수2=+0.4배수, 혼합=+5/+0.2, 미포함·눈0=0");
        DestroyDice(oddTwo);
        DestroyDice(evenTwo);
        DestroyDice(mixed);
        DestroyDice(zero);
        DestroyDice(unused);
    }

    static List<Dice> MakeDice(int count, SpecialDieEffect effect, int value, int[] faces)
    {
        var list = new List<Dice>(count);
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("OddEvenVerifyDie");
            go.AddComponent<SpriteRenderer>();
            Dice die = go.AddComponent<Dice>();
            die.myData = new DiceData1("verify", (int[])faces.Clone());
            die.myData.specialEffect = effect;
            die.currentValue = value;
            die.currentFaceIndex = die.myData.FindFaceIndex(value);
            list.Add(die);
        }
        return list;
    }

    static void DestroyDice(List<Dice> dice)
    {
        for (int i = 0; i < dice.Count; i++)
        {
            if (dice[i] != null)
                UnityEngine.Object.DestroyImmediate(dice[i].gameObject);
        }
    }
}
#endif
