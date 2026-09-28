using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

// InventorySlot에 런타임으로 연결합니다. 씬/프리팹 수정 없이 아이콘과 빛 알갱이만 제어합니다.
public sealed class SnackUseFeedback : MonoBehaviour
{
    private Image icon;
    private SnackDustGraphic dust;
    private Tween animationTween;

    public static SnackUseFeedback Get(InventorySlot slot)
    {
        var effect = slot.GetComponent<SnackUseFeedback>();
        if (effect == null) effect = slot.gameObject.AddComponent<SnackUseFeedback>();
        effect.icon = slot.itemIcon;
        return effect;
    }

    public void ShowUsed(Color tint, bool hidden = false)
    {
        if (icon == null) return;
        icon.color = hidden ? new Color(tint.r, tint.g, tint.b, 0f) : tint;
    }

    public Tween Disappear(float duration, Action completed)
    {
        StopAnimation();
        if (icon == null || !gameObject.activeInHierarchy)
        {
            completed?.Invoke();
            return null;
        }

        EnsureDust();
        Color initial = icon.color;
        bool finished = false;
        Action finish = () =>
        {
            if (finished) return;
            finished = true;
            animationTween = null;
            if (dust != null) dust.SetProgress(1f);
            if (icon != null) icon.color = new Color(initial.r, initial.g, initial.b, 0f);
            completed?.Invoke();
        };

        animationTween = DOVirtual.Float(0f, 1f, Mathf.Max(0.05f, duration), t =>
        {
            // 원래 종류를 알아볼 색을 유지하며 빛 알갱이가 올라가는 동안 서서히 사라집니다.
            float alpha = initial.a * (1f - Mathf.SmoothStep(0f, 1f, t));
            if (icon != null) icon.color = new Color(initial.r, initial.g, initial.b, alpha);
            if (dust != null) dust.SetProgress(t);
        }).SetEase(Ease.Linear).SetLink(gameObject, LinkBehaviour.KillOnDisable).OnComplete(() => finish()).OnKill(() => finish());
        return animationTween;
    }

    public Tween Restore(float duration)
    {
        StopAnimation();
        if (icon == null) return null;
        Color initial = icon.color;
        animationTween = DOVirtual.Float(0f, 1f, Mathf.Max(0.05f, duration), t =>
        {
            if (icon != null) icon.color = Color.Lerp(initial, Color.white, t);
        }).SetEase(Ease.OutQuad).SetLink(gameObject, LinkBehaviour.KillOnDisable).OnKill(() =>
        {
            animationTween = null;
            if (icon != null) icon.color = Color.white;
        });
        return animationTween;
    }

    public void ResetVisual()
    {
        StopAnimation();
        if (dust != null) dust.SetProgress(1f);
        if (icon != null) icon.color = Color.white;
    }

    private void StopAnimation()
    {
        Tween previous = animationTween;
        animationTween = null;
        previous?.Kill();
    }

    private void EnsureDust()
    {
        if (dust != null) return;
        var go = new GameObject("SnackDust", typeof(RectTransform), typeof(CanvasRenderer), typeof(SnackDustGraphic));
        var rect = (RectTransform)go.transform;
        rect.SetParent(icon.transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        dust = go.GetComponent<SnackDustGraphic>();
        dust.raycastTarget = false;
        dust.maskable = false;
    }

    private void OnDisable() => StopAnimation();
}
