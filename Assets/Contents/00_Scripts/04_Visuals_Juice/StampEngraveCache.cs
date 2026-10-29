using System.Collections.Generic;
using UnityEngine;

// 스탬프 문양을 주사위 그림 픽셀에 직접 새긴 스프라이트를 만들어 돌려준다. 테이블 주사위·덱 슬롯·도장 연출이 같은 결과를 쓴다.
// 위에 덮어 그리지 않고 몸체 픽셀 색만 바꾸므로 도트가 어긋나지 않고, 눈·테두리 픽셀은 절대 바뀌지 않는다.
// (주사위 그림, 스탬프 종류) 조합마다 한 번만 만들어 캐시한다. 수치·문양은 StampVisualProfile에서 조절한다.
//
// 주사위 텍스처는 Read/Write가 꺼져 있으므로 임포트 설정을 바꾸지 않고 RenderTexture에 복사해 그림 영역만 읽는다.
// 계산은 이미지 좌표(위가 y=0)로 하고, 텍스처(아래가 y=0)와 오갈 때 뒤집는다.
public static class StampEngraveCache
{
    const int GlyphSize = 26;   // 64px 주사위 기준 문양 크기
    const int BaseDieSize = 64;

    struct Key
    {
        public Sprite sprite;
        public StampType type;
        public bool wet;
    }

    sealed class KeyComparer : IEqualityComparer<Key>
    {
        public bool Equals(Key a, Key b) => a.sprite == b.sprite && a.type == b.type && a.wet == b.wet;
        public int GetHashCode(Key k) => ((k.sprite != null ? k.sprite.GetInstanceID() : 0) * 31 + (int)k.type) * 2 + (k.wet ? 1 : 0);
    }

    static readonly Dictionary<Key, Sprite> cache = new Dictionary<Key, Sprite>(new KeyComparer());
    static readonly Dictionary<Sprite, Color32[]> sourcePixels = new Dictionary<Sprite, Color32[]>();
    static readonly Dictionary<long, bool[]> glyphMasks = new Dictionary<long, bool[]>();
    static readonly HashSet<Sprite> created = new HashSet<Sprite>();

    // 새긴 스프라이트. 스탬프가 없거나 만들 수 없으면 원본을 그대로 돌려준다.
    public static Sprite Get(Sprite dieSprite, StampType type) => Get(dieSprite, type, false);

    // 갓 찍힌 젖은 잉크 모습(문양 기본색만 진하다). 스탬프 선택창의 도장 연출에서 쓴다.
    public static Sprite GetWet(Sprite dieSprite, StampType type) => Get(dieSprite, type, true);

    // 이미 새긴 스프라이트인지. 원본을 다시 찾을 때 쓴다.
    public static bool IsEngraved(Sprite sprite) => sprite != null && created.Contains(sprite);

    // 수치를 바꾼 뒤 다시 만들고 싶을 때 쓴다.
    public static void Clear()
    {
        foreach (Sprite sprite in created)
        {
            if (sprite == null) continue;
            if (sprite.texture != null) DestroySafe(sprite.texture);
            DestroySafe(sprite);
        }
        created.Clear();
        cache.Clear();
        sourcePixels.Clear();
        glyphMasks.Clear();
    }

    static Sprite Get(Sprite dieSprite, StampType type, bool wet)
    {
        if (dieSprite == null || type == StampType.None || created.Contains(dieSprite)) return dieSprite;
        var key = new Key { sprite = dieSprite, type = type, wet = wet };
        if (cache.TryGetValue(key, out Sprite result) && result != null) return result;

        result = Build(dieSprite, type, wet) ?? dieSprite; // 실패하면 원본으로 캐시해서 다시 시도하지 않는다
        cache[key] = result;
        return result;
    }

