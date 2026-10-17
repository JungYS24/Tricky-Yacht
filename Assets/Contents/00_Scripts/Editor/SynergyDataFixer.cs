#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 시너지 대표 아이콘을 피규어 스프라이트와 같은 에셋으로 연결한다.
/// 시너지 YAML은 icon 한 줄만 고친다. skillEffect 등 나머지 필드는 유지한다.
/// </summary>
public static class SynergyDataFixer
{
    const string SynergyFolder = "Assets/Contents/05_DataSO/Synergy";

    // 시너지 Item_ID, 피규어 Item_ID, 시트 Fig 번호
    static readonly (string synergyId, string figureId, string sheetId)[] RepresentativeIcons =
    {
        ("SYN_PHOENIX_BROTHERHOOD", "Fig_PhoenixFeather", "Fig_048"),
        ("SYN_SHIELD_BROTHERS", "Fig_SandShield", "Fig_025"),
        ("SYN_MASQUERADE", "Fig_VineMask", "Fig_022"),
        ("SYN_ANCIENT_REMNANTS", "Fig_GiantEmblem", "Fig_037"),
        ("SYN_STRAY_CATS", "Fig_CatFluffyPaw", "Fig_084"),
        ("SYN_WEIRD_DICE", "Fig_SanunDice", "Fig_077"),
        ("SYN_PLUSH_SERIES", "Fig_TeddyBear", "Fig_097"),
        ("SYN_FROST_RELICS", "Fig_IceCrystal", "Fig_071"),
        ("SYN_GREENHOUSE_FRIENDS", "Fig_CactusPot", "Fig_027"),
        ("SYN_GAMBLERS_TABLE", "Fig_SpadeKing", "Fig_082"),
        ("SYN_BEYOND_DEATH", "Fig_LavaMask", "Fig_046"),
        ("SYN_STONE_KIN", "Fig_AncientRune", "Fig_042"),
        ("SYN_WIZARD_DRAWER", "Fig_PhilosophersStone", "Fig_032"),
        ("SYN_MIDAS_TOUCH", "Fig_GoldClaw", "Fig_095"),
        ("SYN_CROWN_HAT", "Fig_DodoHat", "Fig_094"),
        ("SYN_MUSHROOM_KINGDOM", "Fig_MangroveMushroom", "Fig_019"),
        ("SYN_FLUFF_FRIENDS", "Fig_GoldFurball", "Fig_089"),
        ("SYN_CANDLELIGHT_RITE", "Fig_Candle", "Fig_039"),
        ("SYN_CIRCUS_PROPS", "Fig_SurpriseBox", "Fig_079"),
        ("SYN_SKYBORNE", "Fig_Kite", "Fig_098"),
        ("SYN_BONE_COLLECTORS", "Fig_UndertakerScythe", "Fig_075"),
        ("SYN_SEA_DICE", "Fig_SharkDice", "Fig_064"),
        ("SYN_HELMET_COLLECTION", "Fig_HardHat", "Fig_038"),
        ("SYN_DRAGONIC_KIN", "Fig_BladeScale", "Fig_008"),
        ("SYN_TEMPLE_RELICS", "Fig_GuanyinCrown", "Fig_017"),
        ("SYN_RESONANCE_TRIO", "Fig_GoldBell", "Fig_087"),
        ("SYN_HERO_ARMORY", "Fig_StoneAxe", "Fig_005"),
        ("SYN_BEAST_TRACE", "Fig_HellhoundFang", "Fig_043"),
        ("SYN_LAVA_RELICS", "Fig_FlameCollar", "Fig_045"),
        ("SYN_SMALL_CREATURES", "Fig_SandwormDoll", "Fig_029"),
        ("SYN_ACCESSORY_SHOP", "Fig_ButterflyRing", "Fig_020"),
        ("SYN_CURSED_HEIRLOOMS", "Fig_CursedSarcophagus", "Fig_083"),
        ("SYN_EYE_CONTACT", "Fig_SolgaEye", "Fig_011"),
        ("SYN_PHARAOH_CURSE", "Fig_Tutankhamun", "Fig_031"),
        ("SYN_SEA_CHILDREN", "Fig_WhirlpoolTrophy", "Fig_063"),
        ("SYN_LUCKY_TRINKETS", "Fig_FennecTail", "Fig_024"),
        ("SYN_SAND_SONG", "Fig_Hourglass", "Fig_030"),
        ("SYN_GOLDEN_LEGACY", "Fig_Emerald", "Fig_006"),
    };

