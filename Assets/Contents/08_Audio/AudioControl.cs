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

    // UI 파괴 시 오작동 값이 저장되는 것을 막는 방어막 변수
    private bool isReady = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        ApplySavedVolumes();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        // 씬이 꺼질 때를 감지하는 이벤트 추가
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
    }

    private void Start()
    {
        SyncAudioSettings();
    }

    private void OnSceneUnloaded(Scene scene)
    {
        // 씬이 이동하며 UI가 파괴될 때, 찌그러진 값이 저장되는 것을 원천 차단
        isReady = false;
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
        // 세팅을 진행하는 동안에는 저장이 일어나지 않도록 잠금
        isReady = false;

        masterSlider = FindSliderByName("MasterSlider");
        bgmSlider = FindSliderByName("BGMSlider");
        sfxSlider = FindSliderByName("SFXSlider");

        if (masterSlider != null)
        {
            masterSlider.onValueChanged.RemoveAllListeners();
            masterSlider.minValue = 0.0001f;
            masterSlider.maxValue = 1f;
            masterSlider.value = PlayerPrefs.GetFloat("MasterVol", 1f);
            masterSlider.onValueChanged.AddListener(SetMasterVolume);
        }

        if (bgmSlider != null)
        {
            bgmSlider.onValueChanged.RemoveAllListeners();
            bgmSlider.minValue = 0.0001f;
            bgmSlider.maxValue = 1f;
            bgmSlider.value = PlayerPrefs.GetFloat("BGMVol", 1f);
            bgmSlider.onValueChanged.AddListener(SetBGMVolume);
        }

        if (sfxSlider != null)
        {
            sfxSlider.onValueChanged.RemoveAllListeners();
            sfxSlider.minValue = 0.0001f;
            sfxSlider.maxValue = 1f;
            sfxSlider.value = PlayerPrefs.GetFloat("SFXVol", 1f);
            sfxSlider.onValueChanged.AddListener(SetSFXVolume);
        }

        // 모든 슬라이더 세팅이 무사히 끝나면 잠금 해제
        isReady = true;
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

        // UI 세팅 중이거나 파괴 중이 아닐 때만 실제 세이브 파일에 기록
        if (isReady)
        {
            PlayerPrefs.SetFloat("MasterVol", volume);
            PlayerPrefs.Save();
        }
    }

    public void SetBGMVolume(float volume)
    {
        float dbValue = Mathf.Log10(Mathf.Max(0.0001f, volume)) * 20f;
        masterMixer.SetFloat("BGMVol", dbValue);

        if (isReady)
        {
            PlayerPrefs.SetFloat("BGMVol", volume);
            PlayerPrefs.Save();
        }
    }

    public void SetSFXVolume(float volume)
    {
        float dbValue = Mathf.Log10(Mathf.Max(0.0001f, volume)) * 20f;
        masterMixer.SetFloat("SFXVol", dbValue);

        if (isReady)
        {
            PlayerPrefs.SetFloat("SFXVol", volume);
            PlayerPrefs.Save();
        }
    }
}