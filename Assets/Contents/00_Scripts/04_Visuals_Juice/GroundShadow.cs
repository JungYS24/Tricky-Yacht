using UnityEngine;

// 스프라이트 발밑에 까는 부드러운 타원 그림자 (주사위, 몬스터 공용).
// 주인 스프라이트의 표시 여부, 투명도, 정렬 순서를 따라가고, 주인이 기울어도 바닥에 평평하게 놓인다.
public sealed class GroundShadow : MonoBehaviour
{
    const int TexW = 64, TexH = 32;   // 타원 텍스처 (PPU 64 -> 1 x 0.5 유닛)
    static Sprite ellipseSprite;

    SpriteRenderer owner;
    SpriteRenderer shadow;
    float baseAlpha;
    float widthRatio, heightRatio, liftRatio;
    int orderOffset = -1;   // 주인 정렬 순서 대비 (두께 같은 다른 레이어가 사이에 들어가면 -2)
    bool autoRefit;         // 주인 그림이 바뀔 때마다 크기·위치를 다시 잡을지 (주사위: 켬, 몬스터: 끔 - 프레임마다 들썩이지 않게)
    Sprite fittedSprite;
    Vector2 localCenter;
    static readonly System.Collections.Generic.Dictionary<Sprite, float> bottomCache = new System.Collections.Generic.Dictionary<Sprite, float>();

    // widthRatio / heightRatio: 주인 스프라이트 가로 길이 대비 그림자 가로 / 세로 크기
    // liftRatio: 스프라이트 바닥(그림이 있는 가장 아래)에서 그림자 중심까지의 높이, 스프라이트 세로 길이 대비
    public static GroundShadow Attach(SpriteRenderer owner, float widthRatio, float heightRatio, float liftRatio, float alpha, int orderOffset = -1, bool autoRefit = false)
    {
        if (owner == null) return null;
        var go = new GameObject("GroundShadow");
        go.layer = owner.gameObject.layer;
        go.transform.SetParent(owner.transform, false);
        var gs = go.AddComponent<GroundShadow>();
        gs.owner = owner;
        gs.shadow = go.AddComponent<SpriteRenderer>();
        gs.shadow.sprite = EllipseSprite();
        gs.baseAlpha = alpha;
        gs.widthRatio = widthRatio;
        gs.heightRatio = heightRatio;
        gs.liftRatio = liftRatio;
        gs.orderOffset = orderOffset;
        gs.autoRefit = autoRefit;
        gs.shadow.color = new Color(0f, 0f, 0f, alpha);
        gs.Refit(owner.sprite);
        gs.Sync();
        return gs;
    }

    // 스프라이트가 바뀌면(몬스터 교체 등) 크기와 발 위치를 다시 잡는다.
    public void Refit(Sprite sprite)
    {
        if (sprite == null) return;
        fittedSprite = sprite;
        Bounds b = sprite.bounds;
        float bottom;
        if (!bottomCache.TryGetValue(sprite, out bottom))
        {
            bottom = b.min.y;
            Vector2[] verts = sprite.vertices;   // Tight 메쉬면 실제 그림의 외곽, Full Rect면 프레임 모서리
            if (verts != null && verts.Length > 0)
            {
                bottom = verts[0].y;
                for (int i = 1; i < verts.Length; i++) if (verts[i].y < bottom) bottom = verts[i].y;
            }
            bottomCache[sprite] = bottom;
        }
        float w = b.size.x * widthRatio;
        float h = b.size.x * heightRatio;
        localCenter = new Vector2(b.center.x, bottom + b.size.y * liftRatio);
        transform.localScale = new Vector3(w, h / 0.5f, 1f);
    }

    void LateUpdate() { Sync(); }

    void Sync()
    {
        if (owner == null) { Destroy(gameObject); return; }
        // 생성 시점엔 그림이 비어 있다가 나중에 들어오는 경우(주사위 프리팹)도 있어서, 그림이 바뀌면 다시 맞춘다
        if (owner.sprite != null && (fittedSprite == null || (autoRefit && owner.sprite != fittedSprite))) Refit(owner.sprite);
        float a = baseAlpha * owner.color.a;
        shadow.enabled = owner.enabled && owner.sprite != null && a > 0.01f;
        shadow.color = new Color(0f, 0f, 0f, a);   // 다른 연출이 자식 색을 바꿔도 항상 검은 그림자로
        shadow.sortingLayerID = owner.sortingLayerID;
        shadow.sortingOrder = owner.sortingOrder + orderOffset;

        Transform o = owner.transform;
        Vector3 s = o.lossyScale;
        float cx = owner.flipX ? -localCenter.x : localCenter.x;
        transform.rotation = Quaternion.identity;
        // z를 아주 조금 뒤로: 같은 정렬 순서의 다른 연출(디버프 빛 등)보다 먼저(아래에) 그려지게
        transform.position = new Vector3(o.position.x + cx * s.x, o.position.y + localCenter.y * s.y, o.position.z + 0.001f);
    }

    static Sprite EllipseSprite()
    {
        if (ellipseSprite != null) return ellipseSprite;
        var tex = new Texture2D(TexW, TexH, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        var px = new Color32[TexW * TexH];
        for (int y = 0; y < TexH; y++)
            for (int x = 0; x < TexW; x++)
            {
                float nx = (x + 0.5f) / TexW * 2f - 1f, ny = (y + 0.5f) / TexH * 2f - 1f;
                float r2 = nx * nx + ny * ny;
                float a = Mathf.Clamp01(1f - r2);
                a = Mathf.Pow(a, 0.6f);                  // 안쪽은 거의 균일, 가장자리만 부드럽게
                px[y * TexW + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        tex.SetPixels32(px);
        tex.Apply(false, false);
        // FullRect: 텍스처 픽셀을 읽어 외곽 메쉬를 만들지 않고 사각형 그대로 쓴다 (빈 메쉬로 안 보이는 문제 방지)
        ellipseSprite = Sprite.Create(tex, new Rect(0, 0, TexW, TexH), new Vector2(0.5f, 0.5f), TexW, 0, SpriteMeshType.FullRect);
        ellipseSprite.name = "GroundShadowEllipse";
        return ellipseSprite;
    }
}