    static Sprite Build(Sprite die, StampType type, bool wet)
    {
        StampVisualProfile profile = StampVisualProfile.Profile;
        if (profile == null) return null;
        Sprite glyphSprite = profile.GetEngrave(type);
        if (glyphSprite == null) return null;

        int w = Mathf.RoundToInt(die.rect.width);
        int h = Mathf.RoundToInt(die.rect.height);
        int k = Mathf.Max(1, Mathf.RoundToInt(h / (float)BaseDieSize));
        int glyphSize = GlyphSize * k;
        if (w < glyphSize || h < glyphSize) return null;

        if (!sourcePixels.TryGetValue(die, out Color32[] src))
        {
            src = ReadSprite(die);
            sourcePixels[die] = src;
        }
        bool[] glyph = GetGlyph(glyphSprite, type, k);
        if (src == null || glyph == null) return null;

        Color32[] pixels = Engrave(src, w, h, glyph, glyphSize, k, profile.GetInk(type), (int)type, profile, wet);
        if (pixels == null) return null;

        var texture = new Texture2D(w, h, TextureFormat.RGBA32, false, false)
        {
            name = die.name + "_Stamp" + type + (wet ? "_Wet" : ""),
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave,
        };
        texture.SetPixels32(pixels);
        texture.Apply(false, false); // 타이트 메시를 만들 수 있게 읽기 가능으로 둔다(64px라 메모리 부담이 작다)

        // 원본이 타이트 메시면 bounds가 rect보다 작다. 같은 메시 방식으로 만들어야 bounds를 쓰는 연출 위치가 같다.
        Vector2 rectSize = die.rect.size / die.pixelsPerUnit;
        Vector3 boundsSize = die.bounds.size;
        bool tight = Mathf.Abs(boundsSize.x - rectSize.x) > 0.001f || Mathf.Abs(boundsSize.y - rectSize.y) > 0.001f;
        Vector2 pivot = new Vector2(die.pivot.x / die.rect.width, die.pivot.y / die.rect.height);
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, w, h), pivot, die.pixelsPerUnit, 0, tight ? SpriteMeshType.Tight : SpriteMeshType.FullRect);
        sprite.name = texture.name;
        sprite.hideFlags = HideFlags.DontSave;
        created.Add(sprite);
        return sprite;
    }

    // 스프라이트 rect 크기의 픽셀(텍스처 방향, 아래가 y=0). 아틀라스에서 잘린 투명 여백과 뒤집힌 패킹을 되돌린다.
    static Color32[] ReadSprite(Sprite sprite)
    {
        Texture2D texture = sprite.texture;
        if (texture == null) return null;
        Rect region;
        try { region = sprite.textureRect; }
        catch (System.Exception) { return null; } // 타이트 패킹된 아틀라스 스프라이트는 사각형 영역이 없다

        int w = Mathf.RoundToInt(sprite.rect.width);
        int h = Mathf.RoundToInt(sprite.rect.height);
        int rx = Mathf.RoundToInt(region.x), ry = Mathf.RoundToInt(region.y);
        int rw = Mathf.RoundToInt(region.width), rh = Mathf.RoundToInt(region.height);
        if (rw <= 0 || rh <= 0) return null;

        Color32[] part = texture.isReadable ? ReadDirect(texture, rx, ry, rw, rh) : ReadByBlit(texture, rx, ry, rw, rh);
        if (part == null) return null;

        if (sprite.packed && sprite.packingMode == SpritePackingMode.Rectangle)
        {
            bool flipX = sprite.packingRotation == SpritePackingRotation.FlipHorizontal || sprite.packingRotation == SpritePackingRotation.Rotate180;
            bool flipY = sprite.packingRotation == SpritePackingRotation.FlipVertical || sprite.packingRotation == SpritePackingRotation.Rotate180;
            if (flipX || flipY) part = Flip(part, rw, rh, flipX, flipY);
        }

        Vector2 offset = sprite.textureRectOffset;
        int ox = Mathf.RoundToInt(offset.x), oy = Mathf.RoundToInt(offset.y);
        var full = new Color32[w * h];
        for (int y = 0; y < rh; y++)
        {
            int ty = y + oy;
            if (ty < 0 || ty >= h) continue;
            for (int x = 0; x < rw; x++)
            {
                int tx = x + ox;
                if (tx < 0 || tx >= w) continue;
                full[ty * w + tx] = part[y * rw + x];
            }
        }
        return full;
    }

    static Color32[] ReadDirect(Texture2D texture, int x, int y, int w, int h)
    {
        Color[] colors = texture.GetPixels(x, y, w, h);
        var result = new Color32[colors.Length];
        for (int i = 0; i < colors.Length; i++) result[i] = colors[i];
        return result;
    }

    static Color32[] ReadByBlit(Texture2D texture, int x, int y, int w, int h)
    {
        RenderTexture rt = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        RenderTexture previous = RenderTexture.active;
        var readback = new Texture2D(w, h, TextureFormat.RGBA32, false, false);
        try
        {
            Graphics.Blit(texture, rt);
            RenderTexture.active = rt;
            readback.ReadPixels(new Rect(x, y, w, h), 0, 0, false);
            readback.Apply(false);
            return readback.GetPixels32();
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            DestroySafe(readback);
        }
    }

    // 에디터 배치모드(재생 중 아님)에서도 부를 수 있게 한다.
    static void DestroySafe(Object target)
    {
        if (Application.isPlaying) Object.Destroy(target);
        else Object.DestroyImmediate(target);
    }

    static Color32[] Flip(Color32[] pixels, int w, int h, bool flipX, bool flipY)
    {
        var result = new Color32[pixels.Length];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                result[y * w + x] = pixels[(flipY ? h - 1 - y : y) * w + (flipX ? w - 1 - x : x)];
        return result;
    }

    // 문양 마스크(이미지 좌표, 26k × 26k). 흰색 불투명 픽셀이 문양이다. k배는 픽셀마다 정수 확대.
    static bool[] GetGlyph(Sprite glyph, StampType type, int k)
    {
        long key = ((long)glyph.GetInstanceID() << 8) | ((long)type << 4) | (long)k;
        if (glyphMasks.TryGetValue(key, out bool[] mask)) return mask;

        Color32[] pixels = ReadSprite(glyph);
        int gw = Mathf.RoundToInt(glyph.rect.width), gh = Mathf.RoundToInt(glyph.rect.height);
        if (pixels == null || gw != GlyphSize || gh != GlyphSize)
        {
            if (pixels != null) Debug.LogWarning($"[StampEngraveCache] 새김 문양은 {GlyphSize}×{GlyphSize}이어야 합니다: {glyph.name} ({gw}×{gh})");
            glyphMasks[key] = null;
            return null;
        }

        int size = GlyphSize * k;
        mask = new bool[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Color32 c = pixels[(GlyphSize - 1 - y / k) * GlyphSize + x / k];
                mask[y * size + x] = c.a >= 128;
            }
        glyphMasks[key] = mask;
        return mask;
    }

    static float Luma(Color32 c) => 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
    static int Saturation(Color32 c) => Mathf.Max(c.r, Mathf.Max(c.g, c.b)) - Mathf.Min(c.r, Mathf.Min(c.g, c.b));

    // src는 텍스처 방향(아래가 y=0). 반환도 같은 방향. 몸체를 못 찾으면 null.
    static Color32[] Engrave(Color32[] src, int w, int h, bool[] glyph, int glyphSize, int k, Color32 ink, int typeSeed, StampVisualProfile p, bool wet)
    {
        int n = w * h;
        var img = new Color32[n];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                img[y * w + x] = src[(h - 1 - y) * w + x];

        // 몸체색: 가운데 영역 불투명 픽셀에서 가장 많이 나오는 RGB
        int sampleMin = Mathf.Max(0, p.bodySampleMin * k);
        int sampleMaxX = Mathf.Min(w, p.bodySampleMax * k);
        int sampleMaxY = Mathf.Min(h, p.bodySampleMax * k);
        var counts = new Dictionary<int, int>();
        int bestKey = -1, bestCount = 0;
        for (int y = sampleMin; y < sampleMaxY; y++)
            for (int x = sampleMin; x < sampleMaxX; x++)
            {
                Color32 c = img[y * w + x];
                if (c.a != 255) continue;
                int rgb = (c.r << 16) | (c.g << 8) | c.b;
                counts.TryGetValue(rgb, out int count);
                counts[rgb] = ++count;
                if (count > bestCount) { bestCount = count; bestKey = rgb; }
            }
        if (bestKey < 0) return null;
        var body0 = new Color32((byte)(bestKey >> 16), (byte)(bestKey >> 8), (byte)bestKey, 255);
        float l0 = Luma(body0);
        bool brightDie = l0 > p.highlightBodyLuma;

        // 몸체 픽셀 고르기
        var body = new bool[n];
        var opaque = new bool[n];
        for (int i = 0; i < n; i++)
        {
            Color32 c = img[i];
            if (c.a == 0) continue;
            opaque[i] = true;
            float l = Luma(c);
            int diff = Mathf.Abs(c.r - body0.r) + Mathf.Abs(c.g - body0.g) + Mathf.Abs(c.b - body0.b);
            bool near = Mathf.Abs(l - l0) < p.bodyLumaTolerance && diff < p.bodyColorTolerance;
            // 밝은 주사위의 흰 하이라이트(노멀 5 가운데 링 등)도 끊기지 않게 새긴다
            bool highlight = brightDie && l >= l0 - p.highlightLumaMargin && Saturation(c) < p.highlightMaxSaturation;
            body[i] = near || highlight;
        }

        // 테두리 제외: 알파 영역을 4방향으로 깎은 안쪽만 남긴다
        bool[] inner = opaque;
        int erode = Mathf.Max(0, p.borderErode * k);
        for (int step = 0; step < erode; step++)
        {
            var next = new bool[n];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    next[i] = inner[i]
                        && x > 0 && inner[i - 1] && x < w - 1 && inner[i + 1]
                        && y > 0 && inner[i - w] && y < h - 1 && inner[i + w];
                }
            inner = next;
        }
        for (int i = 0; i < n; i++) body[i] &= inner[i];

        // 문양 마스크: 가운데 정렬, 몸체 픽셀에만
        int ox = (w - glyphSize) / 2;
        int oy = (h - glyphSize) / 2;
        var mark = new bool[n];
        for (int gy = 0; gy < glyphSize; gy++)
            for (int gx = 0; gx < glyphSize; gx++)
            {
                if (!glyph[gy * glyphSize + gx]) continue;
                int x = ox + gx, y = oy + gy;
                if (x < 0 || y < 0 || x >= w || y >= h) continue;
                int i = y * w + x;
                mark[i] = body[i];
            }

        Vector2 fill = wet ? p.engraveWetFill : p.engraveFill;
        uint wearLimit = (uint)Mathf.RoundToInt(Mathf.Clamp01(p.wearRatio) * 10000f);
        var result = new Color32[n];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                Color32 c = img[i];
                if (mark[i])
                {
                    // 위·왼쪽이 문양 밖이면 파인 안쪽 그림자
                    bool shadow = y == 0 || !mark[i - w] || x == 0 || !mark[i - 1];
                    if (shadow) c = Mix(c, ink, p.engraveShadow);
                    else if (Hash(x, y, typeSeed) % 10000u < wearLimit) c = Mix(c, ink, p.engraveWear);
                    else c = Mix(c, ink, fill);
                }
                else if (body[i])
                {
                    // 아래·오른쪽이 문양이면 밝은 턱
                    bool lip = (y < h - 1 && mark[i + w]) || (x < w - 1 && mark[i + 1]);
                    if (lip) c = Lip(c, p.lipScale, p.lipAdd);
                }
                result[(h - 1 - y) * w + x] = c;
            }
        return result;
    }

    static Color32 Mix(Color32 c, Color32 ink, Vector2 weight)
    {
        return new Color32(
            Channel(c.r * weight.x + ink.r * weight.y),
            Channel(c.g * weight.x + ink.g * weight.y),
            Channel(c.b * weight.x + ink.b * weight.y),
            c.a);
    }

    static Color32 Lip(Color32 c, float scale, float add)
    {
        return new Color32(Channel(c.r * scale + add), Channel(c.g * scale + add), Channel(c.b * scale + add), c.a);
    }

    static byte Channel(float v) => (byte)Mathf.Clamp(Mathf.RoundToInt(v), 0, 255);

    // 좌표와 스탬프 종류로 정해지는 해시. 닳은 픽셀을 매번 같은 자리로 고른다.
    static uint Hash(int x, int y, int seed)
    {
        unchecked
        {
            uint hsh = (uint)x * 73856093u ^ (uint)y * 19349663u ^ (uint)(seed + 1) * 83492791u;
            hsh ^= hsh >> 13;
            hsh *= 0x5bd1e995u;
            hsh ^= hsh >> 15;
            return hsh;
        }
    }
}
