using DG.Tweening;
using TMPro;
using UnityEngine;

public class UiCountUpText : MonoBehaviour
{
    public enum FormatKind
    {
        Integer,
        Chips,
        Mult
    }

    [SerializeField] TextMeshProUGUI label;
    [SerializeField] FormatKind format = FormatKind.Integer;
    [SerializeField] float duration = 0.5f;

    float displayed;
    Tweener tween;

    void Awake()
    {
        if (label == null)
            label = GetComponent<TextMeshProUGUI>();
    }

    public void SetFormat(FormatKind kind)
    {
        format = kind;
    }

    public static UiCountUpText On(TextMeshProUGUI text, FormatKind kind)
    {
        if (text == null) return null;

        var counter = text.GetComponent<UiCountUpText>();
        if (counter == null)
            counter = text.gameObject.AddComponent<UiCountUpText>();

        counter.label = text;
        counter.SetFormat(kind);
        return counter;
    }

    public void StopTween()
    {
        KillTween();
    }

    public void Clear()
    {
        KillTween();
        displayed = 0f;
        if (label != null)
            label.text = "";
    }

    public void SetInstant(float value)
    {
        KillTween();
        displayed = value;
        Apply(value);
    }

    public Tween Play(float target, float playDuration = -1f)
    {
        if (label == null)
            label = GetComponent<TextMeshProUGUI>();

        if (playDuration < 0f)
            playDuration = duration;

        KillTween();
        tween = DOVirtual.Float(displayed, target, playDuration, value =>
            {
                displayed = value;
                Apply(value);
            })
            .SetEase(Ease.OutQuad)
            .SetUpdate(true)
            .SetLink(gameObject);

        return tween;
    }

    void Apply(float value)
    {
        if (label == null) return;

        switch (format)
        {
            case FormatKind.Chips:
                label.text = UIManager.FormatChipsValue(Mathf.FloorToInt(value));
                break;
            case FormatKind.Mult:
                label.text = UIManager.FormatMultValue(value);
                break;
            default:
                label.text = Mathf.FloorToInt(value).ToString();
                break;
        }
    }

    void KillTween()
    {
        if (tween != null && tween.IsActive())
            tween.Kill();
        tween = null;
    }

    void OnDisable()
    {
        KillTween();
    }
}
