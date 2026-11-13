using System;
using System.Collections;
using System.Collections.Generic;
using Coffee.UIEffects;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 디버프 연출(DebuffFeedback의 일부): 뽑을 때 발동(도둑·족쇄)과 결산 때 발동(가시·저주·가짜).
// 공용 상태·부품은 DebuffFeedback.cs에 있습니다.
public static partial class DebuffFeedback
{
    // ---------- 뽑을 때 발동: 도둑·족쇄 ----------

    // 도둑은 골드 패널에서 코인이 빠져나가 주사위로 빨려 들어가고, 족쇄는 굴리기 버튼을 X자로 감쌌다가 사라지며 리롤 숫자 칸에 −1을 띄운다.
    // 실제 골드는 이미 깎여 있고, 화면 골드 숫자만 코인이 빠져나갈 때 내려간다.
    public static void PlayDrawTriggers(List<DrawTrigger> triggers, List<Dice> board, UIManager ui, ShopManager shop)
    {
        if (triggers == null || triggers.Count == 0) return;
        var p = Profile;
        int hiddenLoss = 0;
        foreach (var trigger in triggers)
            if (trigger.data != null && trigger.data.debuffType == DebuffDiceType.Thief) hiddenLoss += trigger.stolenGold;
        if (hiddenLoss > 0 && shop != null) ShowGold(shop, shop.currentGold + hiddenLoss);

        Transform goldIcon = GoldIcon(ui);
        Vector2 gold = UiToLocal(goldIcon, new Vector2(0.12f, 0.92f));
        float delay = 0f;
        int thieves = 0, totalStolen = 0;
        foreach (var trigger in triggers)
        {
            if (trigger.data == null) continue;
            Vector2 die = DieLocal(FindDie(board, trigger.data));
            if (trigger.data.debuffType == DebuffDiceType.Thief)
            {
                int stolen = trigger.stolenGold;
                PlayThief(die, gold, stolen, shop, delay, () =>
                {
                    hiddenLoss -= stolen;
                    if (shop != null) ShowGold(shop, shop.currentGold + Mathf.Max(0, hiddenLoss));
                    PunchGoldPanel(ui);
                });
                thieves++;
                totalStolen += stolen;
                delay += 0.35f;
            }
            else if (trigger.data.debuffType == DebuffDiceType.Shackle)
            {
                PlayShackle(die, ui, delay);
                delay += 0.5f;
            }
        }
        // 도둑이 여럿이면 합계를 한 번 더 보여 줍니다.
        if (thieves >= 2 && totalStolen > 0 && p != null)
            PopNumber($"-{totalStolen}", p.lossColor, gold + new Vector2(60f, -96f), delay + 0.4f, 52f);
    }

    static void PlayThief(Vector2 die, Vector2 gold, int stolen, ShopManager shop, float delay, Action onDepart)
    {
        var p = Profile;
        Color smoke = p != null ? p.thiefSmoke : Color.black;
        Sequence seq = NewSequence(Layer.gameObject);
        seq.InsertCallback(delay, () => Smoke(die, smoke, 0f, 3, 54f));
        if (stolen <= 0) return;

        float depart = delay + 0.15f;
        seq.InsertCallback(depart, () =>
        {
            onDepart?.Invoke();
            if (p != null) PopNumber($"-{stolen}", p.lossColor, gold + new Vector2(60f, -44f));
        });

        Sprite coinSprite = shop != null ? shop.coinIconSprite : null;
        if (coinSprite == null)
        {
            seq.InsertCallback(depart + 0.45f, () => Smoke(die, smoke, 0f, 2, 48f));
            return;
        }
        int coins = Mathf.Clamp(Mathf.CeilToInt(stolen / 34f), 1, 3);
        for (int i = 0; i < coins; i++)
        {
            Image coin = CreateImage("Stolen Coin", coinSprite, new Vector2(36f, 36f), Layer);
            RectTransform rect = coin.rectTransform;
            rect.anchoredPosition = gold;
            coin.gameObject.SetActive(false);
            float angle = i * 2.1f + 0.6f;
            Vector2 burst = gold + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 46f;
            float at = depart + i * 0.07f;
            seq.InsertCallback(at, () => coin.gameObject.SetActive(true));
            seq.Insert(at, rect.DOAnchorPos(burst, 0.14f).SetEase(Ease.OutBack));
            seq.Insert(at + 0.16f, rect.DOAnchorPos(die, 0.42f).SetEase(Ease.InQuad));
            seq.Insert(at + 0.16f, rect.DOScale(0.5f, 0.42f).SetEase(Ease.InQuad));
            seq.InsertCallback(at + 0.58f, () => { if (coin != null) UnityEngine.Object.Destroy(coin.gameObject); });
        }
        seq.InsertCallback(depart + 0.58f + (coins - 1) * 0.07f, () => Smoke(die, smoke, 0f, 2, 48f));
    }

