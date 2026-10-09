using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "MapIconSet", menuName = "TrickyYacht/Map Icon Set")]
public class MapIconSet : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public string iconKey;
        public Sprite sprite;
        public Color fallbackColor = Color.white;
    }

    public List<Entry> entries = new List<Entry>();
    readonly Dictionary<string, Sprite> generated = new Dictionary<string, Sprite>();

    void OnEnable()
    {
        if (entries == null || entries.Count == 0)
            FillDefaultColors();
    }

    public void FillDefaultColors()
    {
        entries = new List<Entry>
        {
            New("Icon_Map_Enemy", new Color(0.85f, 0.25f, 0.22f)),
            New("Icon_Map_Boss", new Color(0.55f, 0.2f, 0.75f)),
            New("Icon_Map_Encounter", new Color(0.95f, 0.75f, 0.2f)),
            New("Icon_Map_Shop", new Color(0.25f, 0.7f, 0.4f)),
            New("Icon_Map_Opel", new Color(0.3f, 0.55f, 0.95f)),
            New("Icon_Map_Rest", new Color(0.75f, 0.85f, 0.95f))
        };
    }

    static Entry New(string key, Color color)
    {
        return new Entry { iconKey = key, fallbackColor = color };
    }

    public Sprite GetSprite(string iconKey, out Color tint)
    {
        tint = Color.white;
        Entry entry = Find(iconKey);
        if (entry != null)
        {
            if (entry.sprite != null) return entry.sprite;
            tint = entry.fallbackColor;
        }
        if (string.IsNullOrEmpty(iconKey)) return null;
        if (generated.TryGetValue(iconKey, out Sprite cached) && cached != null)
            return cached;

        Sprite created = MakeCircle(entry != null ? entry.fallbackColor : Color.gray);
        generated[iconKey] = created;
        return created;
    }

    Entry Find(string iconKey)
    {
        if (entries == null) return null;
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i] != null && entries[i].iconKey == iconKey)
                return entries[i];
        }
        return null;
    }

    static Sprite MakeCircle(Color color)
    {
        const int size = 32;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        var clear = new Color(0f, 0f, 0f, 0f);
        float radius = 13.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - 15.5f;
                float dy = y - 15.5f;
                texture.SetPixel(x, y, dx * dx + dy * dy <= radius * radius ? color : clear);
            }
        }
        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
    }
}
