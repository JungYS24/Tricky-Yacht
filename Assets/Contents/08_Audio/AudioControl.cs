using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

public class AudioControl : MonoBehaviour
{
    public static AudioControl Instance;

    [Header("Audio Mixer")]
    public AudioMixer masterMixer;

    [Header("재생 소스")]
    public AudioSource sfxSource;

    [Header("슬라이더 (비우면 이름 검색)")]
    public Slider masterSlider;
    public Slider bgmSlider;
    public Slider sfxSlider;

    private bool isReady;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureSfxSource();
        ApplySavedVolumes();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
    }

    private void Start()
    {
        StartCoroutine(ApplyVolumeNextFrame());
        SyncAudioSettings();
    }

    private void OnSceneUnloaded(Scene scene)
    {
        isReady = false;
        UnbindIfFromScene(ref masterSlider, scene);
        UnbindIfFromScene(ref bgmSlider, scene);
        UnbindIfFromScene(ref sfxSlider, scene);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        StartCoroutine(ApplyVolumeNextFrame());
        SyncAudioSettings();
    }

    private IEnumerator ApplyVolumeNextFrame()
    {
        yield return new WaitForSecondsRealtime(0.05f);
        ApplySavedVolumes();
    }

    public void PlaySFX(AudioEvent audioEvent)
    {
        EnsureSfxSource();
        if (audioEvent != null)
            audioEvent.PlayOneShot(sfxSource);
    }

    public void PlaySFX(AudioClip clip, float volume = 1f)
    {
        EnsureSfxSource();
        if (sfxSource != null && clip != null)
            sfxSource.PlayOneShot(clip, volume);
    }

    private void ApplySavedVolumes()
    {
        SetMixerVolume("Master", PlayerPrefs.GetFloat("MasterVol", 1f));
        SetMixerVolume("BGM", PlayerPrefs.GetFloat("BGMVol", 1f));
        SetMixerVolume("SFX", PlayerPrefs.GetFloat("SFXVol", 1f));
    }

    private void SyncAudioSettings()
    {
        isReady = false;

        BindSlider(ref masterSlider, "MasterSlider", SetMasterVolume, "MasterVol");
        BindSlider(ref bgmSlider, "BGMSlider", SetBGMVolume, "BGMVol");
        BindSlider(ref sfxSlider, "SFXSlider", SetSFXVolume, "SFXVol");

        isReady = true;
    }

    private void BindSlider(ref Slider slider, string fallbackName, UnityEngine.Events.UnityAction<float> listener, string prefsKey)
    {
        if (slider == null)
            slider = FindSliderByName(fallbackName);
        if (slider == null)
            return;

        slider.onValueChanged.RemoveListener(listener);
        slider.minValue = 0.0001f;
        slider.maxValue = 1f;
        slider.value = PlayerPrefs.GetFloat(prefsKey, 1f);
        slider.onValueChanged.AddListener(listener);
    }

    private void UnbindIfFromScene(ref Slider slider, Scene scene)
    {
        if (slider == null || slider.gameObject.scene != scene)
            return;

        slider.onValueChanged.RemoveListener(SetMasterVolume);
        slider.onValueChanged.RemoveListener(SetBGMVolume);
        slider.onValueChanged.RemoveListener(SetSFXVolume);
        slider = null;
    }

    private Slider FindSliderByName(string targetName)
    {
        Slider[] allSliders = Resources.FindObjectsOfTypeAll<Slider>();
        foreach (Slider slider in allSliders)
        {
            if (slider.gameObject.name == targetName &&
                slider.gameObject.scene.IsValid() &&
                slider.gameObject.scene.isLoaded)
            {
                return slider;
            }
        }

        return null;
    }

    public void SetMasterVolume(float volume)
    {
        SetMixerVolume("Master", volume);
        SaveVolume("MasterVol", volume, masterSlider);
    }

    public void SetBGMVolume(float volume)
    {
        SetMixerVolume("BGM", volume);
        SaveVolume("BGMVol", volume, bgmSlider);
    }

    public void SetSFXVolume(float volume)
    {
        SetMixerVolume("SFX", volume);
        SaveVolume("SFXVol", volume, sfxSlider);
    }

    private void SetMixerVolume(string parameter, float volume)
    {
        if (masterMixer == null)
            return;

        float dbValue = volume <= 0.0001f
            ? -80f
            : Mathf.Log10(volume) * 20f;
        masterMixer.SetFloat(parameter, dbValue);
    }

    private void SaveVolume(string key, float volume, Slider sourceSlider)
    {
        if (!isReady || sourceSlider == null || !sourceSlider.gameObject.activeInHierarchy)
            return;

        PlayerPrefs.SetFloat(key, volume);
        PlayerPrefs.Save();
    }

    private void EnsureSfxSource()
    {
        if (sfxSource != null)
            return;

        var holder = new GameObject("SFXSource");
        holder.transform.SetParent(transform, false);
        sfxSource = holder.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        sfxSource.loop = false;
        if (masterMixer != null)
        {
            AudioMixerGroup[] groups = masterMixer.FindMatchingGroups("SFX");
            if (groups != null && groups.Length > 0)
                sfxSource.outputAudioMixerGroup = groups[0];
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
