using UnityEngine;

// 테이블 위 디버프 주사위 뒤에 까는 은은한 보랏빛 후광. 가장자리가 점 없이 부드럽게 사라지는 둥근 네모 빛을
// 주사위 뒤(그리는 순서 -2)에 깔고, 천천히 숨 쉬듯 밝아졌다 어두워지게 한다.
// 주사위의 자식이라 굴림·튀어오름 크기 변화를 그대로 따라가고, 주사위가 흐려지면(가짜 주사위) 같이 흐려진다.
// 디버프가 아닌 주사위로 다시 쓰이면(풀 재사용) 꺼진다. 숨쉬기 박자는 주사위마다 고정된 값으로 어긋나게 해서 게임 난수는 건드리지 않는다.
// 크기·세기·속도·색은 DebuffVisualProfile에서, 바닥을 덮는 정도와 더하는 밝기는 M_DebuffHalo 머티리얼에서 조절한다.
[DisallowMultipleComponent]
public sealed class DebuffDiceGlow : MonoBehaviour
{
    SpriteRenderer dieRenderer;
    SpriteRenderer halo;
    float phase;

    // Dice.SetData 끝에서 부릅니다. 디버프 주사위면 켜고 아니면 끕니다(아직 붙은 적 없는 일반 주사위는 아무것도 붙이지 않음).
    public static void Sync(Dice die, SpriteRenderer dieSprite)
    {
        bool show = die.myData != null && die.myData.IsDebuff;
        var found = die.GetComponent<DebuffDiceGlow>();
        if (!show)
        {
            if (found != null) found.Hide();
            return;
        }
        if (found == null) found = die.gameObject.AddComponent<DebuffDiceGlow>();
        found.Show(dieSprite);
    }

    void Show(SpriteRenderer dieSprite)
    {
        DebuffVisualProfile p = DebuffFeedback.Profile;
        dieRenderer = dieSprite;
        if (p == null || !p.boardGlow || p.haloSprite == null || p.haloMaterial == null
            || dieRenderer == null || dieRenderer.sprite == null)
        {
            Hide();
            return;
        }
        if (halo == null)
        {
            var go = new GameObject("DebuffHalo");
            go.layer = gameObject.layer;
            go.transform.SetParent(transform, false);
            halo = go.AddComponent<SpriteRenderer>();
            // 주사위마다 숨쉬기 박자를 조금씩 어긋나게(황금비로 고르게 퍼짐)
            phase = Mathf.Repeat(transform.GetSiblingIndex() * 0.618034f, 1f);
        }
        halo.sprite = p.haloSprite;
        halo.sharedMaterial = p.haloMaterial;
        halo.gameObject.SetActive(true);
        Place(p);
        Tick(p);
    }

    void Hide()
    {
        if (halo != null) halo.gameObject.SetActive(false);
    }

    void LateUpdate()
    {
        if (halo == null || !halo.gameObject.activeSelf || dieRenderer == null) return;
        DebuffVisualProfile p = DebuffFeedback.Profile;
        if (p == null) return;
        Place(p);
        Tick(p);
    }

    // 주사위 그림 가운데에 맞추고, 주사위 그림의 긴 변 × 배율 크기로 키웁니다. 그리는 순서는 늘 주사위 뒤.
    void Place(DebuffVisualProfile p)
    {
        Sprite dieSprite = dieRenderer.sprite;
        if (dieSprite == null) return;
        Bounds die = dieSprite.bounds;
        Vector3 haloSize = halo.sprite.bounds.size;
        float target = Mathf.Max(die.size.x, die.size.y) * p.boardGlowSize;
        halo.transform.localPosition = new Vector3(die.center.x, die.center.y, 0f);
        halo.transform.localScale = new Vector3(target / Mathf.Max(0.0001f, haloSize.x), target / Mathf.Max(0.0001f, haloSize.y), 1f);
        halo.sortingLayerID = dieRenderer.sortingLayerID;
        halo.sortingOrder = dieRenderer.sortingOrder - 2; // 족보 연출의 금빛(-1)이 보랏빛 위에 오도록
        halo.enabled = dieRenderer.enabled;
    }

    // 최소~최대 세기 사이를 부드럽게 오갑니다. 주사위가 흐려지면 그만큼 같이 흐려집니다.
    void Tick(DebuffVisualProfile p)
    {
        float period = Mathf.Max(0.5f, p.boardGlowPeriod);
        float wave = 0.5f - 0.5f * Mathf.Cos((Time.unscaledTime / period + phase) * Mathf.PI * 2f);
        float strength = Mathf.Lerp(p.boardGlowStrength.x, p.boardGlowStrength.y, wave) * dieRenderer.color.a;
        Color c = p.boardGlowColor;
        halo.color = new Color(c.r, c.g, c.b, Mathf.Clamp01(strength));
    }
}
