using UnityEngine;

// SfxLibrary를 한 번만 로드해 SfxId로 효과음을 재생한다.
public static class Sfx
{
    static SfxLibrary cachedLibrary;

    static SfxLibrary Library
    {
        get
        {
            if (cachedLibrary == null)
                cachedLibrary = Resources.Load<SfxLibrary>("SfxLibrary");
            return cachedLibrary;
        }
    }

    public static void Play(SfxId id, float pitchMul = 1f)
    {
        if (AudioControl.Instance == null) return;

        SfxLibrary library = Library;
        if (library == null) return;

        AudioEvent audioEvent = library.Get(id);
        if (audioEvent == null) return;

        AudioControl.Instance.PlaySFX(audioEvent, pitchMul);
    }
}
