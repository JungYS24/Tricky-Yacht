using UnityEngine;
using TMPro;
using DG.Tweening;

// 최초 눈금·강화 누적 텍스트를 각각 한 번 생성해 재사용합니다. 두 연출은 서로 덮어쓰지 않습니다.
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
    private TextMeshPro floatingText;
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
            label = CreateText("DiceBonusTotal", null, 25001);
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

    public void ShowBaseValue(int bonusValue)
    {
        //매번 Instantiate 하지 않고, 없을 때 딱 한 번만 만둠
        if (floatingText == null)
        {
            floatingText = CreateText("FloatingText", transform, 25000);
            floatingText.fontSize = 5;
            // UI와 가림막을 뚫고 맨 위에 보이도록 기존 정렬 순서를 유지합니다.
        }

        //이전 연출 찌꺼기 초기화
        StopText(floatingText);

        // 주사위 머리 위쪽으로 시작 위치 리셋
        floatingText.transform.localPosition = new Vector3(0, 0.5f, 0);
        floatingText.transform.localScale = Vector3.one;

        floatingText.color = new Color(1f, 0.8f, 0f, 1f); // 황금색 텍스트
        floatingText.text = $"+{bonusValue}";
        floatingText.gameObject.SetActive(true);

        // DOTween 연출 (위로 이동 -> 크기 튕김 -> 서서히 투명해지며 꺼짐)
        float targetY = floatingText.transform.localPosition.y + 1.2f;

        floatingText.transform.DOLocalMoveY(targetY, 0.8f).SetEase(Ease.OutQuad);
        floatingText.transform.DOPunchScale(new Vector3(0.5f, 0.5f, 0f), 0.3f, 2, 0.5f);

        // 0.4초 대기 후 0.4초 동안 투명해지고 비활성화 (Destroy 안함)
        floatingText.DOFade(0f, 0.4f).SetDelay(0.4f).OnComplete(() =>
        {
            floatingText.gameObject.SetActive(false);
        });
    }

    // 표시 대상은 분리하되 텍스트 생성·입력 차단·Tween 정리는 공유합니다.
    private static TextMeshPro CreateText(string objectName, Transform parent, int sortingOrder)
    {
        var go = new GameObject(objectName);
        if (parent != null) go.transform.SetParent(parent);
        var text = go.AddComponent<TextMeshPro>();
        text.alignment = TextAlignmentOptions.Center;
        text.enableWordWrapping = false;
        text.raycastTarget = false;
        text.sortingOrder = sortingOrder;
        return text;
    }

    private static void StopText(TextMeshPro text)
    {
        if (text == null) return;
        text.DOKill();
        text.transform.DOKill();
    }

    private void SetValue(float value, bool isChips)
    {
        if (isChips) label.SetText("+{0}", Mathf.FloorToInt(value));
        else label.SetText("+{0:0.0}", Mathf.Round(value * 10f) / 10f);
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

    private void OnDisable()
    {
        ResetTotals();
        StopText(floatingText);
        if (floatingText != null) floatingText.gameObject.SetActive(false);
    }
    private void OnDestroy()
    {
        animation?.Kill();
        StopText(label);
        StopText(floatingText);
        if (label != null) Destroy(label.gameObject);
        if (floatingText != null) Destroy(floatingText.gameObject);
    }
}
