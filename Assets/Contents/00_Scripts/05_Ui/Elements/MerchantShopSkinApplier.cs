using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 지도 상인 상점이 열릴 때 판·카드·버튼 그림을 그 상인 것으로 바꾸고 MerchantShopSkin 배치대로 놓는다.
// 씬의 상점 오브젝트들은 배율이 제각각(가로·세로도 다름)이라, 부모와 상관없이 화면 기준 위치·크기로 바로 놓는다.
// 튜토리얼 등 일반 상점이 열리면 처음 모습으로 되돌린다.
public class MerchantShopSkinApplier
{
    struct RectRest
    {
        public Vector2 anchorMin, anchorMax, pivot, anchoredPosition, sizeDelta;
        public Vector3 localScale;
    }

    struct ImageRest
    {
        public Sprite sprite;
        public Image.Type type;
        public bool preserveAspect;
    }

    struct TextRest
    {
        public TextAlignmentOptions alignment;
        public Vector4 margin;
        public float fontSize, fontSizeMin, fontSizeMax;
        public bool autoSize;
    }

    readonly ShopManager shop;
    readonly Dictionary<RectTransform, RectRest> rects = new Dictionary<RectTransform, RectRest>();
    readonly Dictionary<RectTransform, float> restScales = new Dictionary<RectTransform, float>();
    readonly Dictionary<Image, ImageRest> images = new Dictionary<Image, ImageRest>();
    readonly Dictionary<TMP_Text, TextRest> texts = new Dictionary<TMP_Text, TextRest>();
    bool applied;

    public MerchantShopSkinApplier(ShopManager shop)
    {
        this.shop = shop;
    }

