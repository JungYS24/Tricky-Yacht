using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

/// <summary>
/// Unity Localization Locale 전환과 PlayerPrefs 저장/로드를 담당하는 싱글톤.
/// Addressables 초기화가 끝날 때까지 기다린 뒤 언어를 적용한다.
/// </summary>
public class LocalizationManager : MonoBehaviour
{
    public const string LanguagePrefsKey = "GameLanguage";
    public const string DefaultLanguageCode = "ko";
    public const string UiTable = "UI_StringTable";
    public const string SysTable = "SYS_StringTable";
    public const string TutTable = "TUT_StringTable";
    public const string HandTable = "GP_Hand_StringTable";
    public const string ItemTable = "CNT_Item_StringTable";
    public const string MonsterTable = "CNT_Monster_StringTable";
    public const string BiomeTable = "CNT_Biome_StringTable";
    public const string EncounterTable = "ENC_StringTable";
    public const string SatelliteTable = "Satellite_StringTable";

    public static readonly string[] SupportedLanguageCodes =
    {
        "ko",
        "en",
        "ja",
        "zh-Hans"
    };

    private static readonly Dictionary<string, string> LocaleAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        { "ko", "ko" },
        { "kr", "ko" },
        { "ko-KR", "ko" },
        { "en", "en" },
        { "en-US", "en" },
        { "en-GB", "en" },
        { "ja", "ja" },
        { "jp", "ja" },
        { "ja-JP", "ja" },
        { "zh-Hans", "zh-Hans" },
        { "zh-CN", "zh-Hans" },
        { "zh", "zh-Hans" }
    };

    public static LocalizationManager Instance { get; private set; }

    public string CurrentLanguageCode { get; private set; } = DefaultLanguageCode;

    private Coroutine _setLanguageRoutine;
    private bool _isReady;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null)
            return;

        var go = new GameObject(nameof(LocalizationManager));
        go.AddComponent<LocalizationManager>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Start()
    {
        _setLanguageRoutine = StartCoroutine(InitializeLanguageRoutine());
    }

    /// <summary>
    /// 지원 언어 코드(ko, en, ja, zh-Hans)로 Locale을 비동기 전환하고 PlayerPrefs에 저장한다.
    /// </summary>
    public void SetLanguage(string localeCode)
    {
        if (_setLanguageRoutine != null)
            StopCoroutine(_setLanguageRoutine);

        _setLanguageRoutine = StartCoroutine(SetLanguageRoutine(localeCode, saveToPrefs: true));
    }

    /// <summary>
    /// String Table에서 현재 Locale 기준 번역 문자열을 가져온다.
    /// 테이블/키가 없으면 키를 그대로 반환한다.
    /// </summary>
    public string GetLocalizedString(string tableName, string entryKey)
    {
        if (string.IsNullOrEmpty(tableName) || string.IsNullOrEmpty(entryKey))
        {
            Debug.LogWarning("[LocalizationManager] tableName 또는 entryKey가 비어 있습니다.");
            return entryKey ?? string.Empty;
        }

        EnsureInitialized();

        try
        {
            var result = LocalizationSettings.StringDatabase.GetLocalizedString(tableName, entryKey);
            if (string.IsNullOrEmpty(result))
                return entryKey;

            return result;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[LocalizationManager] 번역을 찾지 못했습니다. table={tableName}, key={entryKey}, error={e.Message}");
            return entryKey;
        }
    }

    /// <summary>
    /// 인자 치환이 있는 번역 문자열을 가져온다. 예: "남은 굴리기: {0}"
    /// </summary>
    public string GetLocalizedString(string tableName, string entryKey, params object[] arguments)
    {
        if (string.IsNullOrEmpty(tableName) || string.IsNullOrEmpty(entryKey))
        {
            Debug.LogWarning("[LocalizationManager] tableName 또는 entryKey가 비어 있습니다.");
            return entryKey ?? string.Empty;
        }

        EnsureInitialized();

        try
        {
            var result = arguments == null || arguments.Length == 0
                ? LocalizationSettings.StringDatabase.GetLocalizedString(tableName, entryKey)
                : LocalizationSettings.StringDatabase.GetLocalizedString(tableName, entryKey, arguments);

            if (string.IsNullOrEmpty(result))
                return entryKey;

            return result;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[LocalizationManager] 번역을 찾지 못했습니다. table={tableName}, key={entryKey}, error={e.Message}");
            return entryKey;
        }
    }

    public static string GetHandDisplayName(HandRank rank)
    {
        string key = HandRankUtil.GetLocKey(rank);
        string fallback = HandRankUtil.GetFallbackName(rank);
        return GetOrFallback(HandTable, key, fallback);
    }

    public static string GetItemDisplayName(BaseItemDataSO item)
    {
        if (item == null)
            return string.Empty;

        ResolveItemLoc(item, isName: true, out string tableName, out string key);
        return GetOrFallback(tableName, key, item.itemName);
    }

    public static string GetItemDescription(BaseItemDataSO item)
    {
        if (item == null)
            return string.Empty;

        ResolveItemLoc(item, isName: false, out string tableName, out string key);
        return GetOrFallback(tableName, key, item.description);
    }

    public static string GetTut(string entryKey, string fallback)
    {
        return GetOrFallback(TutTable, entryKey, fallback);
    }

    public static string GetEncounter(string entryKey, string fallback)
    {
        return GetOrFallback(EncounterTable, entryKey, fallback);
    }

    public static string GetMonsterDisplayName(MonsterDataSO monster)
    {
        if (monster == null)
            return string.Empty;

        string key = ResolveMonsterNameKey(monster);
        return GetOrFallback(MonsterTable, key, monster.monsterName);
    }

    public static string GetBiomeDisplayName(BiomeType biome)
    {
        string key = "CNT_BIOME_" + biome.ToString().ToUpperInvariant() + "_NAME";
        return GetOrFallback(BiomeTable, key, biome.ToString());
    }

    public static string GetSys(string entryKey, string fallback)
    {
        return GetOrFallback(SysTable, entryKey, fallback);
    }

    public static string GetSys(string entryKey, string fallback, params object[] arguments)
    {
        if (Instance == null)
            return FormatFallback(fallback, arguments);

        string result = Instance.GetLocalizedString(SysTable, entryKey, arguments);
        if (string.IsNullOrEmpty(result) || result == entryKey)
            return FormatFallback(fallback, arguments);

        return result;
    }

    public static string GetUi(string entryKey, string fallback)
    {
        return GetOrFallback(UiTable, entryKey, fallback);
    }

    public static string GetUi(string entryKey, string fallback, params object[] arguments)
    {
        if (Instance == null)
            return FormatFallback(fallback, arguments);

        string result = Instance.GetLocalizedString(UiTable, entryKey, arguments);
        if (string.IsNullOrEmpty(result) || result == entryKey)
            return FormatFallback(fallback, arguments);

        return result;
    }

    public static bool TryGetLocalized(string tableName, string entryKey, out string value)
    {
        value = null;
        if (string.IsNullOrEmpty(tableName) || string.IsNullOrEmpty(entryKey) || Instance == null)
            return false;

        string result = Instance.GetLocalizedString(tableName, entryKey);
        if (string.IsNullOrEmpty(result) || result == entryKey)
            return false;

        if (result.StartsWith("No translation found", StringComparison.Ordinal))
            return false;

        value = result;
        return true;
    }

    private static string GetOrFallback(string tableName, string entryKey, string fallback)
    {
        if (string.IsNullOrEmpty(entryKey))
            return fallback ?? string.Empty;

        if (Instance == null)
            return fallback ?? entryKey;

        string result = Instance.GetLocalizedString(tableName, entryKey);
        if (string.IsNullOrEmpty(result) || result == entryKey)
            return string.IsNullOrEmpty(fallback) ? entryKey : fallback;

        return result;
    }

    private static void ResolveItemLoc(BaseItemDataSO item, bool isName, out string tableName, out string key)
    {
        tableName = ItemTable;
        string suffix = isName ? "_NAME" : "_DESC";
        key = null;

        if (item is SnackItemSO snack)
        {
            key = "CNT_SNACK_" + ToSnakeUpper(snack.snackType.ToString()) + suffix;
            return;
        }

        if (item is TicketPackSO)
        {
            key = "CNT_TICKET_PACK" + suffix;
            return;
        }

        if (item is TicketItemSO ticket)
        {
            key = "CNT_TICKET_" + ToSnakeUpper(ticket.targetHand.ToString()) + suffix;
            return;
        }

        if (item is SatelliteItemSO satellite)
        {
            tableName = SatelliteTable;
            key = "SAT_" + satellite.satelliteType.ToString().ToUpperInvariant() + suffix;
            return;
        }

        if (item is CoatingItemSO coating)
        {
            string token = coating.coatingType == DiceType.Normal ? "Vanilla" : coating.coatingType.ToString();
            key = "CNT_" + token + suffix;
            return;
        }

        if (item is DiceItemSO dice)
        {
            string diceToken = ResolveDiceLocToken(dice);
            if (!string.IsNullOrEmpty(diceToken))
            {
                key = "CNT_" + diceToken + suffix;
                return;
            }
        }

        string id = ResolveRuntimeItemId(item);
        if (string.Equals(id, "Dice2", StringComparison.OrdinalIgnoreCase))
            id = "Dark";

        if (!string.IsNullOrEmpty(id))
            key = "CNT_" + id + suffix;
    }

    private static string ResolveRuntimeItemId(BaseItemDataSO item)
    {
        if (item == null)
            return null;

        if (!string.IsNullOrEmpty(item.Item_ID) && !IsLegacyNumericFigureId(item.Item_ID))
            return item.Item_ID;

        if (!string.IsNullOrEmpty(item.name) && item.name.StartsWith("Fig_"))
            return item.name;

        return item.Item_ID;
    }

    private static string ResolveDiceLocToken(DiceItemSO dice)
    {
        if (dice == null)
            return null;

        switch (dice.specialEffect)
        {
            case SpecialDieEffect.Coin: return "Coin";
            case SpecialDieEffect.Heart: return "Heart";
            case SpecialDieEffect.Flame: return "Flame";
            case SpecialDieEffect.Even: return "Even";
            case SpecialDieEffect.Odd: return "Odd";
        }

        string id = dice.Item_ID ?? string.Empty;
        string assetName = dice.name ?? string.Empty;

        if (id == "tutorialHighRollerDice" || assetName == "High")
            return "High";
        if (id == "Low" || assetName == "Low")
            return "Low";
        if (assetName == "Even Number")
            return "Even";
        if (assetName == "odd number")
            return "Odd";
        if (id == "One" || id == "Two" || id == "Three" || id == "Four" || id == "Five" || id == "Six")
            return id;
        if (assetName == "One" || assetName == "Two" || assetName == "Three" || assetName == "Four" || assetName == "Five" || assetName == "Six")
            return assetName;
        if (id == "88Dice" || assetName == "88Dice")
            return "88Dice";
        if (id == "Heart" || assetName == "Heart")
            return "Heart";
        if (id == "Flame" || assetName == "Flame")
            return "Flame";
        if (id == "Coin" || assetName == "Coin")
            return "Coin";

        return null;
    }

    private static string ResolveMonsterNameKey(MonsterDataSO monster)
    {
        if (monster == null || string.IsNullOrEmpty(monster.monsterID))
            return null;

        string token = monster.monsterID.Trim().Replace(' ', '_').Replace('-', '_');
        return "CNT_MON_" + token.ToUpperInvariant() + "_NAME";
    }

    private static string ToSnakeUpper(string pascal)
    {
        if (string.IsNullOrEmpty(pascal))
            return string.Empty;

        var builder = new System.Text.StringBuilder(pascal.Length + 4);
        for (int i = 0; i < pascal.Length; i++)
        {
            char c = pascal[i];
            if (i > 0 && char.IsUpper(c) && (char.IsLower(pascal[i - 1]) || (i + 1 < pascal.Length && char.IsLower(pascal[i + 1]))))
                builder.Append('_');
            builder.Append(char.ToUpperInvariant(c));
        }

        return builder.ToString();
    }

    private static bool IsLegacyNumericFigureId(string itemId)
    {
        if (string.IsNullOrEmpty(itemId) || !itemId.StartsWith("Fig_") || itemId.Length < 5)
            return false;

        for (int i = 4; i < itemId.Length; i++)
        {
            if (!char.IsDigit(itemId[i]))
                return false;
        }

        return true;
    }

    private static string FormatFallback(string fallback, object[] arguments)
    {
        if (string.IsNullOrEmpty(fallback))
            return string.Empty;

        if (arguments == null || arguments.Length == 0)
            return fallback;

        try
        {
            return string.Format(fallback, arguments);
        }
        catch (FormatException)
        {
            return fallback;
        }
    }

    private IEnumerator InitializeLanguageRoutine()
    {
        yield return LocalizationSettings.InitializationOperation;
        _isReady = true;

        string savedCode = PlayerPrefs.GetString(LanguagePrefsKey, string.Empty);
        if (TryNormalizeLanguageCode(savedCode, out string normalized))
        {
            yield return ApplyLocaleRoutine(normalized, saveToPrefs: false);
            yield break;
        }

        CurrentLanguageCode = ResolveCodeFromLocale(LocalizationSettings.SelectedLocale);
        _setLanguageRoutine = null;
    }

    private IEnumerator SetLanguageRoutine(string localeCode, bool saveToPrefs)
    {
        yield return LocalizationSettings.InitializationOperation;
        _isReady = true;

        if (!TryNormalizeLanguageCode(localeCode, out string normalized))
        {
            Debug.LogWarning($"[LocalizationManager] 지원하지 않는 언어 코드입니다: '{localeCode}'. 지원: ko, en, ja, zh-Hans");
            _setLanguageRoutine = null;
            yield break;
        }

        yield return ApplyLocaleRoutine(normalized, saveToPrefs);
    }

    private IEnumerator ApplyLocaleRoutine(string normalizedCode, bool saveToPrefs)
    {
        Locale locale = FindLocale(normalizedCode);
        if (locale == null)
        {
            Debug.LogError($"[LocalizationManager] Locale 에셋을 찾을 수 없습니다: {normalizedCode}. Locales 폴더와 Localization Settings를 확인하세요.");
            _setLanguageRoutine = null;
            yield break;
        }

        if (LocalizationSettings.SelectedLocale != locale)
            LocalizationSettings.SelectedLocale = locale;

        var selectedHandle = LocalizationSettings.SelectedLocaleAsync;
        if (!selectedHandle.IsDone)
            yield return selectedHandle;

        CurrentLanguageCode = normalizedCode;

        if (saveToPrefs)
        {
            PlayerPrefs.SetString(LanguagePrefsKey, normalizedCode);
            PlayerPrefs.Save();
        }

        _setLanguageRoutine = null;
    }

    private void EnsureInitialized()
    {
        if (_isReady && LocalizationSettings.InitializationOperation.IsDone)
            return;

        var handle = LocalizationSettings.InitializationOperation;
        if (!handle.IsDone)
            handle.WaitForCompletion();

        _isReady = true;
    }

    private static Locale FindLocale(string normalizedCode)
    {
        var locales = LocalizationSettings.AvailableLocales;
        if (locales == null)
            return null;

        Locale locale = locales.GetLocale(normalizedCode);
        if (locale != null)
            return locale;

        return locales.GetLocale(new LocaleIdentifier(normalizedCode));
    }

    private static bool TryNormalizeLanguageCode(string localeCode, out string normalized)
    {
        normalized = null;
        if (string.IsNullOrWhiteSpace(localeCode))
            return false;

        string trimmed = localeCode.Trim();
        if (LocaleAliases.TryGetValue(trimmed, out string mapped))
        {
            normalized = mapped;
            return true;
        }

        for (int i = 0; i < SupportedLanguageCodes.Length; i++)
        {
            if (string.Equals(SupportedLanguageCodes[i], trimmed, StringComparison.OrdinalIgnoreCase))
            {
                normalized = SupportedLanguageCodes[i];
                return true;
            }
        }

        return false;
    }

    private static string ResolveCodeFromLocale(Locale locale)
    {
        if (locale == null)
            return DefaultLanguageCode;

        string code = locale.Identifier.Code;
        if (TryNormalizeLanguageCode(code, out string normalized))
            return normalized;

        return DefaultLanguageCode;
    }
}
