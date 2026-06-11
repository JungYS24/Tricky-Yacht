using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

public class AudioControl : MonoBehaviour
{
    [Header("Audio Mixer")]
    public AudioMixer masterMixer;

    [Header("Volume Sliders")]
    public Slider masterSlider;
    public Slider bgmSlider;
    public Slider sfxSlider;

    private void Start()
    {
        // 씬이 켜질 때마다 저장된 데이터(PlayerPrefs)를 읽어와서 자기 슬라이더와 믹서를 맞춤
        if (masterSlider != null)
        {
            masterSlider.onValueChanged.RemoveAllListeners();
            masterSlider.minValue = 0.0001f;
            masterSlider.maxValue = 1f;
            masterSlider.value = PlayerPrefs.GetFloat("MasterVol", 1f);
            masterSlider.onValueChanged.AddListener(SetMasterVolume);
            SetMasterVolume(masterSlider.value);
        }

        if (bgmSlider != null)
        {
            bgmSlider.onValueChanged.RemoveAllListeners();
            bgmSlider.minValue = 0.0001f;
            bgmSlider.maxValue = 1f;
            bgmSlider.value = PlayerPrefs.GetFloat("BGMVol", 1f);
            bgmSlider.onValueChanged.AddListener(SetBGMVolume);
            SetBGMVolume(bgmSlider.value);
        }

        if (sfxSlider != null)
        {
            sfxSlider.onValueChanged.RemoveAllListeners();
            sfxSlider.minValue = 0.0001f;
            sfxSlider.maxValue = 1f;
            sfxSlider.value = PlayerPrefs.GetFloat("SFXVol", 1f);
            sfxSlider.onValueChanged.AddListener(SetSFXVolume);
            SetSFXVolume(sfxSlider.value);
        }
    }

    public void SetMasterVolume(float volume)
    {
        float dbValue = Mathf.Log10(Mathf.Max(0.0001f, volume)) * 20f;
        masterMixer.SetFloat("MasterVol", dbValue);
        PlayerPrefs.SetFloat("MasterVol", volume);
    }

    public void SetBGMVolume(float volume)
    {
        float dbValue = Mathf.Log10(Mathf.Max(0.0001f, volume)) * 20f;
        masterMixer.SetFloat("BGMVol", dbValue);
        PlayerPrefs.SetFloat("BGMVol", volume);
    }

    public void SetSFXVolume(float volume)
    {
        float dbValue = Mathf.Log10(Mathf.Max(0.0001f, volume)) * 20f;
        masterMixer.SetFloat("SFXVol", dbValue);
        PlayerPrefs.SetFloat("SFXVol", volume);
    }
}