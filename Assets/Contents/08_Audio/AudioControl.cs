using UnityEngine;
using UnityEngine.Audio; 
using UnityEngine.UI;

public class AudioControl : MonoBehaviour
{
    [Header("Audio Mixer")]
    public AudioMixer masterMixer;

    [Header("Volume Sliders")]
    public Slider masterSlider; // 마스터 볼륨
    public Slider bgmSlider;    // 배경음악 볼륨
    public Slider sfxSlider;    // 효과음 볼륨

    private void Start()
    {
        // 게임 시작 시, 기존에 유저가 설정했던 볼륨 값이 있다면 로드하고 슬라이더에 반영
        // 저장된 값이 없다면 기본값인 0f(최대 볼륨)를 사용
        if (masterSlider != null)
        {
            masterSlider.value = PlayerPrefs.GetFloat("MasterVol", 0f);
            masterSlider.onValueChanged.AddListener(SetMasterVolume);
            SetMasterVolume(masterSlider.value);
        }

        if (bgmSlider != null)
        {
            bgmSlider.value = PlayerPrefs.GetFloat("BGMVol", 0f);
            bgmSlider.onValueChanged.AddListener(SetBGMVolume);
            SetBGMVolume(bgmSlider.value);
        }

        if (sfxSlider != null)
        {
            sfxSlider.value = PlayerPrefs.GetFloat("SFXVol", 0f);
            sfxSlider.onValueChanged.AddListener(SetSFXVolume);
            SetSFXVolume(sfxSlider.value);
        }
    }

    // 1. 전체 볼륨 제어 (기존 유지)
    public void SetMasterVolume(float volume)
    {
        masterMixer.SetFloat("MasterVol", volume);

        if (volume <= -40f) masterMixer.SetFloat("MasterVol", -80f);

        PlayerPrefs.SetFloat("MasterVol", volume);
    }

    // 2. 배경음악 볼륨 제어 (추가)
    public void SetBGMVolume(float volume)
    {
        masterMixer.SetFloat("BGMVol", volume);

        if (volume <= -40f) masterMixer.SetFloat("BGMVol", -80f);

        PlayerPrefs.SetFloat("BGMVol", volume);
    }

    // 3. 효과음 볼륨 제어 (추가)
    public void SetSFXVolume(float volume)
    {
        masterMixer.SetFloat("SFXVol", volume);

        if (volume <= -40f) masterMixer.SetFloat("SFXVol", -80f);

        PlayerPrefs.SetFloat("SFXVol", volume);
    }
}