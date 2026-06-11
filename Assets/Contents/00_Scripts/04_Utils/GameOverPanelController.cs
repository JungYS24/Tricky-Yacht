using UnityEngine;
using TMPro;

public class GameOverPanelController : MonoBehaviour
{
    [Header("Text References")]
    public TextMeshProUGUI stageNumberText; // 분리된 스테이지 숫자 TMP
    public TextMeshProUGUI scoreText;       // 최종 점수 숫자 TMP

    public void SetupGameOver(int reachedStage)
    {
        // 1. 패널 활성화
        gameObject.SetActive(true);

        // 2. 도달 스테이지 숫자 세팅 (순수 정수 데이터만)
        if (stageNumberText != null)
        {
            stageNumberText.text = reachedStage.ToString();
        }

        // 3. 점수 계산 : 스테이지 * 랜덤 수 (55 ~ 88 사이의 자연수)
        int randomFactor = Random.Range(55, 89);
        int finalScore = reachedStage * randomFactor;

        // 4. 최종 점수 세팅 (문자열이나 포맷팅 없이 순수 숫자만)
        if (scoreText != null)
        {
            scoreText.text = finalScore.ToString();
        }

        Debug.Log($"[GameOver] 도달: {reachedStage}층, 가중치: x{randomFactor}, 최종 점수: {finalScore}");
    }
}