    // 사슬 두 개가 굴리기 버튼을 X자로 감싸고 잠시 머문 뒤 연기와 함께 사라지고, 리롤 숫자 칸에 −1이 뜬다.
    static void PlayShackle(Vector2 die, UIManager ui, float delay)
    {
        var p = Profile;
        TMP_Text count = ui != null ? ui.roundPlaysText : null;
        Vector2 countPos = UiToLocal(count != null ? count.transform : null, new Vector2(0.15f, 0.35f));
        Color loss = p != null ? p.lossColor : Color.red;
        Sequence seq = NewSequence(Layer.gameObject);
        if (p != null) seq.InsertCallback(delay, () => Smoke(die, p.vanishSmoke, 0f, 2, 44f));

        void ShowLoss()
        {
            if (count != null)
            {
                count.transform.DOComplete();
                count.transform.DOPunchScale(Vector3.one * 0.25f, 0.25f, 4, 0.5f).SetUpdate(true);
            }
            PopNumber("-1", loss, countPos + new Vector2(0f, 44f));
        }

        RectTransform button = ui != null && ui.rollButton != null ? ui.rollButton.transform as RectTransform : null;
        Sprite chainSprite = p != null ? p.chainLink : null;
        if (chainSprite == null || button == null)
        {
            seq.InsertCallback(delay + 0.3f, ShowLoss);
            return;
        }

        // 버튼이 화면 밖으로 일부 나가 있어도 사슬은 화면 안쪽 영역에만 그립니다.
        Rect area = ClampToLayer(UiRectInLayer(button), 8f);
        Vector2 tile = new Vector2(chainSprite.rect.width, chainSprite.rect.height);
        float length = Mathf.Sqrt(area.width * area.width + area.height * area.height) * 0.92f;
        float angle = Mathf.Atan2(area.height, area.width) * Mathf.Rad2Deg;
        float wrap = delay + 0.12f;
        float release = wrap + 0.42f; // 사라지는 시간까지 합쳐 연출 속도 1.2 기준 버튼 위에 약 0.6~0.7초
        var chains = new Image[2];
        for (int i = 0; i < 2; i++)
        {
            Image chain = CreateImage("Shackle Chain", chainSprite, new Vector2(length, tile.y), Layer);
            chain.type = Image.Type.Tiled;
            chain.preserveAspect = false;
            RectTransform rect = chain.rectTransform;
            rect.anchoredPosition = area.center;
            rect.localRotation = Quaternion.Euler(0f, 0f, i == 0 ? angle : -angle);
            rect.localScale = new Vector3(0f, 1f, 1f);
            chain.gameObject.SetActive(false);
            chains[i] = chain;

            float at = wrap + i * 0.14f;
            seq.InsertCallback(at, () => chain.gameObject.SetActive(true));
            seq.Insert(at, rect.DOScaleX(1f, 0.18f).SetEase(Ease.OutQuad));
            seq.Insert(at + 0.18f, rect.DOPunchAnchorPos(new Vector2(0f, -6f), 0.22f, 6, 0.5f));
            seq.Insert(release + i * 0.05f, chain.DOFade(0f, 0.16f));
            seq.Insert(release + i * 0.05f, rect.DOScale(new Vector3(1.06f, 1.06f, 1f), 0.16f).SetEase(Ease.OutQuad));
        }
        if (p != null) seq.InsertCallback(release, () => Smoke(area.center, p.vanishSmoke, 0f, 3, 60f));
        seq.InsertCallback(release + 0.1f, ShowLoss);
        seq.InsertCallback(release + 0.23f, () =>
        {
            foreach (var chain in chains)
                if (chain != null) UnityEngine.Object.Destroy(chain.gameObject);
        });
    }

