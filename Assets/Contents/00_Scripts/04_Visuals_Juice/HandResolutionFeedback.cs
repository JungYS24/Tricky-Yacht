using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;

// FinishTurnRoutine에서만 호출합니다. 미리보기나 족보 재판정은 하지 않습니다.
public sealed class HandResolutionFeedback : MonoBehaviour
{
    [Header("족보별 전체 연출 시간(초)")]
    public float onePairDuration = 0.5f;
    public float twoPairDuration = 1.2f;
    public float tripleDuration = 0.5f;
    public float straightDuration = 1.3f;
    public float fullHouseDuration = 1.2f;
    public float fourOfAKindDuration = 0.53f;
    public float yachtDuration = 0.85f;
    [Header("움직임")]
    public float tripleScale = 1.1f;
    public float fourOfAKindScale = 1.1f;
    public float yachtScale = 1.25f;
    [Header("야추 문구 위치: 비어 있으면 주사위 아래 자동 배치")]
    public Transform yachtTextAnchor;
    public float yachtTextDown = 1.8f;
    public bool animateScoreLabels = true;

    private readonly Dice[] dice = new Dice[5];
    private readonly DiceHandHighlight[] highlights = new DiceHandHighlight[5];
    private readonly int[] values = new int[5];
    private readonly int[] groups = new int[5];
    private readonly int[] sorted = new int[5];
    private HandRank rank;
    private UIManager ui;
    private Vector3 nameScale, multiplierScale;
    private Sequence sequence;
    private TextMeshProUGUI yachtText;
    private RectTransform yachtRoot;
    private bool playing;

    public static HandResolutionFeedback Get(DiceManager manager)
    {
        var feedback = manager.GetComponent<HandResolutionFeedback>();
        return feedback != null ? feedback : manager.gameObject.AddComponent<HandResolutionFeedback>();
    }

    public IEnumerator Play(List<Dice> keptDice, HandRank finalRank, UIManager targetUI)
    {
        if (playing || finalRank == HandRank.HighCard || keptDice == null || keptDice.Count != 5) yield break;
        playing = true;
        ui = null;
        rank = finalRank;
        // 슬롯 이동 코루틴이 위치를 덮어쓰지 않도록 실제 도착을 기다립니다.
        bool moving;
        do
        {
            moving = false;
            for (int i = 0; i < 5; i++)
            {
                Dice item = keptDice[i];
                if (item == null || !item.gameObject.activeInHierarchy) { playing = false; yield break; }
                if (item.IsMoving) moving = true;
            }
            if (moving) yield return null;
        } while (moving);

        for (int i = 0; i < 5; i++)
        {
            dice[i] = keptDice[i];
            values[i] = dice[i].currentValue;
            highlights[i] = DiceHandHighlight.Get(dice[i]);
            highlights[i].Prepare();
        }
        GetEmphasisGroups(rank, values, groups, sorted);
        ui = targetUI;
        if (ui != null)
        {
            if (ui.handInfoText != null) nameScale = ui.handInfoText.transform.localScale;
            if (ui.multSumText != null) multiplierScale = ui.multSumText.transform.localScale;
        }
        if (rank == HandRank.Yacht) PrepareYachtText();
        float progress = 0f;
        sequence = DOTween.Sequence(); // 일시정지(Time.timeScale = 0) 중에는 멈춥니다.
        sequence.Append(DOTween.To(() => progress, value => { progress = value; Render(value); }, 1f, GetDuration()).SetEase(Ease.Linear));
        sequence.OnComplete(Restore).OnKill(Restore);
        yield return sequence.WaitForCompletion();
    }

    // 그룹 -1: 강조 안 함. 스트레이트는 작은 눈금부터 0~4를 부여합니다.
    // 다섯 개 고정 배열을 재사용하며, 88 같은 특수 눈금도 그대로 비교합니다.
    public static void GetEmphasisGroups(HandRank rank, int[] values, int[] groups, int[] sorted)
    {
        int firstPair = -1;
        for (int i = 0; i < 5; i++)
        {
            int same = 0;
            for (int j = 0; j < 5; j++) if (values[i] == values[j]) same++;
            groups[i] = -1;
            switch (rank)
            {
                case HandRank.OnePair: if (same == 2) groups[i] = 0; break;
                case HandRank.TwoPair:
                    if (same == 2)
                    {
                        if (firstPair < 0) firstPair = i;
                        groups[i] = values[i] == values[firstPair] ? 0 : 1;
                    }
                    break;
                case HandRank.Triple: if (same == 3) groups[i] = 0; break;
                case HandRank.FullHouse: groups[i] = same == 3 ? 0 : same == 2 ? 1 : -1; break;
                case HandRank.FourOfAKind: if (same == 4) groups[i] = 0; break;
                case HandRank.Yacht: groups[i] = 0; break;
            }
            sorted[i] = i;
        }
        if (rank != HandRank.Straight) return;
        // 주사위 위치를 옮기지 않고 재생할 인덱스만 정렬합니다.
        for (int i = 1; i < 5; i++)
        {
            int index = sorted[i], j = i - 1;
            while (j >= 0 && values[sorted[j]] > values[index]) { sorted[j + 1] = sorted[j]; j--; }
            sorted[j + 1] = index;
        }
        for (int i = 0; i < 5; i++) groups[sorted[i]] = i;
    }

