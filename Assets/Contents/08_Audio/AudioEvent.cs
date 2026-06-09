using UnityEngine;
using UnityEngine.Audio;

[CreateAssetMenu(menuName = "Audio Events/Simple")]
public class AudioEvent : ScriptableObject
{
    public AudioClip[] clips;

    public Vector2 volumeRange = new Vector2(0.5f, 0.5f);
    public Vector2 pitchRange = new Vector2(1f, 1f);

    [Header("선택 사항 (공란일 시 AudioSource 기본 믹서 준수)")]
    public AudioMixerGroup customMixerGroup;

    public void Play(AudioSource source)
    {
        if (clips.Length == 0) return;

        // 1. 소스 데이터 및 모듈레이터 세팅
        source.clip = clips[Random.Range(0, clips.Length)];
        source.volume = Random.Range(volumeRange.x, volumeRange.y);
        source.pitch = Random.Range(pitchRange.x, pitchRange.y);

        if (customMixerGroup != null)
        {
            source.outputAudioMixerGroup = customMixerGroup;
        }

        source.Play();
    }
}