    // ---------- 결산 발동: 가시·저주·가짜 ----------

    // 가시 파편이 주사위에서 튀었다가 체력 표시로 날아간다. 첫 파편이 닿는 순간에 돌아오므로, 호출한 쪽은 그때 피해를 적용한다.
    public static IEnumerator PlayThornBurst(List<Dice> spineDice, Transform heartTarget)
    {
        Sprite shard = Profile != null ? Profile.thornShard : null;
        if (shard == null || spineDice == null || spineDice.Count == 0) yield break;
        Vector2 heart = UiToLocal(heartTarget, new Vector2(0.12f, 0.85f));
        Vector2 size = new Vector2(shard.rect.width, shard.rect.height);
        Sequence seq = NewSequence(Layer.gameObject);
        int order = 0;
        foreach (var die in spineDice)
        {
            if (die == null) continue;
            Vector2 from = DieLocal(die);
            float start = order++ * 0.1f;
            for (int i = 0; i < 4; i++)
            {
                Image piece = CreateImage("Thorn Shard", shard, size, Layer);
                RectTransform rect = piece.rectTransform;
                // 가시 끝이 오른쪽 위를 향하는 이미지라 90도 단위로만 돌려 픽셀이 깨지지 않게 합니다.
                rect.localRotation = Quaternion.Euler(0f, 0f, 90f * i);
                Vector2 direction = Quaternion.Euler(0f, 0f, 90f * i) * new Vector2(0.707f, 0.707f);
                rect.anchoredPosition = from;
                rect.localScale = Vector3.one * 0.6f;
                piece.gameObject.SetActive(false);
                seq.InsertCallback(start, () => piece.gameObject.SetActive(true));
                seq.Insert(start, rect.DOAnchorPos(from + direction * 70f, 0.2f).SetEase(Ease.OutQuad));
                seq.Insert(start, rect.DOScale(1f, 0.2f));
                float fly = start + 0.32f + i * 0.03f;
                seq.Insert(fly, rect.DOAnchorPos(heart, 0.32f).SetEase(Ease.InQuad));
                seq.Insert(fly, rect.DOScale(0.5f, 0.32f));
                seq.InsertCallback(fly + 0.32f, () => { if (piece != null) UnityEngine.Object.Destroy(piece.gameObject); });
            }
        }
        yield return new WaitForSecondsRealtime(Scaled(0.64f));
    }

    // 저주가 배수에 곱해질 때 주사위와 배수 숫자에 초록 연기를 피웁니다.
    public static void PlayCurse(Dice die, Transform multTarget)
    {
        var p = Profile;
        if (p == null) return;
        if (die != null) Smoke(DieLocal(die), p.curseSmoke, 0f, 3, 48f);
        if (multTarget != null) Smoke(UiToLocal(multTarget, new Vector2(0.15f, 0.45f)), p.curseSmoke, 0.08f, 4, 60f);
    }

    public static Color CurseColor => Profile != null ? Profile.curseColor : new Color(0.45f, 1f, 0.35f);

    // 가짜 주사위는 결산에서 흐릿하게 꺼지며 0으로 보입니다.
    public static void PlayFakeFade(Dice die)
    {
        if (die == null) return;
        die.FadeAsFake();
        if (Profile != null) Smoke(DieLocal(die), Profile.vanishSmoke, 0f, 2, 44f);
    }
}
