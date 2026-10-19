using System.Collections.Generic;
using UnityEngine;

// SfxId → AudioEvent 목록. Resources/SfxLibrary.asset 으로 로드한다.
[CreateAssetMenu(fileName = "SfxLibrary", menuName = "Audio/Sfx Library")]
public class SfxLibrary : ScriptableObject
{
    [System.Serializable]
    public class Entry
    {
        public SfxId id;
        public AudioEvent audioEvent;
    }

    public List<Entry> entries = new List<Entry>();

    public AudioEvent Get(SfxId id)
    {
        if (entries == null) return null;
        for (int i = 0; i < entries.Count; i++)
        {
            Entry entry = entries[i];
            if (entry != null && entry.id == id)
                return entry.audioEvent;
        }
        return null;
    }

    public void Set(SfxId id, AudioEvent audioEvent)
    {
        if (entries == null) entries = new List<Entry>();
        for (int i = 0; i < entries.Count; i++)
        {
            Entry entry = entries[i];
            if (entry != null && entry.id == id)
            {
                entry.audioEvent = audioEvent;
                return;
            }
        }
        entries.Add(new Entry { id = id, audioEvent = audioEvent });
    }
}
