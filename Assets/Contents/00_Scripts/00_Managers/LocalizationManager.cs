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
