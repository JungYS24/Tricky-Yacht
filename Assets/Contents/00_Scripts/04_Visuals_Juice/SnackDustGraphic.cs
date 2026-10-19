using UnityEngine;
using UnityEngine.UI;

// 스낵마다 재사용하는 작은 빛 알갱이 메시. 이미지/프리팹과 게임 난수 사용이 필요 없습니다.
public sealed class SnackDustGraphic : MaskableGraphic
{
    private float progress = 1f;

    public void SetProgress(float value)
    {
        progress = value;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        if (progress <= 0f || progress >= 1f) return;

        Rect rect = rectTransform.rect;
        float unit = Mathf.Min(rect.width, rect.height);
        for (int i = 0; i < 22; i++)
        {
            float seedX = Fraction(i * 0.618034f + 0.13f);
            float seedY = Fraction(i * 0.414214f + 0.31f);
            float age = (progress - (i % 5) * 0.045f) / 0.78f;
            if (age <= 0f || age >= 1f) continue;

            float x = Mathf.Lerp(rect.xMin, rect.xMax, seedX);
            float y = Mathf.Lerp(rect.yMin, rect.yMax, seedY);
            x += Mathf.Sin(age * 3f + i) * unit * 0.065f * age;
            y += unit * (0.4f + seedX * 0.45f) * age;
            float alpha = Mathf.Sin(age * Mathf.PI) * (1f - age * 0.35f);
            float radius = unit * (0.008f + seedY * 0.009f);
            Color warm = Color.Lerp(new Color(1f, 0.7f, 0.3f), new Color(1f, 0.97f, 0.8f), seedX);
            AddGrain(mesh, x, y, radius * 2.5f, new Color(warm.r, warm.g, warm.b, alpha * 0.12f));
            AddGrain(mesh, x, y, radius * 1.5f, new Color(warm.r, warm.g, warm.b, alpha * 0.3f));
            AddGrain(mesh, x, y, radius, new Color(warm.r, warm.g, warm.b, alpha));
        }
    }

    private static float Fraction(float value) => value - Mathf.Floor(value);

    private static void AddGrain(VertexHelper mesh, float x, float y, float size, Color tint)
    {
        int start = mesh.currentVertCount;
        mesh.AddVert(new Vector3(x - size, y - size), tint, Vector2.zero);
        mesh.AddVert(new Vector3(x - size, y + size), tint, Vector2.zero);
        mesh.AddVert(new Vector3(x + size, y + size), tint, Vector2.zero);
        mesh.AddVert(new Vector3(x + size, y - size), tint, Vector2.zero);
        mesh.AddTriangle(start, start + 1, start + 2);
        mesh.AddTriangle(start, start + 2, start + 3);
    }
}
