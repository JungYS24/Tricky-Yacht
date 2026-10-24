using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 코팅·스탬프·위성·주사위 삭제 선택창이 함께 쓰는 창 (상점과 같은 덩굴 나무 판).
// 위: 제목 + 적용할 아이템(아이콘·이름·능력), 가운데: 덱 주사위 카드 5장, 아래: 안내 + 리롤(비용·횟수).
// 카드는 하나씩 튀어나오고, 리롤하면 뒤집히며 다른 주사위로 바뀐다.
// 프리팹은 Resources/DiceChoiceWindow 이고, 각 선택창의 panelRoot 아래에 한 번 만들어 계속 쓴다.
// 리롤 비용·횟수는 ShopManager의 "주사위 선택창 리롤" 값을 쓴다.
public class DiceChoiceWindow : MonoBehaviour
{
    public struct Header
    {
        public string title;
        public Sprite icon;
        public string itemName;
        public string itemDesc;
        public Color nameColor;
        public string hint;
    }

    [Header("연결")]
    public CanvasGroup rootGroup;
    public Image dim;
    public RectTransform frame;
    public TextMeshProUGUI titleText;
    public Image itemIcon;
    public TextMeshProUGUI itemName;
    public TextMeshProUGUI itemDesc;
    public RectTransform cardRow;
    public DiceChoiceCard cardTemplate;
    public TextMeshProUGUI hintText;
    public Button rerollButton;
    public TextMeshProUGUI rerollLabel;
    public TextMeshProUGUI rerollCostLabel;
    public Image rerollCostIcon;

    [Header("등장")]
    public float dimAlpha = 0.65f;
    public float openDuration = 0.22f;
    public float dealDelay = 0.1f;      // 창이 열리고 첫 카드가 나올 때까지
    public float dealInterval = 0.06f;  // 카드가 차례로 나오는 간격
    public float dealDuration = 0.3f;
    public float dealRise = 60f;        // 아래에서 떠오르는 거리

    [Header("리롤 뒤집기")]
    public float flipHalf = 0.12f;
    public float flipInterval = 0.05f;

    [Header("리롤 비용 색")]
    public Color costColor = Color.white;
    public Color costShortColor = new Color(1f, 0.32f, 0.28f, 1f);

    private const string ResourcePath = "DiceChoiceWindow";

    private readonly List<DiceChoiceCard> cards = new List<DiceChoiceCard>();
    private GameObject diceSlotPrefab;
    private Action<DiceData1, DeckSlot> onPick;
    private Func<List<DiceData1>> getCandidates;
    private Func<bool> isBusy;
    private ShopManager shop;
    private List<DiceData1> shown = new List<DiceData1>();
    private int rerollUsed;
    private bool animating;

    // DiceSelectionFeedback에 넘길 카드 목록(고른 카드 말고는 흐려진다)
    public List<GameObject> Cards => cards.Where(c => c != null && c.gameObject.activeSelf).Select(c => c.gameObject).ToList();

    // 선택창 panelRoot 아래에 창을 하나 만들어 둔다. 예전 주사위 줄(SlotParent)·어둡게 깔던 판은 숨긴다.
    public static DiceChoiceWindow Attach(RectTransform panelRoot)
    {
        if (panelRoot == null) return null;
        var existing = panelRoot.GetComponentInChildren<DiceChoiceWindow>(true);
        if (existing != null) return existing;

        var prefab = Resources.Load<DiceChoiceWindow>(ResourcePath);
        if (prefab == null)
        {
            Debug.LogWarning("[DiceChoiceWindow] Resources/DiceChoiceWindow 프리팹을 찾지 못했습니다.");
            return null;
        }

        foreach (Transform child in panelRoot) child.gameObject.SetActive(false);
        DiceChoiceWindow window = Instantiate(prefab, panelRoot, false);
        window.name = "DiceChoiceWindow";
        window.cardTemplate.gameObject.SetActive(false);
        window.rerollButton.onClick.AddListener(window.OnReroll);
        return window;
    }

    public void Open(Header header, List<DiceData1> options, GameObject slotPrefab, Action<DiceData1, DeckSlot> pick,
        ShopManager shopManager, Func<List<DiceData1>> candidates, Func<bool> busy = null)
    {
        diceSlotPrefab = slotPrefab;
        onPick = pick;
        shop = shopManager;
        getCandidates = candidates;
        isBusy = busy;
        shown = new List<DiceData1>(options);
        rerollUsed = 0;

        FitToScreen();
        ApplyHeader(header);
        BuildCards(options);
        PlayOpen();
        RefreshReroll();
    }

