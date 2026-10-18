using UnityEngine;
using UnityEngine.UI;

// 테이블 위 주사위 가운데에 스탬프 자국을 그린다. 스탬프가 찍힌 주사위를 처음 그릴 때 Dice가 붙인다.
// 자국은 도장 잉크처럼 바탕에 곱해 칠해(M_StampInk) 주사위 눈을 가리지 않고,
// 문양 바깥쪽에는 밝은 테두리(M_StampOutline)를 둘러 코팅으로 어둡거나 색이 있는 주사위에서도 모양이 보이게 한다.
// 덱 슬롯(StampUIVisual)과 같은 UI 머티리얼을 쓰려고 주사위 위에 작은 월드 캔버스를 두고 그린다.
// 캔버스 영역을 주사위 그림 영역에 맞춰 덱 슬롯과 같은 비율 배치를 쓴다.
// 크기·진하기·머티리얼은 StampVisualProfile(Resources)에서 조절한다.
[DisallowMultipleComponent]
public sealed class StampBadge : MonoBehaviour
{
    SpriteRenderer dieRenderer;
    Canvas canvas;
    Image edge;
    Image mark;

    public static StampBadge Get(Component owner)
    {
        var found = owner.GetComponent<StampBadge>();
        return found != null ? found : owner.gameObject.AddComponent<StampBadge>();
    }

    public void Bind(SpriteRenderer dieSprite, StampType type)
    {
        dieRenderer = dieSprite;
        Sprite sprite = StampVisualProfile.GetSprite(type);
        StampVisualProfile profile = StampVisualProfile.Profile;
        if (sprite == null || profile == null || dieRenderer == null || dieRenderer.sprite == null)
        {
            if (canvas != null) canvas.gameObject.SetActive(false);
            return;
        }

        if (canvas == null) Create();
        canvas.gameObject.SetActive(true);

        Bounds die = dieRenderer.sprite.bounds;
        var rect = (RectTransform)canvas.transform;
        rect.localPosition = new Vector3(die.center.x, die.center.y, 0f);
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;
        rect.sizeDelta = new Vector2(die.size.x, die.size.y);

        StampUIVisual.PlaceMark(edge.rectTransform, profile);
        StampUIVisual.PlaceMark(mark.rectTransform, profile);
        Material outline = profile.GetOutlineMaterial();
        edge.material = outline;
        edge.sprite = sprite;
        edge.enabled = outline != null && profile.edgeOpacity > 0f;
        mark.material = profile.inkMaterial; // 비어 있으면 기본 UI 머티리얼(일반 반투명)
        mark.sprite = sprite;
        SyncSorting();
    }

    // 킵하면 주사위가 어두워진다. 곱하기 잉크는 어두워진 주사위를 그대로 따라가므로 테두리만 같이 어둡게 한다.
    public void SetTint(Color color)
    {
        if (mark == null) return;
        StampVisualProfile profile = StampVisualProfile.Profile;
        if (profile == null) return;
        bool multiply = profile.inkMaterial != null;
        mark.color = multiply
            ? new Color(1f, 1f, 1f, color.a * profile.markOpacity)
            : new Color(color.r, color.g, color.b, color.a * profile.markOpacity);
        Color edgeColor = profile.edgeColor;
        edge.color = new Color(edgeColor.r * color.r, edgeColor.g * color.g, edgeColor.b * color.b, color.a * profile.edgeOpacity);
    }

    void Create()
    {
        var go = new GameObject("StampMark", typeof(RectTransform), typeof(Canvas));
        go.layer = gameObject.layer;
        go.transform.SetParent(transform, false);
        canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;

        edge = CreateImage("Edge", go.transform);
        mark = CreateImage("Ink", go.transform);
    }

    Image CreateImage(string objectName, Transform parent)
    {
        var go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = gameObject.layer;
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.raycastTarget = false;
        image.preserveAspect = true;
        return image;
    }

    // 주사위 정렬 순서가 바뀌어도 바로 위에 그린다. 결산 때 어둡게 덮는 DiceHandHighlight(+2)보다는 아래.
    void SyncSorting()
    {
        if (canvas == null || dieRenderer == null) return;
        canvas.sortingLayerID = dieRenderer.sortingLayerID;
        canvas.sortingOrder = dieRenderer.sortingOrder + 1;
    }

    void LateUpdate()
    {
        if (canvas != null && canvas.gameObject.activeSelf) SyncSorting();
    }
}
