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
        AudioClip clip = PickClip();
        if (source == null || clip == null)
            return;

        ApplyMixer(source);
        source.clip = clip;
        source.volume = Random.Range(volumeRange.x, volumeRange.y);
        source.pitch = Random.Range(pitchRange.x, pitchRange.y);
        source.Play();
    }

    public void PlayOneShot(AudioSource source)
    {
        AudioClip clip = PickClip();
        if (source == null || clip == null)
            return;

        ApplyMixer(source);
        float previousPitch = source.pitch;
        source.pitch = Random.Range(pitchRange.x, pitchRange.y);
        source.PlayOneShot(clip, Random.Range(volumeRange.x, volumeRange.y));
        source.pitch = previousPitch;
    }

    private AudioClip PickClip()
    {
        if (clips == null || clips.Length == 0)
            return null;

        return clips[Random.Range(0, clips.Length)];
    }

    private void ApplyMixer(AudioSource source)
    {
        if (customMixerGroup != null)
            source.outputAudioMixerGroup = customMixerGroup;
    }
}