    public void Clear()
    {
        DOTween.Kill(this);
        foreach (var card in cards)
            if (card != null) Destroy(card.gameObject);
        cards.Clear();
    }

    // 선택창(panelRoot)이 커지거나 줄어 있어도 1920×1080 기준 크기로 화면 가운데에 보이게 한다.
    private void FitToScreen()
    {
        var rect = (RectTransform)transform;
        Canvas canvas = GetComponentInParent<Canvas>();
        Canvas root = canvas != null ? canvas.rootCanvas : null;
        Transform parent = rect.parent;
        if (root == null || parent == null) return;

        float rootScale = root.transform.lossyScale.x;
        float parentScale = Mathf.Max(0.0001f, parent.lossyScale.x);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.localScale = Vector3.one * (rootScale / parentScale);
        rect.position = root.transform.position;
        rect.SetAsLastSibling();
    }

    private void ApplyHeader(Header header)
    {
        if (titleText != null) titleText.text = header.title;
        bool hasItem = !string.IsNullOrEmpty(header.itemName);
        if (itemIcon != null)
        {
            itemIcon.sprite = header.icon;
            itemIcon.gameObject.SetActive(header.icon != null);
        }
        if (itemName != null)
        {
            itemName.gameObject.SetActive(hasItem);
            itemName.text = header.itemName;
            itemName.color = header.nameColor;
        }
        if (itemDesc != null)
        {
            itemDesc.gameObject.SetActive(!string.IsNullOrEmpty(header.itemDesc));
            itemDesc.text = header.itemDesc;
        }
        if (hintText != null) hintText.text = header.hint;
    }

    private void BuildCards(List<DiceData1> options)
    {
        Clear();
        foreach (var dice in options)
        {
            DiceChoiceCard card = Instantiate(cardTemplate, cardRow, false);
            card.gameObject.SetActive(true);
            card.SetDice(dice, diceSlotPrefab, DiceName(dice));
            card.button.onClick.AddListener(() => OnCardClicked(card));
            cards.Add(card);
        }
        LayoutRebuilder.ForceRebuildLayoutImmediate(cardRow);
    }

    private void OnCardClicked(DiceChoiceCard card)
    {
        if (animating || card == null || card.Dice == null) return;
        if (isBusy != null && isBusy()) return;
        card.ResetHover();
        onPick?.Invoke(card.Dice, card.Slot);
    }

    // 창이 살짝 커지며 나타나고, 카드가 아래에서 하나씩 튀어나온다.
    private void PlayOpen()
    {
        DOTween.Kill(this);
        SetAnimating(true);

        if (rootGroup != null)
        {
            rootGroup.alpha = 0f;
            rootGroup.DOFade(1f, openDuration * 0.7f).SetId(this).SetUpdate(true).SetLink(gameObject);
        }
        if (dim != null)
        {
            Color c = dim.color;
            dim.color = new Color(c.r, c.g, c.b, dimAlpha);
        }
        if (frame != null)
        {
            frame.localScale = Vector3.one * 0.92f;
            frame.DOScale(1f, openDuration).SetEase(Ease.OutBack).SetId(this).SetUpdate(true).SetLink(frame.gameObject);
        }

        Sequence seq = DOTween.Sequence().SetId(this).SetUpdate(true).SetLink(gameObject);
        for (int i = 0; i < cards.Count; i++)
        {
            DiceChoiceCard card = cards[i];
            card.body.anchoredPosition = new Vector2(0f, -dealRise);
            card.body.localScale = Vector3.one * 0.5f;
            card.bodyGroup.alpha = 0f;
            float at = dealDelay + i * dealInterval;
            seq.Insert(at, card.body.DOAnchorPosY(0f, dealDuration).SetEase(Ease.OutBack));
            seq.Insert(at, card.body.DOScale(1f, dealDuration).SetEase(Ease.OutBack));
            seq.Insert(at, card.bodyGroup.DOFade(1f, dealDuration * 0.5f));
        }
        seq.OnComplete(() => SetAnimating(false));
        seq.OnKill(() => SetAnimating(false));
    }

