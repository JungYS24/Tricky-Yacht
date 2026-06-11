using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
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

    [Header("Lobby BGM Settings")]
    // 로비 씬으로 돌아왔을 때 재생할 로비 브금 에셋 슬롯
    public AudioClip lobbyBGM;

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
            if (SceneManager.GetActiveScene().name == "Lobby" && lobbyBGM != null)
            {
                ChangeBGM(lobbyBGM);
                Debug.Log("로비 진입 즉시 감지 및 재생!");
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "Lobby")
        {
            if (lobbyBGM != null)
            {
                ChangeBGM(lobbyBGM);
                Debug.Log("<color=yellow>[BGMManager]</color> 로비 진입 감지 : 로비 BGM 페이드 스왑 시작");
            }
            else
            {
                StopBGM();
            }
        }
    }

    // 외부에서 BGMManager.Instance.ChangeBGM(클립); 으로 호출
    public void ChangeBGM(AudioClip nextClip)
    {
        if (nextClip == null) return;
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (audioSource.clip == nextClip) return; // 이미 재생 중이면 무시

        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);

        fadeCoroutine = StartCoroutine(FadeAndPlay(nextClip));
    }

    public void StopBGM()
    {
        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        if (audioSource != null)
        {
            audioSource.Stop();
            audioSource.volume = 0f;
        }
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