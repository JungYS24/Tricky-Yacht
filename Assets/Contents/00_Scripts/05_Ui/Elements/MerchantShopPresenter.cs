using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Localization.Components;

// 지도 상인 상점의 모습: 상인 NPC 애니메이션과 발 위치 맞추기, 상점 제목(상인 이름), 판·카드·버튼 그림(MerchantShopSkinApplier에 맡김).
// 지도 상인이 아닌 일반 상점이 열리면 NPC와 제목, 판·카드 그림을 처음 모습으로 되돌린다.
// ShopManager가 하나 만들어 들고 있습니다. 인스펙터 값(상인별 애니메이션, merchantSkin)은 ShopManager에 그대로 있습니다.
public class MerchantShopPresenter
{
    readonly ShopManager shop;
    MerchantShopSkinApplier skinApplier;

    public MerchantShopPresenter(ShopManager shop)
    {
        this.shop = shop;
    }

    // 상점 제목에 상인 이름이 나오므로 예전에 만들어져 씬에 저장된 하단 MerchantName 라벨은 지운다.
    internal void RemoveLegacyMerchantLabel()
    {
        if (shop.shopUI == null) return;
        Transform legacy = shop.shopUI.transform.Find("MerchantName");
        if (legacy != null) Object.Destroy(legacy.gameObject);
    }

    TextMeshProUGUI shopTitleText;
    LocalizeStringEvent shopTitleLocalize;
    Image npcImage;
    Animator npcAnimator;
    bool npcRestSaved;
    Vector2 npcAnchorMin, npcAnchorMax, npcPivot, npcAnchoredPosition, npcSizeDelta;
    Vector3 npcLocalScale;
    bool npcPreserveAspect;
    Sprite npcSprite;
    bool npcAnimatorEnabled;
    RuntimeAnimatorController npcController;
    float npcFootX, npcFootY, npcBodyHeight;

    internal void ApplyMerchantPresentation(bool fromMap)
    {
        BindShopChrome();
        if (skinApplier == null && shop.merchantSkin != null) skinApplier = new MerchantShopSkinApplier(shop);
        if (fromMap)
        {
            ApplyNpc(shop.currentMerchant);
            ApplyShopTitle(true);
            skinApplier?.Apply(shop.merchantSkin, shop.currentMerchant, shopTitleText);
        }
        else
        {
            RestoreNpc();
            ApplyShopTitle(false);
            skinApplier?.Restore();
        }
    }

    void BindShopChrome()
    {
        if (shop.shopUI == null) return;
        if (shopTitleText == null)
        {
            TextMeshProUGUI[] texts = shop.shopUI.GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i].gameObject.name != "Shop_TXT_Pannel") continue;
                shopTitleText = texts[i];
                shopTitleLocalize = texts[i].GetComponent<LocalizeStringEvent>();
                break;
            }
        }
        if (npcImage == null)
        {
            Image[] images = shop.shopUI.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                if (images[i].gameObject.name != "NPC_Image") continue;
                npcImage = images[i];
                npcAnimator = images[i].GetComponent<Animator>();
                break;
            }
        }
    }

    internal void ApplyShopTitle(bool fromMap)
    {
        if (shopTitleText == null) return;
        if (!fromMap)
        {
            if (shopTitleLocalize != null)
            {
                shopTitleLocalize.enabled = true;
                shopTitleLocalize.RefreshString();
            }
            return;
        }

        if (shopTitleLocalize != null) shopTitleLocalize.enabled = false;
        string key;
        string fallback;
        switch (shop.currentMerchant)
        {
            case ShopManager.MapMerchant.Nyaon:
                key = "UI_SHOP_TITLE_NYAON";
                fallback = "냐온 딜레마, 기묘한 상점";
                break;
            case ShopManager.MapMerchant.Ronan:
                key = "UI_SHOP_TITLE_RONAN";
                fallback = "남작 로넌의 골동품 상점";
                break;
            default:
                key = "UI_SHOP_TITLE_OPEL";
                fallback = "오펠의 아르카나 상점";
                break;
        }
        shopTitleText.text = LocalizationManager.GetUi(key, fallback);
    }

    RuntimeAnimatorController MerchantController(ShopManager.MapMerchant merchant)
    {
        switch (merchant)
        {
            case ShopManager.MapMerchant.Nyaon: return shop.nyaonAnimator;
            case ShopManager.MapMerchant.Ronan: return shop.ronanAnimator;
            default: return shop.opelAnimator;
        }
    }

    void SaveNpcRest()
    {
        if (npcRestSaved || npcImage == null) return;
        RectTransform rect = npcImage.rectTransform;
        npcAnchorMin = rect.anchorMin;
        npcAnchorMax = rect.anchorMax;
        npcPivot = rect.pivot;
        npcAnchoredPosition = rect.anchoredPosition;
        npcSizeDelta = rect.sizeDelta;
        npcLocalScale = rect.localScale;
        npcPreserveAspect = npcImage.preserveAspect;
        npcSprite = npcImage.sprite;
        npcAnimatorEnabled = npcAnimator != null && npcAnimator.enabled;
        npcController = npcAnimator != null ? npcAnimator.runtimeAnimatorController : null;
        float height = rect.rect.height * Mathf.Abs(rect.localScale.y);
        float width = rect.rect.width * Mathf.Abs(rect.localScale.x);
        npcBodyHeight = height;
        npcFootY = rect.anchoredPosition.y - height * rect.pivot.y;
        npcFootX = rect.anchoredPosition.x + width * (0.5f - rect.pivot.x);
        npcRestSaved = true;
    }

    void ApplyNpc(ShopManager.MapMerchant merchant)
    {
        SaveNpcRest();
        if (npcImage == null) return;
        RuntimeAnimatorController controller = MerchantController(merchant);
        if (npcAnimator != null && controller != null)
        {
            npcAnimator.enabled = true;
            npcAnimator.runtimeAnimatorController = controller;
            npcAnimator.Rebind();
            npcAnimator.Update(0f);
        }
        FitNpcImage();
    }

    void FitNpcImage()
    {
        if (npcImage == null || npcImage.sprite == null || npcBodyHeight <= 1f) return;
        Rect spriteRect = npcImage.sprite.rect;
        float aspect = spriteRect.width / Mathf.Max(1f, spriteRect.height);
        RectTransform rect = npcImage.rectTransform;
        rect.localScale = Vector3.one;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.sizeDelta = new Vector2(npcBodyHeight * aspect, npcBodyHeight);
        rect.anchoredPosition = new Vector2(npcFootX, npcFootY);
        npcImage.preserveAspect = true;
    }

    void RestoreNpc()
    {
        if (!npcRestSaved || npcImage == null) return;
        if (npcAnimator != null)
        {
            npcAnimator.runtimeAnimatorController = npcController;
            npcAnimator.enabled = npcAnimatorEnabled;
        }
        npcImage.sprite = npcSprite;
        npcImage.preserveAspect = npcPreserveAspect;
        RectTransform rect = npcImage.rectTransform;
        rect.anchorMin = npcAnchorMin;
        rect.anchorMax = npcAnchorMax;
        rect.pivot = npcPivot;
        rect.anchoredPosition = npcAnchoredPosition;
        rect.sizeDelta = npcSizeDelta;
        rect.localScale = npcLocalScale;
    }
}
