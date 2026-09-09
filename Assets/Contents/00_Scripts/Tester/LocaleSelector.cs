using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

/// <summary>
/// 런타임 언어 전환 테스트용. 씬에 붙인 뒤 1~4 키로 Locale을 바꿉니다.
/// 0: ko, 1: en, 2: ja, 3: zh-Hans
/// </summary>
public class LocaleSelector : MonoBehaviour
{
    private static readonly string[] LocaleCodes =
    {
        "ko",
        "en",
        "ja",
        "zh-Hans"
    };

    public void ChangeLocale(int localeId)
    {
        StartCoroutine(ChangeLocaleRoutine(localeId));
    }

    private IEnumerator ChangeLocaleRoutine(int localeId)
    {
        if (localeId < 0 || localeId >= LocaleCodes.Length)
        {
            Debug.LogWarning($"[LocaleSelector] 지원하지 않는 localeId입니다: {localeId} (0~3)");
            yield break;
        }

        yield return LocalizationSettings.InitializationOperation;

        Locale locale = LocalizationSettings.AvailableLocales.GetLocale(LocaleCodes[localeId]);
        if (locale == null)
            locale = LocalizationSettings.AvailableLocales.GetLocale(new LocaleIdentifier(LocaleCodes[localeId]));

        if (locale == null)
        {
            Debug.LogError($"[LocaleSelector] Locale을 찾을 수 없습니다: {LocaleCodes[localeId]}");
            yield break;
        }

        LocalizationSettings.SelectedLocale = locale;
        Debug.Log($"[LocaleSelector] Locale 변경: {localeId} ({LocaleCodes[localeId]})");
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame)
            ChangeLocale(0);
        else if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame)
            ChangeLocale(1);
        else if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame)
            ChangeLocale(2);
        else if (keyboard.digit4Key.wasPressedThisFrame || keyboard.numpad4Key.wasPressedThisFrame)
            ChangeLocale(3);
    }
}