    // 리롤: 카드가 차례로 뒤집히며 새 주사위로 바뀐다.
    private void PlayFlip(List<DiceData1> next)
    {
        DOTween.Kill(this);
        SetAnimating(true);

        Sequence seq = DOTween.Sequence().SetId(this).SetUpdate(true).SetLink(gameObject);
        for (int i = 0; i < cards.Count; i++)
        {
            DiceChoiceCard card = cards[i];
            DiceData1 dice = next[i];
            card.ResetHover();
            float at = i * flipInterval;
            seq.Insert(at, card.body.DOScaleX(0f, flipHalf).SetEase(Ease.InQuad));
            seq.InsertCallback(at + flipHalf, () => card.SetDice(dice, diceSlotPrefab, DiceName(dice)));
            seq.Insert(at + flipHalf, card.body.DOScaleX(1f, flipHalf).SetEase(Ease.OutQuad));
        }
        seq.OnComplete(() => SetAnimating(false));
        seq.OnKill(() => SetAnimating(false));
    }

    private void SetAnimating(bool value)
    {
        animating = value;
        foreach (var card in cards)
            if (card != null && card.button != null) card.button.interactable = !value;
        RefreshReroll();
    }

    private int RerollLimit => shop != null ? Mathf.Max(0, shop.diceChoiceRerollLimit) : 0;
    private int RerollCost => shop != null ? Mathf.Max(0, shop.diceChoiceRerollCost) : 0;

    private void OnReroll()
    {
        if (shop == null || animating || rerollUsed >= RerollLimit) return;
        if (isBusy != null && isBusy()) return;

        List<DiceData1> candidates = getCandidates != null ? getCandidates() : null;
        if (!HasFresh(candidates)) return;

        if (shop.currentGold < RerollCost)
        {
            if (ToastPopupController.Instance != null)
                ToastPopupController.Instance.ShowToast(LocalizationManager.GetSys("SYS_GOLD_NOT_ENOUGH", "골드가 부족합니다."));
            RefreshReroll();
            return;
        }

        shop.currentGold -= RerollCost;
        if (shop.diceManager != null && shop.diceManager.ui != null) shop.diceManager.ui.UpdateGoldUI(shop.currentGold);
        if (GoldCounter.Instance != null) GoldCounter.Instance.SetGold(shop.currentGold);
        shop.UpdateRerollUI();

        rerollUsed++;
        List<DiceData1> next = PickReroll(candidates, shown, shown.Count);
        shown = next;
        if (next.Count == cards.Count) PlayFlip(next);
        else
        {
            BuildCards(next);
            PlayOpen();
        }
        RefreshReroll();
    }

    // 지금 보이는 주사위를 뺀 후보에서 count개를 뽑고, 모자라면 이미 본 주사위로 채운다.
    public static List<DiceData1> PickReroll(List<DiceData1> candidates, List<DiceData1> current, int count)
    {
        var fresh = candidates.Where(d => !current.Any(c => ReferenceEquals(c, d))).OrderBy(_ => UnityEngine.Random.value).ToList();
        var result = fresh.Take(count).ToList();
        if (result.Count < count)
        {
            var refill = candidates.Where(d => current.Any(c => ReferenceEquals(c, d))).OrderBy(_ => UnityEngine.Random.value);
            result.AddRange(refill.Take(count - result.Count));
        }
        return result;
    }

    // 지금 안 보이는 후보가 하나라도 있어야 리롤할 의미가 있다.
    private bool HasFresh(List<DiceData1> candidates)
    {
        return candidates != null && candidates.Any(d => !shown.Any(c => ReferenceEquals(c, d)));
    }

    private void RefreshReroll()
    {
        if (rerollButton == null) return;
        bool hasShop = shop != null;
        rerollButton.gameObject.SetActive(hasShop);
        if (rerollCostLabel != null) rerollCostLabel.gameObject.SetActive(hasShop);
        if (rerollCostIcon != null) rerollCostIcon.gameObject.SetActive(hasShop && rerollCostIcon.sprite != null);
        if (!hasShop) return;

        rerollButton.interactable = !animating && rerollUsed < RerollLimit && HasFresh(getCandidates != null ? getCandidates() : null);
        if (rerollLabel != null)
            rerollLabel.text = $"{LocalizationManager.GetUi("UI_REROLL", "리롤")} ({rerollUsed}/{RerollLimit})";

        Color color = shop.currentGold < RerollCost ? costShortColor : costColor;
        if (rerollCostLabel != null)
        {
            rerollCostLabel.text = RerollCost.ToString();
            rerollCostLabel.color = color;
        }
        if (rerollCostIcon != null) rerollCostIcon.color = color;
    }

