using UnityEngine;
using DG.Tweening;

public class PanelPopupController : MonoBehaviour
{
    [Header("UI Target Panel")]
    public GameObject targetPanel; 

    void Start()
    {
        if (targetPanel != null)
        {
            targetPanel.SetActive(false);
        }
    }

    public void OpenPanel()
    {
        if (targetPanel == null) return;

        // 1. 패널 활성화
        targetPanel.SetActive(true);

        targetPanel.transform.localScale = Vector3.zero;
        targetPanel.transform.DOScale(Vector3.one, 0.25f).SetEase(Ease.OutBack);
    }

    public void ClosePanel()
    {
        if (targetPanel == null) return;

        targetPanel.transform.DOScale(Vector3.zero, 0.2f)
            .SetEase(Ease.InBack)
            .OnComplete(() => targetPanel.SetActive(false)); // 연출 끝나면 끄기
    }

    void OnDestroy()
    {
        // 씬 전환 시 혹시 남아있을지 모르는 도트윈 잔여 메모리 킬
        if (targetPanel != null)
        {
            targetPanel.transform.DOKill();
        }
    }
}