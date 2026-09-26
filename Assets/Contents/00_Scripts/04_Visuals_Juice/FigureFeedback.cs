using System.Collections.Generic;
using Coffee.UIEffects;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

// InventorySlot에서 자동으로 연결합니다. 씬에 별도 매니저를 만들 필요가 없습니다.
public sealed class FigureFeedback : MonoBehaviour
{
    private static readonly Dictionary<FigureItemSO, FigureFeedback> Slots = new Dictionary<FigureItemSO, FigureFeedback>();
    private FigureItemSO figure;
    private UIEffect effect;
    private RectTransform icon;
    private Vector3 baseScale;
    private Tween previewTween;
    private Sequence pulseTween;
    private bool preview;
    private bool retiring;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry() => Slots.Clear();

    public static void Bind(InventorySlot slot, FigureItemSO data)
    {
        FigureFeedback feedback = slot.GetComponent<FigureFeedback>();
        if (feedback != null) feedback.Unbind();
        if (data == null || slot.itemIcon == null) return;
        if (feedback == null) feedback = slot.gameObject.AddComponent<FigureFeedback>();
        feedback.figure = data;
        feedback.icon = slot.itemIcon.rectTransform;
        feedback.baseScale = feedback.icon.localScale;
        feedback.effect = slot.itemIcon.GetComponent<UIEffect>();
        if (feedback.effect == null) feedback.effect = slot.itemIcon.gameObject.AddComponent<UIEffect>();
        feedback.effect.colorFilter = ColorFilter.Additive;
        feedback.effect.color = new Color(1f, 0.9f, 0.65f, 1f);
        feedback.effect.colorIntensity = 0f;
        feedback.retiring = false;
        Slots[data] = feedback;
    }

    public static void ApplyPreview(HashSet<FigureItemSO> figures)
    {
        foreach (var pair in Slots) pair.Value.SetPreview(figures.Contains(pair.Key));
    }

    private void SetPreview(bool value)
    {
        if (preview == value) return;
        preview = value;
        previewTween?.Kill();
        previewTween = null;
        if (pulseTween != null && pulseTween.IsActive() && pulseTween.IsPlaying()) return;
        ResumePreview();
    }

    private void ResumePreview()
    {
        if (effect == null || retiring) return;
        previewTween?.Kill();
        effect.colorIntensity = preview ? 0.12f : 0f;
        if (!preview || !isActiveAndEnabled) return;
        previewTween = DOTween.To(() => effect.colorIntensity, value => effect.colorIntensity = value, 0.4f, 0.65f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true);
    }

    public static void Pulse(FigureItemSO data)
    {
        if (data != null && Slots.TryGetValue(data, out var slot)) slot.PlayPulse();
    }

    private void PlayPulse()
    {
        if (!isActiveAndEnabled || effect == null || retiring) return;
        // 연출 중에 여러 효과가 들어와도 확대를 중첩하지 않습니다.
        if (pulseTween != null && pulseTween.IsActive() && pulseTween.IsPlaying()) return;
        previewTween?.Kill();
        previewTween = null;
        icon.localScale = baseScale;
        effect.colorIntensity = 0.7f;
        pulseTween = DOTween.Sequence().SetUpdate(true);
        pulseTween.Append(icon.DOScale(baseScale * 1.4f, 0.09f).SetEase(Ease.OutQuad));
        pulseTween.Append(icon.DOScale(baseScale, 0.4f).SetEase(Ease.OutQuad));
        pulseTween.Join(DOTween.To(() => effect.colorIntensity, value => effect.colorIntensity = value, 0f, 0.7f));
        pulseTween.OnComplete(ResumePreview);
    }

    // 일회용 피규어는 데이터에서 즉시 제거하되, 마지막 발동 연출만 끝내고 UI를 지움
    public static void RemoveSlot(GameObject slotObject)
    {
        FigureFeedback feedback = slotObject.GetComponent<FigureFeedback>();
        if (feedback == null || feedback.pulseTween == null || !feedback.pulseTween.IsActive() || !feedback.pulseTween.IsPlaying())
        {
            Destroy(slotObject);
            return;
        }
        feedback.retiring = true;
        feedback.Unregister();
        InventorySlot slot = slotObject.GetComponent<InventorySlot>();
        if (slot != null) slot.enabled = false;
        foreach (Graphic graphic in slotObject.GetComponentsInChildren<Graphic>()) graphic.raycastTarget = false;
        feedback.pulseTween.OnComplete(() => Destroy(slotObject));
    }

    private void Unregister()
    {
        if (figure != null && Slots.TryGetValue(figure, out var current) && current == this) Slots.Remove(figure);
    }

    private void Unbind()
    {
        Unregister();
        previewTween?.Kill();
        pulseTween?.Kill();
        previewTween = null;
        pulseTween = null;
        if (effect != null) effect.colorIntensity = 0f;
        if (icon != null) icon.localScale = baseScale;
        figure = null;
        preview = false;
    }

    private void OnEnable() { if (effect != null) ResumePreview(); }
    private void OnDisable()
    {
        previewTween?.Kill();
        pulseTween?.Kill();
        if (icon != null) icon.localScale = baseScale;
        if (effect != null) effect.colorIntensity = 0f;
        if (retiring) Destroy(gameObject);
    }
}