using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 코인 획득 연출 공용(지도·전투): 코인 아이콘이 시작 위치에서 튀어나온 뒤 HUD 골드 패널로 빨려 들어간다.
// 실제 골드(shopManager.currentGold)는 호출하는 쪽이 GrantGold(.., deferUi: true)로 먼저 올려 두고,
// 화면의 골드 카운터 숫자만 이 연출이 코인 도착에 맞춰 올린다.
public static class CoinFlyFeedback
{
    const float CoinSize = 36f;               // 코인 아이콘 크기(캔버스 단위)
    const float BurstDuration = 0.15f;        // 튀어나오는 시간
    const float BurstRadiusMin = 40f;
    const float BurstRadiusMax = 70f;
    const float FlyIntervalSeconds = 0.05f;   // 코인이 차례로 출발하는 간격
    const float FlyDurationMin = 0.4f;
    const float FlyDurationMax = 0.5f;
    const float FlyEndScale = 0.6f;           // 도착할 때까지 살짝 줄어드는 비율
    const float PanelPunchScale = 0.08f;      // 골드 패널 펀치(1.08배)
    const float PanelPunchDuration = 0.15f;
    const string PunchId = "CoinFlyPanelPunch";
    const int MaxCoinsPerEffect = 8;

    // 한 번의 Play 호출이 만든 코인 묶음. 마지막 코인이 도착하면 이 묶음의 골드만큼 카운터가 올라간다.
    class Effect
    {
        public int remaining;
        public int gold;
    }

    static ShopManager shop;
    static Canvas canvas;
    static RectTransform coinPanel;
    static RectTransform coinTarget;
    static RectTransform layer;
    static int pendingGold;   // 아직 화면 카운터에 반영되지 않은(코인이 날아가는 중인) 골드
    static int activeCoins;

    // 연출을 재생할 수 있는지(코인 스프라이트와 HUD 골드 패널이 있는지). false면 호출하는 쪽은 UI를 바로 갱신해야 한다.
    public static bool CanPlay(ShopManager shopManager)
    {
        if (shopManager == null || shopManager.coinIconSprite == null) return false;
        return ResolveTarget(shopManager);
    }

