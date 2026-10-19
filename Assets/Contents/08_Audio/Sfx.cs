using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

// SfxLibrary를 한 번만 로드해 SfxId로 효과음을 재생한다.
public static class Sfx
{
    static SfxLibrary cachedLibrary;
    static readonly HashSet<SfxId> warnedIds = new HashSet<SfxId>();

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
        if (library == null)
        {
            WarnOnce(id, "SfxLibrary가 없습니다.");
            return;
        }

        AudioEvent audioEvent = library.Get(id);
        if (audioEvent == null)
        {
            WarnOnce(id, "해당 SfxId 항목이 없습니다.");
            return;
        }

        if (!HasClip(audioEvent))
        {
            WarnOnce(id, "clips가 비어 있습니다.");
            return;
        }

        AudioControl.Instance.PlaySFX(audioEvent, pitchMul);
    }

    static bool HasClip(AudioEvent audioEvent)
    {
        if (audioEvent.clips == null || audioEvent.clips.Length == 0) return false;
        for (int i = 0; i < audioEvent.clips.Length; i++)
        {
            if (audioEvent.clips[i] != null) return true;
        }
        return false;
    }

    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    static void WarnOnce(SfxId id, string reason)
    {
        if (!warnedIds.Add(id)) return;
        Debug.LogWarning("[Sfx] " + id + " " + reason);
    }
}
