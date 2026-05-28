using UnityEngine;
using UnityEngine.UI;
using DG.Tweening; 

public class ButtonPunchEffect : MonoBehaviour
{
    private Button button;
    private RectTransform rectTransform;
    private Tween punchTween;

    void Awake()
    {
        button = GetComponent<Button>();
        rectTransform = GetComponent<RectTransform>();
        if (button != null)
        {
            button.onClick.AddListener(PlayPunchEffect);
        }
    }

    public void PlayPunchEffect()
    {
        if (rectTransform == null) return;

        if (punchTween != null && punchTween.IsActive())
        {
            punchTween.Kill();
            rectTransform.localScale = Vector3.one; // 원래 크기(1,1,1)로 리셋
        }

        punchTween = rectTransform.DOPunchScale(new Vector3(0.15f, 0.15f, 0f), 0.2f, 10, 1f)
            .SetUpdate(true); // 게임이 일시정지(Time.timeScale = 0) 상태여도 UI 연출이 돌도록 설정
    }

    void OnDestroy()
    {
        // 메모리 누수 방지를 위해 트윈 제거
        if (punchTween != null) punchTween.Kill();
    }
}