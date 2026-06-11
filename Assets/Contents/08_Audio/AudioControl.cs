using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class AudioControl : MonoBehaviour
{
    public static AudioControl Instance;

    [Header("Audio Mixer")]
    public AudioMixer masterMixer;

    private Slider masterSlider;
    private Slider bgmSlider;
    private Slider sfxSlider;

    private void Awake()
    {
        // 싱글톤 유지
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 게임 시작 시 저장된 볼륨 즉시 적용
        ApplySavedVolumes();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        SyncAudioSettings();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SyncAudioSettings();
    }

    private void ApplySavedVolumes()
    {
        SetMasterVolume(PlayerPrefs.GetFloat("MasterVol", 1f));
        SetBGMVolume(PlayerPrefs.GetFloat("BGMVol", 1f));
        SetSFXVolume(PlayerPrefs.GetFloat("SFXVol", 1f));
    }

    private void SyncAudioSettings()
    {
        masterSlider = FindSliderByName("MasterSlider");
        bgmSlider = FindSliderByName("BGMSlider");
        sfxSlider = FindSliderByName("SFXSlider");

        if (masterSlider != null)
        {
            masterSlider.onValueChanged.RemoveListener(SetMasterVolume);

            masterSlider.minValue = 0.0001f;
            masterSlider.maxValue = 1f;
            masterSlider.value = PlayerPrefs.GetFloat("MasterVol", 1f);

            masterSlider.onValueChanged.AddListener(SetMasterVolume);
        }

        if (bgmSlider != null)
        {
            bgmSlider.onValueChanged.RemoveListener(SetBGMVolume);

            bgmSlider.minValue = 0.0001f;
            bgmSlider.maxValue = 1f;
            bgmSlider.value = PlayerPrefs.GetFloat("BGMVol", 1f);

            bgmSlider.onValueChanged.AddListener(SetBGMVolume);
        }

        if (sfxSlider != null)
        {
            sfxSlider.onValueChanged.RemoveListener(SetSFXVolume);

            sfxSlider.minValue = 0.0001f;
            sfxSlider.maxValue = 1f;
            sfxSlider.value = PlayerPrefs.GetFloat("SFXVol", 1f);

            sfxSlider.onValueChanged.AddListener(SetSFXVolume);
        }
    }

    private Slider FindSliderByName(string targetName)
    {
        Slider[] allSliders = Resources.FindObjectsOfTypeAll<Slider>();

        foreach (Slider s in allSliders)
        {
            if (s.gameObject.name == targetName &&
                s.gameObject.scene.IsValid() &&
                s.gameObject.scene.isLoaded)
            {
                return s;
            }
        }

        return null;
    }

    public void SetMasterVolume(float volume)
    {
        float dbValue = Mathf.Log10(Mathf.Max(0.0001f, volume)) * 20f;
        masterMixer.SetFloat("MasterVol", dbValue);

        PlayerPrefs.SetFloat("MasterVol", volume);
        PlayerPrefs.Save();
    }

    public void SetBGMVolume(float volume)
    {
        float dbValue = Mathf.Log10(Mathf.Max(0.0001f, volume)) * 20f;
        masterMixer.SetFloat("BGMVol", dbValue);

        PlayerPrefs.SetFloat("BGMVol", volume);
        PlayerPrefs.Save();
    }

    public void SetSFXVolume(float volume)
    {
        float dbValue = Mathf.Log10(Mathf.Max(0.0001f, volume)) * 20f;
        masterMixer.SetFloat("SFXVol", dbValue);

        PlayerPrefs.SetFloat("SFXVol", volume);
        PlayerPrefs.Save();
    }
}