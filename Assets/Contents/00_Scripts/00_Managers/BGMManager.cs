using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class BGMManager : MonoBehaviour
{
    public static BGMManager Instance;

    [System.Serializable]
    public struct BiomeBGM
    {
        public string biomeName; // "Forest", "Glitch", "Desert" 등
        public AudioClip clip;
    }

    public List<BiomeBGM> biomeBgmList; // 인스펙터에서 바이옴별로 세팅
    private AudioSource audioSource;
    public float fadeSpeed = 0.5f;

    void Awake()
    {
        if (Instance == null) Instance = this;
        audioSource = GetComponent<AudioSource>();
    }

    // 외부에서 BGMManager.Instance.ChangeBGM("Forest"); 로 호출
    public void ChangeBGM(string biomeName)
    {
        foreach (var bgm in biomeBgmList)
        {
            if (bgm.biomeName == biomeName)
            {
                if (audioSource.clip == bgm.clip) return; // 이미 재생 중이면 무시
                StartCoroutine(FadeAndPlay(bgm.clip));
                return;
            }
        }
        Debug.LogWarning(biomeName + " 바이옴의 BGM이 설정되지 않았습니다.");
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
        while (audioSource.volume < 1.0f)
        {
            audioSource.volume += Time.deltaTime * fadeSpeed;
            yield return null;
        }
    }
}