    // 코인 coinCount개를 fromWorld에서 골드 패널로 날린다. goldAmount는 이 코인들이 올려 줄 화면 골드 수치.
    // 게임 진행은 기다리지 않는다. startDelay초 뒤에 시작한다.
    public static void Play(Vector3 fromWorld, int coinCount, int goldAmount, float startDelay = 0f)
    {
        ShopManager owner = shop != null ? shop : Object.FindFirstObjectByType<ShopManager>();
        if (owner == null) return;
        coinCount = Mathf.Clamp(coinCount, 0, MaxCoinsPerEffect);
        if (coinCount <= 0 || !CanPlay(owner))
        {
            // 연출을 못 쓰면 숫자만 바로 맞춘다.
            RefreshCounter(owner);
            return;
        }

        RectTransform coinLayer = GetLayer();
        float scale = canvas.transform.lossyScale.x > 0.0001f ? canvas.transform.lossyScale.x : 1f;
        Vector3 target = coinTarget.position;
        var effect = new Effect { remaining = coinCount, gold = Mathf.Max(0, goldAmount) };
        pendingGold += effect.gold;
        activeCoins += coinCount;

        for (int i = 0; i < coinCount; i++)
        {
            var go = new GameObject("CoinFly", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(coinLayer, false);
            go.SetActive(false);
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(CoinSize, CoinSize);
            rect.position = fromWorld;
            Image image = go.GetComponent<Image>();
            image.sprite = owner.coinIconSprite;
            image.preserveAspect = true;
            image.raycastTarget = false;

            float angle = Random.value * Mathf.PI * 2f;
            float radius = Random.Range(BurstRadiusMin, BurstRadiusMax) * scale;
            Vector3 burstPos = fromWorld + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
            float flyDuration = Random.Range(FlyDurationMin, FlyDurationMax);

            Sequence seq = DOTween.Sequence().SetUpdate(true).SetLink(go);
            seq.AppendInterval(Mathf.Max(0f, startDelay));
            seq.AppendCallback(() => go.SetActive(true));
            seq.Append(rect.DOMove(burstPos, BurstDuration).SetEase(Ease.OutBack));
            seq.AppendInterval(i * FlyIntervalSeconds);
            seq.Append(rect.DOMove(target, flyDuration).SetEase(Ease.InQuad));
            seq.Join(rect.DOScale(FlyEndScale, flyDuration).SetEase(Ease.InQuad));

            bool done = false;
            // 끝까지 재생되든 중간에 죽든(씬 전환 등) 한 번만 정산해서 카운터가 멈춰 있지 않게 한다.
            void Arrive(bool played)
            {
                if (done) return;
                done = true;
                activeCoins = Mathf.Max(0, activeCoins - 1);
                if (go != null) Object.Destroy(go);
                if (played)
                {
                    PunchPanel();
                    PlayArriveSound(owner);
                }
                effect.remaining--;
                if (effect.remaining <= 0)
                {
                    pendingGold = Mathf.Max(0, pendingGold - effect.gold);
                    RefreshCounter(owner);
                }
                if (activeCoins <= 0) Cleanup();
            }
            seq.OnComplete(() => Arrive(true));
            seq.OnKill(() => Arrive(false));
        }
    }

    // 화면 골드 숫자를 "현재 골드 - 아직 날아가는 중인 골드"로 맞춘다. 날아가는 코인이 없으면 최종 값이 된다.
    public static void RefreshCounter(ShopManager shopManager)
    {
        if (shopManager == null) return;
        int shown = Mathf.Max(0, shopManager.currentGold - pendingGold);
        if (GoldCounter.Instance != null)
        {
            GoldCounter.Instance.SetGold(shown);
            return;
        }
        // 씬이 내려가는 중에는 참조가 이미 파괴됐을 수 있어 하나씩 확인한다.
        DiceManager dm = shopManager.diceManager;
        UIManager ui = dm != null ? dm.ui : null;
        if (ui != null && ui.goldText != null) ui.UpdateGoldUI(shown);
    }

    static bool ResolveTarget(ShopManager shopManager)
    {
        shop = shopManager;
        if (canvas != null && coinPanel != null && coinTarget != null) return true;

        UIManager ui = shopManager.diceManager != null ? shopManager.diceManager.ui : null;
        TMP_Text goldText = ui != null ? ui.goldText : null;
        if (goldText == null) return false;
        Canvas ownCanvas = goldText.GetComponentInParent<Canvas>();
        if (ownCanvas == null) return false;
        canvas = ownCanvas.rootCanvas;

        // HUD CoinPanel: 골드 텍스트의 조상에서 먼저 찾고, 없으면 캔버스 전체에서 이름으로 찾는다.
        Transform panel = goldText.transform;
        while (panel != null && panel.name != "CoinPanel") panel = panel.parent;
        if (panel == null) panel = FindByName(canvas.transform, "CoinPanel");
        if (panel == null) panel = goldText.transform.parent;
        coinPanel = panel as RectTransform;
        if (coinPanel == null) return false;

        // 골드 아이콘 위치가 목표. 없으면 패널 중심으로 간다.
        Transform icon = coinPanel.Find("Icon_Gold");
        coinTarget = icon != null ? icon as RectTransform : coinPanel;
        if (coinTarget == null) coinTarget = coinPanel;
        return true;
    }

    static Transform FindByName(Transform root, string name)
    {
        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i].name == name) return all[i];
        }
        return null;
    }

    // HUD와 같은 캔버스의 맨 위에 그린다. 다른 UI가 맨 위로 올라올 수 있어 호출 때마다 다시 맨 위로 올린다.
    static RectTransform GetLayer()
    {
        if (layer == null)
        {
            var go = new GameObject("CoinFly_Layer", typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
            layer = (RectTransform)go.transform;
            layer.anchorMin = Vector2.zero;
            layer.anchorMax = Vector2.one;
            layer.offsetMin = Vector2.zero;
            layer.offsetMax = Vector2.zero;
        }
        layer.SetAsLastSibling();
        return layer;
    }

    static void Cleanup()
    {
        pendingGold = 0;
        if (layer != null) Object.Destroy(layer.gameObject);
        layer = null;
    }

    static void PunchPanel()
    {
        if (coinPanel == null) return;
        DOTween.Complete(PunchId);
        coinPanel.DOPunchScale(Vector3.one * PanelPunchScale, PanelPunchDuration, 6, 0.5f)
            .SetId(PunchId)
            .SetUpdate(true)
            .SetLink(coinPanel.gameObject);
    }

    static void PlayArriveSound(ShopManager owner)
    {
        AudioEvent arrive = owner != null ? owner.coinArriveSound : null;
        if (arrive == null) return;
        if (AudioControl.Instance != null)
        {
            AudioControl.Instance.PlaySFX(arrive);
            return;
        }
        if (owner.diceManager != null && owner.diceManager.sfxSource != null)
            arrive.PlayOneShot(owner.diceManager.sfxSource);
    }
}
