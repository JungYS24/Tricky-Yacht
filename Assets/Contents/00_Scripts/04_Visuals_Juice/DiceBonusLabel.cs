using UnityEngine;
using TMPro;
using DG.Tweening;

// 주사위당 텍스트 하나를 재사용합니다. 점수 계산에는 관여하지 않습니다.
[DisallowMultipleComponent]
public sealed class DiceBonusLabel : MonoBehaviour
{
    public Color chipsColor = new Color(0.318f, 0.973f, 0.835f);
    public Color multiplierColor = new Color(0.992f, 0.894f, 0.439f);
    public float fontSize = 5f;
    public float heightRatio = 0.8f;
    public float minimumVisibleTime = 0.4f;
    public float riseDistance = 0.12f;
    private float chips;
    private float multiplier;
    private TextMeshPro label;
    private SpriteRenderer body;
    private Sequence animation;
    private float rise;
    private float height;

    public static DiceBonusLabel Get(Dice die)
    {
        var result = die.GetComponent<DiceBonusLabel>();
        return result != null ? result : die.gameObject.AddComponent<DiceBonusLabel>();
    }

    public void ResetTotals()
    {
        animation?.Kill();
        animation = null;
        chips = multiplier = 0f;
        if (label != null) label.gameObject.SetActive(false);
    }

    public void Add(bool isChips, float amount, float duration, TMP_FontAsset font = null)
    {
        float start = isChips ? chips : multiplier;
        float target = start + amount;
        if (isChips) chips = target; else multiplier = target;
        animation?.Kill();
        if (body == null) body = GetComponent<SpriteRenderer>();
        if (label == null)
        {
            // 주사위 회전은 따라 흔들리지 않도록 독립 텍스트로 생성하고 위치만 추적합니다.
            label = new GameObject("DiceBonusTotal").AddComponent<TextMeshPro>();
            label.alignment = TextAlignmentOptions.Center;
            label.enableWordWrapping = false;
            label.raycastTarget = false;
            label.sortingOrder = 25001;
        }
        if (font != null) label.font = font;
        label.fontSize = fontSize;
        label.transform.localScale = transform.lossyScale;
        label.color = isChips ? chipsColor : multiplierColor;
        label.gameObject.SetActive(true);
        height = body != null ? body.bounds.size.y : Mathf.Abs(transform.lossyScale.y);
        rise = 0f;
        UpdatePosition();
        SetValue(start, isChips);
        float lifetime = Mathf.Max(minimumVisibleTime, duration + 0.12f);
        animation = DOTween.Sequence().SetUpdate(true);
        animation.Append(DOVirtual.Float(start, target, duration, value => SetValue(value, isChips)).SetEase(Ease.OutQuad));
        animation.Insert(0f, DOVirtual.Float(0f, riseDistance * height, lifetime, value => rise = value).SetEase(Ease.OutQuad));
        animation.Insert(lifetime - 0.12f, label.DOFade(0f, 0.12f));
        animation.OnComplete(() => { if (label != null) label.gameObject.SetActive(false); });
    }

    private void SetValue(float value, bool isChips)
    {
        if (isChips) label.SetText("+{0}", Mathf.FloorToInt(value));
        else label.SetText("+{0:0.0}배", Mathf.Round(value * 10f) / 10f);
    }

    private void LateUpdate()
    {
        if (label != null && label.gameObject.activeSelf) UpdatePosition();
    }

    private void UpdatePosition()
    {
        Vector3 center = body != null ? body.bounds.center : transform.position;
        label.transform.position = center + Vector3.up * (height * heightRatio + rise);
    }

    private void OnDisable() => ResetTotals();
    private void OnDestroy()
    {
        animation?.Kill();
        if (label != null) Destroy(label.gameObject);
    }
}
