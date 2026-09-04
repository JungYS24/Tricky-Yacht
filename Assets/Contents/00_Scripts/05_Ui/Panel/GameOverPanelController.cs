using UnityEngine;
using TMPro;
using DG.Tweening;

public class GameOverPanelController : MonoBehaviour
{
    [Header("Text References")]
    public TextMeshProUGUI stageNumberText; // 분리된 스테이지 숫자 TMP
    public TextMeshProUGUI scoreText;       // 최종 점수 숫자 TMP

    [Header("Tween Settings")]
    [SerializeField] private float duration = 0.8f;

    private float currentDisplayedScore = 0f;
    private Tweener scoreTween;

    public void SetupGameOver(int reachedStage)
    {
        // 1. 패널 활성화
        gameObject.SetActive(true);

        if (scoreTween != null && scoreTween.IsActive())
        {
            scoreTween.Kill();
        }

        // 2. 도달 스테이지 숫자 세팅 (순수 정수 데이터만)
        if (stageNumberText != null)
        {
            stageNumberText.text = reachedStage.ToString();
        }

        // 3. 점수 계산 : 스테이지 * 랜덤 수 (55 ~ 88 사이의 자연수)
        int randomFactor = Random.Range(55, 89);
        int targetScore = reachedStage * randomFactor;

        currentDisplayedScore = 0f;
        if (scoreText != null)
        {
            scoreText.text = "0";
        }

        // 실시간 카운팅 연출
        scoreTween = DOVirtual.Float(0f, targetScore, duration, (value) =>
        {
            currentDisplayedScore = value;

            if (scoreText != null)
            {
                scoreText.text = Mathf.FloorToInt(currentDisplayedScore).ToString();
            }
        })
        .SetEase(Ease.OutQuad) 
        .SetUpdate(true);  

        Debug.Log($"[GameOver] 연출 시작 - 목표 점수: {targetScore} (가중치: x{randomFactor})");
    }

    // 오브젝트가 파괴될 때 메모리 누수 방지
    private void OnDestroy()
    {
        if (scoreTween != null && scoreTween.IsActive())
        {
            scoreTween.Kill();
        }
    }
}