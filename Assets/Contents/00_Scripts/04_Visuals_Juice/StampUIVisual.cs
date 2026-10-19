using UnityEngine;
using UnityEngine.UI;

// 덱 화면·선택창 슬롯의 주사위 아이콘 가운데에 스탬프 자국을 그린다. DeckSlot이 자동으로 붙인다.
// 자국은 도장 잉크처럼 바탕에 곱해 칠해(M_StampInk) 주사위 눈을 가리지 않고,
// 문양 바깥쪽에는 밝은 테두리(M_StampOutline)를 둘러 코팅으로 어둡거나 색이 있는 주사위에서도 모양이 보이게 한다.
// 크기·진하기·머티리얼은 StampVisualProfile(Resources)에서 조절한다.
[DisallowMultipleComponent]
public sealed class StampUIVisual : MonoBehaviour
{
    Image edge;
    Image badge;

    public void Bind(DiceData1 data, float alpha = 1f)
    {
        Sprite sprite = data != null ? StampVisualProfile.GetSprite(data.stampType) : null;
        StampVisualProfile profile = StampVisualProfile.Profile;
        if (sprite == null || profile == null)
        {
            if (badge != null) badge.enabled = false;
            if (edge != null) edge.enabled = false;
            return;
        }

        if (badge == null)
        {
            edge = CreateImage("StampEdge");
            badge = CreateImage("StampMark");
        }

        PlaceMark(edge.rectTransform, profile);
        PlaceMark(badge.rectTransform, profile);
        edge.rectTransform.SetAsLastSibling();
        badge.rectTransform.SetAsLastSibling();

        Material outline = profile.GetOutlineMaterial();
        edge.material = outline;
        edge.sprite = sprite;
        Color edgeColor = profile.edgeColor;
        edge.color = new Color(edgeColor.r, edgeColor.g, edgeColor.b, alpha * profile.edgeOpacity);
        edge.enabled = outline != null && profile.edgeOpacity > 0f;

        badge.material = profile.inkMaterial; // 비어 있으면 기본 UI 머티리얼(일반 반투명)
        badge.sprite = sprite;
        badge.color = new Color(1f, 1f, 1f, alpha * profile.markOpacity);
        badge.enabled = true;
    }

    Image CreateImage(string objectName)
    {
        var go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = gameObject.layer;
        go.transform.SetParent(transform, false);
        var image = go.GetComponent<Image>();
        image.raycastTarget = false;
        image.preserveAspect = true;
        return image;
    }

    // 주사위 그림 영역 크기에 비례하는 앵커로 잡아 크기가 달라도 같은 자리에 붙는다.
    // 테이블 위 주사위(StampBadge)와 도장 연출의 갓 찍힌 잉크도 같은 자리를 쓴다.
    public static void PlaceMark(RectTransform rect, StampVisualProfile profile)
    {
        Vector2 center = new Vector2(0.5f, 0.5f) + profile.markOffset;
        Vector2 half = Vector2.one * (profile.markSize * 0.5f);
        rect.anchorMin = center - half;
        rect.anchorMax = center + half;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }
}
