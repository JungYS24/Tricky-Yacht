using System.Collections.Generic;
using UnityEngine;

public static class CharacterSelectUi
{
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
}
