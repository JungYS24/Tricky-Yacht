using UnityEngine;

// 전투와 UI가 빛 이미지를 공유하고 마지막 사용자가 생성 리소스를 반환합니다.
internal static class SatelliteVisualResources
{
    private static SatelliteVisualProfile profile;
    internal static SatelliteVisualProfile Profile => profile != null ? profile : (profile = Resources.Load<SatelliteVisualProfile>("SatelliteVisualProfile"));
    internal static Sprite glowSprite;
    internal static Sprite dotSprite;
    internal static Material effectMaterial;
    private static int users;
    internal static void Acquire(bool worldRenderer)
    {
        users++;
        if (glowSprite == null) glowSprite = CreateRadialSprite(64, false);
        if (dotSprite == null) dotSprite = CreateRadialSprite(16, true);
        if (worldRenderer && effectMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null) effectMaterial = new Material(shader) { name = "Satellite Shared Unlit", hideFlags = HideFlags.DontSave };
        }
    }
    internal static void Release()
    {
        if (--users != 0) return;
        if (glowSprite != null) { Object.Destroy(glowSprite.texture); Object.Destroy(glowSprite); }
        if (dotSprite != null) { Object.Destroy(dotSprite.texture); Object.Destroy(dotSprite); }
        if (effectMaterial != null) Object.Destroy(effectMaterial);
        glowSprite = null;
        dotSprite = null;
        effectMaterial = null;
    }

    internal static Vector3 OrbitPosition(Vector3 center, Vector2 radius, float degrees)
    {
        float angle = degrees * Mathf.Deg2Rad;
        return center + new Vector3(Mathf.Cos(angle) * radius.x, Mathf.Sin(angle) * radius.y, 0f);
    }

    internal static float StartAngle(SatelliteType type)
    {
        switch (type)
        {
            case SatelliteType.Mercury: return 45f;
            case SatelliteType.Mars: return 135f;
            case SatelliteType.Venus: return 225f;
            case SatelliteType.Jupiter: return 315f;
            default: return 0f;
        }
    }

    private static Sprite CreateRadialSprite(int size, bool dot)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = dot ? "SatelliteDot" : "SatelliteGlow", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = (x + 0.5f) / size * 2f - 1f;
            float dy = (y + 0.5f) / size * 2f - 1f;
            float distance = Mathf.Sqrt(dx * dx + dy * dy);
            float alpha = dot ? 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 0.95f, distance)) : Mathf.Pow(Mathf.Clamp01(1f - distance), 2f);
            pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, true); // CPU 픽셀 복사본은 해제하고 GPU 텍스처만 공유합니다.
        var sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size, 0, SpriteMeshType.FullRect);
        sprite.hideFlags = HideFlags.DontSave;
        return sprite;
    }
}