    private float GetDuration()
    {
        float value;
        switch (rank)
        {
            case HandRank.OnePair: value = onePairDuration; break;
            case HandRank.TwoPair: value = twoPairDuration; break;
            case HandRank.Triple: value = tripleDuration; break;
            case HandRank.Straight: value = straightDuration; break;
            case HandRank.FullHouse: value = fullHouseDuration; break;
            case HandRank.FourOfAKind: value = fourOfAKindDuration; break;
            default: value = yachtDuration; break;
        }
        return Mathf.Max(0.05f, value);
    }

    private static float Pulse(float time, float start, float duration)
    {
        float p = (time - start) / duration;
        return p <= 0f || p >= 1f ? 0f : Mathf.Sin(p * Mathf.PI);
    }

    private static Vector3 CompletionBeat(float time, float start, float duration, float peakScale, float peakLift)
    {
        float p = (time - start) / duration;
        if (p < 0f || p >= 1f) return new Vector3(1f, 0f, 0f);

        // 힘 모으기: 0.95배까지 움츠림
        if (p < 0.15f)
        {
            float q = Mathf.SmoothStep(0f, 1f, p / 0.15f);
            return new Vector3(1f - 0.05f * q, 0f, 0.35f * q);
        }

        // 빠르게 확대하고 위로 도약
        if (p < 0.4f)
        {
            float q = 1f - Mathf.Pow(1f - (p - 0.15f) / 0.25f, 3f);
            return new Vector3(Mathf.Lerp(0.95f, peakScale, q), peakLift * q, Mathf.Lerp(0.35f, 1f, q));
        }

        // 완성된 상태 유지
        if (p < 0.625f) return new Vector3(peakScale, peakLift, 1f);

        // 부드럽게 복귀하면서 잔광 제거
        float fade = 1f - Mathf.SmoothStep(0f, 1f, (p - 0.625f) / 0.375f);
        return new Vector3(1f + (peakScale - 1f) * fade, peakLift * fade, fade);
    }


    private void Render(float normalized)
    {
        float canonical = rank == HandRank.Yacht ? 0.85f : rank == HandRank.FullHouse ? 0.12f : rank == HandRank.FourOfAKind ? 0.53f : rank == HandRank.Straight ? 1.3f : rank == HandRank.TwoPair ? 1.2f : 0.4f;
        float t = normalized * canonical;
        float dim = 0.25f * Mathf.SmoothStep(0f, 1f, normalized / 0.15f) * (1f - Mathf.SmoothStep(0f, 1f, (normalized - 0.7f) / 0.3f));

        for (int i = 0; i < 5; i++)
        {
            if (highlights[i] == null || !highlights[i].isActiveAndEnabled) continue;

            // 족보에 포함되지 않은 주사위만 잠깐 어둡게
            highlights[i].SetDim(groups[i] < 0 ? dim : 0f);
            if (groups[i] < 0) continue;

            Vector3 beat = new Vector3(1f, 0f, 0f);
            Vector3 finish = new Vector3(1f, 0f, 0f);
            float start = 0f;

            switch (rank)
            {
                case HandRank.OnePair:
                    beat = CompletionBeat(t, 0f, 0.4f, 1.1f, 0.18f);
                    break;

                case HandRank.TwoPair:
                    start = groups[i] * 0.12f;
                    beat = CompletionBeat(t, start, 0.28f, 1.1f, 0.05f);
                    finish = CompletionBeat(t, 0.58f, 0.62f, 1.13f, 0.18f);
                    break;

                case HandRank.Triple:
                    beat = CompletionBeat(t, 0f, 0.4f, tripleScale, 0.18f);
                    break;

                case HandRank.Straight:
                    start = groups[i] * 0.06f;
                    beat = CompletionBeat(t, start, 0.22f, 1.1f, 0.04f);
                    finish = CompletionBeat(t, 0.4f, 0.4f, 1.18f, 0.18f);
                    break;

                case HandRank.FullHouse:
                    start = groups[i] * 0.12f;
                    beat = CompletionBeat(t, start, 0.22f, 1.1f, 0.05f);
                    finish = CompletionBeat(t, 0.28f, 0.4f, 1.1f, 0.18f);
                    break;

                case HandRank.FourOfAKind:
                    beat = CompletionBeat(t, 0f, 0.7f, fourOfAKindScale, 0.18f);

                    // 정점 유지 후 빠르게 착지하고 빛만 남깁니다.
                    if (t >= 0.4375f)
                    {
                        float landing = 1f - Mathf.SmoothStep(0f, 1f, (t - 0.4375f) / 0.09f);
                        beat.x = 1f + (fourOfAKindScale - 1f) * landing;
                        beat.y = 0.18f * landing;
                        beat.z = Mathf.Max(beat.z, Pulse(t, 0.5f, 0.16f));
                    }
                    break;

                case HandRank.Yacht:
                    beat = CompletionBeat(t, 0f, 0.58f, yachtScale, 0.24f);
                    beat.z = Mathf.Max(beat.z, Pulse(t, 0.32f, 0.3f));
                    break;
            }

            float light = Mathf.Max(beat.z, finish.z);
            float size = beat.x + finish.x - 1f;
            float lift = beat.y + finish.y;
            float spread = 1f + 0.18f * light + 0.2f * finish.z;

            if (rank == HandRank.Yacht)
            {
                spread += 0.35f * Mathf.Clamp01((t - 0.25f) / 0.3f);
            }

            highlights[i].Draw(light, Mathf.Clamp01((t - start - 0.06f) / 0.2f), light * 0.95f, size, lift, spread);
        }

        float nameStart = rank == HandRank.Yacht ? 0.6f : rank == HandRank.FullHouse ? 0.44f : rank == HandRank.Straight ? 0.56f : rank == HandRank.TwoPair ? 0.44f : rank == HandRank.FourOfAKind ? 0.28f : 0.16f;

        if (animateScoreLabels && ui != null)
        {
            if (ui.handInfoText != null) ui.handInfoText.transform.localScale = nameScale * (1f + Pulse(t, nameStart, Mathf.Min(0.16f, canonical - nameStart)) * 0.16f);
            if (ui.multSumText != null) ui.multSumText.transform.localScale = multiplierScale * (1f + Pulse(t, nameStart + 0.06f, Mathf.Min(0.14f, canonical - nameStart - 0.06f)) * 0.12f);
        }

        if (rank == HandRank.Yacht && yachtText != null)
        {
            float alpha = t < 0.35f ? 0f : t < 0.43f ? (t - 0.35f) / 0.08f : t < 0.65f ? 1f : 1f - (t - 0.65f) / 0.15f;
            yachtText.alpha = Mathf.Clamp01(alpha);
            yachtText.rectTransform.localScale = Vector3.one * (1f + Pulse(t, 0.35f, 0.25f) * 0.22f);
        }
    }

