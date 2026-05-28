using UnityEngine;
using TMPro;
using DG.Tweening;

public class GoldCounter : MonoBehaviour
{
    public static GoldCounter Instance { get; private set; }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);// 중복 생성 방지
        }
    }

    [SerializeField] private TextMeshProUGUI goldText;
    [SerializeField] private float duration = 0.5f;

    private int currentDisplayedGold = 0;
    private int targetGold = 0;
    private Tweener goldTween;

    public void SetGold(int newGoldAmount)
    {
        targetGold = newGoldAmount;

        if (goldTween != null && goldTween.IsActive())
        {
            goldTween.Kill();
        }

        goldTween = DOVirtual.Float(currentDisplayedGold, targetGold, duration, (value) =>
        {
            currentDisplayedGold = Mathf.FloorToInt(value);
            goldText.text = currentDisplayedGold.ToString("N0");
        })
        .SetEase(Ease.OutQuad)
        .SetUpdate(true);
    }
}