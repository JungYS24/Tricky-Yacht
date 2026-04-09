using UnityEngine;

[CreateAssetMenu(menuName = "Audio Events/Simple")]
public class AudioEvent : ScriptableObject
{
    public AudioClip[] clips; // 랜덤으로 나올 클립들

    public Vector2 volumeRange = new Vector2(0.5f, 0.5f); // 볼륨 범위 (Modulator 노드)
    public Vector2 pitchRange = new Vector2(1f, 1f);   // 피치 범위 (Modulator 노드)

    public void Play(AudioSource source)
    {
        if (clips.Length == 0) return;

        source.clip = clips[Random.Range(0, clips.Length)];
        source.volume = Random.Range(volumeRange.x, volumeRange.y);
        source.pitch = Random.Range(pitchRange.x, pitchRange.y);
        source.Play();
    }
}