    // 카드 아래 이름: 주사위 에셋 이름을 번역해서 쓰고, 기본 주사위는 번역 키로 찾는다.
    public static string DiceName(DiceData1 dice)
    {
        if (dice == null) return string.Empty;
        BaseItemDataSO item = GameSaveManager.Instance != null ? GameSaveManager.Instance.FindItemByName(dice.diceName) : null;
        if (item != null) return LocalizationManager.GetItemDisplayName(item);
        if (dice.diceName == "기본 주사위" && LocalizationManager.TryGetLocalized(LocalizationManager.ItemTable, "ID_DICE_NAME_1001", out string basic))
            return basic;
        return dice.diceName;
    }

    // ===== 선택창별 머리말 =====

    public static Header CoatingHeader(ShopManager shop, DiceType type, Color color)
    {
        CoatingItemSO item = FindItem<CoatingItemSO>(shop, c => c.coatingType == type);
        Color nameColor = type == DiceType.Dark ? new Color(0.78f, 0.6f, 1f) : Color.Lerp(color, Color.white, 0.45f);
        return MakeHeader(item, "UI_PICK_COATING", "코팅할 주사위를 고르세요", "UI_PICK_COATING_HINT", "주사위를 눌러 코팅해요", nameColor);
    }

    public static Header StampHeader(ShopManager shop, Sprite stampIcon)
    {
        StampItemSO item = FindItem<StampItemSO>(shop, s => stampIcon != null && s.icon == stampIcon);
        Header header = MakeHeader(item, "UI_PICK_STAMP", "스탬프를 찍을 주사위를 고르세요", "UI_PICK_STAMP_HINT", "주사위를 눌러 스탬프를 찍어요", new Color(1f, 0.84f, 0.6f));
        if (header.icon == null) header.icon = stampIcon;
        return header;
    }

    public static Header SatelliteHeader(ShopManager shop, SatelliteType type)
    {
        SatelliteItemSO item = FindItem<SatelliteItemSO>(shop, s => s.satelliteType == type);
        return MakeHeader(item, "UI_PICK_SATELLITE", "위성을 달 주사위를 고르세요", "UI_PICK_SATELLITE_HINT", "주사위를 눌러 위성을 달아요", new Color(0.66f, 0.8f, 1f));
    }

    public static Header DestructionHeader(ShopManager shop)
    {
        DiceDestroyItemSO item = FindItem<DiceDestroyItemSO>(shop, d => true);
        return MakeHeader(item, "UI_PICK_DESTROY", "삭제할 주사위를 고르세요", "UI_PICK_DESTROY_HINT", "주사위를 눌러 덱에서 삭제해요", new Color(1f, 0.55f, 0.45f));
    }

    private static Header MakeHeader(BaseItemDataSO item, string titleKey, string titleFallback, string hintKey, string hintFallback, Color nameColor)
    {
        return new Header
        {
            title = LocalizationManager.GetUi(titleKey, titleFallback),
            hint = LocalizationManager.GetUi(hintKey, hintFallback),
            icon = item != null ? item.icon : null,
            itemName = item != null ? LocalizationManager.GetItemDisplayName(item) : string.Empty,
            itemDesc = item != null ? LocalizationManager.GetItemDescription(item) : string.Empty,
            nameColor = nameColor,
        };
    }

    // 상점 진열 목록 → 저장용 아이템 목록 순서로 찾는다.
    private static T FindItem<T>(ShopManager shop, Func<T, bool> match) where T : BaseItemDataSO
    {
        if (shop != null && shop.allItemsPool != null)
        {
            T found = shop.allItemsPool.OfType<T>().FirstOrDefault(match);
            if (found != null) return found;
        }
        GameSaveManager save = GameSaveManager.Instance;
        if (save != null && save.masterItemDatabase != null)
            return save.masterItemDatabase.OfType<T>().FirstOrDefault(match);
        return null;
    }
}
