using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public static class CharacterSelectUi
{
    // 게임 주사위 떨림 폭(UiHoverIdleJuice 기본 0.015 유닛)을 1080 기준 화면 px로 바꾼 값(전투 카메라 108px/유닛).
    private const float GameDiceShakePixels = 0.015f * 108f;
    private const string JuiceChildName = "DiceJuice";

    // parent 아래에 template 복제본을 count개 켜서 돌려준다. 이미 있는 복제본은 다시 쓰고 남는 것은 끈다.
    // 같은 패널을 캐릭터마다 다시 채우므로 매번 지우고 만들지 않는다.
    public static List<RectTransform> EnsureItems(RectTransform parent, RectTransform template, int count, string namePrefix)
    {
        var items = new List<RectTransform>();
        foreach (Transform child in parent)
            if (child != template)
                items.Add((RectTransform)child);

        template.gameObject.SetActive(false);

        for (int i = items.Count; i < count; i++)
            items.Add(Object.Instantiate(template, parent));

        for (int i = 0; i < items.Count; i++)
        {
            items[i].gameObject.SetActive(i < count);
            if (i < count)
                items[i].name = namePrefix + i;
        }

        items.RemoveRange(count, items.Count - count);
        return items;
    }

    // 주사위 아이콘에 그림을 넣고, 게임 주사위와 같은 떨림·호버 확대(UiHoverIdleJuice)를 붙인다.
    // 레이아웃 그룹이 위치를 정하는 아이콘은 직접 흔들면 레이아웃과 위치를 다투므로, 같은 그림을 그리는 자식 이미지를 만들어 그쪽을 흔든다.
    public static void SetDiceIcon(Image icon, Sprite sprite)
    {
        if (icon == null)
            return;

        bool visible = sprite != null;
        icon.sprite = sprite;

        // 에디터에서 화면을 미리 채울 때는 씬에 오브젝트·컴포넌트가 저장되지 않도록 그림만 넣는다.
        if (!Application.isPlaying)
        {
            icon.enabled = visible;
            return;
        }

        Image target = icon;
        if (IsLayoutDriven(icon.rectTransform))
        {
            target = JuiceChild(icon);
            target.sprite = sprite;
            target.preserveAspect = icon.preserveAspect;
            target.enabled = visible;
            icon.enabled = true; // 크기·레이아웃은 원래 아이콘이 맡고, 그림은 투명하게 둔다
        }
        else
        {
            icon.enabled = visible;
        }

        UiHoverIdleJuice juice = target.GetComponent<UiHoverIdleJuice>();
        if (juice == null)
            juice = target.gameObject.AddComponent<UiHoverIdleJuice>();
        juice.RestoreOnDisable = true;
        Canvas root = target.canvas != null ? target.canvas.rootCanvas : null;
        float worldPerPixel = root != null ? Mathf.Abs(root.transform.lossyScale.y) : 1f;
        juice.WorldShakeAmount = GameDiceShakePixels * worldPerPixel;
    }

    private static bool IsLayoutDriven(RectTransform rect)
    {
        LayoutGroup group = rect.parent != null ? rect.parent.GetComponent<LayoutGroup>() : null;
        if (group == null || !group.enabled)
            return false;
        LayoutElement element = rect.GetComponent<LayoutElement>();
        return element == null || !element.ignoreLayout;
    }

    private static Image JuiceChild(Image icon)
    {
        Transform existing = icon.transform.Find(JuiceChildName);
        if (existing != null)
            return existing.GetComponent<Image>();

        var go = new GameObject(JuiceChildName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rect = (RectTransform)go.transform;
        rect.SetParent(icon.transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = go.GetComponent<Image>();
        image.color = icon.color;
        if (icon.material != icon.defaultMaterial)
            image.material = icon.material;

        // 원래 아이콘은 자리만 잡고 그리지 않는다.
        Color clear = icon.color;
        clear.a = 0f;
        icon.color = clear;
        return image;
    }
}
