using UnityEngine;
using UnityEngine.UI;

// 덱 화면·선택창 슬롯의 주사위 아이콘에 스탬프 문양을 새긴다. DeckSlot이 자동으로 붙인다.
// 위에 덮는 그림 없이 아이콘 Image.sprite를 문양을 새긴 그림(StampEngraveCache)으로 바꿔 끼워 테이블 주사위와 같은 모습이 된다.
// DeckSlot이 눈금 애니메이션으로 아이콘 그림을 바꿔도 같은 프레임 안에 새긴 그림으로 다시 바꾼다.
[DisallowMultipleComponent]
public sealed class StampUIVisual : MonoBehaviour
{
    Image icon;
    StampType stamp;
    Sprite source; // DeckSlot이 넣은 원래 그림
    Sprite shown;  // 마지막으로 넣은 새긴 그림

    // 새기기 전 원래 그림. 도장 연출이 젖은 잉크 모습을 만들 때 쓴다.
    public Sprite SourceSprite
    {
        get
        {
            if (icon == null) icon = GetComponent<Image>();
            if (icon != null && icon.sprite != shown) return icon.sprite;
            return source;
        }
    }

    // alpha는 스탬프 표시 진하기다. 0이면 새기지 않고, 사용한 주사위(0.4)는 아이콘 자체가 흐려지므로 새김도 같이 흐려진다.
    public void Bind(DiceData1 data, float alpha = 1f)
    {
        if (icon == null) icon = GetComponent<Image>();
        stamp = data != null && alpha > 0f ? data.stampType : StampType.None;
        Refresh(true);
    }

    void LateUpdate() => Refresh(false);

    void Refresh(bool force)
    {
        if (icon == null) return;
        Sprite current = icon.sprite;
        if (!force && current == shown) return;
        if (current != shown) source = current; // DeckSlot이 새 그림을 넣었다
        shown = StampEngraveCache.Get(source, stamp);
        if (icon.sprite != shown) icon.sprite = shown;
    }
}
