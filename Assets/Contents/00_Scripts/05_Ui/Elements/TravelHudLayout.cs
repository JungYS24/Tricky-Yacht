using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 전투 HUD(덱·도감·골드·체력 등)를 지도 화면 자리로 옮기고 전투 때 되돌립니다.
// 씬의 HUD 오브젝트는 이름으로 한 번만 찾아 기억합니다. DiceManager가 하나 만들어 들고 있습니다.
public class TravelHudLayout
{
    // 이름으로 HUD 오브젝트를 찾는다. 없으면 null.
    public GameObject Find(string objectName)
    {
        CacheHud();
        return hudObjects.TryGetValue(objectName, out GameObject target) ? target : null;
    }

    // 디버프 표시처럼 실행 중에 붙는 HUD가 기존 HUD 위치를 기준으로 자리를 잡을 때 씁니다.
    public RectTransform RectOf(string objectName)
    {
        CacheHud();
        return PlacementRect(objectName);
    }

    // 지도 화면: 점수판·피규어·설정을 맨 위로 올리고, 덱·도감·골드·체력을 지도용 자리로 옮깁니다.
    public void ShowTravel()
    {
        CacheHud();
        RaiseHud("ScorePanel");
        if (hudObjects.TryGetValue("ScorePanel", out GameObject score) && score != null)
        {
            Image scoreImage = score.GetComponent<Image>();
            if (scoreImage != null) scoreImage.raycastTarget = false;
        }
        RaiseHud("FigureSlotArea");
        RaiseHud("Setting_BTN");
        EnableHudControl("Book_Button");
        EnableHudControl("Deck_BTN");
        EnableHudControl("Setting_BTN");
        EnableHudControl("CoinPanel");
        EnableHudControl("HeartPanel");
        EnableHudControl("FigureSlotArea");
        ApplyTravelChrome();
    }

    // 전투 화면: 지도용 자리로 옮겼던 HUD를 원래 자리로 되돌립니다.
    public void ShowCombat()
    {
        CacheHud();
        RestoreCombatChrome();
        if (hudObjects.TryGetValue("ScorePanel", out GameObject score) && score != null)
        {
            Image scoreImage = score.GetComponent<Image>();
            if (scoreImage != null) scoreImage.raycastTarget = true;
        }
    }

    class HudRectSnapshot
    {
        public RectTransform rect;
        public Transform parent;
        public int siblingIndex;
        public Vector2 anchorMin;
        public Vector2 anchorMax;
        public Vector2 pivot;
        public Vector2 anchoredPosition;
        public Vector2 sizeDelta;
        public Vector3 localScale;
        public bool raycast;
        public bool hasImage;
    }

    readonly Dictionary<string, HudRectSnapshot> combatPlacements = new Dictionary<string, HudRectSnapshot>();
    bool combatPlacementsSaved;
    bool travelPlacementApplied;

    static readonly string[] TravelPlacementNames = { "Book_Button", "Deck_BTN", "CoinPanel", "HeartPanel" };

    void SaveCombatPlacements()
    {
        if (combatPlacementsSaved) return;
        CacheHud();
        combatPlacementsSaved = true;
        for (int i = 0; i < TravelPlacementNames.Length; i++)
        {
            string objectName = TravelPlacementNames[i];
            if (!hudObjects.TryGetValue(objectName, out GameObject target) || target == null) continue;
            RectTransform rect = target.GetComponent<RectTransform>();
            if (rect == null) continue;
            Image image = target.GetComponent<Image>();
            combatPlacements[objectName] = new HudRectSnapshot
            {
                rect = rect,
                parent = rect.parent,
                siblingIndex = rect.GetSiblingIndex(),
                anchorMin = rect.anchorMin,
                anchorMax = rect.anchorMax,
                pivot = rect.pivot,
                anchoredPosition = rect.anchoredPosition,
                sizeDelta = rect.sizeDelta,
                localScale = rect.localScale,
                raycast = image != null && image.raycastTarget,
                hasImage = image != null
            };
        }
    }

    void ApplyTravelChrome()
    {
        SaveCombatPlacements();
        if (!hudObjects.TryGetValue("ScorePanel", out GameObject score) || score == null) return;
        RectTransform canvas = score.transform.parent as RectTransform;
        if (canvas == null) return;

        RectTransform deck = PlacementRect("Deck_BTN");
        RectTransform book = PlacementRect("Book_Button");
        RectTransform coin = PlacementRect("CoinPanel");
        RectTransform heart = PlacementRect("HeartPanel");

        if (deck != null)
            PlaceBottomLeftCenter(deck, canvas, new Vector2(130f, 120f));
        if (book != null)
            PlaceBottomLeftCenter(book, canvas, new Vector2(130f, 300f));
        FitTravelStatusPanels(coin, heart);
        Vector2 coinPos = new Vector2(312f, -32f);
        // 남색 패널에서는 골드·체력 칸이 같은 폭이라 지도 화면에서도 오른쪽 끝을 맞춥니다.
        Vector2 heartPos = new Vector2(coinPos.x, coinPos.y - 66f);
        if (coin != null)
        {
            PlaceAnchored(coin, canvas, new Vector2(0f, 1f), coinPos);
            SetPanelRaycast(coin, false);
        }
        if (heart != null)
        {
            PlaceAnchored(heart, canvas, new Vector2(0f, 1f), heartPos);
            SetPanelRaycast(heart, false);
        }
        travelPlacementApplied = true;
    }