    public void Apply(MerchantShopSkin skin, ShopManager.MapMerchant merchant, TMP_Text title)
    {
        MerchantShopSkin.Look look = skin != null ? skin.Find(merchant) : null;
        RectTransform canvas = CanvasRect();
        if (look == null || canvas == null)
        {
            Restore();
            return;
        }

        Image panel = FindImage(shop.shopUI.transform, "Shop_panel");
        Button reroll = shop.shopRerollButton;
        Button next = shop.nextStageButton;
        TMP_Text rerollLabel = shop.rerollCostText;
        TMP_Text nextLabel = next != null ? next.GetComponentInChildren<TMP_Text>(true) : null;
        RectTransform costRow = shop.rerollCostLabel != null ? shop.rerollCostLabel.transform.parent as RectTransform : null;

        // 처음 모습은 아무것도 옮기기 전에 한꺼번에 기억한다 (자식은 부모의 원래 배율을 기준으로 읽어야 함)
        Remember(panel, canvas);
        Remember(title, canvas);
        Remember(reroll != null ? reroll.transform as RectTransform : null, canvas);
        Remember(reroll != null ? reroll.targetGraphic : null, canvas);
        Remember(rerollLabel, canvas);
        Remember(next != null ? next.transform as RectTransform : null, canvas);
        Remember(next != null ? next.targetGraphic : null, canvas);
        Remember(nextLabel, canvas);
        Remember(costRow, canvas);
        ShopSlot[] slots = shop.shopSlots ?? new ShopSlot[0];
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null) continue;
            Remember(slots[i].transform as RectTransform, canvas);
            Remember(FindImage(slots[i].transform, "item_parchment"), canvas);
            Remember(FindImage(slots[i].transform, "Coin"), canvas);
            Remember(slots[i].itemIcon, canvas);
            Remember(slots[i].priceText, canvas);
            Remember(slots[i].lockUI != null ? slots[i].lockUI.GetComponent<Image>() : null, canvas);
        }

        if (panel != null)
        {
            SetSprite(panel, look.panel);
            Place(panel.rectTransform, canvas, look.panelCenter, look.panelSize, 1f);
        }
        if (title != null)
        {
            float scale = RestScale(title.rectTransform);
            Place(title.rectTransform, canvas, look.titleCenter, look.titleSize, scale);
            SetFont(title, skin.titleFontSize, scale);
        }

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null) continue;
            Vector2 center = look.firstCardCenter + new Vector2((i % 3) * look.cardSpacing.x, (i / 3) * look.cardSpacing.y);
            PlaceSlot(slots[i], skin, look, canvas, center);
        }

        PlaceButton(reroll, rerollLabel, look.rerollButton, skin, canvas, look.rerollCenter, look.rerollSize, look.rerollLabel);
        PlaceButton(next, nextLabel, look.nextButton, skin, canvas, look.nextCenter, look.nextSize, look.nextLabel);
        if (costRow != null)
            Move(costRow, canvas, look.rerollCenter + new Vector2(0f, look.rerollSize.y * 0.5f + skin.costRowGap), RestScale(costRow));

        applied = true;
    }

    public void Restore()
    {
        if (!applied) return;
        if (shop.shopSlots != null)
            foreach (ShopSlot slot in shop.shopSlots)
                if (slot != null) slot.ClearPriceRow();
        foreach (KeyValuePair<RectTransform, RectRest> pair in rects)
        {
            RectTransform rt = pair.Key;
            if (rt == null) continue;
            RectRest rest = pair.Value;
            rt.anchorMin = rest.anchorMin;
            rt.anchorMax = rest.anchorMax;
            rt.pivot = rest.pivot;
            rt.sizeDelta = rest.sizeDelta;
            rt.localScale = rest.localScale;
            rt.anchoredPosition = rest.anchoredPosition;
        }
        foreach (KeyValuePair<Image, ImageRest> pair in images)
        {
            if (pair.Key == null) continue;
            pair.Key.sprite = pair.Value.sprite;
            pair.Key.type = pair.Value.type;
            pair.Key.preserveAspect = pair.Value.preserveAspect;
        }
        foreach (KeyValuePair<TMP_Text, TextRest> pair in texts)
        {
            if (pair.Key == null) continue;
            TextRest rest = pair.Value;
            pair.Key.alignment = rest.alignment;
            pair.Key.margin = rest.margin;
            pair.Key.enableAutoSizing = rest.autoSize;
            pair.Key.fontSizeMin = rest.fontSizeMin;
            pair.Key.fontSizeMax = rest.fontSizeMax;
            pair.Key.fontSize = rest.fontSize;
        }
        if (shop.shopSlots != null)
            foreach (ShopSlot slot in shop.shopSlots)
                if (slot != null) slot.SetIconSize(ShopSlot.ItemIconSize);
        applied = false;
    }

    void PlaceSlot(ShopSlot slot, MerchantShopSkin skin, MerchantShopSkin.Look look, RectTransform canvas, Vector2 center)
    {
        RectTransform root = slot.transform as RectTransform;
        float scale = RestScale(root);
        Place(root, canvas, center, look.cardSize, scale);

        Vector2 cardTopLeft = center - look.cardSize * 0.5f;
        Image card = FindImage(root, "item_parchment");
        if (card != null)
        {
            SetSprite(card, look.card);
            Place(card.rectTransform, canvas, center, look.cardSize, 1f);
        }

        Vector2 iconCenter = cardTopLeft + look.iconCenter;
        if (slot.itemIcon != null)
        {
            slot.SetIconSize(look.iconSize / scale);
            Move(slot.itemIcon.rectTransform, canvas, iconCenter, -1f);
        }
        if (slot.lockUI != null && slot.lockUI.transform is RectTransform lockRect)
        {
            Image lockImage = slot.lockUI.GetComponent<Image>();
            if (lockImage != null) lockImage.preserveAspect = true;
            Place(lockRect, canvas, iconCenter, new Vector2(skin.lockIconSize, skin.lockIconSize), 1f);
        }

        // 가격: 숫자와 코인을 한 묶음으로 가운데 맞춘다 (숫자 길이가 바뀌면 ShopSlot이 다시 맞춤)
        Vector2 priceCenter = cardTopLeft + look.priceCenter;
        float priceFont = look.priceFontSize > 0f ? look.priceFontSize : skin.priceFontSize;
        float coinSize = look.coinSize > 0f ? look.coinSize : skin.coinSize;
        Image coin = FindImage(root, "Coin");
        if (coin != null)
        {
            coin.preserveAspect = true;
            Place(coin.rectTransform, canvas, priceCenter, new Vector2(coinSize, coinSize), 1f);
        }
        if (slot.priceText != null)
        {
            float textScale = RestScale(slot.priceText.rectTransform);
            Place(slot.priceText.rectTransform, canvas, priceCenter, new Vector2(look.priceWidth, priceFont * 1.3f), textScale);
            SetFont(slot.priceText, priceFont, textScale);
            slot.SetPriceRow(coin != null ? coin.rectTransform : null, skin.priceCoinGap * canvas.lossyScale.x);
        }
    }

    void PlaceButton(Button button, TMP_Text label, Sprite sprite, MerchantShopSkin skin, RectTransform canvas, Vector2 center, Vector2 size, Vector2 labelSpot)
    {
        if (button == null) return;
        RectTransform rt = button.transform as RectTransform;
        if (button.targetGraphic is Image image) SetSprite(image, sprite);
        Place(rt, canvas, center, size, RestScale(rt));
        if (label == null) return;
        float scale = RestScale(label.rectTransform);
        Vector2 labelCenter = new Vector2(center.x - size.x * 0.5f + labelSpot.x, center.y);
        Place(label.rectTransform, canvas, labelCenter, new Vector2(labelSpot.y, size.y * 0.75f), scale);
        SetFont(label, skin.labelFontSize, scale);
    }

    // 글자 크기는 화면 px로 받는다. 칸보다 길면 자동으로 줄어든다.
    static void SetFont(TMP_Text text, float screenPx, float scale)
    {
        text.margin = Vector4.zero;
        text.alignment = TextAlignmentOptions.Center;
        text.enableAutoSizing = true;
        text.fontSizeMin = 10f / scale;
        text.fontSizeMax = screenPx / scale;
        text.fontSize = screenPx / scale;
    }

    // 화면 좌표(1920×1080, 왼쪽 위 0,0) → 캔버스 가운데 기준 좌표
    static Vector3 ToWorld(RectTransform canvas, Vector2 screen, float z)
    {
        Vector3 p = canvas.TransformPoint(new Vector2(screen.x - 960f, 540f - screen.y));
        p.z = z;
        return p;
    }

    // 화면 기준 가운데·크기로 놓는다. scale = 화면 1px당 이 오브젝트의 로컬 단위 배율(가로·세로 같게).
    static void Place(RectTransform rt, RectTransform canvas, Vector2 center, Vector2 size, float scale)
    {
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        SetScale(rt, canvas, scale);
        rt.sizeDelta = size / scale;
        rt.position = ToWorld(canvas, center, rt.position.z);
    }

    // 크기·기준점은 두고 위치(와 scale > 0이면 배율)만 바꾼다.
    static void Move(RectTransform rt, RectTransform canvas, Vector2 at, float scale)
    {
        if (scale > 0f) SetScale(rt, canvas, scale);
        rt.position = ToWorld(canvas, at, rt.position.z);
    }

    static void SetScale(RectTransform rt, RectTransform canvas, float scale)
    {
        Vector3 parent = rt.parent != null ? rt.parent.lossyScale : Vector3.one;
        Vector3 unit = canvas.lossyScale;
        rt.localScale = new Vector3(unit.x * scale / parent.x, unit.y * scale / parent.y, rt.localScale.z);
    }

    float RestScale(RectTransform rt)
    {
        return rt != null && restScales.TryGetValue(rt, out float s) && s > 0f ? s : 1f;
    }

    void SetSprite(Image image, Sprite sprite)
    {
        if (image == null || sprite == null) return;
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.preserveAspect = false;
    }

    void Remember(Graphic graphic, RectTransform canvas)
    {
        if (graphic == null) return;
        Remember(graphic.rectTransform, canvas);
        if (graphic is Image image && !images.ContainsKey(image))
            images[image] = new ImageRest { sprite = image.sprite, type = image.type, preserveAspect = image.preserveAspect };
        if (graphic is TMP_Text text && !texts.ContainsKey(text))
            texts[text] = new TextRest
            {
                alignment = text.alignment,
                margin = text.margin,
                fontSize = text.fontSize,
                fontSizeMin = text.fontSizeMin,
                fontSizeMax = text.fontSizeMax,
                autoSize = text.enableAutoSizing
            };
    }

    void Remember(RectTransform rt, RectTransform canvas)
    {
        if (rt == null || rects.ContainsKey(rt)) return;
        rects[rt] = new RectRest
        {
            anchorMin = rt.anchorMin,
            anchorMax = rt.anchorMax,
            pivot = rt.pivot,
            anchoredPosition = rt.anchoredPosition,
            sizeDelta = rt.sizeDelta,
            localScale = rt.localScale
        };
        restScales[rt] = rt.lossyScale.y / Mathf.Max(0.0001f, canvas.lossyScale.y);
    }

    RectTransform CanvasRect()
    {
        if (shop.shopUI == null) return null;
        Canvas canvas = shop.shopUI.GetComponentInParent<Canvas>(true);
        return canvas != null ? canvas.rootCanvas.transform as RectTransform : null;
    }

    static Image FindImage(Transform root, string name)
    {
        Image[] found = root.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < found.Length; i++)
            if (found[i].gameObject.name == name) return found[i];
        return null;
    }
}