    public static void Apply()
    {
        FixFigureSprite(
            "Assets/Contents/02_Sprites/07_Figure/17_Skyisland/Fig_GoldClaw.png",
            "Assets/Contents/02_Sprites/07_Figure/17_Skyisland/Fig_Dodohat.png");
        FixFigureSprite(
            "Assets/Contents/02_Sprites/07_Figure/12_Abyss/Fig_BlackFin.png",
            "Assets/Contents/02_Sprites/07_Figure/12_Abyss/Fig_Bone.png");

        Dictionary<string, FigureItemSO> figures = LoadFigures();
        AssignFigureIcon(figures, "Fig_GoldClaw", "Assets/Contents/02_Sprites/07_Figure/17_Skyisland/Fig_GoldClaw.png");
        AssignFigureIcon(figures, "Fig_BlackFin", "Assets/Contents/02_Sprites/07_Figure/12_Abyss/Fig_BlackFin.png");
        ReportEmptyIcons(figures);
        AssetDatabase.SaveAssets();

        foreach ((string synergyId, string figureId, string sheetId) in RepresentativeIcons)
        {
            if (!figures.TryGetValue(figureId, out FigureItemSO figure) || figure.icon == null)
            {
                Debug.LogWarning($"[SynergyFix] skip {synergyId} figure={figureId} icon empty");
                continue;
            }

            string path = $"{SynergyFolder}/{synergyId}.asset";
            if (!SetIconReference(path, figure.icon))
            {
                continue;
            }

            Debug.Log($"[SynergyFix] {synergyId} name={ReadItemName(path)} icon={sheetId}");
        }
    }

    static void FixFigureSprite(string path, string siblingPath)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        var sibling = (TextureImporter)AssetImporter.GetAtPath(siblingPath);
        bool changed = false;

        if (importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            changed = true;
        }

        if (importer.spriteImportMode != SpriteImportMode.Single)
        {
            importer.spriteImportMode = SpriteImportMode.Single;
            changed = true;
        }

        if (importer.filterMode != FilterMode.Point)
        {
            importer.filterMode = FilterMode.Point;
            changed = true;
        }

        if (!Mathf.Approximately(importer.spritePixelsPerUnit, sibling.spritePixelsPerUnit))
        {
            importer.spritePixelsPerUnit = sibling.spritePixelsPerUnit;
            changed = true;
        }

        if (importer.textureCompression != sibling.textureCompression)
        {
            importer.textureCompression = sibling.textureCompression;
            changed = true;
        }

        if (importer.mipmapEnabled != sibling.mipmapEnabled)
        {
            importer.mipmapEnabled = sibling.mipmapEnabled;
            changed = true;
        }

        if (importer.crunchedCompression != sibling.crunchedCompression)
        {
            importer.crunchedCompression = sibling.crunchedCompression;
            changed = true;
        }

        if (changed)
        {
            importer.SaveAndReimport();
        }
    }

    static Dictionary<string, FigureItemSO> LoadFigures()
    {
        var figures = new Dictionary<string, FigureItemSO>();
        foreach (string guid in AssetDatabase.FindAssets("t:FigureItemSO"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var figure = AssetDatabase.LoadAssetAtPath<FigureItemSO>(path);
            if (figure == null || string.IsNullOrEmpty(figure.Item_ID))
            {
                continue;
            }

            figures[figure.Item_ID] = figure;
        }

        return figures;
    }

    static void AssignFigureIcon(Dictionary<string, FigureItemSO> figures, string figureId, string spritePath)
    {
        if (!figures.TryGetValue(figureId, out FigureItemSO figure))
        {
            Debug.LogWarning($"[SynergyFix] figure missing {figureId}");
            return;
        }

        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
        if (sprite == null)
        {
            Debug.LogWarning($"[SynergyFix] sprite missing {spritePath}");
            return;
        }

        if (figure.icon == sprite)
        {
            return;
        }

        figure.icon = sprite;
        EditorUtility.SetDirty(figure);
    }

    static void ReportEmptyIcons(Dictionary<string, FigureItemSO> figures)
    {
        foreach (FigureItemSO figure in figures.Values)
        {
            if (figure.icon != null)
            {
                continue;
            }

            Debug.Log($"[SynergyFix] empty icon {figure.Item_ID} {figure.itemName}");
        }
    }

    // Unity로 SynergyData를 저장하면 YAML의 skillEffect가 빠지므로 icon 줄만 바꾼다.
    static bool SetIconReference(string assetPath, Sprite sprite)
    {
        if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out string guid, out long fileId))
        {
            Debug.LogWarning($"[SynergyFix] sprite id missing {sprite.name}");
            return false;
        }

        string desired = $"  icon: {{fileID: {fileId}, guid: {guid}, type: 3}}";
        string text = File.ReadAllText(assetPath);
        Match match = Regex.Match(text, "^  icon: .*$", RegexOptions.Multiline);
        if (!match.Success || match.Value == desired)
        {
            return false;
        }

        string updated = text.Substring(0, match.Index) + desired + text.Substring(match.Index + match.Length);
        File.WriteAllText(assetPath, updated, new UTF8Encoding(false));
        return true;
    }

    static string ReadItemName(string assetPath)
    {
        string text = File.ReadAllText(assetPath);
        Match quoted = Regex.Match(text, "itemName: \"(.*)\"");
        if (quoted.Success)
        {
            return Regex.Unescape(quoted.Groups[1].Value);
        }

        Match plain = Regex.Match(text, "itemName: (.+)");
        return plain.Success ? plain.Groups[1].Value.Trim() : "";
    }
}
#endif