    void RestoreCombatChrome()
    {
        SaveCombatPlacements();
        if (!travelPlacementApplied) return;
        foreach (KeyValuePair<string, HudRectSnapshot> pair in combatPlacements)
        {
            HudRectSnapshot snap = pair.Value;
            if (snap.rect == null) continue;
            snap.rect.SetParent(snap.parent, false);
            snap.rect.anchorMin = snap.anchorMin;
            snap.rect.anchorMax = snap.anchorMax;
            snap.rect.pivot = snap.pivot;
            snap.rect.sizeDelta = snap.sizeDelta;
            snap.rect.anchoredPosition = snap.anchoredPosition;
            snap.rect.localScale = snap.localScale;
            snap.rect.SetSiblingIndex(Mathf.Clamp(snap.siblingIndex, 0, snap.parent.childCount - 1));
            if (snap.hasImage)
            {
                Image image = snap.rect.GetComponent<Image>();
                if (image != null) image.raycastTarget = snap.raycast;
            }
        }
        travelPlacementApplied = false;
    }

    void FitTravelStatusPanels(RectTransform coin, RectTransform heart)
    {
        float width = 0f;
        if (coin != null && combatPlacements.TryGetValue("CoinPanel", out HudRectSnapshot coinSnap))
            width = Mathf.Max(width, coinSnap.sizeDelta.x);
        if (heart != null && combatPlacements.TryGetValue("HeartPanel", out HudRectSnapshot heartSnap))
            width = Mathf.Max(width, heartSnap.sizeDelta.x);
        ApplyTravelPanel(coin, "CoinPanel", width);
        ApplyTravelPanel(heart, "HeartPanel", width);
    }

    void ApplyTravelPanel(RectTransform rect, string objectName, float width)
    {
        if (rect == null || !combatPlacements.TryGetValue(objectName, out HudRectSnapshot snap)) return;
        Vector2 size = snap.sizeDelta;
        if (width > 0f) size.x = width;
        rect.sizeDelta = size;
        rect.localScale = snap.localScale * 0.7f;
    }

    static void PlaceBottomLeftCenter(RectTransform rect, RectTransform canvas, Vector2 center)
    {
        rect.SetParent(canvas, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.anchoredPosition = center;
        rect.SetAsLastSibling();
    }

    static void PlaceAnchored(RectTransform rect, RectTransform canvas, Vector2 anchor, Vector2 position)
    {
        rect.SetParent(canvas, false);
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.anchoredPosition = position;
        rect.SetAsLastSibling();
    }

    public void SetHudActive(string objectName, bool active)
    {
        if (!hudObjects.TryGetValue(objectName, out GameObject target) || target == null) return;
        target.SetActive(active);
    }

    RectTransform PlacementRect(string objectName)
    {
        if (!hudObjects.TryGetValue(objectName, out GameObject target) || target == null) return null;
        return target.GetComponent<RectTransform>();
    }

    static void SetPanelRaycast(RectTransform rect, bool enabled)
    {
        Image image = rect.GetComponent<Image>();
        if (image != null) image.raycastTarget = enabled;
    }

    void RaiseHud(string objectName)
    {
        if (!hudObjects.TryGetValue(objectName, out GameObject target) || target == null) return;
        target.SetActive(true);
        target.transform.SetAsLastSibling();
    }

    void EnableHudControl(string objectName)
    {
        if (!hudObjects.TryGetValue(objectName, out GameObject target) || target == null) return;
        target.SetActive(true);
        Button button = target.GetComponent<Button>();
        if (button != null) button.interactable = true;
    }

    static readonly string[] CombatHudNames =
    {
        "ScorePanel_Outer", "Hand Description Text", "Active Figure Text", "Stage_Panel",
        "Drop_Rate_Panel", "slot_counter_Dice", "slot_counter_chip (1)", "plaque_chips_mult", "panel_score", "Snack"
    };

    static readonly string[] TravelHudNames =
    {
        "CoinPanel", "HeartPanel", "FigureSlotArea", "Setting_BTN", "Book_Button", "Deck_BTN"
    };

    readonly Dictionary<string, GameObject> hudObjects = new Dictionary<string, GameObject>();
    bool hudCached;

    public void SetCombatHud(bool combat)
    {
        CacheHud();
        for (int i = 0; i < CombatHudNames.Length; i++)
        {
            if (hudObjects.TryGetValue(CombatHudNames[i], out GameObject target) && target != null)
                target.SetActive(combat);
        }
        if (hudObjects.TryGetValue("EnemyHPSlider", out GameObject enemyHp) && enemyHp != null)
            enemyHp.SetActive(combat);
        if (!combat)
        {
            for (int i = 0; i < TravelHudNames.Length; i++)
            {
                if (hudObjects.TryGetValue(TravelHudNames[i], out GameObject target) && target != null)
                    target.SetActive(true);
            }
        }
    }

    void CacheHud()
    {
        if (hudCached) return;
        hudCached = true;
        var transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var wanted = new HashSet<string>();
        for (int i = 0; i < CombatHudNames.Length; i++) wanted.Add(CombatHudNames[i]);
        for (int i = 0; i < TravelHudNames.Length; i++) wanted.Add(TravelHudNames[i]);
        wanted.Add("ScorePanel");
        wanted.Add("EnemyHPSlider");
        for (int i = 0; i < transforms.Length; i++)
        {
            if (transforms[i] == null || !wanted.Contains(transforms[i].name)) continue;
            if (!hudObjects.ContainsKey(transforms[i].name))
                hudObjects[transforms[i].name] = transforms[i].gameObject;
        }
    }
}
