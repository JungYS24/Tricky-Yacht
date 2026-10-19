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
    const float TickInterval = 0.06f;
    float lastTickUnscaledTime = -999f;

    // 세이브 복원·초기 표시처럼 숫자를 바로 맞출 때. 틱 효과음은 내지 않는다.
    public void SetInstant(int gold)
    {
        if (goldTween != null && goldTween.IsActive())
            goldTween.Kill();

        currentDisplayedGold = gold;
        targetGold = gold;
        if (goldText != null)
            goldText.text = currentDisplayedGold.ToString("N0");
    }

    public void SetGold(int newGoldAmount)
    {
        targetGold = newGoldAmount;
        bool increasing = newGoldAmount > currentDisplayedGold;

        if (goldTween != null && goldTween.IsActive())
        {
            goldTween.Kill();
        }

        goldTween = DOVirtual.Float(currentDisplayedGold, targetGold, duration, (value) =>
        {
            int next = Mathf.FloorToInt(value);
            if (increasing && next > currentDisplayedGold)
                TryPlayGoldTick();
            currentDisplayedGold = next;
            goldText.text = currentDisplayedGold.ToString("N0");
        })
        .SetEase(Ease.OutQuad)
        .SetUpdate(true);
    }

    void TryPlayGoldTick()
    {
        float now = Time.unscaledTime;
        if (now - lastTickUnscaledTime < TickInterval)
            return;
        lastTickUnscaledTime = now;
        Sfx.Play(SfxId.Gold_Count_Tick);
    }
}