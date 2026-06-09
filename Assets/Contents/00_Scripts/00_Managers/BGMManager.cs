using UnityEngine;
using UnityEngine.Audio;
using System.Collections;
using System.Collections.Generic;

public class BGMManager : MonoBehaviour
{
    public static BGMManager Instance;

    private AudioSource audioSource;
    private Coroutine fadeCoroutine;

    [Header("Audio Mixer Settings")]
    public AudioMixerGroup bgmMixerGroup;

    public float maxVolume = 1.0f;
    public float fadeSpeed = 0.5f;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            audioSource = GetComponent<AudioSource>();

            if (audioSource != null && bgmMixerGroup != null)
            {
                audioSource.outputAudioMixerGroup = bgmMixerGroup;
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // 외부에서 BGMManager.Instance.ChangeBGM(클립); 으로 호출
    public void ChangeBGM(AudioClip nextClip)
    {
        if (nextClip == null) return;
        if (audioSource.clip == nextClip) return; // 이미 재생 중이면 무시

        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);

        fadeCoroutine = StartCoroutine(FadeAndPlay(nextClip));
    }

    IEnumerator FadeAndPlay(AudioClip nextClip)
    {
        // 1. 기존 음악 페이드 아웃
        while (audioSource.volume > 0)
        {
            audioSource.volume -= Time.deltaTime * fadeSpeed;
            yield return null;
        }

        // 2. 음악 교체 및 재생
        audioSource.clip = nextClip;
        audioSource.Play();

        // 3. 새 음악 페이드 인
        while (audioSource.volume < maxVolume)
        {
            audioSource.volume += Time.deltaTime * fadeSpeed;
            yield return null;
        }

        audioSource.volume = maxVolume;
    }
}