    private void PrepareYachtText()
    {
        if (yachtText == null)
        {
            var root = new GameObject("YachtAnnouncement", typeof(RectTransform), typeof(Canvas));
            yachtRoot = root.GetComponent<RectTransform>();
            yachtRoot.SetParent(transform, false);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true;
            var renderer = dice[0].GetComponent<SpriteRenderer>();
            if (renderer != null) { canvas.sortingLayerID = renderer.sortingLayerID; canvas.sortingOrder = renderer.sortingOrder + 10; }
            var textObject = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            yachtText = textObject.GetComponent<TextMeshProUGUI>();
            yachtText.rectTransform.SetParent(yachtRoot, false);
            yachtText.rectTransform.sizeDelta = new Vector2(600f, 140f);
            yachtText.fontSize = 90f;
            yachtText.alignment = TextAlignmentOptions.Center;
            yachtText.color = new Color(1f, 0.87f, 0.3f);
            yachtText.raycastTarget = false;
        }
        if (ui != null && ui.handInfoText != null) yachtText.font = ui.handInfoText.font;
        yachtText.text = LocalizationManager.GetHandDisplayName(HandRank.Yacht) + "!";
        Vector3 center = Vector3.zero;
        for (int i = 0; i < 5; i++) center += dice[i].transform.position;
        float height = highlights[0].Height;
        yachtRoot.position = yachtTextAnchor != null ? yachtTextAnchor.position : center / 5f + Vector3.down * (height * yachtTextDown);
        yachtRoot.rotation = Quaternion.identity;
        float factor = height * 4f / 600f;
        Vector3 parentScale = transform.lossyScale;
        yachtRoot.localScale = new Vector3(factor / Mathf.Max(0.001f, Mathf.Abs(parentScale.x)), factor / Mathf.Max(0.001f, Mathf.Abs(parentScale.y)), 1f);
        yachtText.alpha = 0f;
        yachtRoot.gameObject.SetActive(true);
    }

    private void Restore()
    {
        if (!playing) return;
        for (int i = 0; i < 5; i++) if (highlights[i] != null) highlights[i].Restore();
        if (ui != null && animateScoreLabels)
        {
            if (ui.handInfoText != null) ui.handInfoText.transform.localScale = nameScale;
            if (ui.multSumText != null) ui.multSumText.transform.localScale = multiplierScale;
        }
        if (yachtRoot != null) yachtRoot.gameObject.SetActive(false);
        playing = false;
    }

    private void OnDisable() { sequence?.Kill(); Restore(); }
    private void OnDestroy() { sequence?.Kill(); Restore(); }
}
