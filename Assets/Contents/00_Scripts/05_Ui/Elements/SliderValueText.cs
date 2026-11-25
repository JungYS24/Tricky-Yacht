using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 슬라이더 값을 0~100 숫자로 보여준다. 설정 팝업의 볼륨 수치 칸에서 쓴다.
/// </summary>
public class SliderValueText : MonoBehaviour
{
    [SerializeField] private Slider slider;
    [SerializeField] private TextMeshProUGUI valueText;

    private void Awake()
    {
        if (slider == null) slider = GetComponentInParent<Slider>();
        if (valueText == null) valueText = GetComponentInChildren<TextMeshProUGUI>();
    }

    private void OnEnable()
    {
        if (slider == null) return;
        slider.onValueChanged.AddListener(Refresh);
        Refresh(slider.value);
    }

    private void OnDisable()
    {
        if (slider != null) slider.onValueChanged.RemoveListener(Refresh);
    }

    private void Refresh(float value)
    {
        if (valueText == null || slider == null) return;
        float range = slider.maxValue - slider.minValue;
        float t = range > 0f ? (value - slider.minValue) / range : 0f;
        valueText.text = Mathf.RoundToInt(t * 100f).ToString();
    }
}
